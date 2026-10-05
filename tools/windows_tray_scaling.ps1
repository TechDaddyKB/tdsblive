# Dot-source only from the disposable native Windows package qualification.
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'Native scaling qualification is restricted to the isolated CI runner.'
}
function Display-SettingsWindow {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    foreach ($window in $windows) {
        if ($window.Current.Name -ne 'Settings') { continue }
        $owner = Get-Process -Id $window.Current.ProcessId -ErrorAction SilentlyContinue
        if ($owner -and $owner.SessionId -eq $session -and $owner.ProcessName -in @('SystemSettings', 'ApplicationFrameHost')) { return $window }
    }
    return $null
}
function Open-DisplaySettings {
    if (Display-SettingsWindow) { throw 'A preexisting Settings window will not be changed or closed.' }
    Start-Process 'ms-settings:display' | Out-Null
    return Wait-For { Display-SettingsWindow } 'The owned display Settings window did not open.'
}
function Display-ScaleChoices($Settings) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'SystemSettings_Display_Scaling_ItemSizeOverride_ComboBox')
    $combo = Wait-For { $Settings.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition) } 'The measured Windows scaling selector was unavailable.'
    if (-not $combo.Current.IsEnabled) { throw 'Windows display scaling is disabled on this CI desktop.' }
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $conditions = @(
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $combo.Current.ProcessId),
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))
    $items = Wait-For {
        $found = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.AndCondition]::new($conditions)) | Where-Object { $_.Current.Name -match '^\d+%' })
        if ($found.Count) { return $found }
    } 'Windows did not expose selectable display scales.'
    $choices = @()
    foreach ($item in $items) {
        $pattern = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $choices += @{ name = $item.Current.Name; percent = [int]([regex]::Match($item.Current.Name, '^\d+').Value)
            selected = $pattern.Current.IsSelected; pattern = $pattern }
    }
    return @{ choices = $choices; expand = $expand }
}
function Set-DisplayScale($Settings, [string]$Name) {
    $available = Display-ScaleChoices $Settings
    try {
        $choice = @($available.choices | Where-Object { $_.name -eq $Name })
        if ($choice.Count -ne 1) { throw 'The requested native display scale is no longer available.' }
        $choice[0].pattern.Select()
    } finally {
        # Selection normally collapses the popup. Collapse is idempotent.
        $available.expand.Collapse()
    }
}
function Qualify-WindowsScaling($Desktop) {
    $settings = $null
    $original = $null
    $changed = $false
    try {
        $settings = Open-DisplaySettings
        $available = Display-ScaleChoices $settings
        try {
            $selected = @($available.choices | Where-Object { $_.selected })
            if ($selected.Count -ne 1) { throw 'The original Windows scale could not be established; no scale was changed.' }
            $original = $selected[0].name
            # Exercise the highest ordinary scale offered by this physical CI
            # display, up to 200%. Never inject synthetic DPI notifications.
            $target = $available.choices | Where-Object { $_.percent -gt 100 -and $_.percent -le 200 } |
                Sort-Object percent -Descending | Select-Object -First 1
            if (-not $target) { throw 'This desktop offers no actual high-DPI display scale.' }
            $offered = @($available.choices | ForEach-Object { $_.name })
        } finally { $available.expand.Collapse() }
        $changed = $true
        Set-DisplayScale $settings $target.name
        $settings.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
        $settings = $null
        $expectedDpi = [int](96 * $target.percent / 100)
        foreach ($action in @('Restart', 'Quit')) {
            Menu-Action $Desktop $action
            $dialog = Wait-For {
                $window = Element $Desktop.Id "$action TDSBLive?" ([System.Windows.Automation.ControlType]::Window)
                if ($window -and [TdsTrayDesktop]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle) -eq $expectedDpi) { return $window }
            } "The actual $action window did not receive $expectedDpi DPI after Windows scaling."
            $cancel = Element $Desktop.Id 'Cancel' ([System.Windows.Automation.ControlType]::Button)
            $accept = Element $Desktop.Id $action ([System.Windows.Automation.ControlType]::Button)
            if (-not $cancel -or -not $accept -or -not $cancel.Current.HasKeyboardFocus) { throw 'The scaled confirmation lost its controls or Cancel-first focus.' }
            $bounds = $dialog.Current.BoundingRectangle
            foreach ($button in @($cancel, $accept)) {
                $rectangle = $button.Current.BoundingRectangle
                if ($button.Current.IsOffscreen -or -not $button.Current.IsEnabled -or -not $bounds.Contains($rectangle) -or
                    $rectangle.Height -lt (44 * $target.percent / 100 - 1)) {
                    throw 'A scaled confirmation button is clipped, disabled or smaller than its touch target.'
                }
            }
            Screenshot $dialog "scale-$($target.percent)-$($action.ToLowerInvariant()).png"
            Send-OwnedKey $cancel '{ENTER}'
            Wait-For { -not (Element $Desktop.Id "$action TDSBLive?" ([System.Windows.Automation.ControlType]::Window)) } 'Keyboard Cancel failed in the scaled confirmation.' | Out-Null
        }
        return @{ passed = $true; evidence = 'actual Windows display scale, native window DPI, visible controls and keyboard Cancel'
            original = $original; testedPercent = $target.percent; windowDpi = $expectedDpi; offeredScales = $offered }
    } finally {
        try {
            if ($changed) {
                if (-not $settings) { $settings = Open-DisplaySettings }
                Set-DisplayScale $settings $original
                $restored = Display-ScaleChoices $settings
                try {
                    if (-not ($restored.choices | Where-Object { $_.name -eq $original -and $_.selected })) {
                        throw 'The original Windows display scale was not restored.'
                    }
                } finally { $restored.expand.Collapse() }
            }
        } finally {
            if ($settings) { $settings.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
        }
    }
}

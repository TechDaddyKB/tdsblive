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
function Close-DisplaySettings($Settings) {
    $Settings.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Wait-For { -not (Display-SettingsWindow) } 'The owned Settings window did not finish closing.' | Out-Null
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
        $script:scalingEvidence = @{ original = $original; targetPercent = $target.percent; offeredScales = $offered; restored = $false; dialogs = @{} }
        Set-DisplayScale $settings $target.name
        Close-DisplaySettings $settings
        $settings = $null
        $expectedDpi = [int](96 * $target.percent / 100)
        foreach ($action in @('Restart', 'Quit')) {
            Menu-Action $Desktop $action
            $dialog = Wait-For {
                $window = Element $Desktop.Id "$action TDSBLive?" ([System.Windows.Automation.ControlType]::Window)
                if ($window -and [TdsTrayDesktop]::GetDpiForWindow([IntPtr]$window.Current.NativeWindowHandle) -eq $expectedDpi) { return $window }
            } "The actual $action window did not receive $expectedDpi DPI after Windows scaling."
            Screenshot $dialog "scale-$($target.percent)-$($action.ToLowerInvariant()).png"
            try {
                $buttons = Wait-For {
                    $cancelControl = Dialog-Button $dialog 'Cancel'
                    $acceptControl = Dialog-Button $dialog $action
                    $script:scalingEvidence.dialogs[$action] = @{ state = 'waiting for controls and initial focus'
                        cancelFound = [bool]$cancelControl; acceptFound = [bool]$acceptControl
                        cancelFocused = [bool]($cancelControl -and $cancelControl.Current.HasKeyboardFocus) }
                    if ($cancelControl -and $acceptControl -and $cancelControl.Current.HasKeyboardFocus) {
                        return @{ cancel = $cancelControl; accept = $acceptControl }
                    }
                } 'The scaled confirmation lost its controls or Cancel-first focus after becoming ready.'
            } catch {
                Screenshot $dialog "scale-$($target.percent)-$($action.ToLowerInvariant()).png"
                throw
            }
            $cancel = $buttons.cancel
            $accept = $buttons.accept
            # Capture even a failing layout, and wait for real layout to settle
            # rather than treating an initial automation frame as final geometry.
            Screenshot $dialog "scale-$($target.percent)-$($action.ToLowerInvariant()).png"
            Wait-For {
                $bounds = $dialog.Current.BoundingRectangle
                $minimumHeight = 44 * $target.percent / 100 - 1
                $checks = @()
                foreach ($button in @($cancel, $accept)) {
                    $rectangle = $button.Current.BoundingRectangle
                    $checks += @{ name = $button.Current.Name; enabled = $button.Current.IsEnabled; offscreen = $button.Current.IsOffscreen; focused = $button.Current.HasKeyboardFocus
                        insideDialog = $bounds.Contains($rectangle); height = $rectangle.Height; minimumHeight = $minimumHeight
                        bounds = @{ x = $rectangle.X; y = $rectangle.Y; width = $rectangle.Width; height = $rectangle.Height } }
                }
                $script:scalingEvidence.dialogs[$action] = @{ windowDpi = [TdsTrayDesktop]::GetDpiForWindow([IntPtr]$dialog.Current.NativeWindowHandle)
                    bounds = @{ x = $bounds.X; y = $bounds.Y; width = $bounds.Width; height = $bounds.Height }; buttons = $checks }
                if (-not ($checks | Where-Object { $_.offscreen -or -not $_.enabled -or -not $_.insideDialog -or $_.height -lt $_.minimumHeight })) { return $true }
            } 'A scaled confirmation button is clipped, disabled or smaller than its touch target after layout settled.' | Out-Null
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
                    $script:scalingEvidence.restored = $true
                } finally { $restored.expand.Collapse() }
            }
        } finally {
            if ($settings) { Close-DisplaySettings $settings }
        }
    }
}

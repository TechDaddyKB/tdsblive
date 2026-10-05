param([Parameter(Mandatory)][string]$EvidenceDirectory)
# Discover supported display controls on the disposable CI desktop. Do not
# change scale, theme, resolution or an operator's desktop settings.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'Windows display inventory is restricted to the isolated CI runner.'
}
$session = (Get-Process -Id $PID).SessionId
if ($session -eq 0) { throw 'Display inventory needs an interactive CI desktop.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
New-Item -ItemType Directory -Force $EvidenceDirectory | Out-Null
$inventory = @{ purpose = 'owned CI display-control discovery'; settingsChanged = $false; controls = @(); scaleChoices = @() }
$settings = $null
function Settings-Window {
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
try {
    try {
        [Windows.UI.ViewManagement.UISettings, Windows.UI.ViewManagement, ContentType = WindowsRuntime] | Out-Null
        $ui = New-Object Windows.UI.ViewManagement.UISettings
        $background = $ui.GetColorValue([Windows.UI.ViewManagement.UIColorType]::Background)
        $inventory.uiSettings = @{ available = $true; background = @{ r = $background.R; g = $background.G; b = $background.B } }
    } catch { $inventory.uiSettings = @{ available = $false } }
    if (Settings-Window) { throw 'A preexisting Settings window will not be inspected or closed.' }
    Start-Process 'ms-settings:display' | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not $settings -and [DateTime]::UtcNow -lt $deadline) {
        $settings = Settings-Window
        Start-Sleep -Milliseconds 200
    }
    if (-not $settings) { throw 'The CI image did not expose a Windows display Settings window.' }
    $elements = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $controls = @()
    foreach ($element in $elements) {
        $name = $element.Current.Name
        $identifier = $element.Current.AutomationId
        if ($name -notmatch '(?i)(scal(e|ing)|size of text|resolution|\d+%)' -and $identifier -notmatch '(?i)(scal(e|ing)|resolution)') { continue }
        $pattern = $null
        $value = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) { $value = $pattern.Current.Value }
        $controls += @{ name = $name; automationId = $identifier; controlType = $element.Current.ControlType.ProgrammaticName
            enabled = $element.Current.IsEnabled; value = $value }
        if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox -and
            $element.Current.IsEnabled -and ($name -match '(?i)(scal(e|ing)|size of text)' -or $identifier -match '(?i)scal(e|ing)')) {
            $expand = $null
            if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) {
                try {
                    $expand.Expand()
                    Start-Sleep -Milliseconds 250
                    $conditions = @(
                        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $element.Current.ProcessId),
                        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))
                    $choices = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                        [System.Windows.Automation.AndCondition]::new($conditions))
                    foreach ($choice in $choices) {
                        if ($choice.Current.Name -match '^\d+%') { $inventory.scaleChoices += $choice.Current.Name }
                    }
                } finally { $expand.Collapse() }
            }
        }
    }
    $inventory.controls = $controls
    $inventory.available = $true
} catch {
    # Availability is evidence, never a passing high-DPI qualification result.
    $inventory.available = $false
    $inventory.limitation = $_.Exception.Message
} finally {
    if ($settings) { $settings.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
    $inventory | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $EvidenceDirectory 'display-inventory.json') -Encoding UTF8
}
Write-Output 'CI display-control availability recorded. This is discovery, not appearance or high-DPI acceptance.'

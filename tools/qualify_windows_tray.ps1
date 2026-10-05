param(
    [Parameter(Mandatory)][string]$ApplicationDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
# Run with Windows PowerShell 5.1 for its built-in UIAutomation/desktop assemblies.
# This owns a disposable package/profile on a GitHub Actions Windows runner. It
# must never inspect or restart Explorer on an operator's production desktop.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'Native tray qualification is restricted to the isolated Windows CI runner.'
}
$application = [IO.Path]::GetFullPath($ApplicationDirectory)
$runnerTemporary = [IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\') + '\'
if (-not $application.StartsWith($runnerTemporary, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only a package extracted into the owned runner temporary directory can be tested.'
}
& sonar analyze secrets $application
if ($LASTEXITCODE -ne 0) { throw 'Package secret scan failed. Package contents were not inspected.' }
$session = (Get-Process -Id $PID).SessionId
if ($session -eq 0) { throw 'An interactive Windows desktop is required for actual tray qualification.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class TdsTrayDesktop {
    public delegate bool Visitor(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Visitor visitor, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string name, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll")] public static extern int Shell_NotifyIconGetRect(ref IconIdentifier identifier, out Rect rectangle);
    [StructLayout(LayoutKind.Sequential)] public struct IconIdentifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    public static IntPtr MessageWindow(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, parameter) => {
            uint process; GetWindowThreadProcessId(window, out process);
            if (process != pid) return true;
            var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
            if (!name.ToString().StartsWith("AvaloniaMessageWindow ", StringComparison.Ordinal)) return true;
            found = window; return false;
        }, IntPtr.Zero);
        return found;
    }
    public static bool Registered(int pid) {
        var window = MessageWindow(pid);
        if (window == IntPtr.Zero) return false;
        var identifier = new IconIdentifier { Size = (uint)Marshal.SizeOf(typeof(IconIdentifier)), Window = window, Id = 1 };
        Rect rectangle;
        return Shell_NotifyIconGetRect(ref identifier, out rectangle) == 0;
    }
    public static void OpenMenu(int pid) {
        var window = MessageWindow(pid);
        if (window == IntPtr.Zero || !PostMessage(window, 0x400 + 1024, new IntPtr(1), new IntPtr(0x205)))
            throw new InvalidOperationException("Owned native tray window was not available.");
    }
}
'@
$root = Join-Path $env:RUNNER_TEMP ('tdsblive-tray-' + [Guid]::NewGuid().ToString('N'))
$data = Join-Path $root 'profile with spaces'
New-Item -ItemType Directory -Force $data, $EvidenceDirectory | Out-Null
$executable = Join-Path $application 'TDSBLive.exe'
$companionExecutable = Join-Path $application 'desktop\TDSBLive.Desktop.exe'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
@{ server = @{ host = '127.0.0.1'; port = $port }; displayName = 'Owned Windows tray check' } |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $data 'configuration.json') -Encoding UTF8
$origin = "http://127.0.0.1:$port"
$shellRestarted = $false
$hostProcess = $null
$desktopIds = New-Object 'System.Collections.Generic.HashSet[int]'

function Wait-For([scriptblock]$Probe, [string]$Failure, [int]$Seconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try { $value = & $Probe; if ($value) { return $value } } catch { }
        Start-Sleep -Milliseconds 150
    }
    throw $Failure
}
function Owned-Hosts {
    @(Get-CimInstance Win32_Process -Filter "Name='TDSBLive.exe'" | Where-Object {
        $_.ExecutablePath -eq $executable -and $_.CommandLine -and
        $_.CommandLine.IndexOf($data, [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
}
function Companion {
    $hosts = @(Owned-Hosts)
    if ($hosts.Count -ne 1) { return $null }
    $children = @(Get-CimInstance Win32_Process -Filter "Name='TDSBLive.Desktop.exe'" | Where-Object {
        $_.ExecutablePath -eq $companionExecutable -and $_.ParentProcessId -eq $hosts[0].ProcessId
    })
    if ($children.Count -eq 1) {
        $desktopIds.Add([int]$children[0].ProcessId) | Out-Null
        return Get-Process -Id $children[0].ProcessId
    }
    return $null
}
function Element([int]$ProcessId, [string]$Name, $Type = $null) {
    $conditions = New-Object 'System.Collections.Generic.List[System.Windows.Automation.Condition]'
    $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId))
    $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $Name))
    if ($Type) { $conditions.Add([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type)) }
    $condition = [System.Windows.Automation.AndCondition]::new($conditions.ToArray())
    return [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Invoke-Element($Target) {
    if (-not $Target.Current.IsEnabled) { throw 'The native desktop action was not enabled.' }
    $Target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Screenshot($Target, [string]$Name) {
    $rectangle = $Target.Current.BoundingRectangle
    if ($rectangle.Width -lt 100 -or $rectangle.Height -lt 50 -or $rectangle.Left -lt 0 -or $rectangle.Top -lt 0) {
        throw 'Owned native UI was not visibly laid out on an interactive desktop.'
    }
    $bitmap = New-Object Drawing.Bitmap ([int]$rectangle.Width), ([int]$rectangle.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen([int]$rectangle.Left, [int]$rectangle.Top, 0, 0, $bitmap.Size)
        $colors = New-Object 'System.Collections.Generic.HashSet[int]'
        for ($y = 0; $y -lt $bitmap.Height; $y += 8) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 8) { $colors.Add($bitmap.GetPixel($x, $y).ToArgb()) | Out-Null }
        }
        if ($colors.Count -lt 4) { throw 'Native screenshot was blank or unavailable; this is not visual qualification.' }
        $bitmap.Save((Join-Path $EvidenceDirectory $Name), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Menu-Action($Desktop, [string]$Action) {
    [TdsTrayDesktop]::OpenMenu($Desktop.Id)
    $item = Wait-For { Element $Desktop.Id $Action ([System.Windows.Automation.ControlType]::MenuItem) } "Native tray menu action '$Action' was not accessible."
    Invoke-Element $item
}
function Confirm-Action($Desktop, [string]$Action, [bool]$Accept, [string]$ScreenshotName) {
    Menu-Action $Desktop $Action
    $dialog = Wait-For { Element $Desktop.Id "$Action TDSBLive?" ([System.Windows.Automation.ControlType]::Window) } 'Native confirmation did not open.'
    $cancel = Wait-For { Element $Desktop.Id 'Cancel' ([System.Windows.Automation.ControlType]::Button) } 'Confirmation had no accessible Cancel button.'
    if (-not $cancel.Current.HasKeyboardFocus) { throw 'Confirmation did not initially focus Cancel.' }
    Screenshot $dialog $ScreenshotName
    if ($Accept) { Invoke-Element (Element $Desktop.Id $Action ([System.Windows.Automation.ControlType]::Button)) }
    else { Invoke-Element $cancel }
}
function Ready {
    try { return (Invoke-RestMethod "$origin/api/application/status" -TimeoutSec 2).generation } catch { return $null }
}
function Browser-Editor {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    foreach ($window in $windows) {
        if ($window.Current.Name -notlike '*TDSBLive Editor*') { continue }
        $browserProcess = Get-Process -Id $window.Current.ProcessId -ErrorAction SilentlyContinue
        if (-not $browserProcess -or $browserProcess.SessionId -ne $session -or $browserProcess.ProcessName -notin @('msedge', 'chrome', 'firefox')) { continue }
        $edits = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))
        foreach ($edit in $edits) {
            $pattern = $null
            if ($edit.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern) -and
                $pattern.Current.Value -like "*127.0.0.1:$port/editor*") { return $window }
        }
    }
    return $null
}

try {
    if ([TdsTrayDesktop]::FindWindow('Shell_TrayWnd', $null) -eq [IntPtr]::Zero) {
        throw 'Explorer tray is unavailable. Native tray qualification requires a real Windows desktop.'
    }
    $hostProcess = Start-Process $executable -ArgumentList "--TDSBLive:DataDirectory=`"$data`" --TDSBLive:OpenEditor=false" -PassThru
    $generation = Wait-For { Ready } 'Owned packaged host never became ready.'
    $desktop = Wait-For { Companion } 'Windows host did not start its desktop companion.'
    Wait-For { [TdsTrayDesktop]::Registered($desktop.Id) } 'The native notification icon was not registered with Explorer.' | Out-Null

    Menu-Action $desktop 'Open editor'
    $browserWindow = Wait-For { Browser-Editor } 'Open editor did not display the configured address in a native Windows browser.'
    Screenshot $browserWindow 'open-editor.png'
    $browserWindow.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if ((Ready) -ne $generation) { throw 'Closing the editor browser stopped TDSBLive.' }

    # Repeated launch cannot create another backend or tray.
    $duplicate = Start-Process $executable -ArgumentList "--TDSBLive:DataDirectory=`"$data`" --TDSBLive:OpenEditor=false" -PassThru
    if (-not $duplicate.WaitForExit(15000) -or $duplicate.ExitCode -ne 0) { throw 'Duplicate launch did not hand off to the existing owner.' }
    if (@(Owned-Hosts).Count -ne 1 -or (Companion).Id -ne $desktop.Id) { throw 'Duplicate launch replaced or duplicated the owned profile.' }

    Confirm-Action $desktop 'Restart' $false 'restart-cancel.png'
    if ((Ready) -ne $generation) { throw 'Cancel unexpectedly restarted TDSBLive.' }
    Confirm-Action $desktop 'Quit' $false 'quit-cancel.png'
    if ((Ready) -ne $generation) { throw 'Cancel unexpectedly quit TDSBLive.' }

    # Explorer restart is isolated to this CI session; never restart an operator's shell.
    $explorers = @(Get-Process explorer | Where-Object { $_.SessionId -eq $session })
    if ($explorers.Count -ne 1) { throw 'Expected one Explorer in the owned interactive CI session.' }
    Stop-Process -Id $explorers[0].Id -Force
    $shellRestarted = $true
    $fallback = Wait-For {
        # Explorer may automatically relaunch. Keep only this disposable CI
        # session's shell absent until the missing-service fallback is observed.
        foreach ($returned in @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $session })) {
            Stop-Process -Id $returned.Id -Force -ErrorAction SilentlyContinue
        }
        Element $desktop.Id 'TDSBLive is running' ([System.Windows.Automation.ControlType]::Window)
    } 'Missing tray did not expose the native control window.'
    Screenshot $fallback 'tray-unavailable.png'
    # Closing the fallback must keep the host running.
    $fallback.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if ((Ready) -ne $generation) { throw 'Closing desktop controls stopped the backend.' }
    Start-Process explorer.exe | Out-Null
    Wait-For { [TdsTrayDesktop]::Registered($desktop.Id) } 'The notification icon did not recover after Explorer restarted.' | Out-Null
    $shellRestarted = $false

    Confirm-Action $desktop 'Restart' $true 'restart-confirm.png'
    $generationAfter = Wait-For { $current = Ready; if ($current -and $current -ne $generation) { $current } } 'Confirmed restart did not start a new backend generation.'
    $previousDesktop = $desktop.Id
    $desktop = Wait-For { $current = Companion; if ($current -and $current.Id -ne $previousDesktop) { $current } } 'Restart did not recreate the native tray companion.'
    Wait-For { -not (Get-Process -Id $previousDesktop -ErrorAction SilentlyContinue) } 'Restart left the old tray process running.' | Out-Null
    Wait-For { [TdsTrayDesktop]::Registered($desktop.Id) } 'Restarted tray was not registered.' | Out-Null

    # A companion crash must leave streaming/backend work intact. Recover using
    # the existing authenticated browser restart, never by killing the backend.
    Stop-Process -Id $desktop.Id -Force
    if ((Ready) -ne $generationAfter) { throw 'Companion failure stopped the streaming backend.' }
    Wait-For {
        $diagnostics = Invoke-RestMethod "$origin/api/diagnostics" -TimeoutSec 2
        $diagnostics.integrationHealth.'Desktop controls'.state -eq 'degraded'
    } 'Companion failure did not become visible in editor diagnostics.' | Out-Null
    $csrf = Invoke-RestMethod "$origin/api/auth/csrf" -SessionVariable editorSession -TimeoutSec 5
    $headers = @{ Origin = $origin; 'X-TDSBLive-CSRF' = $csrf.requestToken }
    Invoke-RestMethod "$origin/api/application/restart" -Method Post -WebSession $editorSession -Headers $headers -TimeoutSec 10 | Out-Null
    $recoveredGeneration = Wait-For { $current = Ready; if ($current -and $current -ne $generationAfter) { $current } } 'Browser restart did not recover a missing companion.'
    $desktop = Wait-For { Companion } 'Browser restart did not recreate desktop controls.'
    Wait-For { [TdsTrayDesktop]::Registered($desktop.Id) } 'Recovered tray was not registered.' | Out-Null

    # A backend crash is never restart intent. The companion shows recovery
    # guidance and offers to close itself without claiming it stopped the backend.
    $ownedBackend = @(Owned-Hosts)[0]
    Stop-Process -Id $ownedBackend.ProcessId -Force
    Wait-For { @(Owned-Hosts).Count -eq 0 } 'The owned backend did not stop.' | Out-Null
    $notice = Wait-For { Element $desktop.Id 'TDSBLive is running' ([System.Windows.Automation.ControlType]::Window) } 'Backend failure did not expose desktop recovery guidance.'
    Screenshot $notice 'backend-stopped.png'
    Invoke-Element (Wait-For { Element $desktop.Id 'Close desktop controls' ([System.Windows.Automation.ControlType]::Button) } 'Recovery guidance had no way to close desktop controls.')
    Wait-For { -not (Get-Process -Id $desktop.Id -ErrorAction SilentlyContinue) } 'Closing stopped desktop controls did not exit the companion.' | Out-Null
    Start-Sleep -Seconds 3
    if (@(Owned-Hosts).Count -ne 0) { throw 'A crashed backend was automatically restarted.' }
    $hostProcess = Start-Process $executable -ArgumentList "--TDSBLive:DataDirectory=`"$data`" --TDSBLive:OpenEditor=false" -PassThru
    Wait-For { Ready } 'Manual recovery did not start the retained profile.' | Out-Null
    $desktop = Wait-For { Companion } 'Manual recovery did not recreate the desktop companion.'
    Wait-For { [TdsTrayDesktop]::Registered($desktop.Id) } 'Manually recovered tray was not registered.' | Out-Null
    Confirm-Action $desktop 'Quit' $true 'quit-confirm.png'
    Wait-For { @(Owned-Hosts).Count -eq 0 } 'Confirmed Quit left the backend running.' | Out-Null
    Wait-For { -not (Get-Process -Id $desktop.Id -ErrorAction SilentlyContinue) } 'Quit left a stale tray process running.' | Out-Null
    if (-not (Test-Path (Join-Path $data 'tdsblive.db'))) { throw 'Quit discarded the owned profile.' }
    @{
        passed = $true; evidence = 'actual packaged native Windows UI'; package = [IO.Path]::GetFileName($application)
        scenarios = @('native icon registration', 'Open editor native browser handoff', 'browser close leaves host running',
            'duplicate launch', 'accessible Restart and Quit', 'Cancel-first focus',
            'Cancel preserves generation', 'Explorer loss and fallback', 'fallback close leaves host running',
            'Explorer re-registration', 'confirmed restart', 'old tray cleanup', 'companion crash isolation and diagnostics',
            'browser restart recovers companion', 'backend crash guidance without automatic restart', 'confirmed quit', 'profile retained')
        remaining = @('light/dark and high-DPI desktop checks', 'actual final-package OBS checks')
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $EvidenceDirectory 'tray-result.json') -Encoding UTF8
    Write-Output 'Actual owned Windows tray/confirmation/Explorer/restart/quit qualification passed. Remaining release gates are recorded separately.'
} finally {
    if ($shellRestarted -and [TdsTrayDesktop]::FindWindow('Shell_TrayWnd', $null) -eq [IntPtr]::Zero) { Start-Process explorer.exe | Out-Null }
    foreach ($owned in @(Owned-Hosts)) {
        foreach ($child in @(Get-CimInstance Win32_Process -Filter "Name='TDSBLive.Desktop.exe'" | Where-Object { $_.ParentProcessId -eq $owned.ProcessId -and $_.ExecutablePath -eq $companionExecutable })) {
            Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Stop-Process -Id $owned.ProcessId -Force -ErrorAction SilentlyContinue
    }
    foreach ($desktopId in $desktopIds) {
        $orphan = Get-Process -Id $desktopId -ErrorAction SilentlyContinue
        if ($orphan -and $orphan.MainModule.FileName -eq $companionExecutable) { Stop-Process -Id $desktopId -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
}

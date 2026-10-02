param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '../release')
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Native package qualification requires Windows.' }
$package = [IO.Path]::GetFullPath($PackageDirectory)
$root = Join-Path $env:RUNNER_TEMP ('tdsblive-package-check-' + [Guid]::NewGuid().ToString('N'))
$previousPackageCheckRoot = $env:TDSBLIVE_PACKAGE_CHECK_ROOT
$env:TDSBLIVE_PACKAGE_CHECK_ROOT = $root
New-Item -ItemType Directory -Path $root | Out-Null
$installed = Join-Path $root 'installed'
$data = Join-Path $root 'data'
$installer = Join-Path $package "TDSBLive-$Version-win-x64-setup.exe"
$process = $null
$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'TDSBLive.lnk'
if (Test-Path $startupShortcut) { throw 'Package qualification requires a runner without an existing TDSBLive startup shortcut.' }
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
function Invoke-Installer([string]$Executable, [string]$Arguments) {
    $operation = Start-Process $Executable -ArgumentList $Arguments -Wait -PassThru
    if ($operation.ExitCode -ne 0) { throw 'Installer operation failed.' }
}
function Test-Application([string]$Directory) {
    & sonar analyze secrets $Directory
    if ($LASTEXITCODE -ne 0) { throw 'Package secrets scan failed; contents were not inspected.' }
    $runtime = Get-Content (Join-Path $Directory 'TDSBLive.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks) { throw 'Package requires a separately installed runtime.' }
    foreach ($required in @('coreclr.dll', 'hostfxr.dll', 'Microsoft.AspNetCore.dll', 'TDSBLive.exe', 'guide/Home.html', 'guide/images/guided-setup.png', 'integrations/tdsblive-streamerbot.sb')) {
        if (-not (Test-Path (Join-Path $Directory $required))) { throw "Self-contained package is missing $required" }
    }
    $guide = Join-Path $Directory 'guide'
    foreach ($page in Get-ChildItem $guide -Filter '*.html') {
        $document = Get-Content $page.FullName -Raw -Encoding utf8
        foreach ($link in [regex]::Matches($document, '(?:href|src)="([^"]+)"')) {
            $target = $link.Groups[1].Value
            if ($target.StartsWith('http://') -or $target.StartsWith('https://')) { continue }
            if (-not (Test-Path (Join-Path $guide $target))) { throw 'Offline guide contains a broken local link.' }
        }
    }
    $home = Get-Content (Join-Path $guide 'Home.html') -Raw -Encoding utf8
    if ($home -notmatch 'href="https://github.com/TechDaddyKB/tdsblive/blob/[^" ]+/docs/implementation-plan\.md"') {
        throw 'Offline guide must preserve the external implementation-plan Markdown link.'
    }
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    @{ server = @{ host = '127.0.0.1'; port = $port; enableLan = $false; allowedHosts = @() } } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $data 'configuration.json')
    # Start the shipped EXE directly, not dotnet. No live integrations or browser launch.
    $script:process = Start-Process (Join-Path $Directory 'TDSBLive.exe') `
        -ArgumentList "--TDSBLive:DataDirectory=`"$data`"" -PassThru `
        -RedirectStandardOutput (Join-Path $root 'host.stdout') -RedirectStandardError (Join-Path $root 'host.stderr')
    try {
        $ready = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($script:process.HasExited) { throw 'Packaged executable exited before readiness.' }
            try {
                $status = Invoke-RestMethod "http://127.0.0.1:$port/api/status" -TimeoutSec 2 -NoProxy
                if ($status.name -eq 'TDSBLive' -and $status.httpSupported) { $ready = $true; break }
            } catch { }
            Start-Sleep -Milliseconds 200
        }
        if (-not $ready) { throw 'Packaged application did not become ready.' }
        foreach ($route in @('/editor', '/overlay/combined-chat', '/chat/combined-chat')) {
            $response = Invoke-WebRequest "http://127.0.0.1:$port$route" -TimeoutSec 5 -NoProxy
            if ($response.StatusCode -ne 200 -or $response.Content -notmatch '<html') { throw 'Packaged browser assets did not load.' }
        }
        if (-not (Test-Path (Join-Path $data 'tdsblive.db'))) { throw 'Packaged application did not initialize its database.' }
    } finally {
        if ($script:process -and -not $script:process.HasExited) { Stop-Process -Id $script:process.Id -Force; $script:process.WaitForExit() }
        $script:process = $null
    }
}
try {
    $portable = Join-Path $root 'portable'
    Expand-Archive (Join-Path $package "TDSBLive-$Version-win-x64.zip") $portable
    Test-Application $portable
    & node (Join-Path $PSScriptRoot 'browser-qualification/recovery-process.mjs') portable
    if ($LASTEXITCODE -ne 0) { throw 'Portable EXE restart/restore qualification failed.' }
    Invoke-Installer $installer "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$installed`" /TASKS=`"`""
    if (Test-Path $startupShortcut) { throw 'Login startup must be disabled by default.' }
    Test-Application $installed
    & node (Join-Path $PSScriptRoot 'browser-qualification/recovery-process.mjs') installed
    if ($LASTEXITCODE -ne 0) { throw 'Installed EXE restart/restore qualification failed.' }
    # A repeated install exercises replacement/upgrade mechanics without inventing a prior release.
    Invoke-Installer $installer "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$installed`" /TASKS=`"startup`""
    if (-not (Test-Path $startupShortcut)) { throw 'Opt-in login startup shortcut was not installed.' }
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($startupShortcut)
    if ($shortcut.TargetPath -ne (Join-Path $installed 'TDSBLive.exe') -or $shortcut.Arguments) {
        throw 'Startup shortcut must target only the installed executable without opening an editor.'
    }
    Test-Application $installed
    Invoke-Installer (Join-Path $installed 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    if (Test-Path (Join-Path $installed 'TDSBLive.exe')) { throw 'Uninstall left the application executable behind.' }
    if (Test-Path $startupShortcut) { throw 'Uninstall left the login startup shortcut behind.' }
    if (-not (Test-Path (Join-Path $data 'tdsblive.db'))) { throw 'Uninstall removed separately stored user data.' }
    Write-Output 'Native Windows portable/install/reinstall/uninstall checks passed. Streaming-PC performance and OBS rendering are separate checks.'
} finally {
    $env:TDSBLIVE_PACKAGE_CHECK_ROOT = $previousPackageCheckRoot
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
    # Failed qualification must not leave an enabled startup entry on the runner.
    if (Test-Path (Join-Path $installed 'unins000.exe')) {
        Invoke-Installer (Join-Path $installed 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    }
    Remove-Item $root -Recurse -Force
}

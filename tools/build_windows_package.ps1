param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$CompilerPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../release')
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows packages must be built on the GitHub Actions Windows runner.' }
$repository = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
$publish = Join-Path $output 'portable'
if (Test-Path $publish) { throw 'Use a fresh packaging output directory.' }
New-Item -ItemType Directory -Path $publish -Force | Out-Null
Push-Location $repository
try {
    & dotnet restore src/ExtensionSuite.Host/ExtensionSuite.Host.csproj --runtime win-x64 --locked-mode -p:NuGetLockFilePath=packages.win-x64.lock.json
    if ($LASTEXITCODE -ne 0) { throw 'Locked Windows restore failed.' }
    & dotnet publish src/ExtensionSuite.Host/ExtensionSuite.Host.csproj --configuration Release --no-restore `
        -p:PublishProfile=WindowsPortable "-p:Version=$Version" --output $publish
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    & python tools/build_offline_guide.py (Join-Path $publish 'guide')
    if ($LASTEXITCODE -ne 0) { throw 'Offline user guide generation failed.' }
    foreach ($required in @('TDSBLive.exe', 'TDSBLive.dll', 'TDSBLive.runtimeconfig.json', 'wwwroot/editor/index.html', 'wwwroot/runtime/index.html')) {
        if (-not (Test-Path (Join-Path $publish $required))) { throw "Published package is missing $required" }
    }
    $privateFiles = Get-ChildItem $publish -Recurse -File | Where-Object {
        $_.Name -eq 'configuration.json' -or $_.Extension -in @('.db', '.sqlite', '.sqlite3', '.dpapi', '.log', '.pfx', '.pem') -or
        $_.FullName -match '[\\/](credentials|references|node_modules|logs|\.secrets)[\\/]'
    }
    if ($privateFiles) { throw 'Published package contains forbidden runtime/private content.' }
    Copy-Item (Join-Path $repository 'LICENSE') (Join-Path $publish 'LICENSE.txt')
    $archive = Join-Path $output "TDSBLive-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
    & $CompilerPath "/DAppVersion=$Version" "/DPublishDirectory=$publish" "/DOutputDirectory=$output" `
        "/DRepositoryDirectory=$repository" (Join-Path $repository 'packaging/windows/tdsblive.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $output "TDSBLive-$Version-win-x64-setup.exe"
    if (-not (Test-Path $installer)) { throw 'Installer artifact was not produced.' }
    @($archive, $installer) | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_)
    } | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8NoBOM
}
finally { Pop-Location }

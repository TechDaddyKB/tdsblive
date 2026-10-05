# Dot-source only from the guarded, disposable native Windows qualification.
if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) {
    throw 'Native appearance qualification is restricted to the isolated CI runner.'
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TdsThemeDesktop {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr parameter,
        string setting, uint flags, uint timeout, out UIntPtr result);
    public static void NotifyTheme() {
        UIntPtr result;
        // Broadcast the ordinary Windows setting-change notification. Some
        // unrelated CI windows may time out; actual UISettings is checked next.
        SendMessageTimeout(new IntPtr(0xffff), 0x1a, IntPtr.Zero, "ImmersiveColorSet", 2, 250, out result);
    }
}
'@
function Windows-AppTheme {
    $ui = New-Object Windows.UI.ViewManagement.UISettings
    $background = $ui.GetColorValue([Windows.UI.ViewManagement.UIColorType]::Background)
    if (($background.R + $background.G + $background.B) -lt 383) { return 'dark' }
    return 'light'
}
function Qualify-WindowsThemes($Desktop) {
    try {
        [Windows.UI.ViewManagement.UISettings, Windows.UI.ViewManagement, ContentType = WindowsRuntime] | Out-Null
        Windows-AppTheme | Out-Null
    } catch {
        return @{ passed = $false; limitation = 'This Windows CI image does not expose UISettings theme observation.' }
    }
    $path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    $existed = Test-Path $path
    if (-not $existed) { New-Item -Path $path -Force | Out-Null }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Themes\Personalize', $true)
    $saved = @{}
    try {
        foreach ($name in @('AppsUseLightTheme', 'SystemUsesLightTheme')) {
            $present = $key.GetValueNames() -contains $name
            if ($present -and $key.GetValueKind($name) -ne [Microsoft.Win32.RegistryValueKind]::DWord) {
                throw 'CI theme preferences have an unexpected registry type; they were not changed.'
            }
            $saved[$name] = @{ present = $present; value = $key.GetValue($name) }
        }
        foreach ($theme in @('light', 'dark')) {
            $value = 0
            if ($theme -eq 'light') { $value = 1 }
            foreach ($name in $saved.Keys) { $key.SetValue($name, $value, [Microsoft.Win32.RegistryValueKind]::DWord) }
            [TdsThemeDesktop]::NotifyTheme()
            Wait-For { (Windows-AppTheme) -eq $theme } "The actual Windows theme did not become $theme." | Out-Null
            foreach ($action in @('Restart', 'Quit')) {
                $file = "theme-$theme-$($action.ToLowerInvariant()).png"
                Confirm-Action $Desktop $action $false $file 'enter'
                $luminance = $screenshotMetrics[$file].dominantLuminance
                if (($theme -eq 'dark' -and $luminance -ge 128) -or ($theme -eq 'light' -and $luminance -lt 128)) {
                    throw "The actual $action confirmation did not render the requested $theme theme."
                }
            }
        }
        return @{ passed = $true; evidence = 'actual Windows UISettings and visible packaged confirmations'; variants = @('light', 'dark') }
    } finally {
        foreach ($name in $saved.Keys) {
            if ($saved[$name].present) { $key.SetValue($name, $saved[$name].value, [Microsoft.Win32.RegistryValueKind]::DWord) }
            else { $key.DeleteValue($name, $false) }
        }
        $key.Close()
        if (-not $existed) { Remove-Item $path }
        [TdsThemeDesktop]::NotifyTheme()
    }
}

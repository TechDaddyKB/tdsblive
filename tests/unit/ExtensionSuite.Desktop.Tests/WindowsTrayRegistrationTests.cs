using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class WindowsTrayRegistrationTests
{
    [WindowsOnlyFact]
    public void OtherApplicationsTrayIconsCannotBeMistakenForThisCompanion()
    {
        // This test process owns no native notification icon. A successful
        // desktop API call must still report that this process is unregistered.
        if (OperatingSystem.IsWindows()) Assert.False(WindowsTrayRegistration.IsAvailable());
    }
}

internal sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires actual Windows notification-area APIs.";
    }
}

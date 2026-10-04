using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using ExtensionSuite.Desktop;
using Xunit;

namespace ExtensionSuite.Desktop.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class DesktopConfirmationTests
{
    private static async Task RunAsync(Func<Task> test)
    {
        // Keep the repository's pinned xUnit 2 stack. Avalonia's 12.1 XUnit
        // adapter targets xUnit 3; its plain headless session supports both.
        await using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await session.Dispatch(async () => { await test(); return true; }, deadline.Token);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("quit")]
    public Task ConfirmationStartsOnCancelAndEnterDoesNotAdmitAnOperation(string command) => RunAsync(async () =>
    {
        var dialog = new DesktopConfirmation(command);
        var answer = dialog.ShowAsync();
        Assert.True(dialog.Cancel.IsFocused);
        Assert.False(dialog.Confirm.IsDefault);
        Assert.False(answer.IsCompleted);
        dialog.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.False(await answer);
    });

    [Fact]
    public Task EscapeAndClosingWindowCancelRatherThanQuit() => RunAsync(async () =>
    {
        var dialog = new DesktopConfirmation("quit");
        var answer = dialog.ShowAsync();
        dialog.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(await answer);
        var closed = new DesktopConfirmation("quit");
        closed.Window.Show();
        closed.Window.Close();
        Assert.False(await closed.Answer);
    });

    [Theory]
    [InlineData("restart", "Restart")]
    [InlineData("quit", "Quit")]
    public Task ExplicitConfirmationAdmitsExactlyTheNamedOperation(string command, string label) => RunAsync(async () =>
    {
        var dialog = new DesktopConfirmation(command);
        var answer = dialog.ShowAsync();
        Assert.Equal(label, dialog.Confirm.Content);
        Assert.Equal(label + " TDSBLive?", dialog.Window.Title);
        Assert.True(dialog.Cancel.MinHeight >= 44);
        Assert.True(dialog.Confirm.MinHeight >= 44);
        dialog.Confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(await answer);
        Assert.False(dialog.Window.IsVisible);
    });
}

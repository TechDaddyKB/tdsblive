using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExtensionSuite.Desktop;

internal sealed class DesktopConfirmation
{
    private readonly TaskCompletionSource<bool> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Window Window { get; }
    public Button Cancel { get; } = new() { Content = "Cancel", IsCancel = true, MinHeight = 44, MinWidth = 100 };
    public Button Confirm { get; }
    public Task<bool> Answer => answer.Task;

    public DesktopConfirmation(string command)
    {
        if (command is not ("restart" or "quit")) throw new ArgumentException("Invalid desktop confirmation.");
        var label = command == "restart" ? "Restart" : "Quit";
        Confirm = new Button { Content = label, MinHeight = 44, MinWidth = 100 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        buttons.Children.Add(Cancel);
        buttons.Children.Add(Confirm);
        var content = new StackPanel { Margin = new Thickness(24), Spacing = 20 };
        content.Children.Add(new TextBlock { Text = label + " TDSBLive?", FontSize = 22, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            Text = "Save any changes in the editor first. " + (command == "restart" ?
                "Your overlays will briefly stop while TDSBLive restarts." : "Your overlays will stop until you open TDSBLive again."),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(buttons);
        Window = new Window { Title = label + " TDSBLive?", Width = 440, SizeToContent = SizeToContent.Height,
            CanResize = false, Content = content, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        Cancel.Click += (_, _) => Window.Close();
        Confirm.Click += (_, _) => { answer.TrySetResult(true); Window.Close(); };
        Window.Closed += (_, _) => answer.TrySetResult(false);
        Window.Opened += (_, _) => { Window.Activate(); Cancel.Focus(); };
        // Modeless confirmation still has an explicit Escape alternative.
        Window.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape) { eventArgs.Handled = true; Window.Close(); }
        };
    }

    public Task<bool> ShowAsync()
    {
        Window.Show();
        return Answer;
    }
}

using System.ComponentModel;
using System.Diagnostics;
using ExtensionSuite.Core;

namespace ExtensionSuite.Host;

public interface IEditorBrowserLauncher
{
    bool Open(ServerConfiguration server);
}

public sealed class EditorBrowserLauncher(ILogger<EditorBrowserLauncher> logger,
    Func<ProcessStartInfo, Process?>? start = null) : IEditorBrowserLauncher
{
    public static Uri EditorUri(ServerConfiguration server)
    {
        server.Validate();
        var host = server.Host switch { "0.0.0.0" => "127.0.0.1", "::" => "::1", var address => address };
        return new UriBuilder("http", host, server.Port, "/editor").Uri;
    }

    public bool Open(ServerConfiguration server)
    {
        var editor = EditorUri(server);
        try
        {
            using var process = (start ?? Process.Start)(new ProcessStartInfo(editor.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning("Could not open the browser. Open {EditorAddress} in your browser.", editor.AbsoluteUri);
            return false;
        }
    }
}

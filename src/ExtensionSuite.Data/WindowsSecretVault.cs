using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ExtensionSuite.Data;

/// <summary>Windows per-user DPAPI credentials. Never falls back to plaintext storage.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSecretVault(string directory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TDSBLive.credentials.v1");

    public string[] Names() => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.dpapi").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToArray()
        : [];

    private string FilePath(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Secret names must be ASCII letters, digits or hyphens.", nameof(name));
        return Path.Combine(directory, name + ".dpapi");
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        var path = FilePath(name);
        await gate.WaitAsync(cancellationToken);
        byte[]? clear = null;
        try
        {
            Directory.CreateDirectory(directory);
            clear = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            var temporary = path + ".pending";
            await File.WriteAllBytesAsync(temporary, encrypted, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
            gate.Release();
        }
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = FilePath(name);
        await gate.WaitAsync(cancellationToken);
        byte[]? clear = null;
        try
        {
            if (!File.Exists(path)) return null;
            var encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
            clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        finally
        {
            if (clear is not null) CryptographicOperations.ZeroMemory(clear);
            gate.Release();
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = FilePath(name);
        await gate.WaitAsync(cancellationToken);
        try { File.Delete(path); }
        finally { gate.Release(); }
    }
}

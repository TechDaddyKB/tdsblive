using System.Collections.Concurrent;

namespace ExtensionSuite.Core;

public sealed class SensitiveValues
{
    private readonly ConcurrentDictionary<string, string> values = new();
    public void Set(string name, string value) => values[name] = value;
    public void Remove(string name) => values.TryRemove(name, out _);
    public string[] Snapshot() => values.Values.ToArray();
}

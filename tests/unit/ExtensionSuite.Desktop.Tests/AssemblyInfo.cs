using Xunit;

// Avalonia uses process-wide platform/URI registration even in headless sessions.
// Each session owns its UI thread and must finish before another session starts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

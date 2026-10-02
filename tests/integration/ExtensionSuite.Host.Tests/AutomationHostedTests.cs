using ExtensionSuite.Core;
using ExtensionSuite.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExtensionSuite.Host.Tests;

public sealed class AutomationHostedTests
{
    [Fact]
    public async Task HostAutomaticallyPlansLiveEventWithoutInvokingAnyRealAdapter()
    {
        var adapter = new RecordingAdapter();
        using var owner = new FoundationHostFactory();
        using var app = owner.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IAutomationActionDispatcher>(adapter))));
        using var http = app.CreateClient();
        var rules = app.Services.GetRequiredService<AutomationRuleStore>();
        await rules.SaveAsync(new()
        {
            Enabled = true, Condition = new("twitch", "support.bits"),
            Actions = [new() { Speech = new() { Voice = "owned" } }]
        });
        var item = new CanonicalEvent { Source = "owned", Platform = "twitch", Type = "support.bits", NativeType = "owned",
            DedupeKey = "hosted-live", OccurredAt = DateTimeOffset.UtcNow, Support = new("bits", 100) };
        var events = app.Services.GetRequiredService<EventStore>();
        Assert.True(await events.AcceptAsync(item, "live"));
        var execution = await adapter.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(Guid.Empty, execution);
        Assert.Equal(1, adapter.Calls);
        Assert.True(await events.AcceptAsync(item with { Id = Guid.CreateVersion7(), Provenance = EventProvenance.Replay }, "replay", persistTest: true));
        await app.Services.GetRequiredService<AutomationEventReader>().ProcessAsync();
        Assert.Equal(1, adapter.Calls);
        var receipts = await app.Services.GetRequiredService<AutomationExecutionStore>().ListAsync();
        Assert.Single(receipts);
    }

    private sealed class RecordingAdapter : IAutomationActionDispatcher
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource<Guid> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AutomationDispatchOutcome> DispatchAsync(Guid executionId, AutomationPlannedAction action, CancellationToken ct)
        {
            Interlocked.Increment(ref calls); Called.TrySetResult(executionId);
            return Task.FromResult(new AutomationDispatchOutcome("completed"));
        }
    }
}

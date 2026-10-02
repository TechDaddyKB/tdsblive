namespace ExtensionSuite.StreamerBot;

internal static class BotConnectionProbe
{
    public static async Task<BotConnectionState> TestAsync(BotProtocolSession? session,
        BotConnectionState state, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (session is null || state.State != "connected")
            return state with { State = state.State == "connected" ? "disconnected" : state.State };
        try
        {
            // Correlated read-only response: never speak, change queues or run an action.
            await session.RequestAsync("GetInfo", null, timeout, cancellationToken);
            return state with { FailureKind = null };
        }
        catch (BotRequestException error)
        {
            // Older Speaker.bot versions may reject this optional metadata request.
            // Report the test limitation without disconnecting their working session.
            return state with { State = "probeFailed", FailureKind = error.Kind };
        }
    }
}

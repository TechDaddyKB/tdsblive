using System;

public class CPHInline
{
    public void Init()
    {
        Register("Chat message", "tdsblive.rumble.chat", "Rumble");
        Register("Rant", "tdsblive.rumble.rant", "Rumble");
        Register("Follow", "tdsblive.rumble.follow", "Rumble");
        Register("Subscription (evidence gated)", "tdsblive.rumble.subscription", "Rumble");
        Register("Gift (evidence gated)", "tdsblive.rumble.gift", "Rumble");
        Register("Stream online", "tdsblive.rumble.online", "Rumble");
        Register("Stream offline", "tdsblive.rumble.offline", "Rumble");
        Register("Viewer count changed", "tdsblive.rumble.viewers", "Rumble");
        Register("Likes changed", "tdsblive.rumble.likes", "Rumble");
        Register("API health changed", "tdsblive.rumble.health", "Rumble");
        Register("Financial totals changed", "tdsblive.finance.changed", "Finance");
        Register("Overlay state changed", "tdsblive.overlay.changed", "Overlays");
        Register("Local qualification probe", "tdsblive.test.trigger", "Tests");
    }

    private void Register(string label, string eventName, string category)
    {
        if (!CPH.RegisterCustomTrigger(label, eventName, new[] { "TDSBLive", category }))
            CPH.LogWarn("TDSBLive custom-trigger registration was rejected: " + eventName);
    }

    public bool Execute() { return true; }
}

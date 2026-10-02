namespace ExtensionSuite.Core;

public sealed record AutomationPlannedAction(Guid RuleId, int RuleVersion, AutomationAction Action,
    string State, string? SpeechText, string QueueGroup, string QueuePolicy, int MaximumQueueLength,
    int CooldownSeconds)
{
    public Guid? ExecutionId { get; init; }
}

public static class AutomationPlanner
{
    public static AutomationPlannedAction[] Evaluate(IEnumerable<AutomationRule> rules, CanonicalEvent item,
        bool? anonymous = null, string? language = null)
    {
        var result = new List<AutomationPlannedAction>();
        foreach (var rule in rules)
        {
            rule.Validate();
            if (!rule.Enabled || !rule.Condition.Matches(item)) continue;
            foreach (var action in rule.Actions)
            {
                string state = "queued";
                string? speech = null;
                if (action.Speech is { } settings)
                {
                    // Unknown anonymity cannot authorize reading an anonymous donation message.
                    var prepared = AutomationSpeech.Prepare(settings, item,
                        anonymous ?? item.Automation?.Anonymous ?? true, language ?? item.Automation?.Language);
                    state = prepared.State == "ready" ? "queued" : prepared.State;
                    speech = prepared.Text;
                }
                result.Add(new(rule.Id, rule.Version, action, state, speech, rule.QueueGroup,
                    rule.QueuePolicy, rule.MaximumQueueLength, rule.CooldownSeconds));
            }
        }
        return result.ToArray();
    }
}

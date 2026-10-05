# Automation: speech and sound rules

An automation rule says, “When this kind of event arrives and matches my conditions, do this action.” Keep rules disabled while you learn what they do. A successful connection test does not mean every rule is safe or useful for your stream.

## 1. Create one draft rule

Open **Automation** and choose **New speech rule** or **New sound rule**. Give the rule a
name you recognize. Choose a named platform and incoming trigger, then the
condition and action. Keep the rule disabled until its simulation looks right.

For speech, use a voice alias that exists in **your own Speaker.bot setup**. An example alias in a picture or guide is not a voice automatically installed on your computer. Test that voice in Speaker.bot first.

For a sound, select the intended media or approved action. Grant only the action permissions you need. Review any imported action before allowing a rule to call it.

![Automation rules with made-up examples](images/automation-rules.png)

The example screen illustrates the controls. Choose values for your own show rather than copying all the example settings.

## 2. Choose a money amount or a quantity

Choose **Reported money amount** for an incoming amount of money. Select the
currency, then enter an ordinary amount: `5.00` in USD means five dollars.
Choose the comparison you want, such as a minimum or a range. A range's upper
bound is excluded; an amount equal to that bound does not match.

Choose **Quantity** for a count such as Bits. Missing or incompatible support
facts do not satisfy a money/quantity condition. Use the simulation's ordinary
amount field to check one example before enabling the rule. **Advanced currency
scale** remains available for unusual currencies; ordinary setup needs no cents
conversion or internal event names. Older builds may show raw minor-unit fields;
use the guide bundled with that version.

## 3. Decide how repeated events should behave

A **cooldown** is a pause before the rule can fire again. It helps prevent repeated notifications from speaking or playing continuously.

A **queue** holds events to process later. **Ignore** drops an event in the situation described by the control. **Interrupt** can stop an ongoing action to handle another one. Choose intentionally; a busy chat can behave very differently from one quiet test.

## 4. Simulate before enabling

Use the rule's simulation or test view to inspect what would match. Simulation does not prove an action actually ran, does not prove the audio reached your speakers, and is not a real financial event.

Check the selected source, amount units, text, voice alias, cooldown and queue behavior. Then perform a bounded local playback check if you intend the rule to speak or play sound. Listen at a comfortable volume.

Enable the rule only after its behavior is what you want. Review the enabled rules before a broadcast. You can leave all rules disabled while using chat and overlays.

## 5. Avoid accidental repeats after recovery

Restored rules are disabled for safety. Review and enable only the ones you want after a restore. Do not replay an uncertain event simply because you are unsure whether an earlier action ran; it may have already produced its effect.

If an action appears twice, inspect duplicate forwarding and trigger registrations before changing unrelated audio settings.

Next: [Backup and recovery](Backup-and-Recovery.md).

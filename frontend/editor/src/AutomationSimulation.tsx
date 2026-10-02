import { useState } from 'react';
import { automation, type AutomationRule } from './automationApi';

export function AutomationSimulation({ rule }: Readonly<{ rule: AutomationRule }>) {
  const [amount, setAmount] = useState(String(rule.condition.value)); const [message, setMessage] = useState('Owned test message');
  const [anonymous, setAnonymous] = useState(false); const [language, setLanguage] = useState('');
  const [messagePublic, setMessagePublic] = useState(true);
  const [result, setResult] = useState(''); const [busy, setBusy] = useState(false);
  async function simulate() {
    setBusy(true);
    try {
      const money = rule.condition.unit === 'native-money';
      const response = await automation.simulate({ rule, anonymous, language: language || null,
        event: { source: 'automation-preview', platform: rule.condition.platform, type: rule.condition.eventType,
          nativeType: 'OwnedSimulation', dedupeKey: crypto.randomUUID(), occurredAt: new Date().toISOString(),
          user: { displayName: 'Owned test viewer', isBot: false }, message: { text: message },
          automation: { anonymous, messagePublic, language: language || null },
          support: { kind: money ? 'donation' : 'bits', quantity: money ? '1' : amount, tier: '', giftRole: 'none',
            ...(money ? { nativeMoney: { amountMinor: amount, currency: rule.condition.currency ?? 'USD', minorUnitDigits: rule.condition.minorUnitDigits ?? 2 } } : {}) }
        } });
      setResult(JSON.stringify(response, null, 2));
    } catch { setResult('Simulation failed. Check the rule, voice and test amount.'); }
    finally { setBusy(false); }
  }
  return <fieldset disabled={busy}><legend>Safe simulation</legend>
    <p>Evaluates this draft even while disabled. No speech, sounds, actions or financial writes occur. Queue admission and external capabilities are checked during live execution.</p>
    <label>Test quantity or native minor units <input required inputMode="numeric" pattern="[0-9]+" value={amount} onChange={event => setAmount(event.target.value)} /></label>
    <label>Test donation message <textarea value={message} onChange={event => setMessage(event.target.value)} /></label>
    <label><input type="checkbox" checked={anonymous} onChange={event => setAnonymous(event.target.checked)} />Anonymous test viewer</label>
    <label><input type="checkbox" checked={messagePublic} onChange={event => setMessagePublic(event.target.checked)} />Public test message</label>
    <label>Verified test language <input value={language} onChange={event => setLanguage(event.target.value)} /></label>
    <button type="button" onClick={() => void simulate()}>Simulate selected rule</button>
    <pre aria-label="Simulation result">{result}</pre>
  </fieldset>;
}

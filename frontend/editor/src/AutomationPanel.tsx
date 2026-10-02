import { useEffect, useState } from 'react';
import { automation, type AutomationAction, type AutomationExecution, type AutomationRule } from './automationApi';
import { bots, type Discovery } from './api';
import { AutomationActionSettings } from './AutomationActionSettings';
import { AutomationSimulation } from './AutomationSimulation';
import { AutomationReceiptReview } from './AutomationReceiptReview';
import { AutomationCapabilities } from './AutomationCapabilities';
import { AutomationTemporaryEffects } from './AutomationTemporaryEffects';

export function newAutomationRule(kind: string): AutomationRule {
  return { id: crypto.randomUUID(), name: kind === 'speech' ? 'Ko-fi TTS' : kind === 'sound' ? 'Bits sound' : 'Bits VTube Studio action', enabled: false, version: 0,
    condition: { platform: kind === 'speech' ? 'kofi' : 'twitch', eventType: kind === 'speech' ? 'support.donation' : 'support.bits',
      unit: kind === 'speech' ? 'native-money' : 'quantity', operator: 'minimum', value: kind === 'speech' ? '1000' : '100',
      ...(kind === 'speech' ? { currency: 'USD', minorUnitDigits: 2 } : {}) },
    actions: [newAction(kind)], queueGroup: kind === 'speech' ? 'tts' : kind === 'sound' ? 'sounds' : 'vts', queuePolicy: 'queue', maximumQueueLength: 20, cooldownSeconds: 0 };
}
function newAction(kind: string): AutomationAction {
  return { id: crypto.randomUUID(), kind, ...(kind === 'speech' ? { speech: { voice: '', template: '{from} donated {amount} {currency}. {message}',
    maximumCharacters: 300, stripUrls: true, maximumRepeatedCharacters: 3, maximumPunctuationRun: 3, ignoreAnonymousMessage: true,
    speakerBadWordFilter: true, speakUsername: true, speakAmount: true, speakMessage: true, manualModeration: false } } : {}) };
}

export function AutomationPanel() {
  const [rules, setRules] = useState<AutomationRule[]>([]); const [rule, setRule] = useState<AutomationRule | null>(null);
  const [executions, setExecutions] = useState<AutomationExecution[]>([]); const [discovery, setDiscovery] = useState<Discovery | null>(null);
  const [busy, setBusy] = useState(false); const [status, setStatus] = useState('Loading automation');
  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([automation.rules(controller.signal), automation.executions(controller.signal)]).then(([rows, receipts]) => {
      if (!controller.signal.aborted) { setRules(rows); setExecutions(receipts); setStatus(''); }
    }).catch(() => { if (!controller.signal.aborted) setStatus('Automation unavailable'); });
    void bots.discovery().then(value => { if (!controller.signal.aborted) setDiscovery(value); }).catch(() => {});
    const timer = setInterval(() => { void automation.executions(controller.signal).then(value => { if (!controller.signal.aborted) setExecutions(value); }).catch(() => {}); }, 3000);
    return () => { controller.abort(); clearInterval(timer); };
  }, []);
  async function operation(task: () => Promise<unknown>, message: string) {
    setBusy(true);
    try { await task(); setRules(await automation.rules()); setExecutions(await automation.executions()); setStatus(message); }
    catch { setStatus('Operation failed. Check configuration or reload after a version conflict.'); }
    finally { setBusy(false); }
  }
  function actionChange(index: number, patch: Partial<AutomationAction>) {
    setRule(current => current && ({ ...current, actions: current.actions?.map((action, i) => i === index ? { ...action, ...patch } : action) }));
  }
  return <section aria-label="Automation rules">
    <h2>Automation rules</h2><p>Rules do not control financial ingestion. New rules are disabled. VTube Studio actions run through Streamer.bot.</p>
    <output aria-live="polite">{status}</output>
    <fieldset disabled={busy}><legend>Rules</legend>
      {['speech', 'sound', 'streamerbot'].map(kind => <button key={kind} onClick={() => setRule(newAutomationRule(kind))}>New {kind === 'streamerbot' ? 'VTube Studio' : kind} rule</button>)}
      <ul>{rules.map(value => <li key={value.id}>{value.name} · {value.enabled ? 'Enabled' : 'Disabled'} <button onClick={() => setRule(value)}>Edit {value.name}</button></li>)}</ul>
    </fieldset>
    {rule && <form onSubmit={event => { event.preventDefault(); void operation(async () => setRule(await automation.save(rule)), 'Rule saved'); }}>
      <fieldset disabled={busy}><legend>Edit automation rule</legend>
        <label>Rule name <input required maxLength={128} value={rule.name ?? ''} onChange={event => setRule({ ...rule, name: event.target.value })} /></label>
        <label><input type="checkbox" checked={rule.enabled ?? false} onChange={event => setRule({ ...rule, enabled: event.target.checked })} />Enable live automation</label>
        <label>Platform <input required value={rule.condition.platform} onChange={event => setRule({ ...rule, condition: { ...rule.condition, platform: event.target.value } })} /></label>
        <label>Normalized event type <input required value={rule.condition.eventType} onChange={event => setRule({ ...rule, condition: { ...rule.condition, eventType: event.target.value } })} /></label>
        <label>Condition <select value={rule.condition.operator} onChange={event => setRule({ ...rule, condition: { ...rule.condition, operator: event.target.value,
          upperExclusive: event.target.value === 'range' ? (/^[0-9]{1,19}$/.test(String(rule.condition.value)) ? String(BigInt(rule.condition.value) + 1n) : '1') : null } })}>{['exact', 'minimum', 'range', 'multiple'].map(value => <option key={value}>{value}</option>)}</select></label>
        <label>Condition units <select value={rule.condition.unit} onChange={event => setRule({ ...rule, condition: { ...rule.condition, unit: event.target.value,
          currency: event.target.value === 'native-money' ? 'USD' : null, minorUnitDigits: event.target.value === 'native-money' ? 2 : null } })}><option value="quantity">Quantity</option><option value="native-money">Native minor units</option></select></label>
        <label>{rule.condition.unit === 'native-money' ? 'Native amount in minor units' : 'Quantity'} <input required inputMode="numeric" pattern="[0-9]+" value={rule.condition.value} onChange={event => setRule({ ...rule, condition: { ...rule.condition, value: event.target.value } })} /></label>
        {rule.condition.operator === 'range' && <label>Upper bound (exclusive) <input required inputMode="numeric" pattern="[0-9]+" value={rule.condition.upperExclusive ?? ''} onChange={event => setRule({ ...rule, condition: { ...rule.condition, upperExclusive: event.target.value } })} /></label>}
        {rule.condition.unit === 'native-money' && <><label>Currency <input required pattern="[A-Z]{3}" value={rule.condition.currency ?? ''} onChange={event => setRule({ ...rule, condition: { ...rule.condition, currency: event.target.value } })} /></label>
          <label>Minor-unit digits <input type="number" min={0} max={4} value={rule.condition.minorUnitDigits ?? 2} onChange={event => setRule({ ...rule, condition: { ...rule.condition, minorUnitDigits: event.target.value } })} /></label></>}
        <label>Queue group <input required value={rule.queueGroup ?? 'main'} onChange={event => setRule({ ...rule, queueGroup: event.target.value })} /></label>
        <label>Queue policy <select value={rule.queuePolicy ?? 'queue'} onChange={event => setRule({ ...rule, queuePolicy: event.target.value })}>{['queue', 'interrupt', 'ignore'].map(value => <option key={value}>{value}</option>)}</select></label>
        <label>Queue limit <input type="number" min={1} max={100} value={rule.maximumQueueLength ?? 20} onChange={event => setRule({ ...rule, maximumQueueLength: event.target.value })} /></label>
        <label>Cooldown seconds <input type="number" min={0} max={86400} value={rule.cooldownSeconds ?? 0} onChange={event => setRule({ ...rule, cooldownSeconds: event.target.value })} /></label>
        {rule.actions?.map((action, index) => <fieldset key={action.id}><legend>Action {index + 1}: {action.kind}</legend>
          {action.kind === 'speech' && <><label>Voice alias <input required value={action.speech?.voice ?? ''} onChange={event => actionChange(index, { speech: { ...action.speech, voice: event.target.value } })} /></label>
            <label>Speech template <textarea value={action.speech?.template ?? ''} onChange={event => actionChange(index, { speech: { ...action.speech, template: event.target.value } })} /></label></>}
          {action.kind === 'sound' && <><label>Target overlay ID <input required value={action.overlayId ?? ''} onChange={event => actionChange(index, { overlayId: event.target.value })} /></label>
            <label>Audio asset IDs (one per line) <textarea required value={action.soundAssetIds?.join('\n') ?? ''} onChange={event => actionChange(index, { soundAssetIds: event.target.value.split(/\s+/).filter(Boolean) })} /></label></>}
          {action.kind === 'streamerbot' && <label>Streamer.bot action <select required value={action.streamerBotActionId ?? ''} onChange={event => actionChange(index, { streamerBotActionId: event.target.value })}>
            <option value="">Select an action</option>{discovery?.actions.map(value => <option key={value.id} value={value.id} disabled={!value.enabled}>{value.name}</option>)}</select></label>}
          <AutomationActionSettings action={action} change={patch => actionChange(index, patch)} discovery={discovery} />
          <button type="button" disabled={index === 0} onClick={() => { const actions = [...rule.actions ?? []]; [actions[index - 1], actions[index]] = [actions[index], actions[index - 1]]; setRule({ ...rule, actions }); }}>Move action {index + 1} up</button>
          <button type="button" onClick={() => setRule({ ...rule, actions: rule.actions?.filter((_, i) => i !== index) })}>Remove action {index + 1}</button>
        </fieldset>)}
        {['speech', 'sound', 'streamerbot'].map(kind => <button type="button" key={kind} onClick={() => setRule({ ...rule, actions: [...rule.actions ?? [], newAction(kind)] })}>Add {kind} action</button>)}
        <button type="submit">Save automation rule</button>
        <AutomationSimulation key={rule.id} rule={rule} />
        {Number(rule.version) > 0 && <button type="button" onClick={() => void operation(async () => { await automation.remove(rule); setRule(null); }, 'Rule deleted')}>Delete automation rule</button>}
      </fieldset>
    </form>}
    <AutomationCapabilities />
    <AutomationTemporaryEffects />
    <h3>Execution history</h3><ul>{executions.map(receipt => <li key={receipt.id}>{receipt.state} · {receipt.detail ?? receipt.queueGroup}
      {(receipt.state === 'moderation-pending' || receipt.state === 'language-review') &&
        <AutomationReceiptReview receipt={receipt} busy={busy} operation={operation} />}</li>)}</ul>
  </section>;
}

import { useEffect, useState } from 'react';
import { automation, type AutomationTemporaryEffect } from './automationApi';

function UncertainEffect({ effect, resolved }: Readonly<{ effect: AutomationTemporaryEffect; resolved: () => void }>) {
  const [restored, setRestored] = useState(false); const [busy, setBusy] = useState(false); const [status, setStatus] = useState('');
  async function resolve() {
    if (!restored) return;
    setBusy(true);
    try { await automation.resolveRestored(effect); resolved(); }
    catch { setStatus('Resolution failed or the effect changed. Refresh before retrying.'); }
    finally { setBusy(false); }
  }
  return <fieldset disabled={busy}>
    <legend>Uncertain effect {effect.actionId}</legend>
    <p>Inspect VTube Studio and manually restore the intended inactive state before resolving. Pending repetitions will be discarded. This button sends no toggle.</p>
    <label><input type="checkbox" checked={restored} onChange={event => setRestored(event.target.checked)} />I inspected and restored the external state for {effect.actionId}</label>
    <button type="button" disabled={!restored} onClick={() => void resolve()}>Resolve restored effect {effect.actionId}</button>
    <output aria-live="polite">{status}</output>
  </fieldset>;
}

export function AutomationTemporaryEffects() {
  const [effects, setEffects] = useState<AutomationTemporaryEffect[]>([]); const [refresh, setRefresh] = useState(0);
  const [status, setStatus] = useState('Loading temporary effects…');
  useEffect(() => {
    const controller = new AbortController();
    void automation.temporaryEffects(controller.signal).then(value => {
      if (!controller.signal.aborted) { setEffects(value); setStatus('Temporary effect states are dispatch records; inspect the model to verify its actual state.'); }
    }).catch(() => { if (!controller.signal.aborted) setStatus('Temporary effects unavailable. Refresh to retry.'); });
    return () => controller.abort();
  }, [refresh]);
  return <section aria-label="Temporary effects">
    <h3>Temporary effects</h3><p>{status}</p>
    <button type="button" onClick={() => setRefresh(value => value + 1)}>Refresh temporary effects</button>
    <ul>{effects.map(effect => <li key={`${effect.actionId}-${effect.version}`}>
      {effect.actionId} · {effect.state}
      {effect.state === 'uncertain' && <UncertainEffect effect={effect} resolved={() => setRefresh(value => value + 1)} />}
    </li>)}</ul>
  </section>;
}

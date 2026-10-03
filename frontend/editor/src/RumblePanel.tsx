import { useEffect, useState } from 'react';
import { rumble, type RumbleStatus } from './api';
import { RumbleSettings } from './RumbleSettings';

export function RumblePanel() {
  const [status, setStatus] = useState<RumbleStatus | null>(null);
  const [url, setUrl] = useState('');
  const [sessionOnly, setSessionOnly] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    const update = () => { void rumble.status().then(value => { if (active) setStatus(value); }).catch(() => { if (active) setError('Unable to read Rumble status.'); }); };
    update(); const timer = window.setInterval(update, 2000);
    return () => { active = false; window.clearInterval(timer); };
  }, []);
  async function operation(action: () => Promise<void>) {
    setBusy(true); setError('');
    try { await action(); setStatus(await rumble.status()); }
    catch { setError('Rumble operation failed. Check the URL and host credential-storage support.'); }
    finally { setUrl(''); setBusy(false); }
  }
  return <section aria-label="Rumble integration">
    <h2>Rumble</h2>
    <p role="status">{status?.state ?? 'Loading'} · {status?.baselineEstablished ? 'Baseline established' : 'Awaiting baseline'}</p>
    <p>Polling uses 7 seconds by default. Historical messages and support do not alert on connection or reset.</p>
    <form onSubmit={event => { event.preventDefault(); void operation(() => rumble.connect(url, sessionOnly)); }}>
      <label>Live API URL <input type="password" autoComplete="off" value={url} onChange={event => setUrl(event.target.value)} required /></label>
      <label><input type="checkbox" checked={sessionOnly} onChange={event => setSessionOnly(event.target.checked)} /> Keep credential only for this session</label>
      <p>Persistent storage uses Windows DPAPI. The URL is never displayed in status or logs.</p>
      <button type="submit" disabled={busy}>Connect Rumble</button>
    </form>
    <button disabled={busy} onClick={() => void operation(() => rumble.resetBaseline())}>Reset Rumble baseline</button>
    <button disabled={busy} onClick={() => void operation(() => rumble.disconnect())}>Disconnect Rumble</button>
    <p>Trigger forwarding: {status?.forwardTriggers ? 'Enabled' : 'Disabled'}. Subscription and gift automation remain unverified and gated.</p>
    {status && <p>Successful baseline: {status.baselineEstablished ? 'Yes' : 'No'}; polls: {status.pollSequence}; live streams: {status.liveStreams.length}; consecutive failures: {status.consecutiveFailures}.</p>}
    {status && <p>Last poll: {status.lastPollAt ?? 'Not polled'}; interval: {status.pollIntervalSeconds ?? 7}s; latency: {Math.round(Number(status.pollLatencyMilliseconds ?? 0))}ms; viewers: {status.viewers ?? 'Unknown'}; accepted events this process: {status.acceptedEventsThisProcess ?? 0}; duplicate records suppressed: {status.duplicateRecords ?? 0}; possible gaps: {status.possibleGaps ?? 0}.</p>}
    {error && <p role="alert">{error}</p>}
    <RumbleSettings />
  </section>;
}

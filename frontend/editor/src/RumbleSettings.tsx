import { useState } from 'react';
import { api, type Configuration } from './api';

export function RumbleSettings() {
  const [configuration, setConfiguration] = useState<Configuration | null>(null);
  const [message, setMessage] = useState('');
  async function load() {
    try { setConfiguration(await api.configuration()); setMessage(''); }
    catch { setMessage('Unable to load polling settings.'); }
  }
  async function save() {
    if (!configuration) return;
    try { await api.saveConfiguration(configuration); setMessage('Polling settings saved. Restart TDSBLive to apply them.'); }
    catch { setMessage('Unable to save polling settings. Check the allowed ranges.'); }
  }
  function update(changes: NonNullable<Configuration['rumble']>) {
    if (configuration) setConfiguration({ ...configuration, rumble: { ...configuration.rumble, ...changes } });
  }
  const settings = configuration?.rumble;
  return <details>
    <summary>Rumble polling settings</summary>
    <button onClick={() => void load()}>Load polling settings</button>
    {configuration && <form onSubmit={event => { event.preventDefault(); void save(); }}>
      <label><input type="checkbox" checked={settings?.enabled ?? false} onChange={event => update({ enabled: event.target.checked })} /> Enable Rumble on host startup</label>
      <label><input type="checkbox" checked={settings?.advancedSlowerPolling ?? false} onChange={event => update({ advancedSlowerPolling: event.target.checked, pollIntervalSeconds: Math.min(Number(settings?.pollIntervalSeconds ?? 7), event.target.checked ? 86400 : 10) })} /> Allow advanced slower polling</label>
      <label>Polling interval (seconds) <input type="number" required min={5} max={settings?.advancedSlowerPolling ? 86400 : 10} value={settings?.pollIntervalSeconds ?? 7} onChange={event => update({ pollIntervalSeconds: Number(event.target.value) })} /></label>
      <label>Request timeout (seconds) <input type="number" required min={1} max={60} value={settings?.requestTimeoutSeconds ?? 15} onChange={event => update({ requestTimeoutSeconds: Number(event.target.value) })} /></label>
      <label>Successful offline confirmations <input type="number" required min={2} max={10} value={settings?.offlineConfirmationPolls ?? 2} onChange={event => update({ offlineConfirmationPolls: Number(event.target.value) })} /></label>
      <label><input type="checkbox" checked={settings?.forwardTriggers ?? false} onChange={event => update({ forwardTriggers: event.target.checked })} /> Forward qualified Rumble events to Streamer.bot</label>
      <p>Also enable qualified live event forwarding under Bot integrations → Streamer.bot action permissions. Subscription and gift automation remain gated.</p>
      <button type="submit">Save polling settings</button>
    </form>}
    {message && <p role="status">{message}</p>}
  </details>;
}

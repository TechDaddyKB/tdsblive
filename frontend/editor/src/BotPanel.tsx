import { useEffect, useState } from 'react';
import { api, bots, type BotOverview, type Discovery, type InspectorItem } from './api';
import { ConnectionIndicator } from './ConnectionIndicator';

export function BotPanel() {
  const [overview, setOverview] = useState<BotOverview | null>(null);
  const [discovery, setDiscovery] = useState<Discovery | null>(null);
  const [entries, setEntries] = useState<InspectorItem[]>([]);
  const [filter, setFilter] = useState('');
  const [paused, setPaused] = useState(false);
  const [selected, setSelected] = useState<unknown>(null);
  const [notice, setNotice] = useState('');
  useEffect(() => {
    let active = true;
    async function refresh() {
      try {
        const [state, catalog] = await Promise.all([bots.overview(), bots.discovery()]);
        if (active) { setOverview(state); setDiscovery(catalog); }
        if (!paused) { const items = await bots.inspector(filter); if (active) setEntries(items); }
      } catch { if (active) setNotice('Unable to refresh bot connections.'); }
    }
    void refresh(); const timer = window.setInterval(() => { void refresh(); }, 2000);
    return () => { active = false; window.clearInterval(timer); };
  }, [filter, paused]);
  async function configure(bot: 'streamerBot' | 'speakerBot') {
    try {
      const configuration = await api.configuration();
      if (!configuration[bot]) throw new Error('Missing integration configuration');
      configuration[bot].enabled = !configuration[bot].enabled;
      await api.saveConfiguration(configuration);
      setNotice('Configuration saved. Restart TDSBLive to apply connection changes.');
    } catch { setNotice('Unable to save connection settings.'); }
  }
  async function sample(id: string, download: boolean) {
    try {
      const fixture = await bots.fixture(id); setSelected(fixture);
      if (download) {
        const url = URL.createObjectURL(new Blob([JSON.stringify(fixture, null, 2)], { type: 'application/json' }));
        const link = document.createElement('a'); link.href = url; link.download = `private-tdsblive-fixture-${id}.json`; link.click(); URL.revokeObjectURL(url);
        setNotice('Private fixture saved. Review and sanitize before sharing.');
      }
    } catch { setNotice('Unable to load this sample.'); }
  }
  async function replay(id: string) {
    try { await bots.replay(id); setNotice('Replay published locally without persistence or live automation.'); }
    catch { setNotice('Unable to replay this event.'); }
  }
  async function copy() {
    const text = JSON.stringify(selected, null, 2);
    try {
      if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(text);
      else {
        const area = document.createElement('textarea'); area.value = text; document.body.append(area); area.select();
        const copied = document.execCommand('copy'); area.remove(); if (!copied) throw new Error('Copy unavailable');
      }
      setNotice('Sample copied. Review before sharing.');
    } catch { setNotice('Copy unavailable. Select the displayed sample or save a fixture.'); }
  }
  return <section aria-label="Bot integrations">
    <h2>Bot integrations</h2>
    {(['streamerBot', 'speakerBot'] as const).map(bot => <div key={bot}>
      <ConnectionIndicator integration={bot === 'streamerBot' ? 'Streamer.bot' : 'Speaker.bot'} state={overview?.[bot].state === 'connected' ? 'connected' : overview?.[bot].state === 'authenticationFailed' ? 'error' : ['connecting', 'discovering', 'authenticating', 'reconnecting'].includes(overview?.[bot].state ?? '') ? 'connecting' : 'disconnected'} />
      <p>{bot === 'streamerBot' ? 'Streamer.bot' : 'Speaker.bot'}: {overview?.[bot].state ?? 'loading'} {overview?.[bot].version ?? ''} {overview?.[bot].failureKind ?? ''}</p>
      <button onClick={() => { void configure(bot); }}>Toggle {bot === 'streamerBot' ? 'Streamer.bot' : 'Speaker.bot'} connection</button>
    </div>)}
    <p>Host, port, endpoint, reconnect bounds, and selected action GUIDs are configured through the configuration API. Live execution is opt-in.</p>
    <details><summary>Discovered actions and triggers</summary>
      <ul>{discovery?.actions.map(action => <li key={action.id}>{action.name} — {action.id} {action.enabled ? '' : '(disabled)'}</li>)}</ul>
      <ul>{discovery?.codeTriggers.map(trigger => <li key={trigger.eventName}>{trigger.name} — {trigger.eventName}</li>)}</ul>
    </details>
    <h2>Event inspector</h2>
    <label>Search events <input value={filter} maxLength={128} onChange={event => setFilter(event.target.value)} /></label>
    <button onClick={() => setPaused(value => !value)}>{paused ? 'Resume inspector' : 'Pause inspector'}</button>
    <ul>{entries.map(entry => <li key={entry.id}>{entry.event?.nativeType ?? entry.classification} {entry.limitation ?? ''}
      <button onClick={() => { void sample(entry.id, false); }}>Inspect sample</button>
      <button onClick={() => { void sample(entry.id, true); }}>Save private fixture</button>
      {entry.event && <button onClick={() => { void replay(entry.id); }}>Replay locally</button>}
    </li>)}</ul>
    {selected !== null && <><pre>{JSON.stringify(selected, null, 2)}</pre><button onClick={() => { void copy(); }}>Copy sample</button></>}
    {notice && <p role="status">{notice}</p>}
  </section>;
}

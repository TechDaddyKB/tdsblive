import { useState } from 'react';
import { api, type Discovery } from './api';

export function BotPermissions({ actions }: { actions: Discovery['actions'] }) {
  const [ready, setReady] = useState(false);
  const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const [forward, setForward] = useState(false);
  const [message, setMessage] = useState('');
  async function load() {
    setBusy(true); setMessage('');
    try {
      const configuration = await api.configuration();
      if (!configuration.streamerBot) throw new Error('Missing connection configuration');
      setSelected(configuration.streamerBot.allowedActionIds ?? []);
      setForward(configuration.streamerBot.forwardLiveEvents ?? false); setReady(true);
    } catch { setReady(false); setMessage('Unable to load permissions. Retry before making changes.'); }
    finally { setBusy(false); }
  }
  return <details><summary>Streamer.bot action permissions</summary>
    <p>Allow only the actions TDSBLive should be able to run. Saving permissions does not execute an action. Rules must also be enabled separately.</p>
    <button disabled={busy} onClick={() => void load()}>Load action permissions</button>
    {ready && <form onSubmit={event => {
      event.preventDefault(); setBusy(true); setMessage('');
      void (async () => {
        try {
          const configuration = await api.configuration();
          if (!configuration.streamerBot) throw new Error('Missing connection configuration');
          configuration.streamerBot.allowedActionIds = selected;
          configuration.streamerBot.forwardLiveEvents = forward;
          await api.saveConfiguration(configuration);
          setMessage('Permissions saved. Restart TDSBLive to apply them, then review live rules and trigger bindings.');
        } catch { setMessage('Unable to save action permissions. Check the selected actions and retry.'); }
        finally { setBusy(false); }
      })();
    }}><fieldset disabled={busy}>
      <legend>Allowed actions and forwarding</legend>
      <label><input type="checkbox" checked={forward} onChange={event => setForward(event.target.checked)} /> Allow qualified live event forwarding to Streamer.bot</label>
      <p>Rumble trigger forwarding additionally requires its own forwarding setting. Subscription and gift behavior without evidence remains gated.</p>
      {actions.map(action => <label key={action.id}><input type="checkbox" checked={selected.includes(action.id)}
        disabled={!action.enabled && !selected.includes(action.id)}
        onChange={event => setSelected(values => event.target.checked ? [...values, action.id] : values.filter(id => id !== action.id))} />
        Allow action {action.name}{action.enabled ? '' : ' (disabled in Streamer.bot)'}</label>)}
      {selected.filter(id => !actions.some(action => action.id === id)).map(id => <p key={id}>Previously allowed action is not in current discovery: {id}
        <button type="button" onClick={() => setSelected(values => values.filter(value => value !== id))}>Remove unavailable permission</button></p>)}
      {!actions.length && <p>No actions discovered. Connect Streamer.bot and refresh discovery before selecting actions.</p>}
      <button type="submit">Save action permissions</button>
    </fieldset></form>}
    {message && <p role="status">{message}</p>}
  </details>;
}

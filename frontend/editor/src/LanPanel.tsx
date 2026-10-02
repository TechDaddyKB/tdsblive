import { useEffect, useState } from 'react';
import { api, write } from './api';

export function LanPanel() {
  const [enabled, setEnabled] = useState(false);
  const [hosts, setHosts] = useState('');
  const [port, setPort] = useState(17474);
  const [ready, setReady] = useState(false);
  const [busy, setBusy] = useState(false);
  const [credential, setCredential] = useState('');
  const [message, setMessage] = useState('');
  useEffect(() => {
    let active = true;
    void api.configuration().then(value => {
      if (active && value.server) {
        setEnabled(value.server.enableLan ?? false); setHosts(value.server.allowedHosts?.join('\n') ?? '');
        setPort(Number(value.server.port ?? 17474)); setReady(true);
      }
    }).catch(() => { if (active) setMessage('Unable to load LAN settings. Reload this page.'); });
    return () => { active = false; };
  }, []);
  async function provision() {
    setBusy(true); setMessage(''); setCredential('');
    try {
      const result = await write<{ credential: string }>('/api/auth/provision', 'POST');
      setCredential(result.credential);
      setMessage('Access credential created. Copy it privately before closing this page. Previous remote sessions have been revoked.');
    } catch { setMessage('Credential provisioning requires the Windows application and an editor opened on this computer.'); }
    finally { setBusy(false); }
  }
  return <section aria-labelledby="lan-title">
    <h2 id="lan-title">Access from another computer</h2>
    <p>Optional LAN access lets another computer on your network open the editor. HTTP works without certificates. Keep credentials and viewing links private.</p>
    <p>Before enabling LAN, create an access credential on this computer. Creating another credential rotates it and signs out existing remote sessions.</p>
    <button disabled={busy || !ready} onClick={() => void provision()}>Create or rotate LAN access credential</button>
    {credential && <div>
      <label>New LAN access credential <input type="password" readOnly autoComplete="off" value={credential} /></label>
      <button onClick={() => {
        void (async () => {
          try {
            if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
            await navigator.clipboard.writeText(credential);
            setMessage('Credential copied. Paste it privately into the remote sign-in page.');
          } catch { setMessage('Clipboard unavailable. Select the credential field and copy it manually.'); }
        })();
      }}>Copy LAN access credential</button>
      <button onClick={() => setCredential('')}>Hide credential</button>
    </div>}
    <form onSubmit={event => {
      event.preventDefault(); setBusy(true); setMessage('');
      void (async () => {
        try {
          const value = await api.configuration();
          if (!value.server) throw new Error('Missing server settings');
          value.server = { ...value.server, host: enabled ? '0.0.0.0' : '127.0.0.1', port,
            enableLan: enabled, allowedHosts: enabled ? hosts.split(/\r?\n/).map(host => host.trim()).filter(Boolean) : [] };
          await api.saveConfiguration(value);
          setMessage('Access settings saved. Restart TDSBLive to apply them. Update OBS URLs if you changed the port.');
        } catch { setMessage('Unable to save. Check the port and explicit network addresses.'); }
        finally { setBusy(false); }
      })();
    }}>
      <fieldset disabled={busy || !ready}>
        <legend>Network settings</legend>
        <label><input type="checkbox" checked={enabled} onChange={event => setEnabled(event.target.checked)} /> Enable authenticated LAN access</label>
        <label>HTTP port <input required type="number" min="1" max="65535" value={port} onChange={event => setPort(Number(event.target.value))} /></label>
        {enabled && <label>Streaming computer addresses (one per line) <textarea required value={hosts} onChange={event => setHosts(event.target.value)} placeholder="192.168.1.50" /></label>}
        <p>Enter the streaming computer's IP address or hostname used by remote browsers. Do not enter a full URL, port or wildcard. Local loopback access remains available.</p>
        <button type="submit">Save access settings</button>
      </fieldset>
    </form>
    {message && <p role="status">{message}</p>}
  </section>;
}

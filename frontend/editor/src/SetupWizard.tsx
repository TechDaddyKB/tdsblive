import { useEffect, useState } from 'react';
import { BotPanel } from './BotPanel';
import { RumblePanel } from './RumblePanel';
import { FinancialPanel } from './FinancialPanel';
import { ChatPanel } from './ChatPanel';
import { api, request, write } from './api';

function ConnectionSetup({ bot, label, defaultPort }: { bot: 'streamerBot' | 'speakerBot'; label: string; defaultPort: number }) {
  const [host, setHost] = useState('127.0.0.1');
  const [port, setPort] = useState(defaultPort);
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [message, setMessage] = useState('');
  useEffect(() => {
    let active = true;
    void api.configuration().then(configuration => {
      const connection = configuration[bot];
      if (!connection) throw new Error('Missing connection settings');
      if (active) { setHost(connection.host ?? '127.0.0.1'); setPort(Number(connection.port ?? defaultPort)); setLoaded(true); }
    }).catch(() => { if (active) setMessage('Unable to load saved settings. Close and reopen guided setup to retry.'); });
    return () => { active = false; };
  }, [bot, defaultPort]);
  return <form onSubmit={event => {
    event.preventDefault(); setBusy(true); setMessage('');
    void (async () => {
      try {
        const configuration = await api.configuration();
        const connection = configuration[bot];
        if (!connection) throw new Error('Missing connection settings');
        connection.host = host; connection.port = port; connection.enabled = true;
        await api.saveConfiguration(configuration);
        setMessage(`${label} connection saved. Restart TDSBLive to connect, then check its status.`);
      } catch { setMessage('Unable to save. Check the address and port, then try again.'); }
      finally { setBusy(false); }
    })();
  }}>
    <fieldset disabled={busy || !loaded}>
      <legend>{label} connection address</legend>
      <label>{label} host <input required value={host} onChange={event => setHost(event.target.value)} /></label>
      <label>{label} port <input required type="number" min="1" max="65535" value={port} onChange={event => setPort(Number(event.target.value))} /></label>
      <button type="submit">Save {label} connection</button>
    </fieldset>
    {message && <p role="status">{message}</p>}
  </form>;
}

function StreamerPassword() {
  const [password, setPassword] = useState('');
  const [sessionOnly, setSessionOnly] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  return <form onSubmit={event => {
    event.preventDefault(); setBusy(true); setMessage('');
    void write('/api/integrations/streamerbot/credential', 'POST', { value: password, sessionOnly })
      .then(() => setMessage(sessionOnly ? 'Password saved for this host session. It is cleared when TDSBLive restarts.' : 'Password saved using Windows credential protection.'))
      .catch(() => setMessage('Password could not be saved. Persistent storage requires the Windows application; try session-only storage.'))
      .finally(() => { setPassword(''); setBusy(false); });
  }}>
    <fieldset disabled={busy}>
      <legend>Streamer.bot authentication</legend>
      <p>If Streamer.bot requires a password, enter it here. If authentication is disabled, leave this form unused. For session-only storage, enter it after restarting to apply your connection settings.</p>
      <label>Streamer.bot password <input type="password" autoComplete="off" required maxLength={4096} value={password} onChange={event => setPassword(event.target.value)} /></label>
      <label><input type="checkbox" checked={sessionOnly} onChange={event => setSessionOnly(event.target.checked)} /> Keep Streamer.bot password only for this session</label>
      <button type="submit">Save Streamer.bot password</button>
    </fieldset>
    {message && <p role="status">{message}</p>}
  </form>;
}

const steps = ['Welcome', 'Bot connections', 'Rumble', 'Supporter totals', 'OBS chat', 'Review'];

export function SetupWizard() {
  const [open, setOpen] = useState(false);
  const [step, setStep] = useState(0);
  const [version, setVersion] = useState(0);
  const [ready, setReady] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState('');
  useEffect(() => {
    let active = true;
    void request<{ step: number; reviewed: boolean; version: number }>('/api/setup').then(progress => {
      if (active) { setStep(progress.step); setVersion(progress.version); setOpen(!progress.reviewed); setReady(true); }
    }).catch(() => { if (active) setNotice('Unable to load setup progress. Reload this page to retry.'); });
    return () => { active = false; };
  }, []);
  async function move(next: number, reviewed = false) {
    setSaving(true); setNotice('');
    try {
      const saved = await write<{ step: number; version: number }>('/api/setup', 'PUT', { step: next, reviewed, version });
      setStep(saved.step); setVersion(saved.version);
      if (reviewed) setOpen(false);
    } catch { setNotice('Unable to save setup progress. Reload the page before continuing.'); }
    finally { setSaving(false); }
  }
  return <section aria-labelledby="setup-title">
    <h2 id="setup-title">Guided setup</h2>
    <p>Set up one part at a time. You can skip services you do not use and return whenever you need.</p>
    <button disabled={!ready || saving} onClick={() => setOpen(!open)}>{open ? 'Close guided setup' : 'Open guided setup'}</button>
    {notice && <p role="alert">{notice}</p>}
    {open && <>
      <p role="status">Step {step + 1} of {steps.length}: {steps[step]}</p>
      <h3>{steps[step]}</h3>
      {step === 0 && <>
        <p>TDSBLive brings your chat, overlays and supporter totals together. Streamer.bot connects to your streaming platforms; Speaker.bot reads messages aloud.</p>
        <p>Start Streamer.bot and Speaker.bot before connecting them. Your editor runs on this computer over HTTP. You do not need a certificate.</p>
        <p>Connection settings may require a restart. After saving them, use Restart TDSBLive under Backup and recovery, then reopen this guide.</p>
      </>}
      {step === 1 && <>
        <p>In Streamer.bot, enable the WebSocket server in Servers/Clients. Check that its address and port match TDSBLive. Import the supplied TDSBLive actions and register their custom triggers before enabling Rumble forwarding.</p>
        <p>Enable Speaker.bot’s WebSocket server if you want speech. Use the voice alias <strong>local english</strong> for your current setup. Connecting does not verify that you can hear speech; test audio through OBS before using live rules.</p>
        <ConnectionSetup bot="streamerBot" label="Streamer.bot" defaultPort={8080} />
        <ConnectionSetup bot="speakerBot" label="Speaker.bot" defaultPort={7680} />
        <StreamerPassword />
        <BotPanel />
      </>}
      {step === 2 && <>
        <p>Paste your private Rumble Live API URL below. Keep it out of screenshots and public messages. “Baseline established” means TDSBLive has learned the current snapshot; older messages do not create new alerts.</p>
        <RumblePanel />
      </>}
      {step === 3 && <>
        <p>Choose your timezone so daily totals change at the right time. Use the browser timezone button if it matches where you stream. Set the current stream start when your stream begins.</p>
        <p>Review nominal valuation rules before using subscription estimates. Unknown amounts stay unknown until you supply evidence; they are not automatically counted as money.</p>
        <FinancialPanel />
      </>}
      {step === 4 && <>
        <p>In OBS, add a Browser Source using <code>http://127.0.0.1:17474/overlay/combined-chat</code>. This view has a transparent background.</p>
        <p>For a chat window only you watch, add a Custom Browser Dock using <code>http://127.0.0.1:17474/chat/combined-chat</code>. Its Light/Dark button changes the dock appearance.</p>
        <p>If you changed the host port, use that port in both addresses. Choose your chat appearance below, then send a real message to check it in OBS.</p>
        <ChatPanel />
      </>}
      {step === 5 && <>
        <p>Before streaming, check each service you use: connection status, a new chat message in OBS, and any alert sound or speech you configured. Successful dispatch alone does not prove audible output.</p>
        <p>Keep live automation disabled until its filters and action permissions are reviewed. Preview events are isolated from production supporter totals and live automation.</p>
        <p>Download a backup once you are happy with your setup. You can open this guide again whenever you need it.</p>
      </>}
      <button disabled={step === 0 || saving} onClick={() => void move(step - 1)}>Previous setup step</button>
      {step < steps.length - 1
        ? <button disabled={saving} onClick={() => void move(step + 1)}>Next setup step</button>
        : <button disabled={saving} onClick={() => void move(5, true)}>Finish setup review</button>}
    </>}
  </section>;
}

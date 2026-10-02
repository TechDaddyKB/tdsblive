import { useState } from 'react';
import { ApiError, request, write } from './api';

interface Preview { id: string; expiresAt: string }

async function protectedFetch(path: string, body?: Blob): Promise<Response> {
  const csrf = await request<{ requestToken: string }>('/api/auth/csrf');
  const response = await fetch(path, {
    method: 'POST', credentials: 'same-origin',
    headers: { 'X-TDSBLive-CSRF': csrf.requestToken,
      ...(body ? { 'Content-Type': body.type || 'application/octet-stream' } : {}) },
    ...(body ? { body } : {}),
  });
  if (!response.ok) throw new ApiError(response.status);
  return response;
}

async function download(path: string, filename: string) {
  const response = await protectedFetch(path);
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url; link.download = filename; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function MaintenancePanel() {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [backup, setBackup] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [settings, setSettings] = useState<File | null>(null);
  const [stopping, setStopping] = useState(false);

  async function run(action: () => Promise<void>) {
    setBusy(true); setError(''); setMessage('');
    try { await action(); }
    catch (failure) {
      setError(failure instanceof ApiError && failure.status === 403
        ? 'Open the editor on the computer running TDSBLive to use these controls.'
        : 'The operation did not finish. Check the file and try again. Keep your original backup.');
    } finally { setBusy(false); }
  }

  async function stop(kind: 'restart' | 'quit' | 'restore') {
    await write(`/api/${kind === 'restore' ? 'recovery/restore' : `application/${kind}`}`, 'POST',
      kind === 'restore' ? { id: preview?.id, confirm: confirmed } : undefined);
    setStopping(true);
    setMessage(kind === 'quit' ? 'TDSBLive is closing. You can close this page.'
      : 'TDSBLive is restarting. Reopen the editor when it is ready. Restored connections and automation stay disabled until you review them.');
  }

  return <section aria-labelledby="maintenance-title">
    <h2 id="maintenance-title">Backup and recovery</h2>
    <p>Use these controls on the computer running TDSBLive. Save a backup before updating or making big changes.</p>
    <p>Backups contain your saved overlays, assets, chat history and supporter records. Keep them private. Connection passwords are not included.</p>
    <fieldset disabled={busy || stopping}>
      <legend>Save a copy</legend>
      <button onClick={() => void run(async () => {
        await download('/api/recovery/backup', `TDSBLive-backup-${new Date().toISOString().slice(0, 10)}.zip`);
        setMessage('Backup download started. Keep the ZIP file in a safe place.');
      })}>Download backup</button>
    </fieldset>
    <fieldset disabled={busy || stopping}>
      <legend>Restore a backup</legend>
      <label>Backup ZIP <input type="file" accept=".zip,application/zip" onChange={event => {
        setBackup(event.target.files?.[0] ?? null); setPreview(null); setConfirmed(false);
      }} /></label>
      <button disabled={!backup} onClick={() => void run(async () => {
        if (!backup) return;
        setPreview(null); setConfirmed(false);
        const response = await protectedFetch('/api/recovery/validate', backup);
        setPreview(await response.json() as Preview);
        setMessage('Backup checked. Nothing has been replaced yet.');
      })}>Check backup</button>
      {preview && <>
        <p>Restoring replaces your current saved data and restarts TDSBLive. A safety copy of the current data is retained. Connections and automation are disabled after restore.</p>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> I want to replace my current saved data with this backup.</label>
        <button disabled={!confirmed} onClick={() => void run(() => stop('restore'))}>Restore checked backup</button>
      </>}
    </fieldset>
    <fieldset disabled={busy || stopping}>
      <legend>Move connection settings</legend>
      <p>This smaller file contains connection settings only. It does not include passwords, overlays, assets or supporter records. Imported connections stay disabled.</p>
      <button onClick={() => void run(async () => {
        await download('/api/configuration/export', 'TDSBLive-connection-settings.json');
        setMessage('Connection settings download started.');
      })}>Export connection settings</button>
      <label>Connection settings JSON <input type="file" accept=".json,application/json" onChange={event => setSettings(event.target.files?.[0] ?? null)} /></label>
      <button disabled={!settings} onClick={() => void run(async () => {
        if (!settings) return;
        await protectedFetch('/api/configuration/import', new Blob([settings], { type: 'application/json' }));
        setMessage('Connection settings imported. Restart TDSBLive, then review and enable the connections you need.');
      })}>Import connection settings</button>
    </fieldset>
    <fieldset disabled={busy || stopping}>
      <legend>Application</legend>
      <button onClick={() => void run(() => stop('restart'))}>Restart TDSBLive</button>
      <button onClick={() => void run(() => stop('quit'))}>Quit TDSBLive</button>
    </fieldset>
    {busy && <p role="status">Working… Please keep this page open.</p>}
    {message && <p role="status">{message}</p>}
    {error && <p role="alert">{error}</p>}
  </section>;
}

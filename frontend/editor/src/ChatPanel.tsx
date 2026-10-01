import { useEffect, useState } from 'react';
import { platforms, type ChatSettings, type OverlayDefinition } from '../../overlay-runtime/src/chat';

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(url, options);
  if (!response.ok) throw new Error(response.status === 409 ? 'Settings changed elsewhere. Reload before saving.' : 'Request failed.');
  return await response.json() as T;
}
async function csrf(): Promise<string> { return (await request<{ requestToken: string }>('/api/auth/csrf')).requestToken; }
const displayFields = ['showPlatformIcon', 'showAvatar', 'showBadges', 'showUsername', 'showMessage', 'showTimestamp', 'persistent', 'newestOnTop', 'hideBotMessages'] as const;
const labels: Record<typeof displayFields[number], string> = { showPlatformIcon: 'Platform icons', showAvatar: 'Avatars', showBadges: 'Badges', showUsername: 'Usernames', showMessage: 'Message text', showTimestamp: 'Timestamps', persistent: 'Keep messages visible', newestOnTop: 'Newest messages on top', hideBotMessages: 'Hide bot messages' };

export function ChatPanel() {
  const [overlay, setOverlay] = useState<OverlayDefinition | null>(null);
  const [status, setStatus] = useState('');
  const [token, setToken] = useState('');
  const [tokens, setTokens] = useState<{ id: string; revoked: boolean; expiresAt: string }[]>([]);
  const [assets, setAssets] = useState<{ id: string; filename: string; mime: string }[]>([]);
  const load = async () => {
    try { setOverlay(await request<OverlayDefinition>('/api/overlays/combined-chat')); setStatus(''); }
    catch { setStatus('Unable to load chat settings.'); }
  };
  useEffect(() => { void load(); }, []);
  if (!overlay) return <section aria-label="Combined chat setup"><h2>Combined Chat</h2><output>{status || 'Loading chat settings…'}</output></section>;
  const change = <K extends keyof ChatSettings>(key: K, value: ChatSettings[K]) => setOverlay({ ...overlay, chat: { ...overlay.chat, [key]: value } });
  const save = async () => {
    try { setOverlay(await request<OverlayDefinition>('/api/overlays/combined-chat', { method: 'PUT', headers: { 'Content-Type': 'application/json', 'X-TDSBLive-CSRF': await csrf() }, body: JSON.stringify(overlay) })); setStatus('Chat settings saved. Open views update automatically.'); }
    catch (error) { setStatus(error instanceof Error ? error.message : 'Unable to save.'); }
  };
  const fragment = token ? `#token=${encodeURIComponent(token)}` : '';
  const mint = async () => {
    try { const result = await request<{ token: string }>('/api/overlays/combined-chat/tokens', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-TDSBLive-CSRF': await csrf() }, body: JSON.stringify({ lifetimeDays: 30 }) }); setToken(result.token); setStatus('Read-only link created. Keep it private; it expires in 30 days.'); }
    catch { setStatus('Unable to create link.'); }
  };
  return <section aria-label="Combined chat setup"><h2>Combined Chat</h2>
    <p>Use the transparent overlay in an OBS Browser Source. Open the streamer view in a browser or add its URL under OBS Docks → Custom Browser Docks.</p>
    <p><a href={`/overlay/combined-chat${fragment}`} target="_blank" rel="noreferrer">Open transparent overlay</a> · <a href={`/chat/combined-chat${fragment}`} target="_blank" rel="noreferrer">Open streamer chat</a> · <a href="/overlay/combined-chat?preview=1" target="_blank" rel="noreferrer">Open test preview</a></p>
    <label>OBS overlay URL<input readOnly value={`${location.origin}/overlay/combined-chat${fragment}`} /></label>
    <label>Streamer dock URL<input readOnly value={`${location.origin}/chat/combined-chat${fragment}`} /></label>
    <button onClick={() => { void mint(); }}>Create private LAN viewing links</button>
    {token && <button onClick={() => setToken('')}>Hide private links</button>}
    <button onClick={() => { void request<typeof tokens>('/api/overlays/combined-chat/tokens').then(setTokens).catch(() => setStatus('Unable to load viewing links.')); }}>Manage viewing links</button>
    {tokens.map(t => <p key={t.id}>Expires {new Date(t.expiresAt).toLocaleDateString()} · {t.revoked ? 'Revoked' : <button onClick={() => { void csrf().then(value => fetch(`/api/overlays/combined-chat/tokens/${encodeURIComponent(t.id)}`, { method: 'DELETE', headers: { 'X-TDSBLive-CSRF': value } })).then(response => { if (!response.ok) { throw new Error('Revocation failed.'); } setTokens(tokens.map(x => x.id === t.id ? { ...x, revoked: true } : x)); setToken(''); setStatus('Viewing link revoked.'); }).catch(() => setStatus('Unable to revoke link.')); }}>Revoke viewing link</button>}</p>)}
    <details><summary>Chat appearance and filters</summary>
      <fieldset><legend>Platforms</legend>{platforms.map(platform => <label key={platform}><input type="checkbox" checked={overlay.chat.platforms.includes(platform)} onChange={e => change('platforms', e.target.checked ? [...overlay.chat.platforms, platform] : overlay.chat.platforms.filter(p => p !== platform))} />{platform}</label>)}</fieldset>
      <fieldset><legend>Display</legend>{displayFields.map(key => <label key={key}><input type="checkbox" checked={overlay.chat[key]} onChange={e => change(key, e.target.checked)} />{labels[key]}</label>)}</fieldset>
      <label>Message duration (seconds)<input type="number" min="1" max="86400" value={overlay.chat.messageDurationSeconds} onChange={e => change('messageDurationSeconds', Number(e.target.value))} /></label>
      <label>Maximum messages<input type="number" min="1" max="500" value={overlay.chat.maximumMessages} onChange={e => change('maximumMessages', Number(e.target.value))} /></label>
      <label>Font family<input value={overlay.chat.font} maxLength={64} onChange={e => change('font', e.target.value)} /></label>
      <label>Font size<input type="number" min="8" max="120" value={overlay.chat.fontSize} onChange={e => change('fontSize', Number(e.target.value))} /></label>
      <label>Background opacity<input type="range" min="0" max="1" step="0.05" value={overlay.chat.backgroundOpacity} onChange={e => change('backgroundOpacity', Number(e.target.value))} /></label>
      {(['animationIn', 'animationOut'] as const).map(key => <label key={key}>{key === 'animationIn' ? 'Arrival animation' : 'Departure animation'}<select value={overlay.chat[key]} onChange={e => change(key, e.target.value as ChatSettings[typeof key])}><option value="none">None</option><option value="fade">Fade</option><option value="slide">Slide</option></select></label>)}
      {platforms.map(p => <label key={p}>{p} color<input type="color" value={overlay.chat.platformColors[p]} onChange={e => change('platformColors', { ...overlay.chat.platformColors, [p]: e.target.value })} /></label>)}
      {(['ignoredUsers', 'ignoredPrefixes', 'botUsers'] as const).map(key => <label key={key}>{key === 'ignoredUsers' ? 'Ignored users' : key === 'ignoredPrefixes' ? 'Ignored message prefixes' : 'Bot usernames'} (one per line)<textarea value={overlay.chat[key].join('\n')} onChange={e => change(key, e.target.value.split('\n').filter(v => v.trim().length > 0))} /></label>)}
      <button onClick={() => { void save(); }}>Save chat settings</button><button onClick={() => { void load(); }}>Reload chat settings</button>
    </details>
    <details><summary>Local asset library</summary><p>Upload images, SVG, audio, video, or licensed fonts. Files are served by content ID.</p>
      <label>Font license declaration<input id="font-license" maxLength={256} placeholder="For fonts: license or permission to use" /></label>
      <label>Upload asset<input type="file" onChange={e => {
        const file = e.target.files?.[0]; if (!file) return;
        if (file.size > 20 * 1024 * 1024) { setStatus('File exceeds 20 MiB.'); return; }
        const license = (document.getElementById('font-license') as HTMLInputElement).value;
        void csrf().then(value => request<{ filename: string }>('/api/assets', { method: 'POST', headers: { 'X-TDSBLive-CSRF': value, 'X-Asset-Filename': file.name, 'X-Asset-License': license, 'Content-Type': file.type }, body: file }))
          .then(info => setStatus(`Asset ready: ${info.filename}`)).catch(() => setStatus('Upload rejected. Check MIME, size, SVG, and font license.'));
      }} /></label>
      <button onClick={() => { void request<typeof assets>('/api/assets').then(setAssets).catch(() => setStatus('Unable to load assets.')); }}>Refresh assets</button>
      <label>Custom font<select value={overlay.chat.fontAssetId ?? ''} onChange={e => change('fontAssetId', e.target.value || null)}><option value="">System font</option>{assets.filter(a => a.mime.startsWith('font/')).map(a => <option key={a.id} value={a.id}>{a.filename}</option>)}</select></label>
      <ul>{assets.map(a => <li key={a.id}>{a.filename} ({a.mime})</li>)}</ul>
    </details><output>{status}</output>
  </section>;
}

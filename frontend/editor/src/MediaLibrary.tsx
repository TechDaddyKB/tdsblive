import { useEffect, useState } from 'react';
import { request } from './api';
import type { EditorAsset } from './WidgetProperties';
export function MediaLibrary() {
  const [assets, setAssets] = useState<EditorAsset[]>([]); const [search, setSearch] = useState(''); const [notice, setNotice] = useState(''); const [license, setLicense] = useState('');
  const refresh = () => request<EditorAsset[]>('/api/assets').then(setAssets).catch(() => setNotice('Unable to load media. Try refreshing.'));
  useEffect(() => { void refresh(); }, []);
  return <section aria-label="Media library"><h2>Your media</h2><p>Images, GIFs, videos, sounds and licensed fonts. Each file may be up to 20 MiB.</p>
    <div className="form-grid"><label>Search media<input value={search} onChange={e => setSearch(e.target.value)} /></label><label>Font license or permission<input value={license} maxLength={256} onChange={e => setLicense(e.target.value)} /></label><label>Upload media<input type="file" onChange={async e => { const file = e.target.files?.[0]; if (!file) return; setNotice('Uploading…'); try {
      const csrf = await request<{ requestToken: string }>('/api/auth/csrf');
      const response = await fetch('/api/assets', { method: 'POST', headers: { 'Content-Type': file.type, 'X-Asset-Filename': file.name, 'X-TDSBLive-CSRF': csrf.requestToken, ...(license ? { 'X-Asset-License': license } : {}) }, body: file });
      if (!response.ok) throw new Error(); await refresh(); setNotice('Media uploaded. Choose it in your widget settings.'); window.dispatchEvent(new Event('tdsblive:assets-changed'));
    } catch { setNotice('Upload failed. Check the file type, size and font license.'); } }} /></label></div>
    <button onClick={() => { void refresh(); }}>Refresh media</button><output role="status">{notice}</output>
    <div className="media-grid">{assets.filter(a => a.filename.toLowerCase().includes(search.toLowerCase())).map(a => <article className="media-card" key={a.id}>
      {a.mime.startsWith('image/') && <img src={`/assets/${a.id}`} alt={a.filename} loading="lazy" />}
      {a.mime.startsWith('video/') && <video src={`/assets/${a.id}`} controls preload="metadata" />}
      {a.mime.startsWith('audio/') && <audio src={`/assets/${a.id}`} controls preload="none" />}
      <h3>{a.filename}</h3><p>{a.mime}{a.size !== undefined ? ` · ${(a.size / 1024).toFixed(1)} KiB` : ''}</p>{a.license && <p>License: {a.license}</p>}<details><summary>Technical details</summary><code>{a.id}</code>{a.uploadedAt && <p>Added {new Date(a.uploadedAt).toLocaleString()}</p>}{a.sanitized && <p>Unsafe asset content was removed during upload.</p>}</details>
    </article>)}</div>{assets.length === 0 && <p>Add your first image or sound to start designing alerts.</p>}
  </section>;
}

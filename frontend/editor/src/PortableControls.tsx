import { useState } from 'react';
import { request } from './api';
import type { Scene } from '../../overlay-runtime/src/scene';

export function PortableControls({ overlay, widget, flush, imported }: { overlay?: string; widget?: string; flush: () => Promise<boolean>; imported: (value: Scene) => Promise<void> }) {
  const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false);
  const download = async (widgetId?: string) => {
    if (!overlay) return; setBusy(true);
    try {
      if (!await flush()) throw new Error();
      const response = await fetch(`/api/overlays/${encodeURIComponent(overlay)}/export${widgetId ? `?widgetId=${encodeURIComponent(widgetId)}` : ''}`);
      if (!response.ok) throw new Error();
      const url = URL.createObjectURL(await response.blob()); const link = document.createElement('a');
      link.href = url; link.download = widgetId ? 'widget.sbxwidget' : 'overlay.sbxoverlay'; link.click(); URL.revokeObjectURL(url);
      setNotice('Portable package exported. Widget state and access tokens are excluded.');
    } catch { setNotice('Export failed. Save changes and remove credentials or absolute paths; check referenced assets and package size.'); }
    finally { setBusy(false); }
  };
  const upload = async (file: File) => {
    setBusy(true);
    try {
      if (!await flush() || file.size > 32 * 1024 * 1024 || !/\.(sbxoverlay|sbxwidget)$/.test(file.name)) throw new Error();
      const csrf = await request<{ requestToken: string }>('/api/auth/csrf');
      const response = await fetch(`/api/packages/import${overlay ? `?overlayId=${encodeURIComponent(overlay)}` : ''}`, {
        method: 'POST', headers: { 'Content-Type': 'application/zip', 'X-Package-Filename': file.name, 'X-TDSBLive-CSRF': csrf.requestToken }, body: file,
      });
      if (!response.ok) throw new Error(); await imported(await response.json() as Scene);
      setNotice('Package imported with new identities. Review custom code and grant permissions explicitly.');
    } catch { setNotice('Import failed. Select an overlay for a widget package; check its version, entries, assets and size.'); }
    finally { setBusy(false); }
  };
  return <section aria-label="Portable packages"><button disabled={!overlay || busy} onClick={() => { void download(); }}>Export overlay</button>
    <button disabled={!overlay || !widget || busy} onClick={() => { void download(widget); }}>Export selected widget</button>
    <label>Import portable package<input disabled={busy} aria-label="Import portable package" type="file" accept=".sbxoverlay,.sbxwidget" onChange={e => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void upload(file); }} /></label>
    {notice && <output>{notice}</output>}</section>;
}

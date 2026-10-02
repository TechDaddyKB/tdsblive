import { useEffect, useState } from 'react';

export function useDonorAsset(id: string | null, family: 'image/' | 'font/', overlay: string, token: string): string | null {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    setUrl(null);
    if (!id || !/^[0-9a-f]{64}$/.test(id)) return;
    const abort = new AbortController(); let objectUrl: string | undefined;
    void fetch(`/assets/${id}`, { method: token ? 'GET' : 'HEAD', headers: token ? { Authorization: `Bearer ${token}`, 'X-TDSBLive-Overlay': overlay } : {}, signal: abort.signal })
      .then(async response => {
        if (!response.ok || !response.headers.get('Content-Type')?.startsWith(family)) return;
        if (token) objectUrl = URL.createObjectURL(await response.blob());
        if (abort.signal.aborted) { if (objectUrl) URL.revokeObjectURL(objectUrl); return; }
        setUrl(objectUrl ?? `/assets/${id}`);
      }).catch(() => { /* Unavailable optional presentation asset falls back to built-in appearance. */ });
    return () => { abort.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [id, family, overlay, token]);
  return url;
}

export function useDonorFont(url: string | null, widgetId: string): string | null {
  const [family, setFamily] = useState<string | null>(null);
  useEffect(() => {
    setFamily(null);
    if (!url || typeof FontFace === 'undefined') return;
    let active = true; const name = `tdsblive-donor-${widgetId.replace(/[^a-zA-Z0-9]/g, '')}`;
    const face = new FontFace(name, `url(${JSON.stringify(url)})`);
    void face.load().then(loaded => {
      if (active) { document.fonts.add(loaded); setFamily(name); }
    }).catch(() => { /* Preserve the configured fallback font. */ });
    return () => { active = false; document.fonts.delete(face); };
  }, [url, widgetId]);
  return family;
}

import { useEffect, useMemo, useRef, useState } from 'react';
import workerUrl from './CustomWorker.ts?worker&url';
import { customCsp, defaultCustom, validFrameMessage, type CustomDelivery, type CustomSettings } from './custom';
import { widgetId, type Widget } from './scene';

// This function is serialized into the opaque iframe. It is trusted rendering
// code; custom JavaScript runs exclusively in its disposable dedicated worker.
export function frameBootstrap(channel: string, settings: CustomSettings, workerCode: string, assets: Record<string, string>, parentOrigin: string, initialSession: unknown = {}) {
  const program = `${workerCode}\nself.__SBX_RUN = function(SBX, document, window) {\n${settings.javaScript}\n};`;
  const worker = new Worker(URL.createObjectURL(new Blob([program], { type: 'text/javascript' })));
  worker.onerror = event => { event.preventDefault(); parent.postMessage({ op: 'error', channel }, parentOrigin); };
  const root = document.createElement('div'); document.body.append(root);
  const style = document.createElement('style'); style.textContent = settings.css.replace(/sbx-asset:([0-9a-f]{64})/g, (_match, id: string) => assets[id] ?? ''); document.head.append(style);
  const tags = new Set('DIV SPAN P BR BR HR H1 H2 H3 H4 UL OL LI STRONG EM B I SMALL PRE CODE TABLE THEAD TBODY TR TD TH IMG SVG PATH CIRCLE RECT G BUTTON LABEL INPUT SELECT OPTION TEXTAREA VIDEO AUDIO SOURCE'.split(' '));
  const attributes = new Set('id class style title role aria-label viewBox d fill stroke cx cy r x y width height type value checked disabled min max step placeholder muted loop controls'.split(' '));
  function reconcile(target: Node, source: Node) {
    const desired = [...source.childNodes];
    for (let index = 0; index < desired.length; index++) {
      const next = desired[index]; const old = target.childNodes[index];
      if (!old) { target.appendChild(next.cloneNode(true)); continue; }
      if (old.nodeType !== next.nodeType || old.nodeName !== next.nodeName || old instanceof Element && next instanceof Element && old.id !== next.id) {
        target.replaceChild(next.cloneNode(true), old); continue;
      }
      if (old instanceof Element && next instanceof Element) {
        for (const attr of [...old.attributes]) if (!next.hasAttribute(attr.name)) old.removeAttribute(attr.name);
        for (const attr of [...next.attributes]) if (old.getAttribute(attr.name) !== attr.value) old.setAttribute(attr.name, attr.value);
        reconcile(old, next);
      } else if (old.textContent !== next.textContent) old.textContent = next.textContent;
    }
    while (target.childNodes.length > desired.length) target.lastChild?.remove();
  }
  function render(html: string) {
    if (typeof html !== 'string' || html.length > 131072) return;
    const template = document.createElement('template'); template.innerHTML = html;
    const elements = [...template.content.querySelectorAll('*')]; if (elements.length > 2000) return;
    for (const element of elements) {
      if (!tags.has(element.tagName.toUpperCase()) || ['AUDIO', 'VIDEO', 'SOURCE'].includes(element.tagName.toUpperCase()) && !settings.permissions.includes('audio')) { element.remove(); continue; }
      for (const attr of [...element.attributes]) {
        if (attr.name === 'src' && ['IMG', 'VIDEO', 'AUDIO', 'SOURCE'].includes(element.tagName.toUpperCase())) {
          const asset = assets[attr.value];
          if (asset) element.setAttribute('src', asset);
          else if (!(settings.permissions.includes('network') && /^https:\/\//.test(attr.value) && settings.networkDomains.includes(new URL(attr.value).hostname))) element.removeAttribute('src');
        } else if (!attributes.has(attr.name)) element.removeAttribute(attr.name);
      }
      if (['VIDEO', 'AUDIO'].includes(element.tagName.toUpperCase())) { element.setAttribute('autoplay', ''); element.setAttribute('playsinline', ''); }
    }
    reconcile(root, template.content);
  }
  let messages = 0;
  setInterval(() => { messages = 0; }, 1000);
  worker.onmessage = e => {
    if (++messages > 60) { worker.terminate(); return; }
    const value = e.data;
    if (value?.op === 'render') { render(value.html); return; }
    if (value?.op === 'store' || value?.op === 'error') parent.postMessage({ ...value, channel }, parentOrigin);
  };
  addEventListener('message', event => {
    if (event.source !== parent || event.origin !== parentOrigin || event.data?.channel !== channel) return;
    worker.postMessage(event.data);
  });
  for (const type of ['click', 'input', 'change']) root.addEventListener(type, e => {
    const target = e.target as HTMLInputElement;
    if (target.id) worker.postMessage({ op: 'dom-event', type, id: target.id, value: target.value });
  });
  worker.postMessage({ op: 'init', html: settings.html, config: settings.config, session: initialSession });
  parent.postMessage({ op: 'ready', channel }, parentOrigin);
}

const safeJson = (value: unknown) => JSON.stringify(value).replaceAll('<', '\\u003c');
export function assetDataUrl(bytes: Uint8Array, mime: string): string {
  let binary = '';
  for (let index = 0; index < bytes.length; index += 16384) binary += String.fromCharCode(...bytes.subarray(index, index + 16384));
  return `data:${mime};base64,${btoa(binary)}`;
}
export function frameDocument(settings: CustomSettings, worker: string, channel: string, assets: Record<string, string> = {}, session: unknown = {}): string {
  const csp = customCsp(settings, channel).replaceAll('&', '&amp;').replaceAll('"', '&quot;');
  return `<!doctype html><html><head><meta http-equiv="Content-Security-Policy" content="${csp}"><meta charset="utf-8"></head><body><script nonce="${channel}">(${frameBootstrap.toString()})(${safeJson(channel)},${safeJson(settings)},${safeJson(worker)},${safeJson(assets)},${safeJson(location.origin)},${safeJson(session)})</script></body></html>`;
}

export function CustomFrame({ widget, overlay, token, preview, deliveries, session, audioEnabled = true, draft = false }: {
  widget: Widget; overlay: string; token: string; preview: boolean; deliveries: CustomDelivery[]; session: unknown; audioEnabled?: boolean; draft?: boolean;
}) {
  const frame = useRef<HTMLIFrameElement>(null); const [source, setSource] = useState(''); const [error, setError] = useState('');
  const channel = useRef(''); const ready = useRef(false);
  const draftStorage = useRef<Record<string, unknown>>({});
  const settings = useMemo(() => { const base = widget.custom ?? defaultCustom; return { ...base, permissions: base.permissions.filter(p => (audioEnabled || p !== 'audio') && (!draft || p !== 'network')) }; }, [widget.custom, audioEnabled, draft]);
  const publicSession = useMemo(() => ({ connected: !!(session && typeof session === 'object' && 'connected' in session && session.connected), preview, muted: !settings.permissions.includes('audio') }), [session, preview, settings.permissions]);
  const sessionValue = useRef(publicSession); sessionValue.current = publicSession;
  const configValue = useRef(settings.config); configValue.current = settings.config;
  const executionKey = JSON.stringify({ ...settings, config: {} });
  useEffect(() => {
    let disposed = false; let requests = 0; const controller = new AbortController(); ready.current = false;
    const capability = widgetId().replaceAll('-', ''); channel.current = capability; setError(''); setSource('');
    const headers: Record<string, string> = token ? { Authorization: `Bearer ${token}`, 'X-TDSBLive-Overlay': overlay } : {};
    const send = (value: object) => frame.current?.contentWindow?.postMessage({ ...value, channel: capability }, '*');
    const receive = async (event: MessageEvent) => {
      if (!validFrameMessage(event, frame.current?.contentWindow ?? null, capability) || disposed) return;
      const data = event.data;
      if (data.op === 'ready') { ready.current = true; send({ op: 'session', session: sessionValue.current }); send({ op: 'config', config: configValue.current }); if (draft) for (const delivery of deliveries.filter(d => d.widgetId === widget.id)) send({ op: 'event', event: delivery.event }); return; }
      if (data.op === 'error') { setError('Custom widget JavaScript failed'); return; }
      if (data.op !== 'store' || !Number.isSafeInteger(data.id) || !['get', 'set'].includes(data.method)) return;
      if (!settings.permissions.includes('storage') || requests >= 8) { send({ op: 'store-result', id: data.id, error: 'Storage permission denied or busy' }); return; }
      requests++;
      try {
        if (draft) {
          if (data.method === 'set') { if (!data.value || typeof data.value !== 'object' || Array.isArray(data.value) || JSON.stringify(data.value).length > 32768) throw new Error(); draftStorage.current = structuredClone(data.value); }
          send({ op: 'store-result', id: data.id, value: structuredClone(draftStorage.current) }); return;
        }
        const url = `/api/overlays/${overlay}/widgets/${widget.id}/store${preview ? '?preview=1' : ''}`;
        if (data.method === 'set') {
          if (!data.value || typeof data.value !== 'object' || Array.isArray(data.value) || JSON.stringify(data.value).length > 32768) throw new Error();
          const csrfResponse = await fetch('/api/auth/csrf', { signal: controller.signal });
          const csrf = await csrfResponse.json() as { requestToken: string };
          const result = await fetch(url, { method: 'PUT', headers: { ...headers, 'Content-Type': 'application/json', 'X-TDSBLive-CSRF': csrf.requestToken }, body: JSON.stringify(data.value), signal: controller.signal });
          if (!result.ok) throw new Error(); send({ op: 'store-result', id: data.id, value: data.value });
        } else {
          const result = await fetch(url, { headers, signal: controller.signal }); if (!result.ok) throw new Error();
          send({ op: 'store-result', id: data.id, value: await result.json() });
        }
      } catch { if (!disposed) send({ op: 'store-result', id: data.id, error: 'Widget storage unavailable' }); }
      finally { requests--; }
    };
    addEventListener('message', receive);
    void (async () => {
      try {
        const worker = await fetch(workerUrl, { signal: controller.signal }); if (!worker.ok) throw new Error();
        const code = await worker.text(); const assets: Record<string, string> = {}; let totalBytes = 0;
        for (const id of settings.assetIds) {
          const response = await fetch(`/assets/${id}`, { headers, signal: controller.signal }); if (!response.ok) throw new Error();
          const bytes = new Uint8Array(await response.arrayBuffer()); totalBytes += bytes.length;
          if (totalBytes > 64 * 1024 * 1024) throw new Error();
          assets[id] = assetDataUrl(bytes, response.headers.get('Content-Type') ?? 'application/octet-stream');
        }
        if (!disposed) setSource(frameDocument(settings, code, capability, assets, sessionValue.current));
      } catch { if (!disposed) setError('Custom widget unavailable'); }
    })();
    return () => { disposed = true; ready.current = false; controller.abort(); removeEventListener('message', receive); };
  }, [overlay, token, preview, widget.id, executionKey, draft]);
  useEffect(() => {
    if (!ready.current) return;
    for (const delivery of deliveries.filter(d => d.widgetId === widget.id)) frame.current?.contentWindow?.postMessage({ op: 'event', event: delivery.event, channel: channel.current }, '*');
  }, [deliveries, widget.id]);
  useEffect(() => { if (ready.current) frame.current?.contentWindow?.postMessage({ op: 'config', config: settings.config, channel: channel.current }, '*'); }, [settings.config]);
  useEffect(() => { if (ready.current) frame.current?.contentWindow?.postMessage({ op: 'session', session: publicSession, channel: channel.current }, '*'); }, [publicSession]);
  return <>{error && <output role="status">{error}{draft ? '. Draft previews use local memory and block network access.' : ''}</output>}{source && <iframe ref={frame} title={widget.name} sandbox="allow-scripts" referrerPolicy="no-referrer" srcDoc={source} style={{ border: 0, width: '100%', height: '100%' }} />}</>;
}

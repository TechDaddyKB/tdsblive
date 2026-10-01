import { useEffect, useMemo, useRef, useState } from 'react';
import { CombinedChat } from './CombinedChat';
import { ChatConnection } from './ChatConnection';
import { AlertQueue, alertText, type AlertJob } from './AlertQueue';
import type { ChatEvent, OverlayDefinition } from './chat';
import type { Scene, Widget } from './scene';
import './canvas.css';
export function MediaAsset({ id, overlay, token, volume, muted, loop, name }: { id: string | null; overlay: string; token: string; volume: number; muted: boolean; loop: boolean; name: string }) {
  const [asset, setAsset] = useState<{ url: string; mime: string } | null>(null); const [error, setError] = useState('');
  const media = useRef<HTMLMediaElement | null>(null);
  useEffect(() => {
    setAsset(null); setError('');
    if (!id || id.length !== 64 || !/^[0-9a-f]{64}$/.test(id)) return;
    const controller = new AbortController(); let objectUrl: string | undefined;
    void fetch(`/assets/${encodeURIComponent(id)}`, { method: token ? 'GET' : 'HEAD', headers: token ? { Authorization: `Bearer ${token}`, 'X-TDSBLive-Overlay': overlay } : {}, signal: controller.signal })
      .then(async response => {
        if (!response.ok) throw new Error('Media unavailable');
        const mime = response.headers.get('Content-Type') ?? '';
        if (token) objectUrl = URL.createObjectURL(await response.blob());
        if (controller.signal.aborted) { if (objectUrl) URL.revokeObjectURL(objectUrl); return; }
        setAsset({ url: objectUrl ?? `/assets/${id}`, mime });
      }).catch(() => { if (!controller.signal.aborted) setError('Media unavailable'); });
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [id, overlay, token]);
  useEffect(() => {
    const element = media.current; if (!element) return; element.volume = volume; element.muted = muted;
    void element.play().catch(() => { if (!muted) setError('Audio blocked by browser. Enable autoplay or use OBS.'); });
    return () => { element.pause(); };
  }, [asset, volume, muted]);
  if (error) return <span role="status">{error}</span>;
  if (!asset) return null;
  if (asset.mime.startsWith('image/')) return <img src={asset.url} alt={name} onError={() => setError('Image unavailable')} />;
  if (asset.mime.startsWith('video/')) return <video ref={media as React.RefObject<HTMLVideoElement>} src={asset.url} autoPlay muted={muted} loop={loop} playsInline onError={() => setError('Video unavailable')} />;
  if (asset.mime.startsWith('audio/')) return <audio ref={media as React.RefObject<HTMLAudioElement>} src={asset.url} autoPlay muted={muted} loop={loop} onError={() => setError('Audio unavailable')} />;
  return null;
}
function EmbeddedChat({ scene, widget, events, token }: { scene: Scene; widget: Widget; events: ChatEvent[]; token: string }) {
  const feed = useMemo(() => ({ definition: { ...scene, chat: widget.chat }, events }), [scene, widget, events]);
  return <CombinedChat id={scene.id} feed={feed} token={token} />;
}
function ActiveAlert({ job, overlay, token, silent }: { job: AlertJob; overlay: string; token: string; silent: boolean }) {
  const w = job.widget;
  return <div className={`active-alert enter-${w.alert.animation}`} data-alert-event={job.event.id}>
    <MediaAsset id={w.alert.mediaAssetId} overlay={overlay} token={token} volume={w.volume} muted={silent} loop={false} name={w.name} />
    <p>{alertText(w.alert.template, job.event)}</p>
    <MediaAsset id={w.alert.soundAssetId} overlay={overlay} token={token} volume={w.volume} muted={silent} loop={false} name={w.name} />
  </div>;
}
export function CanvasRuntime({ id, token = '', preview = false, previewAudio = false }: { id: string; token?: string; preview?: boolean; previewAudio?: boolean }) {
  const [scene, setScene] = useState<Scene | null>(null); const [events, setEvents] = useState<ChatEvent[]>([]); const [active, setActive] = useState<AlertJob[]>([]);
  const [status, setStatus] = useState('Connecting'); const settings = useRef<Scene | null>(null); const queue = useRef(new AlertQueue());
  useEffect(() => {
    const scheduler = queue.current; scheduler.clear();
    const connection = new ChatConnection(id, token, preview, definition => {
      const value = definition as Scene; settings.current = value; scheduler.reconcile(value.widgets); setScene(value); setActive(scheduler.tick(Date.now()));
    }, (incoming, delivery) => {
      const value = settings.current; if (!value) return;
      const now = Date.now(); if (delivery !== 'history') for (const event of incoming) for (const widget of value.widgets) scheduler.enqueue(widget, event, now);
      setActive(scheduler.tick(now)); setEvents(incoming);
    }, setStatus, true);
    void connection.start();
    const timer = setInterval(() => { const jobs = scheduler.tick(Date.now()); setActive(old => old.map(j => j.key).join(',') === jobs.map(j => j.key).join(',') ? old : jobs); }, 100);
    return () => { clearInterval(timer); connection.stop(); scheduler.clear(); };
  }, [id, token, preview]);
  if (!scene) return <output>{status}</output>;
  return <div className="canvas-runtime" style={{ width: scene.width, height: scene.height }} aria-label="Overlay scene">
    {preview && <output className="canvas-preview-label">Test preview · {previewAudio ? 'audio enabled' : 'silent'} · {status}</output>}
    {scene.widgets.filter(w => !w.hidden).map(w => <div className="runtime-widget" key={w.id} data-widget-id={w.id}
      style={{ left: w.x, top: w.y, width: w.width, height: w.height, transform: `rotate(${w.rotation}deg)`, color: w.color, fontSize: w.fontSize }}>
      {w.kind === 'text' && <div className="widget-text">{w.text}</div>}
      {['image', 'video', 'audio'].includes(w.kind) && <MediaAsset id={w.assetId} overlay={id} token={token} volume={w.volume} muted={w.muted || preview && !previewAudio} loop={w.loop} name={w.name} />}
      {w.kind === 'chat' && <EmbeddedChat scene={scene} widget={w} events={events} token={token} />}
      {w.kind === 'alert' && active.filter(j => j.widget.id === w.id).map(job => <ActiveAlert key={job.key} job={job} overlay={id} token={token} silent={preview && !previewAudio} />)}
    </div>)}
  </div>;
}
export function OverlayView({ id, token, preview, previewAudio }: { id: string; token: string; preview: boolean; previewAudio: boolean }) {
  const [definition, setDefinition] = useState<OverlayDefinition | null>(null); const [error, setError] = useState('');
  useEffect(() => {
    const abort = new AbortController();
    void fetch(`/api/overlays/${id}`, { headers: token ? { Authorization: `Bearer ${token}` } : {}, signal: abort.signal }).then(async response => {
      if (!response.ok) throw new Error('Overlay unavailable. Sign in or use an overlay token.');
      const value = await response.json() as OverlayDefinition; if (!abort.signal.aborted) setDefinition(value);
    }).catch(() => { if (!abort.signal.aborted) setError('Overlay unavailable. Sign in or use an overlay token.'); });
    return () => abort.abort();
  }, [id, token]);
  if (!definition) return <output>{error || 'Connecting'}</output>;
  return definition.canvasEnabled ? <CanvasRuntime id={id} token={token} preview={preview} previewAudio={previewAudio} /> : <CombinedChat id={id} token={token} preview={preview} />;
}

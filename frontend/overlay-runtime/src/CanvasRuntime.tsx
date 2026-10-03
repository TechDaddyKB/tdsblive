import { CustomFrame } from './CustomFrame';
import type { CustomDelivery } from './custom';
import { EventList } from './EventList';
import { ProgressWidget } from './ProgressWidget';
import { useEffect, useMemo, useRef, useState } from 'react';
import { CombinedChat } from './CombinedChat';
import { ChatConnection } from './ChatConnection';
import { AlertQueue, alertText, type AlertJob } from './AlertQueue';
import type { ChatEvent, OverlayDefinition } from './chat';
import { defaultEventList, type Scene, type Widget } from './scene';
import './canvas.css';
import { DonorWidget, type DonorSnapshot } from './DonorWidget';
import { donorKinds } from './scene';
import { AutomationSoundPlayer } from './AutomationSound';
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
  const [listEvents, setListEvents] = useState<ChatEvent[]>([]); const [clock, setClock] = useState(Date.now());
  const [customEvents, setCustomEvents] = useState<CustomDelivery[]>([]);
  const customSession = useMemo(() => ({ connected: status === 'Connected', preview }), [status, preview]);
  const [donors, setDonors] = useState<DonorSnapshot[]>([]);
  useEffect(() => {
    const scheduler = queue.current; scheduler.clear(); setListEvents([]);
    const sound = new AutomationSoundPlayer(id, token, (executionId, state) => connection.reportSound(executionId, state));
    const connection = new ChatConnection(id, token, preview, definition => {
      const value = definition as Scene; settings.current = value; scheduler.reconcile(value.widgets); setScene(value); setActive(scheduler.tick(Date.now()));
    }, (incoming, delivery) => {
      const value = settings.current; if (!value) return;
      const now = Date.now(); if (delivery !== 'history') for (const event of incoming) for (const widget of value.widgets) scheduler.enqueue(widget, event, now);
      setActive(scheduler.tick(now)); setEvents(incoming);
      setListEvents(old => { const unique = new Map(old.map(e => [e.id, e])); for (const e of incoming) unique.set(e.id, e); return [...unique.values()].sort((a, b) => Date.parse(a.receivedAt) - Date.parse(b.receivedAt)).slice(-500); });
    }, setStatus, true, setDonors, command => { void sound.play(command); }, executionId => sound.interrupt(executionId), setCustomEvents);
    void connection.start();
    const timer = setInterval(() => { const now = Date.now(); const jobs = scheduler.tick(now);
      if (settings.current?.widgets.some(w => !w.hidden && w.kind === 'event-list' && !(w.eventList ?? defaultEventList).persistent))
        setClock(old => Math.floor(old / 1000) === Math.floor(now / 1000) ? old : now);
      setActive(old => old.map(j => j.key).join(',') === jobs.map(j => j.key).join(',') ? old : jobs); }, 100);
    return () => { clearInterval(timer); sound.stop(); connection.stop(); scheduler.clear(); };
  }, [id, token, preview]);
  if (!scene) return <output>{status}</output>;
  return <div className="canvas-runtime" style={{ width: scene.width, height: scene.height }} aria-label="Overlay scene">
    {preview && <output className="canvas-preview-label">Test preview · {previewAudio ? 'audio enabled' : 'silent'} · {status}</output>}
    {scene.widgets.filter(w => !w.hidden).map(w => <div className="runtime-widget" key={w.id} data-widget-id={w.id}
      style={{ left: w.x, top: w.y, width: w.width, height: w.height, transform: `rotate(${w.rotation}deg)`, color: w.color, fontSize: w.fontSize }}>
      {w.kind === 'custom' && <CustomFrame widget={w} overlay={id} token={token} preview={preview} audioEnabled={!preview || previewAudio} deliveries={customEvents} session={customSession} />}
      {w.kind === 'event-list' && <EventList widget={w} events={listEvents} now={clock} />}
      {['goal-bar', 'progress-bar'].includes(w.kind) && <ProgressWidget widget={w} snapshot={donors.find(s => s.widgetId === w.id)} />}
      {w.kind === 'text' && <div className="widget-text">{w.text}</div>}
      {donorKinds.includes(w.kind) && <DonorWidget widget={w} snapshot={donors.find(s => s.widgetId === w.id)} overlay={id} token={token} />}
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

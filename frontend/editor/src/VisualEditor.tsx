import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { defaultSettings } from '../../overlay-runtime/src/chat';
import { createWidget, inheritGroupSettings, type Scene, type Widget } from '../../overlay-runtime/src/scene';
import { EditorSession, type EditorState } from './EditorSession';
import { visualApi, type Revision } from './visualApi';
import { request } from './api';
import { WidgetProperties, type EditorAsset } from './WidgetProperties';
import { selection, groupSelection, copySelection, pasteSelection, alignSelection, distributeSelection, nudgeSelection, rotateSelection, reorderSelection, resizeSelection, type Alignment } from './sceneOperations';
import './visual-editor.css';
const sizes = [[1920, 1080], [2560, 1440], [3840, 2160], [1080, 1920]];
const kinds: Widget['kind'][] = ['text', 'image', 'video', 'audio', 'chat', 'alert', 'donor-crown', 'donor-leaderboard', 'latest-supporter', 'current-stream-leader', 'current-stream-total', 'event-list', 'goal-bar', 'progress-bar'];
export function VisualEditor() {
  const [overlays, setOverlays] = useState<Scene[]>([]); const [state, setState] = useState<EditorState | null>(null);
  const [assets, setAssets] = useState<EditorAsset[]>([]); const [selectedIds, setSelectedIds] = useState<string[]>([]); const [notice, setNotice] = useState('');
  const [grid, setGrid] = useState(true); const [panMode, setPanMode] = useState(false);
  const scroll = useRef<HTMLDivElement | null>(null); const pan = useRef<{ x: number; y: number; left: number; top: number } | null>(null);
  const setSelected = (id: string) => setSelectedIds(id ? [id] : []);
  const [zoom, setZoom] = useState(.4); const [snap, setSnap] = useState(true); const [revisions, setRevisions] = useState<Revision[]>([]);
  const [createName, setCreateName] = useState('Main Stream'); const [createId, setCreateId] = useState('main-stream');
  const [preset, setPreset] = useState('1920x1080'); const [width, setWidth] = useState(1920); const [height, setHeight] = useState(1080);
  const [preview, setPreview] = useState(false); const [previewAudio, setPreviewAudio] = useState(false);
  const [testType, setTestType] = useState('community.follow'); const [testPlatform, setTestPlatform] = useState('twitch'); const [raw, setRaw] = useState('{}');
  const [nativeInjection, setNativeInjection] = useState(false);
  const [dragView, setDragView] = useState<Widget | null>(null); const [clipboard, setClipboard] = useState<Widget[]>([]);
  const session = useRef<EditorSession | null>(null); const unsubscribe = useRef<(() => void) | undefined>(undefined);
  const gesture = useRef<{ widget: Widget; mode: 'move' | 'resize'; x: number; y: number; latest: Widget; ids: string[]; scene: Scene } | null>(null);
  const mounted = useRef(true);
  const operation = useRef(0);
  const current = (id: number) => mounted.current && operation.current === id;
  const install = (value: Scene) => {
    session.current?.dispose(); unsubscribe.current?.(); const next = new EditorSession(value, visualApi.save); session.current = next;
    unsubscribe.current = next.subscribe(setState); setState(next.snapshot); setSelected(''); setRevisions([]); setPreview(false); setNotice('');
  };
  useEffect(() => {
    mounted.current = true;
    const id = ++operation.current;
    void Promise.all([visualApi.list(), request<EditorAsset[]>('/api/assets')]).then(([values, media]) => {
      if (!current(id)) return; setOverlays(values); setAssets(media); const first = values.find(o => o.canvasEnabled); if (first) install(first);
    }).catch(() => { if (current(id)) setNotice('Unable to load overlays.'); });
    const leave = (event: BeforeUnloadEvent) => { if (session.current?.snapshot.status !== 'saved' && session.current) event.preventDefault(); };
    window.addEventListener('beforeunload', leave);
    return () => { mounted.current = false; ++operation.current; session.current?.dispose(); unsubscribe.current?.(); window.removeEventListener('beforeunload', leave); };
  }, []);
  const doc = state?.document; const selected = selectedIds[0] ?? ''; const chosen = doc?.widgets.find(w => w.id === selected);
  const selectedWidgets = doc ? selection(doc, selectedIds) : [];
  const selectedKeys = new Set(selectedWidgets.map(w => w.id));
  const select = (id: string, additive = false) => setSelectedIds(old => additive ? old.includes(id) ? old.filter(key => key !== id) : [...old, id] : [id]);
  const edit = (value: Scene) => session.current?.edit(value);
  const change = (w: Widget) => {
    if (!doc) return;
    const previous = doc.widgets.find(old => old.id === w.id);
    if (previous?.locked && w.locked) return;
    if (previous?.groupId && w.groupId === previous.groupId) {
      if (w.x !== previous.x || w.y !== previous.y) { edit(nudgeSelection(doc, [w.id], w.x - previous.x, w.y - previous.y)); return; }
      if (w.width !== previous.width || w.height !== previous.height) { edit(resizeSelection(doc, [w.id], previous, w.width, w.height)); return; }
      if (w.rotation !== previous.rotation) { edit(rotateSelection(doc, [w.id], w.rotation - previous.rotation)); return; }
    }
    if (w.kind === 'alert' && doc.widgets.find(old => old.id === w.id)?.alert.group !== w.alert.group) w = inheritGroupSettings(w, doc.widgets);
    edit({ ...doc, widgets: doc.widgets.map(old => old.id === w.id ? w : old.kind === 'alert' && w.kind === 'alert' && old.alert.group === w.alert.group ?
      { ...old, alert: { ...old.alert, concurrency: w.alert.concurrency, maximumQueueLength: w.alert.maximumQueueLength, overflowPolicy: w.alert.overflowPolicy } } : old) });
  };
  const load = async (id: string, reload = false) => {
    const operationId = ++operation.current;
    try {
      const saved = !session.current || await session.current.flush();
      if (!current(operationId)) return;
      if (!saved && !reload) { setNotice('Resolve the unsaved changes before switching overlays.'); return; }
      const value = await visualApi.load(id);
      if (current(operationId)) install(value);
    } catch { if (current(operationId)) setNotice('Unable to load overlay.'); }
  };
  const create = async () => {
    const operationId = ++operation.current;
    try {
      const saved = !session.current || await session.current.flush();
      if (!current(operationId)) return;
      if (!saved) { setNotice('Resolve the unsaved changes first.'); return; }
      const dimensions = preset === 'custom' ? [width, height] : preset.split('x').map(Number);
      const value = await visualApi.create({ id: createId, name: createName, width: dimensions[0], height: dimensions[1], background: 'transparent', version: 1,
        canvasEnabled: true, revisionLimit: 50, chat: structuredClone(defaultSettings), widgets: [] });
      const values = await visualApi.list();
      if (current(operationId)) { setOverlays(values); install(value); }
    } catch { if (current(operationId)) setNotice('Unable to create overlay. Use a unique lowercase slug, a name, and dimensions between 1 and 7680.'); }
  };
  const add = (kind: Widget['kind']) => { if (!doc || doc.widgets.length >= 100) return; const w = inheritGroupSettings(createWidget(kind), doc.widgets); edit({ ...doc, widgets: [...doc.widgets, w] }); setSelected(w.id); };
  const remove = () => { if (doc && selectedWidgets.length && !selectedWidgets.some(w => w.locked)) { edit({ ...doc, widgets: doc.widgets.filter(w => !selectedKeys.has(w.id)) }); setSelected(''); } };
  const duplicate = (copied?: Widget[]) => { if (!doc) return; const result = pasteSelection(doc, copied ?? copySelection(doc, selectedIds)); if (!result.ids.length) return;
    edit({ ...result.scene, widgets: result.scene.widgets.map(w => inheritGroupSettings(w, result.scene.widgets)) }); setSelectedIds(result.ids); };
  const begin = (e: ReactPointerEvent<HTMLElement>, widget: Widget, mode: 'move' | 'resize') => {
    e.stopPropagation(); if (!doc || panMode) return; const ids = selectedKeys.has(widget.id) ? selectedIds : [widget.id];
    if (e.shiftKey || e.ctrlKey || e.metaKey) { select(widget.id, true); return; }
    setSelectedIds(ids); e.currentTarget.focus(); if (selection(doc, ids).some(w => w.locked) || widget.hidden || e.button !== 0) return;
    e.currentTarget.setPointerCapture(e.pointerId); gesture.current = { widget, mode, x: e.clientX, y: e.clientY, latest: widget, ids, scene: doc }; setDragView(widget);
  };
  const move = (e: ReactPointerEvent<HTMLElement>) => {
    const g = gesture.current; if (!g) return; const dx = (e.clientX - g.x) / zoom, dy = (e.clientY - g.y) / zoom;
    const round = (v: number) => snap ? Math.round(v / 10) * 10 : Math.round(v);
    const bounded = (v: number, min: number) => Math.max(min, Math.min(7680, round(v)));
    g.latest = g.mode === 'move' ? { ...g.widget, x: bounded(g.widget.x + dx, -7680), y: bounded(g.widget.y + dy, -7680) } :
      { ...g.widget, width: bounded(g.widget.width + dx, 1), height: bounded(g.widget.height + dy, 1) }; setDragView(g.latest);
  };
  const finish = () => { const g = gesture.current; gesture.current = null; setDragView(null); if (g && g.mode === 'move') edit(nudgeSelection(g.scene, g.ids, g.latest.x - g.widget.x, g.latest.y - g.widget.y)); else if (g) edit(resizeSelection(g.scene, g.ids, g.widget, g.latest.width, g.latest.height)); };
  const reorder = (offset: number) => { if (doc) edit(reorderSelection(doc, selectedIds, offset > 0 ? 'up' : 'down')); };
  const test = async () => {
    if (!doc) return;
    try { const parsed: unknown = JSON.parse(raw); if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('Invalid raw object');
      const result = await visualApi.preview(doc.id, { type: testType, platform: testPlatform, user: 'Test viewer', message: 'Synthetic test message', raw: parsed, mode: nativeInjection ? 'native' : 'synthetic' });
      setNotice(`Test delivered only to this overlay’s preview. Persisted: ${result.persisted}. Live actions: ${result.liveActionsAllowed}.`);
    } catch { setNotice('Unable to send test event. Raw injection must be a JSON object.'); }
  };
  return <section aria-label="Visual overlay editor" className="visual-editor"><h2>Visual Overlay Editor</h2>
    <div className="canvas-toolbar"><label>Overlay<select aria-label="Overlay" value={doc?.id ?? ''} onChange={e => { void load(e.target.value); }}><option value="">Choose overlay…</option>{overlays.filter(o => o.canvasEnabled).map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
      <label>New overlay name<input value={createName} onChange={e => setCreateName(e.target.value)} /></label><label>New overlay ID<input value={createId} onChange={e => setCreateId(e.target.value)} /></label>
      <label>Canvas preset<select aria-label="Canvas preset" value={preset} onChange={e => setPreset(e.target.value)}>{sizes.map(([w, h]) => <option key={w} value={`${w}x${h}`}>{w} × {h}</option>)}<option value="custom">Custom</option></select></label>
      {preset === 'custom' && <><label>Custom width<input type="number" min={1} max={7680} value={width} onChange={e => setWidth(e.target.valueAsNumber)} /></label><label>Custom height<input type="number" min={1} max={7680} value={height} onChange={e => setHeight(e.target.valueAsNumber)} /></label></>}
      <button onClick={() => { void create(); }}>Create overlay</button></div>
    <output aria-label="Editor save status">{state?.status ?? 'Create a canvas to begin.'}</output>{notice && <p role="status">{notice}</p>}
    {doc && <><div className="canvas-toolbar"><label>Overlay name<input aria-label="Overlay name" value={doc.name} maxLength={128} onChange={e => edit({ ...doc, name: e.target.value })} /></label>
      <label>Canvas width<input aria-label="Canvas width" type="number" min={1} max={7680} value={doc.width} onChange={e => { const value = e.target.valueAsNumber; if (Number.isInteger(value) && value >= 1 && value <= 7680) edit({ ...doc, width: value }); }} /></label>
      <label>Canvas height<input aria-label="Canvas height" type="number" min={1} max={7680} value={doc.height} onChange={e => { const value = e.target.valueAsNumber; if (Number.isInteger(value) && value >= 1 && value <= 7680) edit({ ...doc, height: value }); }} /></label>
      <button onClick={() => { void session.current?.flush(); }}>Save now</button><button disabled={!state.undo} onClick={() => session.current?.undo()}>Undo</button><button disabled={!state.redo} onClick={() => session.current?.redo()}>Redo</button>
      <button onClick={() => { void load(doc.id, true); }}>Reload saved version</button>
      <label>Zoom<input aria-label="Canvas zoom" type="range" min={.1} max={2} step={.05} value={zoom} onChange={e => setZoom(Number(e.target.value))} /></label>
      <label><input type="checkbox" checked={snap} onChange={e => setSnap(e.target.checked)} />Snap to 10px grid</label>
      <label><input type="checkbox" checked={grid} onChange={e => setGrid(e.target.checked)} />Show grid</label>
      <button aria-pressed={panMode} onClick={() => setPanMode(!panMode)}>Pan canvas</button>
      <button disabled={selectedWidgets.length < 2} onClick={() => edit(groupSelection(doc, selectedIds))}>Group selection</button>
      <button disabled={!selectedWidgets.some(w => w.groupId)} onClick={() => edit(groupSelection(doc, selectedIds, true))}>Ungroup selection</button>
      {(['left', 'center', 'right', 'top', 'middle', 'bottom'] as Alignment[]).map(alignment => <button key={alignment} disabled={!selectedWidgets.length} onClick={() => edit(alignSelection(doc, selectedIds, alignment))}>Align {alignment}</button>)}
      <button disabled={selectedWidgets.length < 3} onClick={() => edit(distributeSelection(doc, selectedIds, 'x'))}>Distribute horizontally</button>
      <button disabled={selectedWidgets.length < 3} onClick={() => edit(distributeSelection(doc, selectedIds, 'y'))}>Distribute vertically</button>
      <button disabled={!selectedWidgets.length} onClick={() => edit(rotateSelection(doc, selectedIds, 15))}>Rotate selection 15°</button>
      <button disabled={!selectedWidgets.length} onClick={() => edit({ ...doc, widgets: doc.widgets.map(w => selectedKeys.has(w.id) ? { ...w, locked: !selectedWidgets.every(v => v.locked) } : w) })}>Toggle selection lock</button>
      <button disabled={!selectedWidgets.length} onClick={() => edit({ ...doc, widgets: doc.widgets.map(w => selectedKeys.has(w.id) && !w.locked ? { ...w, hidden: !selectedWidgets.every(v => v.hidden) } : w) })}>Toggle selection visibility</button>
      <button disabled={!selectedWidgets.length} onClick={() => edit(reorderSelection(doc, selectedIds, 'front'))}>Bring selection to front</button>
      <button disabled={!selectedWidgets.length} onClick={() => edit(reorderSelection(doc, selectedIds, 'back'))}>Send selection to back</button>
      <output aria-label="Selected layers">{selectedWidgets.length} selected</output>
      <button onClick={() => {
        const url = new URL(`/overlay/${doc.id}`, location.href).href;
        void (async () => {
          try {
            if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
            await navigator.clipboard.writeText(url); setNotice('OBS URL copied.');
          } catch { setNotice(`OBS URL: ${url}`); }
        })();
      }}>Copy OBS URL</button>
      <a href={`/overlay/${doc.id}`} target="_blank" rel="noreferrer">Open OBS overlay</a><button onClick={async () => { if (await session.current?.flush()) setPreview(!preview); }}>Preview</button>
      <button onClick={() => { void visualApi.revisions(doc.id).then(setRevisions).catch(() => setNotice('Unable to load revisions.')); }}>Revision history</button>
      <label>Retained revisions<input aria-label="Retained revisions" type="number" min={1} max={200} value={doc.revisionLimit} onChange={e => { const value = e.target.valueAsNumber; if (Number.isInteger(value) && value >= 1 && value <= 200) edit({ ...doc, revisionLimit: value }); }} /></label></div>
      {state.status === 'conflict' && <p role="alert">Another editor changed this overlay. Your local edits are retained. Reload the saved version to resolve the conflict.</p>}
      <div className="canvas-workspace" onKeyDown={e => {
        if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement || e.target instanceof HTMLSelectElement) return;
        const modifier = e.ctrlKey || e.metaKey, key = e.key.toLowerCase();
        if (modifier && key === 'z') { e.preventDefault(); if (e.shiftKey) session.current?.redo(); else session.current?.undo(); }
        else if (modifier && key === 'y') { e.preventDefault(); session.current?.redo(); }
        else if (modifier && key === 'd') { e.preventDefault(); duplicate(); }
        else if (modifier && key === 'c' && chosen) { e.preventDefault(); setClipboard(copySelection(doc, selectedIds)); }
        else if (modifier && key === 'v' && clipboard.length) { e.preventDefault(); duplicate(clipboard); }
        else if (modifier && key === 'a') { e.preventDefault(); setSelectedIds(doc.widgets.map(w => w.id)); }
        else if (modifier && key === 'g') { e.preventDefault(); edit(groupSelection(doc, selectedIds, e.shiftKey)); }
        else if (key === 'delete' || key === 'backspace') { e.preventDefault(); remove(); }
        else if (selectedWidgets.length && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key)) { e.preventDefault(); const n = e.shiftKey ? 10 : 1; edit(nudgeSelection(doc, selectedIds, e.key === 'ArrowRight' ? n : e.key === 'ArrowLeft' ? -n : 0, e.key === 'ArrowDown' ? n : e.key === 'ArrowUp' ? -n : 0)); }
      }}>
        <aside><h3>Widgets</h3>{kinds.map(kind => <button key={kind} onClick={() => add(kind)}>Add {kind === 'alert' ? 'AlertBox' : kind === 'chat' ? 'Combined Chat' : kind}</button>)}
          <h3>Layers</h3><ol>{[...doc.widgets].reverse().map(w => <li key={w.id}><button aria-pressed={selectedKeys.has(w.id)} onClick={e => select(w.id, e.shiftKey || e.ctrlKey || e.metaKey)}>{w.name}{w.locked ? ' 🔒' : ''}{w.hidden ? ' (hidden)' : ''}{w.groupId ? ' (group)' : ''}</button></li>)}</ol>
          <button disabled={!chosen} onClick={() => duplicate()}>Duplicate</button><button disabled={!chosen || chosen.locked} onClick={remove}>Delete layer</button><button disabled={!chosen} onClick={() => reorder(1)}>Raise layer</button><button disabled={!chosen} onClick={() => reorder(-1)}>Lower layer</button>
        </aside>
        <div className="canvas-scroll" ref={scroll} onPointerDown={e => {
          if (!panMode && e.button !== 1 || !scroll.current) return; e.preventDefault(); e.currentTarget.setPointerCapture(e.pointerId);
          pan.current = { x: e.clientX, y: e.clientY, left: scroll.current.scrollLeft, top: scroll.current.scrollTop };
        }} onPointerMove={e => { if (pan.current && scroll.current) { scroll.current.scrollLeft = pan.current.left - e.clientX + pan.current.x; scroll.current.scrollTop = pan.current.top - e.clientY + pan.current.y; } }}
        onPointerUp={() => { pan.current = null; }} onPointerCancel={() => { pan.current = null; }}><div style={{ width: doc.width * zoom, height: doc.height * zoom }}><div aria-label="Overlay canvas" className={`editor-canvas ${grid ? 'show-grid' : ''}`} tabIndex={0}
          style={{ width: doc.width, height: doc.height, transform: `scale(${zoom})`, transformOrigin: 'top left' }} onPointerDown={e => { if (!panMode && !e.shiftKey) setSelected(''); }}>
          {doc.widgets.filter(w => !w.hidden).map(saved => { const w = dragView?.id === saved.id ? dragView : saved; return <div key={w.id} data-widget-id={w.id} aria-label={`${w.name} widget`} tabIndex={0} className={`canvas-widget ${selectedKeys.has(w.id) ? 'selected' : ''}`}
            style={{ left: w.x, top: w.y, width: w.width, height: w.height, transform: `rotate(${w.rotation}deg)`, color: w.color, fontSize: w.fontSize }}
            onPointerDown={e => begin(e, w, 'move')} onPointerMove={move} onPointerUp={finish} onPointerCancel={() => { gesture.current = null; setDragView(null); }}>
            {w.kind === 'text' ? w.text : w.kind === 'image' && w.assetId ? <img draggable={false} src={`/assets/${w.assetId}`} alt={w.name} /> : <span>{w.name}</span>}
            {selectedKeys.has(w.id) && !w.locked && <button className="resize-handle" aria-label="Resize widget" onPointerDown={e => begin(e, w, 'resize')} onPointerMove={move} onPointerUp={finish}>↘</button>}
          </div>; })}</div></div></div>
        <aside>{chosen ? <WidgetProperties widget={chosen} assets={assets} change={change} /> : <p>Select a layer to edit its properties.</p>}</aside>
      </div>
      {revisions.length > 0 && <section aria-label="Revision history"><h3>Saved revisions</h3>{revisions.map(r => <p key={r.version}>v{r.version} · {r.name} · {new Date(r.savedAt).toLocaleString()} <button onClick={async () => { const operationId = ++operation.current; try { if (!await session.current?.flush() || !current(operationId)) return; const v = session.current!.snapshot.document.version; const value = await visualApi.restore(doc.id, r.version, v); if (!current(operationId)) return; install(value); setNotice(`Restored revision ${r.version} as a new revision.`); } catch { if (current(operationId)) setNotice('Restore failed. Refresh history; a revision may have expired or another editor saved.'); } }}>Restore v{r.version}</button></p>)}</section>}
      <section aria-label="Isolated overlay tests"><h3>Test events</h3><p>Tests reach only this overlay’s preview viewers. No persistence, financial changes, or external automation.</p><label>Test event type<input value={testType} onChange={e => setTestType(e.target.value)} /></label><label>Test platform<input value={testPlatform} onChange={e => setTestPlatform(e.target.value)} /></label>
        <details><summary>Developer raw injection</summary><label><input type="checkbox" checked={nativeInjection} onChange={e => setNativeInjection(e.target.checked)} />Use native Streamer.bot payload</label><p>Native mode normalizes an event/data envelope, then forces simulation. Example: {'{"event":{"source":"Twitch","type":"Follow"},"data":{"userName":"Test viewer"}}'}</p><label>Raw JSON<textarea aria-label="Raw JSON" value={raw} onChange={e => setRaw(e.target.value)} /></label><p>Raw fields are diagnostic input; the renderer never executes HTML or scripts.</p></details><button onClick={() => { void test(); }}>Send isolated test event</button></section>
      {preview && <section aria-label="Overlay preview"><label><input type="checkbox" checked={previewAudio} onChange={e => setPreviewAudio(e.target.checked)} />Enable preview audio</label><iframe title="Overlay test preview" allow="autoplay" src={`/overlay/${doc.id}?preview=1${previewAudio ? '&audio=1' : ''}`} style={{ width: '100%', height: 500, border: '1px solid #666' }} /></section>}
      <section><h3>Assets</h3><button onClick={() => { void request<EditorAsset[]>('/api/assets').then(setAssets); }}>Refresh assets</button><p>Upload images, GIFs, audio and video here or in Combined Chat setup.</p><input type="file" aria-label="Upload widget media" onChange={async e => { const file = e.target.files?.[0]; if (!file) return; try { const csrf = await request<{ requestToken: string }>('/api/auth/csrf'); const response = await fetch('/api/assets', { method: 'POST', headers: { 'Content-Type': file.type, 'X-Asset-Filename': file.name, 'X-TDSBLive-CSRF': csrf.requestToken }, body: file }); if (!response.ok) throw new Error(); setAssets(await request<EditorAsset[]>('/api/assets')); setNotice('Media uploaded.'); } catch { setNotice('Media upload failed. Check supported MIME type and 20 MiB maximum.'); } }} /></section>
    </>}
  </section>;
}

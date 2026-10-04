import { NamedPicker, eventLabels } from './TriggerControls';
import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { defaultCustom } from '../../overlay-runtime/src/custom';
import type { Widget } from '../../overlay-runtime/src/scene';
import { SettingsFields, type SettingsField } from './SettingsFields';
import type { EditorAsset } from './WidgetProperties';
import { bots } from './api';
const CodeEditor = lazy(() => import('./MonacoCodeEditor'));

export function CustomProperties({ widget, assets, change }: { widget: Widget; assets: EditorAsset[]; change: (widget: Widget) => void }) {
  const custom = widget.custom ?? defaultCustom; const [tab, setTab] = useState<'html' | 'css' | 'javaScript' | 'fields'>('html');
  const [fields, setFields] = useState(JSON.stringify(custom.fields, null, 2)); const [error, setError] = useState('');
  const [events, setEvents] = useState<string[]>([]); const [actions, setActions] = useState<{ id: string; name: string }[]>([]);
  const identity = useRef(widget.id);
  useEffect(() => { if (identity.current !== widget.id) { identity.current = widget.id; setFields(JSON.stringify(custom.fields, null, 2)); setError(''); } }, [widget.id, custom.fields]);
  useEffect(() => { let active = true; void bots.discovery().then(d => { if (active) { setEvents(Object.entries(d.events).flatMap(([source, names]) => names.map(n => `${source}.${n}`))); setActions(d.actions); } }).catch(() => {}); return () => { active = false; }; }, []);
  const update = (value: Partial<typeof custom>) => change({ ...widget, custom: { ...custom, ...value } });
  return <section aria-label="Custom widget editor"><h3>Custom code</h3>
    <p>JavaScript runs with a virtual DOM in an isolated worker. Use document, window lifecycle events, SBX.on, SBX.getConfig/getSession and asynchronous SBX.store.get/set. Browser navigation and parent access are unavailable.</p>
    <label>Package version<input value={custom.packageVersion} onChange={e => update({ packageVersion: e.target.value })} /></label>
    <label>Author<input maxLength={128} value={custom.author} onChange={e => update({ author: e.target.value })} /></label>
    <nav aria-label="Code tabs">{(['html', 'css', 'javaScript', 'fields'] as const).map(t => <button key={t} aria-pressed={tab === t} onClick={() => setTab(t)}>{t === 'fields' ? 'Settings JSON' : t === 'javaScript' ? 'JavaScript' : t.toUpperCase()}</button>)}</nav>
    <Suspense fallback={<p>Loading code editor…</p>}><CodeEditor language={tab === 'fields' ? 'json' : tab === 'javaScript' ? 'javascript' : tab} value={tab === 'fields' ? fields : custom[tab]} change={text => tab === 'fields' ? setFields(text) : update({ [tab]: text })} /></Suspense>
    {tab === 'fields' && <button onClick={() => { try { const value = JSON.parse(fields) as SettingsField[]; if (!Array.isArray(value)) throw new Error(); update({ fields: value }); setError(''); } catch { setError('Settings must be a valid JSON field array.'); } }}>Apply settings schema</button>}
    {error && <output>{error}</output>}
    <NamedPicker label="Subscribed events" values={custom.subscriptions} options={{ ...eventLabels, ...Object.fromEntries(events.map(e => [e, e.replace('.', ' · ')])) }} change={subscriptions => update({ subscriptions })} />
    <details><summary>Advanced subscription identifiers</summary><label>Subscribed event types<input aria-label="Custom subscriptions" list="custom-events" value={custom.subscriptions.join(',')} onChange={e => update({ subscriptions: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })} /></label>
    <datalist id="custom-events">{events.map(e => <option key={e} value={e} />)}</datalist><p>Use canonical event names or * for all permitted events. Unknown event names remain available for future adapters.</p>
    </details><fieldset><legend>Explicit permissions</legend>{['storage', 'chat', 'financial', 'raw', 'audio', 'network'].map(permission => <label key={permission}><input aria-label={`Allow ${permission}`} type="checkbox" checked={custom.permissions.includes(permission)} onChange={e => update({ permissions: e.target.checked ? [...custom.permissions, permission] : custom.permissions.filter(p => p !== permission), ...(permission === 'network' && !e.target.checked ? { networkDomains: [] } : {}) })} />{permission}</label>)}</fieldset>
    <label>Allowed HTTPS domains<input aria-label="Network domains" disabled={!custom.permissions.includes('network')} value={custom.networkDomains.join(',')} onChange={e => update({ networkDomains: e.target.value.split(',').map(d => d.trim()).filter(Boolean) })} /></label>
    <label>Widget assets<select multiple aria-label="Custom assets" value={custom.assetIds} onChange={e => update({ assetIds: Array.from(e.target.selectedOptions, o => o.value) })}>{assets.map(a => <option key={a.id} value={a.id}>{a.filename}</option>)}</select></label>
    <p>Use an asset ID in an image/audio/video src attribute. Imported packages start with all permissions disabled; review their code before granting access.</p>
    <SettingsFields fields={custom.fields} values={custom.config} change={(key, value) => update({ config: { ...custom.config, [key]: value } })} context={{ assets, events, actions, button: key => update({ config: { ...custom.config, [key]: Date.now() } }) }} />
  </section>;
}

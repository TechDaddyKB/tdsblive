import { useState } from 'react';
import { widgetId, type Scene, type AlertSet } from '../../overlay-runtime/src/scene';
export function AlertSets({ scene, change }: { scene: Scene; change: (value: Scene) => void }) {
  const [name, setName] = useState('Donation designs'); const sets = scene.alertSets ?? [];
  const update = (value: AlertSet) => change({ ...scene, alertSets: sets.map(s => s.id === value.id ? value : s) });
  return <details className="alert-sets"><summary>Choose between alert designs</summary><p>Sets choose designs; queue groups control playback. Existing alerts outside a set all run when they match.</p>
    <label>New alert set name<input value={name} maxLength={128} onChange={e => setName(e.target.value)} /></label>
    <button disabled={!name.trim() || !scene.widgets.some(w => w.kind === 'alert' && !sets.some(s => s.widgetIds.includes(w.id)))} onClick={() => { const w = scene.widgets.find(w => w.kind === 'alert' && !sets.some(s => s.widgetIds.includes(w.id))); if (w) change({ ...scene, alertSets: [...sets, { id: widgetId(), name, widgetIds: [w.id], selection: 'first' }] }); }}>Create alert set</button>
    {sets.map(set => <fieldset key={set.id}><legend>{set.name}</legend><label>Set name<input value={set.name} onChange={e => update({ ...set, name: e.target.value })} /></label><label>Design selection<select value={set.selection} onChange={e => update({ ...set, selection: e.target.value as AlertSet['selection'] })}><option value="first">First matching design</option><option value="all">All matching designs</option></select></label>
      <ol>{set.widgetIds.map((id, index) => <li key={id}>{scene.widgets.find(w => w.id === id)?.name}<button disabled={index === 0} onClick={() => { const ids = [...set.widgetIds]; [ids[index - 1], ids[index]] = [ids[index], ids[index - 1]]; update({ ...set, widgetIds: ids }); }}>Move design {index + 1} up</button><button disabled={index === set.widgetIds.length - 1} onClick={() => { const ids = [...set.widgetIds]; [ids[index + 1], ids[index]] = [ids[index], ids[index + 1]]; update({ ...set, widgetIds: ids }); }}>Move design {index + 1} down</button><button disabled={set.widgetIds.length === 1} onClick={() => update({ ...set, widgetIds: set.widgetIds.filter(w => w !== id) })}>Remove design {index + 1}</button></li>)}</ol>
      <label>Add design<select value="" onChange={e => { if (e.target.value) update({ ...set, widgetIds: [...set.widgetIds, e.target.value] }); }}><option value="">Choose an alert design…</option>{scene.widgets.filter(w => w.kind === 'alert' && !sets.some(s => s.widgetIds.includes(w.id))).map(w => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
      <button onClick={() => change({ ...scene, alertSets: sets.filter(s => s.id !== set.id) })}>Dissolve {set.name}</button>
    </fieldset>)}
  </details>;
}

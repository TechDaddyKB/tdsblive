import { widgetId, type Scene, type Widget } from '../../overlay-runtime/src/scene';

export type Alignment = 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom';
function units(widgets: Widget[]): { id: string; members: Widget[]; x: number; y: number; width: number; height: number }[] {
  const groups = new Map<string, Widget[]>();
  for (const w of widgets) { const id = w.groupId ?? w.id; groups.set(id, [...groups.get(id) ?? [], w]); }
  return [...groups].map(([id, members]) => {
    const x = Math.min(...members.map(w => w.x)), y = Math.min(...members.map(w => w.y));
    return { id, members, x, y, width: Math.max(...members.map(w => w.x + w.width)) - x, height: Math.max(...members.map(w => w.y + w.height)) - y };
  });
}
export function selection(scene: Scene, ids: string[]): Widget[] {
  const groups = new Set(scene.widgets.filter(w => ids.includes(w.id) && w.groupId).map(w => w.groupId));
  return scene.widgets.filter(w => ids.includes(w.id) || w.groupId && groups.has(w.groupId));
}
export function transformSelection(scene: Scene, ids: string[], transform: (widget: Widget) => Widget): Scene {
  const chosen = selection(scene, ids);
  if (chosen.some(w => w.locked)) return scene;
  const keys = new Set(chosen.map(w => w.id));
  return { ...scene, widgets: scene.widgets.map(w => keys.has(w.id) ? transform(w) : w) };
}
export function groupSelection(scene: Scene, ids: string[], ungroup = false): Scene {
  const chosen = selection(scene, ids);
  if (chosen.some(w => w.locked) || !ungroup && chosen.length < 2) return scene;
  const groupId = ungroup ? null : widgetId();
  return transformSelection(scene, ids, w => ({ ...w, groupId }));
}
export function copySelection(scene: Scene, ids: string[]): Widget[] { return structuredClone(selection(scene, ids)); }
export function pasteSelection(scene: Scene, copied: Widget[], sourceSets = scene.alertSets ?? []): { scene: Scene; ids: string[] } {
  if (!copied.length || scene.widgets.length + copied.length > 100) return { scene, ids: [] };
  const groups = new Map<string, string>();
  const widgets = copied.map(w => {
    if (w.groupId && !groups.has(w.groupId)) groups.set(w.groupId, widgetId());
    return { ...structuredClone(w), id: widgetId(), groupId: w.groupId ? groups.get(w.groupId)! : null,
      x: Math.min(7680, w.x + 20), y: Math.min(7680, w.y + 20), locked: false };
  });
  const identities = new Map(copied.map((w, i) => [w.id, widgets[i].id]));
  const sets = sourceSets.filter(s => s.widgetIds.every(id => identities.has(id))).map(s => ({ ...s, id: widgetId(), name: (s.name + ' copy').slice(0, 128), widgetIds: s.widgetIds.map(id => identities.get(id)!) }));
  return { scene: { ...scene, widgets: [...scene.widgets, ...widgets], alertSets: [...scene.alertSets ?? [], ...sets] }, ids: widgets.map(w => w.id) };
}
export function alignSelection(scene: Scene, ids: string[], alignment: Alignment): Scene {
  const widgets = selection(scene, ids); if (!widgets.length) return scene;
  const items = units(widgets);
  const left = items.length === 1 ? 0 : Math.min(...items.map(w => w.x));
  const top = items.length === 1 ? 0 : Math.min(...items.map(w => w.y));
  const right = items.length === 1 ? scene.width : Math.max(...items.map(w => w.x + w.width));
  const bottom = items.length === 1 ? scene.height : Math.max(...items.map(w => w.y + w.height));
  const offsets = new Map<string, { x: number; y: number }>();
  for (const item of items) {
    const x = alignment === 'left' ? left - item.x : alignment === 'center' ? (left + right - item.width) / 2 - item.x : alignment === 'right' ? right - item.width - item.x : 0;
    const y = alignment === 'top' ? top - item.y : alignment === 'middle' ? (top + bottom - item.height) / 2 - item.y : alignment === 'bottom' ? bottom - item.height - item.y : 0;
    for (const w of item.members) offsets.set(w.id, { x, y });
  }
  return transformSelection(scene, ids, w => ({ ...w, x: w.x + offsets.get(w.id)!.x, y: w.y + offsets.get(w.id)!.y }));
}
export function distributeSelection(scene: Scene, ids: string[], axis: 'x' | 'y'): Scene {
  const selected = selection(scene, ids), widgets = units(selected).sort((a, b) => a[axis] - b[axis]);
  if (widgets.length < 3 || selected.some(w => w.locked)) return scene;
  const size = axis === 'x' ? 'width' : 'height';
  const start = widgets[0][axis], last = widgets.at(-1)!;
  const gap = (last[axis] + last[size] - start - widgets.reduce((sum, w) => sum + w[size], 0)) / (widgets.length - 1);
  const positions = new Map<string, number>(); let position = start;
  for (const unit of widgets) { for (const w of unit.members) positions.set(w.id, w[axis] + position - unit[axis]); position += unit[size] + gap; }
  return transformSelection(scene, ids, w => ({ ...w, [axis]: positions.get(w.id)! }));
}
export function resizeSelection(scene: Scene, ids: string[], anchor: Widget, width: number, height: number): Scene {
  const selected = selection(scene, ids); if (!selected.length) return scene;
  const left = Math.min(...selected.map(w => w.x)), top = Math.min(...selected.map(w => w.y));
  const scaleX = width / anchor.width, scaleY = height / anchor.height;
  const result = transformSelection(scene, ids, w => ({ ...w, x: left + (w.x - left) * scaleX, y: top + (w.y - top) * scaleY, width: w.width * scaleX, height: w.height * scaleY }));
  return result.widgets.some(w => w.width < 1 || w.width > 7680 || w.height < 1 || w.height > 7680 || Math.abs(w.x) > 7680 || Math.abs(w.y) > 7680) ? scene : result;
}
export function nudgeSelection(scene: Scene, ids: string[], dx: number, dy: number): Scene {
  const widgets = selection(scene, ids); if (!widgets.length) return scene;
  const boundedX = Math.max(-7680 - Math.min(...widgets.map(w => w.x)), Math.min(7680 - Math.max(...widgets.map(w => w.x)), dx));
  const boundedY = Math.max(-7680 - Math.min(...widgets.map(w => w.y)), Math.min(7680 - Math.max(...widgets.map(w => w.y)), dy));
  return transformSelection(scene, ids, w => ({ ...w, x: w.x + boundedX, y: w.y + boundedY }));
}
export function rotateSelection(scene: Scene, ids: string[], degrees: number): Scene {
  const widgets = selection(scene, ids); if (!widgets.length) return scene;
  const cx = (Math.min(...widgets.map(w => w.x)) + Math.max(...widgets.map(w => w.x + w.width))) / 2;
  const cy = (Math.min(...widgets.map(w => w.y)) + Math.max(...widgets.map(w => w.y + w.height))) / 2;
  const radians = degrees * Math.PI / 180;
  const result = transformSelection(scene, ids, w => {
    const dx = w.x + w.width / 2 - cx, dy = w.y + w.height / 2 - cy;
    return { ...w, x: cx + dx * Math.cos(radians) - dy * Math.sin(radians) - w.width / 2,
      y: cy + dx * Math.sin(radians) + dy * Math.cos(radians) - w.height / 2,
      rotation: ((w.rotation + degrees + 540) % 360 + 360) % 360 - 180 };
  });
  return result.widgets.some(w => Math.abs(w.x) > 7680 || Math.abs(w.y) > 7680) ? scene : result;
}
export function reorderSelection(scene: Scene, ids: string[], direction: 'up' | 'down' | 'front' | 'back'): Scene {
  const chosen = selection(scene, ids); if (chosen.some(w => w.locked)) return scene;
  const keys = new Set(chosen.map(w => w.id));
  const rest = scene.widgets.filter(w => !keys.has(w.id));
  if (direction === 'front' || direction === 'back') return { ...scene, widgets: direction === 'front' ? [...rest, ...chosen] : [...chosen, ...rest] };
  const widgets = [...scene.widgets];
  if (direction === 'up') {
    for (let i = widgets.length - 2; i >= 0; i--) if (keys.has(widgets[i].id) && !keys.has(widgets[i + 1].id)) [widgets[i], widgets[i + 1]] = [widgets[i + 1], widgets[i]];
  } else {
    for (let i = 1; i < widgets.length; i++) if (keys.has(widgets[i].id) && !keys.has(widgets[i - 1].id)) [widgets[i], widgets[i - 1]] = [widgets[i - 1], widgets[i]];
  }
  return { ...scene, widgets };
}

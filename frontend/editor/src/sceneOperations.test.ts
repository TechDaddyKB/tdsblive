import { expect, it } from 'vitest';
import { createWidget, type Scene } from '../../overlay-runtime/src/scene';
import { defaultSettings } from '../../overlay-runtime/src/chat';
import { alignSelection, copySelection, distributeSelection, groupSelection, nudgeSelection, pasteSelection, reorderSelection, resizeSelection, rotateSelection, selection } from './sceneOperations';
function fixture(): Scene {
  return { id: 'test', name: 'Test', width: 1920, height: 1080, background: 'transparent', version: 1, chat: defaultSettings, canvasEnabled: true, revisionLimit: 50,
    widgets: [0, 200, 700].map((x, i) => ({ ...createWidget('text'), x, y: i * 100, width: 100, height: 100 })) };
}
it('keeps groups intact through movement, rotation, copying and independent ungrouping', () => {
  const original = fixture(), ids = original.widgets.slice(0, 2).map(w => w.id);
  const grouped = groupSelection(original, ids); expect(selection(grouped, [ids[0]])).toHaveLength(2);
  const moved = nudgeSelection(grouped, [ids[0]], 10, 20); expect(moved.widgets.slice(0, 2).map(w => w.x)).toEqual([10, 210]);
  const rotated = rotateSelection(moved, [ids[0]], 180); expect(rotated.widgets[0].x).toBeCloseTo(210); expect(rotated.widgets[0].y).toBeCloseTo(120);
  const pasted = pasteSelection(rotated, copySelection(rotated, [ids[0]])); expect(pasted.ids).toHaveLength(2);
  expect(pasted.scene.widgets[3].groupId).not.toBe(grouped.widgets[0].groupId); expect(selection(pasted.scene, [pasted.ids[0]])).toHaveLength(2);
  const ungrouped = groupSelection(pasted.scene, pasted.ids, true); expect(selection(ungrouped, [pasted.ids[0]])).toHaveLength(1);
  expect(selection(ungrouped, [ids[0]])).toHaveLength(2); expect(original.widgets[0].groupId).toBeNull();
});
it('aligns, distributes and reorders selected layers without disturbing other layers', () => {
  const scene = fixture(), ids = scene.widgets.map(w => w.id);
  expect(distributeSelection(scene, ids, 'x').widgets.map(w => w.x)).toEqual([0, 350, 700]);
  expect(distributeSelection(scene, ids, 'y').widgets.map(w => w.y)).toEqual([0, 100, 200]);
  for (const alignment of ['left', 'center', 'right', 'top', 'middle', 'bottom'] as const) expect(alignSelection(scene, ids, alignment)).not.toBe(scene);
  expect(alignSelection(scene, [ids[0]], 'right').widgets[0].x).toBe(1820);
  expect(reorderSelection(scene, [ids[0]], 'front').widgets.map(w => w.id)).toEqual([ids[1], ids[2], ids[0]]);
  expect(reorderSelection(scene, [ids[2]], 'back').widgets.map(w => w.id)).toEqual([ids[2], ids[0], ids[1]]);
  expect(reorderSelection(scene, [ids[0]], 'up').widgets.map(w => w.id)).toEqual([ids[1], ids[0], ids[2]]);
  expect(reorderSelection(scene, [ids[2]], 'down').widgets.map(w => w.id)).toEqual([ids[0], ids[2], ids[1]]);
});
it('scales grouped sizes and spacing and rejects a resize beyond supported geometry', () => {
  const scene = fixture(), ids = scene.widgets.slice(0, 2).map(w => w.id), grouped = groupSelection(scene, ids);
  const scaled = resizeSelection(grouped, [ids[0]], grouped.widgets[0], 200, 50);
  expect(scaled.widgets.slice(0, 2).map(w => [w.x, w.y, w.width, w.height])).toEqual([[0, 0, 200, 50], [400, 50, 200, 50]]);
  expect(scaled.widgets[2]).toBe(grouped.widgets[2]);
  expect(resizeSelection(grouped, ids, grouped.widgets[0], 10000, 10000)).toBe(grouped);
  expect(resizeSelection(grouped, [], grouped.widgets[0], 200, 50)).toBe(grouped);
  const aligned = alignSelection(grouped, [ids[0]], 'right'); expect(aligned.widgets[1].x - aligned.widgets[0].x).toBe(200);
  expect(alignSelection(scene, [], 'left')).toBe(scene); expect(nudgeSelection(scene, [], 1, 1)).toBe(scene); expect(rotateSelection(scene, [], 15)).toBe(scene);
});
it('enforces lock and capacity, and bounds a group move without changing relative spacing', () => {
  const scene = fixture(), ids = scene.widgets.map(w => w.id);
  scene.widgets[1].locked = true;
  expect(nudgeSelection(scene, ids, 10, 0)).toBe(scene); expect(groupSelection(scene, ids)).toBe(scene);
  expect(rotateSelection(scene, ids, 15)).toBe(scene); expect(reorderSelection(scene, ids, 'front')).toBe(scene);
  expect(distributeSelection(scene, ids, 'x')).toBe(scene);
  scene.widgets[1].locked = false;
  const moved = nudgeSelection(scene, ids, 10000, -10000); expect(moved.widgets[2].x).toBe(7680); expect(moved.widgets[0].y).toBe(-7680);
  expect(moved.widgets[1].x - moved.widgets[0].x).toBe(200);
  expect(pasteSelection({ ...scene, widgets: Array.from({ length: 100 }, () => createWidget('text')) }, scene.widgets).ids).toEqual([]);
  expect(distributeSelection(scene, [ids[0]], 'x')).toBe(scene); expect(groupSelection(scene, [ids[0]])).toBe(scene);
  expect(rotateSelection({ ...scene, widgets: scene.widgets.map(w => ({ ...w, x: 7680 })) }, ids, 90)).toBeDefined();
});

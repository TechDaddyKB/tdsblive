import type { KeyboardEvent } from 'react';
interface Commands {
  undo: () => void; redo: () => void; duplicate: () => void;
  copy?: () => void; paste?: () => void; selectAll: () => void;
  group: (ungroup: boolean) => void; remove: () => void;
  nudge?: (x: number, y: number) => void;
}
const directions: Record<string, [number, number]> = {
  ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1],
};
export function editorKeyboard(event: KeyboardEvent<HTMLElement>, commands: Commands) {
  const target = event.target;
  if (target instanceof HTMLElement && (target.isContentEditable || target.closest('.monaco-editor') || target.matches('input,textarea,select'))) return;
  const key = event.key.toLowerCase();
  const shortcuts: Record<string, (() => void) | undefined> = {
    z: event.shiftKey ? commands.redo : commands.undo, y: commands.redo,
    d: commands.duplicate, c: commands.copy, v: commands.paste,
    a: commands.selectAll, g: () => commands.group(event.shiftKey),
  };
  const modified = event.ctrlKey || event.metaKey;
  if (modified && shortcuts[key]) { event.preventDefault(); shortcuts[key](); return; }
  if (key === 'delete' || key === 'backspace') { event.preventDefault(); commands.remove(); return; }
  const direction = directions[event.key];
  if (direction && commands.nudge) {
    event.preventDefault(); const step = event.shiftKey ? 10 : 1;
    commands.nudge(direction[0] * step, direction[1] * step);
  }
}

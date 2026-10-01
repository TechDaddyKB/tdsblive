import { afterEach, describe, expect, it, vi } from 'vitest';
import { EditorSession } from './EditorSession';
import { defaultSettings } from '../../overlay-runtime/src/chat';
import type { Scene } from '../../overlay-runtime/src/scene';
const scene = (): Scene => ({ id: 'test', name: 'Test', width: 1920, height: 1080, background: 'transparent', version: 1,
  chat: structuredClone(defaultSettings), canvasEnabled: true, revisionLimit: 50, widgets: [] });
afterEach(() => vi.useRealTimers());
describe('editor autosave and local history', () => {
  it('debounces exactly 750ms and combines repeated edits into one versioned save', async () => {
    vi.useFakeTimers(); const save = vi.fn(async (s: Scene) => ({ ...s, version: s.version + 1 })); const session = new EditorSession(scene(), save);
    session.edit({ ...session.snapshot.document, name: 'One' }); await vi.advanceTimersByTimeAsync(500);
    session.edit({ ...session.snapshot.document, name: 'Two' }); await vi.advanceTimersByTimeAsync(749); expect(save).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); expect(save).toHaveBeenCalledTimes(1); expect(save.mock.calls[0][0].name).toBe('Two'); expect(session.snapshot.status).toBe('saved');
    session.dispose();
  });
  it('preserves edits made while saving and uses the returned version for the next save', async () => {
    let complete: ((scene: Scene) => void) | undefined;
    const save = vi.fn((s: Scene) => new Promise<Scene>(resolve => { complete = resolve; expect(s.version).toBe(save.mock.calls.length); }));
    const session = new EditorSession(scene(), save); session.edit({ ...session.snapshot.document, name: 'One' });
    const flushing = session.flush(); await vi.waitFor(() => expect(complete).toBeDefined()); session.edit({ ...session.snapshot.document, name: 'Two' }); complete!({ ...scene(), name: 'One', version: 2 });
    await vi.waitFor(() => expect(save).toHaveBeenCalledTimes(2)); complete!({ ...scene(), name: 'Two', version: 3 }); expect(await flushing).toBe(true);
    expect(session.snapshot.document.name).toBe('Two'); expect(session.snapshot.document.version).toBe(3); session.dispose();
  });
  it('undoes and redoes without reusing stale persisted versions', async () => {
    const session = new EditorSession(scene(), async s => ({ ...s, version: s.version + 1 }));
    session.edit({ ...session.snapshot.document, name: 'Changed' }); await session.flush(); session.undo();
    expect(session.snapshot.document.name).toBe('Test'); expect(session.snapshot.document.version).toBe(2); await session.flush();
    session.redo(); expect(session.snapshot.document.name).toBe('Changed'); expect(session.snapshot.document.version).toBe(3); session.dispose();
  });
  it('stops automatic retries on a version conflict and retains local edits', async () => {
    vi.useFakeTimers(); const save = vi.fn(async () => { throw Object.assign(new Error('Conflict'), { status: 409 }); });
    const session = new EditorSession(scene(), save); session.edit({ ...session.snapshot.document, name: 'Local' }); await session.flush();
    expect(session.snapshot.status).toBe('conflict'); session.edit({ ...session.snapshot.document, name: 'More local' }); await vi.advanceTimersByTimeAsync(10000);
    expect(save).toHaveBeenCalledTimes(1); expect(session.snapshot.document.name).toBe('More local'); expect(await session.flush()).toBe(false); session.dispose();
  });
  it('allows explicit retry after an error and cancels unsent saves on disposal', async () => {
    vi.useFakeTimers(); const save = vi.fn().mockRejectedValueOnce(new Error('Offline')).mockImplementation(async (s: Scene) => ({ ...s, version: s.version + 1 }));
    const session = new EditorSession(scene(), save); session.edit({ ...session.snapshot.document, name: 'Changed' }); expect(await session.flush()).toBe(false);
    expect(await session.flush()).toBe(true); session.edit({ ...session.snapshot.document, name: 'Unsent' }); session.dispose(); await vi.advanceTimersByTimeAsync(1000);
    expect(save).toHaveBeenCalledTimes(2);
  });
});

it('reports synchronous persistence errors and allows a later explicit retry', async () => {
  const save = vi.fn().mockImplementationOnce(() => { throw new Error('Synchronous failure'); }).mockImplementation(async (s: Scene) => ({ ...s, version: s.version + 1 }));
  const session = new EditorSession(scene(), save); session.edit({ ...session.snapshot.document, name: 'Local' }); expect(await session.flush()).toBe(false);
  expect(session.snapshot.status).toBe('error'); expect(session.snapshot.document.name).toBe('Local'); expect(await session.flush()).toBe(true); session.dispose();
});

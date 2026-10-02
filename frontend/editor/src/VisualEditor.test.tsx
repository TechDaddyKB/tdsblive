import { StrictMode } from 'react';
import { visualApi } from './visualApi';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { VisualEditor } from './VisualEditor';
import { createWidget, type Scene } from '../../overlay-runtime/src/scene';
import { defaultSettings } from '../../overlay-runtime/src/chat';
function host() {
  let document: Scene = { id: 'main', name: 'Main', width: 1920, height: 1080, version: 1, background: 'transparent', canvasEnabled: true, revisionLimit: 50, chat: structuredClone(defaultSettings), widgets: [createWidget('text')] };
  let conflict = false;
  const fetcher = vi.fn(async (url: string, options?: RequestInit) => {
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'synthetic-csrf' });
    if (url === '/api/assets') return Response.json([]);
    if (url.endsWith('/preview-events')) return Response.json({ persisted: false, liveActionsAllowed: false });
    if (url.endsWith('/restore')) { document = { ...document, version: document.version + 1, name: 'Restored' }; return Response.json(document); }
    if (url.endsWith('/revisions')) return Response.json([{ version: 1, savedAt: new Date(0).toISOString(), name: 'Original' }]);
    if (options?.method === 'PUT') { if (conflict) return new Response(null, { status: 409 }); document = { ...JSON.parse(String(options.body)), version: document.version + 1 }; return Response.json(document); }
    if (url === '/api/overlays') { if (options?.method === 'POST') { document = JSON.parse(String(options.body)); return Response.json(document, { status: 201 }); } return Response.json([document]); }
    return Response.json(document);
  }); vi.stubGlobal('fetch', fetcher);
  return { fetcher, get: () => document, conflict: (value: boolean) => { conflict = value; } };
}
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });
it.each(['missing', 'denied', 'available'] as const)('copies the OBS URL or offers manual copying with a %s clipboard', async mode => {
  host();
  const descriptor = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: mode === 'missing' ? undefined : {
    writeText: mode === 'denied' ? vi.fn().mockRejectedValue(new Error('Clipboard denied')) : vi.fn().mockResolvedValue(undefined),
  } });
  try {
    render(<VisualEditor />); await screen.findByLabelText('Overlay canvas');
    fireEvent.click(screen.getByRole('button', { name: 'Copy OBS URL' }));
    await screen.findByText(mode === 'available' ? 'OBS URL copied.' : `OBS URL: ${new URL('/overlay/main', location.href).href}`);
  } finally {
    if (descriptor) Object.defineProperty(navigator, 'clipboard', descriptor);
    else Reflect.deleteProperty(navigator, 'clipboard');
  }
});
it('preserves keyboard history and layers, saves geometry and switches to a newly created custom canvas', async () => {
  const api = host(); render(<VisualEditor />); await screen.findByLabelText('Overlay canvas');
  fireEvent.click(screen.getByRole('listitem').querySelector('button')!); const widget = document.querySelector('.canvas-widget')!;
  fireEvent.keyDown(widget, { key: 'ArrowRight', shiftKey: true }); expect(screen.getByLabelText('X', { exact: true })).toHaveValue(50);
  fireEvent.keyDown(widget, { key: 'd', ctrlKey: true }); expect(document.querySelectorAll('.canvas-widget')).toHaveLength(2);
  fireEvent.keyDown(widget, { key: 'c', ctrlKey: true }); fireEvent.keyDown(widget, { key: 'v', ctrlKey: true }); expect(document.querySelectorAll('.canvas-widget')).toHaveLength(3);
  fireEvent.click(screen.getByText('Raise layer')); fireEvent.click(screen.getByText('Lower layer')); fireEvent.click(screen.getByText('Delete layer')); expect(document.querySelectorAll('.canvas-widget')).toHaveLength(2);
  fireEvent.click(screen.getByText('Undo', { exact: true })); expect(document.querySelectorAll('.canvas-widget')).toHaveLength(3);
  fireEvent.click(screen.getByText('Redo', { exact: true })); expect(document.querySelectorAll('.canvas-widget')).toHaveLength(2);
  fireEvent.change(screen.getByLabelText('Canvas width', { exact: true }), { target: { value: '1080' } }); fireEvent.change(screen.getByLabelText('Canvas height', { exact: true }), { target: { value: '1920' } });
  fireEvent.click(screen.getByText('Save now')); await waitFor(() => expect(screen.getByLabelText('Editor save status')).toHaveTextContent('saved')); expect(api.get().width).toBe(1080);
  fireEvent.change(screen.getByLabelText('Canvas preset'), { target: { value: 'custom' } }); fireEvent.change(screen.getByLabelText('Custom width'), { target: { value: '900' } }); fireEvent.change(screen.getByLabelText('Custom height'), { target: { value: '1600' } });
  fireEvent.change(screen.getByLabelText('New overlay ID'), { target: { value: 'custom' } }); fireEvent.click(screen.getByText('Create overlay'));
  await waitFor(() => expect(api.get().id).toBe('custom')); expect(api.get().width).toBe(900); expect(api.get().widgets).toEqual([]);
});
it('retains conflicting local changes until explicit reload and prevents switching unsaved documents', async () => {
  const api = host(); render(<VisualEditor />); await screen.findByLabelText('Overlay canvas'); api.conflict(true);
  fireEvent.change(screen.getByLabelText('Overlay name'), { target: { value: 'Local edits' } }); fireEvent.click(screen.getByText('Save now'));
  await screen.findByText(/Another editor changed this overlay/); expect(screen.getByLabelText('Overlay name')).toHaveValue('Local edits');
  fireEvent.change(screen.getByLabelText('Overlay', { exact: true }), { target: { value: 'main' } }); await screen.findByText('Resolve the unsaved changes before switching overlays.');
  fireEvent.click(screen.getByText('Reload saved version')); await waitFor(() => expect(screen.getByLabelText('Overlay name')).toHaveValue('Main'));
});
it('restores a revision as a new save, previews silently and sends native simulation without enabling actions', async () => {
  const api = host(); render(<VisualEditor />); await screen.findByLabelText('Overlay canvas');
  fireEvent.click(screen.getByText('Revision history', { exact: true })); fireEvent.click(await screen.findByText('Restore v1'));
  await screen.findByText('Restored revision 1 as a new revision.'); expect(api.get().name).toBe('Restored');
  fireEvent.click(screen.getByText('Preview', { exact: true })); const frame = await screen.findByTitle('Overlay test preview'); expect(frame).toHaveAttribute('src', '/overlay/main?preview=1');
  fireEvent.click(screen.getByLabelText('Enable preview audio')); expect(frame).toHaveAttribute('src', '/overlay/main?preview=1&audio=1');
  fireEvent.click(screen.getByText('Developer raw injection')); fireEvent.click(screen.getByLabelText('Use native Streamer.bot payload'));
  fireEvent.change(screen.getByLabelText('Raw JSON'), { target: { value: '{"event":{"source":"Twitch","type":"Follow"},"data":{}}' } });
  fireEvent.click(screen.getByText('Send isolated test event')); await screen.findByText(/Persisted: false. Live actions: false/);
  const sent = JSON.parse(String(api.fetcher.mock.calls.find(call => call[0].endsWith('/preview-events'))![1]!.body)); expect(sent.mode).toBe('native');
  fireEvent.change(screen.getByLabelText('Raw JSON'), { target: { value: '[]' } }); fireEvent.click(screen.getByText('Send isolated test event')); await screen.findByText(/Raw injection must be a JSON object/);
});
it('adds every initial widget type, exposes test controls and reports invalid creation', async () => {
  const api = host(); render(<VisualEditor />); await screen.findByLabelText('Overlay canvas');
  for (const name of ['Add image', 'Add video', 'Add audio', 'Add Combined Chat', 'Add AlertBox']) fireEvent.click(screen.getByText(name, { exact: true }));
  expect(document.querySelectorAll('.canvas-widget')).toHaveLength(6); fireEvent.click(screen.getByText('Save now')); await waitFor(() => expect(api.get().widgets).toHaveLength(6));
  fireEvent.change(screen.getByLabelText('New overlay ID'), { target: { value: '../invalid' } }); fireEvent.click(screen.getByText('Create overlay')); await screen.findByText(/Unable to create overlay/);
});

it('inherits group queue policies when adding or copying alert boxes after settings change', async () => {
  const api = host(); render(<VisualEditor />); await screen.findByLabelText('Overlay canvas');
  fireEvent.click(screen.getByText('Add AlertBox', { exact: true }));
  for (const [label, value] of [['Concurrency', '2'], ['Maximum queue length', '3'], ['Overflow policy', 'drop-newest']]) fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value } });
  fireEvent.click(screen.getByText('Add AlertBox', { exact: true })); fireEvent.click(screen.getByText('Duplicate', { exact: true })); fireEvent.click(screen.getByText('Save now'));
  await waitFor(() => expect(api.get().widgets.filter(w => w.kind === 'alert')).toHaveLength(3));
  for (const alert of api.get().widgets.filter(w => w.kind === 'alert')) expect(alert.alert).toMatchObject({ concurrency: 2, maximumQueueLength: 3, overflowPolicy: 'drop-newest' });
});

it('ignores a stale StrictMode initialization after the user selects and edits another overlay', async () => {
  const api = host();
  const first = structuredClone(api.get());
  const donor: Scene = { ...structuredClone(first), id: 'donor', name: 'Donor', widgets: [createWidget('donor-crown')] };
  let resolveInitial!: (scenes: Scene[]) => void;
  vi.spyOn(visualApi, 'list').mockImplementationOnce(() => new Promise(resolve => { resolveInitial = resolve; })).mockResolvedValue([first, donor]);
  vi.spyOn(visualApi, 'load').mockResolvedValue(donor);
  render(<StrictMode><VisualEditor /></StrictMode>);
  await screen.findByLabelText('Overlay canvas');
  fireEvent.change(screen.getByLabelText('Overlay', { exact: true }), { target: { value: 'donor' } });
  await waitFor(() => expect(screen.getByLabelText('Overlay name')).toHaveValue('Donor'));
  fireEvent.click(screen.getByRole('listitem').querySelector('button')!);
  expect(screen.getByLabelText('Minimum USD cents')).toHaveValue('0');
  await act(async () => { resolveInitial([first, donor]); });
  expect(screen.getByLabelText('Overlay name')).toHaveValue('Donor');
  expect(screen.getByLabelText('Minimum USD cents')).toHaveValue('0');
});

it('keeps the latest overlay selection when load responses arrive out of order', async () => {
  const api = host();
  const first = structuredClone(api.get());
  const second = { ...structuredClone(first), id: 'second', name: 'Second' };
  const third = { ...structuredClone(first), id: 'third', name: 'Third' };
  vi.spyOn(visualApi, 'list').mockResolvedValue([first, second, third]);
  let resolveSecond!: (scene: Scene) => void;
  const loader = vi.spyOn(visualApi, 'load').mockImplementation(id => id === 'second' ? new Promise(resolve => { resolveSecond = resolve; }) : Promise.resolve(third));
  render(<VisualEditor />);
  await screen.findByLabelText('Overlay canvas');
  fireEvent.change(screen.getByLabelText('Overlay', { exact: true }), { target: { value: 'second' } });
  await waitFor(() => expect(loader).toHaveBeenCalledWith('second'));
  fireEvent.change(screen.getByLabelText('Overlay', { exact: true }), { target: { value: 'third' } });
  await waitFor(() => expect(screen.getByLabelText('Overlay name')).toHaveValue('Third'));
  await act(async () => { resolveSecond(second); });
  expect(screen.getByLabelText('Overlay name')).toHaveValue('Third');
});

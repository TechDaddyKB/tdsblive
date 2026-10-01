import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ChatPanel } from './ChatPanel';
import { defaultSettings, type OverlayDefinition } from '../../overlay-runtime/src/chat';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const definition: OverlayDefinition = { id: 'combined-chat', name: 'Chat', width: 1920, height: 1080, background: 'transparent', version: 1, chat: defaultSettings };
it('rejects malformed server-supplied token identifiers before any mutation', async () => {
  const id = '../../rumble/disconnect?x=1';
  const fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'synthetic-csrf' });
    if (url.endsWith('/tokens')) return Response.json([{ id, revoked: false, expiresAt: '2026-11-01T00:00:00Z' }]);
    if (init?.method === 'DELETE') return new Response(null, { status: 404 });
    return Response.json(definition);
  }); vi.stubGlobal('fetch', fetcher); render(<ChatPanel />); await screen.findByLabelText('OBS overlay URL');
  fireEvent.click(screen.getByText('Manage viewing links')); fireEvent.click(await screen.findByText('Revoke viewing link')); await screen.findByText('Invalid viewing link identifier.');
  expect(fetcher.mock.calls.some(call => call[1]?.method === 'DELETE')).toBe(false);
});
function mockHost() {
  const fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return Response.json({ requestToken: 'synthetic-csrf' });
    if (url.endsWith('/tokens') && init?.method === 'POST') return Response.json({ token: 'synthetic-viewing-link' });
    if (url.endsWith('/tokens')) return Response.json([{ id: '10000000-0000-4000-8000-000000000001', revoked: false, expiresAt: '2026-11-01T00:00:00Z' }]);
    if (url.endsWith('/10000000-0000-4000-8000-000000000001')) return new Response(null, { status: 204 });
    if (url === '/api/assets') return Response.json(init?.method === 'POST' ? { filename: 'icon.svg' } : [{ id: 'font-id', filename: 'Licensed font', mime: 'font/woff2' }]);
    if (init?.method === 'PUT') return Response.json({ ...JSON.parse(String(init.body)), version: 2 });
    return Response.json(definition);
  }); vi.stubGlobal('fetch', fetcher); return fetcher;
}
it('saves complete appearance/filter changes with a version and exposes usable OBS/dock URLs', async () => {
  const fetcher = mockHost(); render(<ChatPanel />); await screen.findByLabelText('OBS overlay URL');
  expect(screen.getByLabelText('Streamer dock URL')).toHaveValue(`${location.origin}/chat/combined-chat`);
  fireEvent.click(screen.getByText('Chat appearance and filters'));
  for (const label of ['Platform icons', 'Avatars', 'Badges', 'Usernames', 'Message text', 'Timestamps', 'Keep messages visible', 'Newest messages on top', 'Hide bot messages', 'twitch']) fireEvent.click(screen.getByLabelText(label));
  for (const [label, value] of [['Message duration (seconds)', '45'], ['Maximum messages', '50'], ['Font family', 'Verdana'], ['Font size', '28'], ['Background opacity', '0.5'], ['Arrival animation', 'slide'], ['Departure animation', 'none'], ['rumble color', '#abcdef'], ['Ignored users (one per line)', 'quiet-viewer'], ['Ignored message prefixes (one per line)', '!'], ['Bot usernames (one per line)', 'MyBot']]) fireEvent.change(screen.getByLabelText(label), { target: { value } });
  fireEvent.click(screen.getByText('Save chat settings')); await screen.findByText('Chat settings saved. Open views update automatically.');
  const sent = JSON.parse(String(fetcher.mock.calls.find(call => call[1]?.method === 'PUT')![1]!.body)); expect(sent.version).toBe(1); expect(sent.chat).toMatchObject({ platforms: ['youtube', 'kick', 'rumble'], font: 'Verdana', persistent: true, ignoredUsers: ['quiet-viewer'], ignoredPrefixes: ['!'] });
  fireEvent.click(screen.getByText('Reload chat settings')); await waitFor(() => expect(screen.getByLabelText('Font family')).toHaveValue('Arial'));
});
it('creates/revokes private read-only links and uploads/selects a licensed font asset', async () => {
  const fetcher = mockHost(); render(<ChatPanel />); await screen.findByLabelText('OBS overlay URL');
  fireEvent.click(screen.getByText('Create private LAN viewing links')); await screen.findByText('Read-only link created. Keep it private; it expires in 30 days.');
  expect(screen.getByLabelText('Streamer dock URL')).toHaveValue(`${location.origin}/chat/combined-chat#token=synthetic-viewing-link`);
  fireEvent.click(screen.getByText('Hide private links')); expect(screen.getByLabelText('OBS overlay URL')).toHaveValue(`${location.origin}/overlay/combined-chat`);
  fireEvent.click(screen.getByText('Manage viewing links')); fireEvent.click(await screen.findByText('Revoke viewing link')); await screen.findByText('Viewing link revoked.');
  fireEvent.click(screen.getByText('Local asset library')); fireEvent.change(screen.getByLabelText('Font license declaration'), { target: { value: 'OFL-1.1' } });
  fireEvent.change(screen.getByLabelText('Upload asset'), { target: { files: [new File(['synthetic'], 'icon.svg', { type: 'image/svg+xml' })] } }); await screen.findByText('Asset ready: icon.svg');
  expect(fetcher.mock.calls.find(call => call[1]?.method === 'POST' && call[0] === '/api/assets')![1]!.headers).toMatchObject({ 'X-Asset-License': 'OFL-1.1', 'Content-Type': 'image/svg+xml' });
  fireEvent.click(screen.getByText('Refresh assets')); await screen.findByText('Licensed font (font/woff2)'); fireEvent.change(screen.getByLabelText('Custom font'), { target: { value: 'font-id' } });
  fireEvent.click(screen.getByText('Chat appearance and filters')); fireEvent.click(screen.getByText('Save chat settings')); await screen.findByText('Chat settings saved. Open views update automatically.');
  expect(JSON.parse(String(fetcher.mock.calls.find(call => call[1]?.method === 'PUT')![1]!.body)).chat.fontAssetId).toBe('font-id');
});
it('reports load failures and save conflicts without treating them as successful changes', async () => {
  const fetcher = mockHost(); fetcher.mockResolvedValueOnce(new Response(null, { status: 500 })); const view = render(<ChatPanel />); await screen.findByText('Unable to load chat settings.'); view.unmount();
  render(<ChatPanel />); await screen.findByLabelText('OBS overlay URL'); fireEvent.click(screen.getByText('Chat appearance and filters'));
  fetcher.mockImplementation(async url => url === '/api/auth/csrf' ? Response.json({ requestToken: 'synthetic' }) : new Response(null, { status: 409 }));
  fireEvent.click(screen.getByText('Save chat settings')); await screen.findByText('Settings changed elsewhere. Reload before saving.');
  fireEvent.click(screen.getByText('Create private LAN viewing links')); await screen.findByText('Unable to create link.');
  fireEvent.click(screen.getByText('Manage viewing links')); await screen.findByText('Unable to load viewing links.');
  fireEvent.click(screen.getByText('Local asset library')); fireEvent.click(screen.getByText('Refresh assets')); await screen.findByText('Unable to load assets.');
  fireEvent.change(screen.getByLabelText('Upload asset'), { target: { files: [] } });
  fireEvent.change(screen.getByLabelText('Upload asset'), { target: { files: [{ name: 'large', size: 21 * 1024 * 1024 }] } }); expect(screen.getByText('File exceeds 20 MiB.')).toBeVisible();
  fireEvent.change(screen.getByLabelText('Upload asset'), { target: { files: [new File(['invalid'], 'bad.svg', { type: 'image/svg+xml' })] } }); await screen.findByText('Upload rejected. Check MIME, size, SVG, and font license.');
});

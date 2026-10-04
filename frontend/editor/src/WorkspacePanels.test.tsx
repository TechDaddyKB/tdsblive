import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MediaLibrary } from './MediaLibrary';
import { DiagnosticsPanel } from './DiagnosticsPanel';
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('searches media by filename and previews supported types without autoplay audio', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 'a', filename: 'Owned logo.png', mime: 'image/png' },{ id: 'b', filename: 'Owned tone.wav', mime: 'audio/wav' },{ id: 'c', filename: 'Owned clip.webm', mime: 'video/webm' }] }));
  const view = render(<MediaLibrary />); await screen.findByRole('heading', { name: 'Owned logo.png' }); expect(view.container.querySelector('audio')).not.toHaveAttribute('autoplay');
  fireEvent.change(screen.getByLabelText('Search media'), { target: { value: 'tone' } }); expect(screen.queryByRole('heading', { name: 'Owned logo.png' })).not.toBeInTheDocument(); expect(screen.getByRole('heading', { name: 'Owned tone.wav' })).toBeVisible();
});
it('uploads media with its license, refreshes choices and reports failures', async () => {
  const fetcher = vi.fn().mockImplementation(async (url: string) => ({ ok: true, json: async () => url === '/api/auth/csrf' ? { requestToken: 'owned-csrf-placeholder' } : [] })); vi.stubGlobal('fetch', fetcher); const refreshed = vi.fn(); window.addEventListener('tdsblive:assets-changed', refreshed);
  try { render(<MediaLibrary />); await screen.findByText('Add your first image or sound to start designing alerts.'); fireEvent.change(screen.getByLabelText('Font license or permission'), { target: { value: 'Owned permission' } });
    fireEvent.change(screen.getByLabelText('Upload media'), { target: { files: [new File(['owned'], 'Owned.png', { type: 'image/png' })] } }); await screen.findByText('Media uploaded. Choose it in your widget settings.'); expect(refreshed).toHaveBeenCalledOnce(); expect(fetcher).toHaveBeenCalledWith('/api/assets', expect.objectContaining({ method: 'POST', headers: expect.objectContaining({ 'X-Asset-License': 'Owned permission' }) }));
    fetcher.mockRejectedValue(new Error('unavailable')); fireEvent.click(screen.getByRole('button', { name: 'Refresh media' })); await screen.findByText('Unable to load media. Try refreshing.');
  } finally { window.removeEventListener('tdsblive:assets-changed', refreshed); }
});
it('shows readable diagnostic status and retains technical evidence on demand', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ database: 'ready', eventCount: 10, pendingDeliveries: 0, sqliteLogFailures: 0, secretStorage: 'windowsDpapi' }) })); render(<DiagnosticsPanel />);
  await screen.findByText('Database ready'); expect(screen.getByText('Saved events')).toBeVisible(); fireEvent.click(screen.getByRole('button', { name: 'Refresh diagnostics' })); await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
  fireEvent.click(screen.getByText('Technical details and local logs')); expect(screen.getByText('Open full local diagnostics')).toBeVisible();
});
it('explains diagnostic loading failure without exposing private data', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('private-failure'))); render(<DiagnosticsPanel />); await screen.findByText('Unable to load diagnostics. Check whether TDSBLive is running.'); expect(screen.queryByText('private-failure')).not.toBeInTheDocument();
});

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { BotPermissions } from './BotPermissions';
import { api } from './api';

vi.mock('./api', () => ({ api: { configuration: vi.fn(), saveConfiguration: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('saves explicit action permission without overwriting newer connection settings', async () => {
  const id = crypto.randomUUID();
  vi.mocked(api.configuration).mockResolvedValueOnce({ streamerBot: { host: 'localhost', port: 8080, forwardLiveEvents: false } })
    .mockResolvedValueOnce({ streamerBot: { host: 'localhost', port: 8081, enabled: false }, speakerBot: { host: 'localhost', port: 7680 } });
  vi.mocked(api.saveConfiguration).mockResolvedValue({ restartRequired: true });
  render(<BotPermissions actions={[{ id, name: 'Owned test action', enabled: true }]} />);
  fireEvent.click(screen.getByText('Streamer.bot action permissions'));
  fireEvent.click(screen.getByRole('button', { name: 'Load action permissions' }));
  fireEvent.click(await screen.findByLabelText('Allow action Owned test action'));
  fireEvent.click(screen.getByLabelText('Allow qualified live event forwarding to Streamer.bot'));
  fireEvent.click(screen.getByRole('button', { name: 'Save action permissions' }));
  await waitFor(() => expect(api.saveConfiguration).toHaveBeenCalledWith(expect.objectContaining({
    streamerBot: expect.objectContaining({ port: 8081, enabled: false, allowedActionIds: [id], forwardLiveEvents: true }),
    speakerBot: { host: 'localhost', port: 7680 },
  })));
  expect(await screen.findByText(/Permissions saved. Restart TDSBLive/)).toBeVisible();
});

it('allows removing an unavailable permission and prevents selecting a disabled action', async () => {
  const missing = crypto.randomUUID();
  const disabled = crypto.randomUUID();
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: 'localhost', port: 8080, allowedActionIds: [missing] } });
  render(<BotPermissions actions={[{ id: disabled, name: 'Disabled action', enabled: false }]} />);
  fireEvent.click(screen.getByText('Streamer.bot action permissions'));
  fireEvent.click(screen.getByRole('button', { name: 'Load action permissions' }));
  expect(await screen.findByLabelText('Allow action Disabled action (disabled in Streamer.bot)')).toBeDisabled();
  fireEvent.click(screen.getByRole('button', { name: 'Remove unavailable permission' }));
  expect(screen.queryByText(/Previously allowed action/)).not.toBeInTheDocument();
  expect(api.saveConfiguration).not.toHaveBeenCalled();
});

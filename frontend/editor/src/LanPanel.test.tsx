import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { LanPanel } from './LanPanel';
import { api, write } from './api';

vi.mock('./api', () => ({ api: { configuration: vi.fn(), saveConfiguration: vi.fn() }, write: vi.fn() }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('saves explicit LAN hosts while preserving integration settings', async () => {
  vi.mocked(api.configuration).mockImplementation(async () => ({ server: { host: '127.0.0.1', port: 17474, enableLan: false, allowedHosts: [] }, streamerBot: { host: '127.0.0.1', port: 8080, enabled: false } }));
  vi.mocked(api.saveConfiguration).mockResolvedValue({ restartRequired: true });
  render(<LanPanel />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Save access settings' })).toBeEnabled());
  fireEvent.click(screen.getByLabelText('Enable authenticated LAN access'));
  fireEvent.change(screen.getByLabelText('Streaming computer addresses (one per line)'), { target: { value: '192.0.2.20\nstream-pc' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save access settings' }));
  await waitFor(() => expect(api.saveConfiguration).toHaveBeenCalledWith(expect.objectContaining({
    server: expect.objectContaining({ host: '0.0.0.0', enableLan: true, allowedHosts: ['192.0.2.20', 'stream-pc'] }),
    streamerBot: { host: '127.0.0.1', port: 8080, enabled: false },
  })));
});

it('keeps provisioned credentials masked and removes them when hidden', async () => {
  vi.mocked(api.configuration).mockResolvedValue({ server: { host: '127.0.0.1', port: 17474 } });
  const credential = crypto.randomUUID();
  vi.mocked(write).mockResolvedValue({ credential });
  render(<LanPanel />);
  const provision = screen.getByRole('button', { name: 'Create or rotate LAN access credential' });
  await waitFor(() => expect(provision).toBeEnabled());
  fireEvent.click(provision);
  const field = await screen.findByLabelText('New LAN access credential');
  expect(field).toHaveAttribute('type', 'password');
  expect(field).toHaveAttribute('readonly');
  expect(field).toHaveValue(credential);
  expect(api.saveConfiguration).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: 'Hide credential' }));
  expect(screen.queryByLabelText('New LAN access credential')).not.toBeInTheDocument();
});

it('explains failed provisioning and never displays a credential', async () => {
  vi.mocked(api.configuration).mockResolvedValue({ server: { host: '127.0.0.1', port: 17474 } });
  vi.mocked(write).mockRejectedValue(new Error('Unavailable platform'));
  render(<LanPanel />);
  const provision = screen.getByRole('button', { name: 'Create or rotate LAN access credential' });
  await waitFor(() => expect(provision).toBeEnabled());
  fireEvent.click(provision);
  expect(await screen.findByText(/Credential provisioning requires the Windows application/)).toBeVisible();
  expect(screen.queryByLabelText('New LAN access credential')).not.toBeInTheDocument();
});

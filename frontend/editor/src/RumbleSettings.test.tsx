import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { RumbleSettings } from './RumbleSettings';
import { api } from './api';
vi.mock('./api', () => ({ api: { configuration: vi.fn(), saveConfiguration: vi.fn() } }));
beforeEach(() => {
  vi.mocked(api.configuration).mockResolvedValue({ displayName: 'Preserved', rumble: { pollIntervalSeconds: 7 } });
  vi.mocked(api.saveConfiguration).mockResolvedValue({ restartRequired: true });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });
it('edits bounded polling settings while preserving other configuration', async () => {
  render(<RumbleSettings />); fireEvent.click(screen.getByText('Rumble polling settings')); fireEvent.click(screen.getByText('Load polling settings'));
  const interval = await screen.findByLabelText('Polling interval (seconds)'); expect(interval).toHaveAttribute('min', '5'); expect(interval).toHaveAttribute('max', '10');
  fireEvent.click(screen.getByLabelText('Enable Rumble on host startup'));
  fireEvent.click(screen.getByLabelText('Allow advanced slower polling')); expect(interval).toHaveAttribute('max', '86400');
  fireEvent.change(interval, { target: { value: '60' } });
  fireEvent.click(screen.getByLabelText('Allow advanced slower polling')); expect(interval).toHaveValue(10);
  fireEvent.change(screen.getByLabelText('Request timeout (seconds)'), { target: { value: '20' } });
  fireEvent.change(screen.getByLabelText('Successful offline confirmations'), { target: { value: '3' } });
  fireEvent.click(screen.getByLabelText('Forward qualified Rumble events to Streamer.bot'));
  fireEvent.click(screen.getByText('Save polling settings'));
  await waitFor(() => expect(api.saveConfiguration).toHaveBeenCalledWith(expect.objectContaining({ displayName: 'Preserved', rumble: expect.objectContaining({ enabled: true, pollIntervalSeconds: 10, requestTimeoutSeconds: 20, offlineConfirmationPolls: 3, forwardTriggers: true }) })));
  expect(await screen.findByText(/Restart TDSBLive to apply them/)).toBeVisible();
});
it('handles configuration errors without echoing private values', async () => {
  vi.mocked(api.configuration).mockRejectedValueOnce(new Error('private response'));
  render(<RumbleSettings />); fireEvent.click(screen.getByText('Rumble polling settings')); fireEvent.click(screen.getByText('Load polling settings'));
  expect(await screen.findByText('Unable to load polling settings.')).toBeVisible();
  fireEvent.click(screen.getByText('Load polling settings')); await screen.findByLabelText('Polling interval (seconds)');
  vi.mocked(api.saveConfiguration).mockRejectedValueOnce(new Error('private response'));
  fireEvent.click(screen.getByText('Save polling settings')); expect(await screen.findByText(/Unable to save polling settings/)).toBeVisible();
  expect(screen.queryByText('private response')).not.toBeInTheDocument();
});

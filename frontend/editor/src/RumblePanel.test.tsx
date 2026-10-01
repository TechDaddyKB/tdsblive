import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { RumblePanel } from './RumblePanel';
import { rumble } from './api';
vi.mock('./api', () => ({ rumble: { status: vi.fn(), connect: vi.fn(), resetBaseline: vi.fn(), disconnect: vi.fn() } }));
beforeEach(() => {
  vi.mocked(rumble.status).mockResolvedValue({ state: 'healthy', enabled: true, credentialPresent: true, baselineEstablished: true,
    pollSequence: 2, consecutiveFailures: 0, forwardTriggers: false, liveStreams: [], lastPollAt: null, nextPollAt: null, subscriptionsLiveVerified: false, giftsAuthoritative: false });
  vi.mocked(rumble.connect).mockResolvedValue(undefined); vi.mocked(rumble.resetBaseline).mockResolvedValue(undefined); vi.mocked(rumble.disconnect).mockResolvedValue(undefined);
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });
it('connects with session-only defaults and clears the credential after submission', async () => {
  render(<RumblePanel />); await screen.findByText(/Baseline established/);
  const input = screen.getByLabelText('Live API URL');
  expect(input).toHaveAttribute('type', 'password');
  fireEvent.change(input, { target: { value: 'synthetic-credential' } });
  fireEvent.click(screen.getByText('Connect Rumble'));
  await waitFor(() => expect(rumble.connect).toHaveBeenCalledWith('synthetic-credential', true));
  await waitFor(() => expect(input).toHaveValue(''));
  fireEvent.click(screen.getByText('Reset Rumble baseline')); await waitFor(() => expect(rumble.resetBaseline).toHaveBeenCalled());
  fireEvent.click(screen.getByText('Disconnect Rumble')); await waitFor(() => expect(rumble.disconnect).toHaveBeenCalled());
});
it('allows persistent storage selection and sanitizes connection failures', async () => {
  vi.mocked(rumble.connect).mockRejectedValue(new Error('private remote body'));
  render(<RumblePanel />); await screen.findByText(/Baseline established/);
  fireEvent.click(screen.getByLabelText('Keep credential only for this session'));
  fireEvent.change(screen.getByLabelText('Live API URL'), { target: { value: 'synthetic-credential' } });
  fireEvent.click(screen.getByText('Connect Rumble'));
  expect(await screen.findByRole('alert')).toHaveTextContent('Rumble operation failed');
  expect(rumble.connect).toHaveBeenCalledWith('synthetic-credential', false);
  expect(screen.getByLabelText('Live API URL')).toHaveValue(''); expect(screen.queryByText('private remote body')).not.toBeInTheDocument();
});
it('reports status failures without exposing the response', async () => {
  vi.mocked(rumble.status).mockRejectedValue(new Error('private response'));
  render(<RumblePanel />); expect(await screen.findByRole('alert')).toHaveTextContent('Unable to read Rumble status.');
});

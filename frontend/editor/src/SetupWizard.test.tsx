import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SetupWizard } from './SetupWizard';
import { api, write } from './api';

vi.mock('./BotPanel', () => ({ BotPanel: () => <div>Bot status panel</div> }));
vi.mock('./RumblePanel', () => ({ RumblePanel: () => <div>Rumble settings panel</div> }));
vi.mock('./FinancialPanel', () => ({ FinancialPanel: () => <div>Supporter settings panel</div> }));
vi.mock('./ChatPanel', () => ({ ChatPanel: () => <div>Chat settings panel</div> }));
vi.mock('./api', () => ({ api: { configuration: vi.fn(), saveConfiguration: vi.fn() },
  request: vi.fn(async () => ({ step: 0, reviewed: true, version: 0 })),
  write: vi.fn(async (_path, _method, progress) => ({ ...progress, version: progress.version + 1 })) }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('walks through all six steps without changing settings automatically', async () => {
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: 'localhost', port: 8081 }, speakerBot: { host: 'localhost', port: 7681 } });
  render(<SetupWizard />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Open guided setup' })).toBeEnabled());
  fireEvent.click(screen.getByRole('button', { name: 'Open guided setup' }));
  expect(screen.getByRole('status')).toHaveTextContent('Step 1 of 6');
  expect(screen.getByRole('button', { name: 'Previous setup step' })).toBeDisabled();
  for (let step = 2; step <= 6; step++) {
    fireEvent.click(screen.getByRole('button', { name: 'Next setup step' }));
    await waitFor(() => expect(screen.getAllByRole('status')[0]).toHaveTextContent(`Step ${step} of 6`));
  }
  fireEvent.click(screen.getByRole('button', { name: 'Finish setup review' }));
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Finish setup review' })).not.toBeInTheDocument());
  expect(api.saveConfiguration).not.toHaveBeenCalled();
});

it('defaults authentication to session-only and clears the password after saving', async () => {
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: 'localhost', port: 8080 }, speakerBot: { host: 'localhost', port: 7680 } });
  render(<SetupWizard />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Open guided setup' })).toBeEnabled());
  fireEvent.click(screen.getByRole('button', { name: 'Open guided setup' }));
  fireEvent.click(screen.getByRole('button', { name: 'Next setup step' }));
  const field = await screen.findByLabelText('Streamer.bot password');
  const value = crypto.randomUUID();
  expect(field).toHaveAttribute('type', 'password');
  expect(screen.getByLabelText('Keep Streamer.bot password only for this session')).toBeChecked();
  fireEvent.change(field, { target: { value } });
  fireEvent.click(screen.getByRole('button', { name: 'Save Streamer.bot password' }));
  await waitFor(() => expect(write).toHaveBeenCalledWith('/api/integrations/streamerbot/credential', 'POST', { value, sessionOnly: true }));
  await waitFor(() => expect(field).toHaveValue(''));
  expect(screen.getByText(/Password saved for this host session/)).toBeVisible();
});

it('loads saved addresses and preserves unrelated settings when saving one connection', async () => {
  vi.mocked(api.configuration).mockImplementation(async () => ({ displayName: 'Owned example', streamerBot: { host: 'localhost', port: 8081, enabled: false, forwardLiveEvents: false }, speakerBot: { host: 'localhost', port: 7681, enabled: false } }));
  vi.mocked(api.saveConfiguration).mockResolvedValue({ restartRequired: true });
  render(<SetupWizard />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Open guided setup' })).toBeEnabled());
  fireEvent.click(screen.getByRole('button', { name: 'Open guided setup' }));
  fireEvent.click(screen.getByRole('button', { name: 'Next setup step' }));
  await waitFor(() => expect(screen.getByLabelText('Streamer.bot port')).toHaveValue(8081));
  fireEvent.change(screen.getByLabelText('Streamer.bot port'), { target: { value: '8082' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save Streamer.bot connection' }));
  await waitFor(() => expect(api.saveConfiguration).toHaveBeenCalledWith(expect.objectContaining({
    displayName: 'Owned example', streamerBot: expect.objectContaining({ port: 8082, enabled: true, forwardLiveEvents: false }),
    speakerBot: expect.objectContaining({ port: 7681, enabled: false }),
  })));
  expect(await screen.findByText(/Streamer.bot connection saved/)).toBeVisible();
});

it.each([
  ['streamerBot', 'Streamer.bot', { state: 'connected' }, 'answered the read-only connection test'],
  ['speakerBot', 'Speaker.bot', { state: 'connected' }, 'answered the read-only connection test'],
  ['speakerBot', 'Speaker.bot', { state: 'probeFailed', failureKind: 'rejected' }, 'does not support the read-only test request'],
  ['streamerBot', 'Streamer.bot', { state: 'disabled' }, 'did not pass the connection test'],
] as const)('tests %s without saving configuration or running automation', async (bot, label, response, notice) => {
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: 'localhost', port: 8080 }, speakerBot: { host: 'localhost', port: 7680 } });
  vi.mocked(write).mockImplementation(async (path, _method, progress) => path.endsWith('/test') ? response : { ...progress as object, step: 1, version: 1 });
  render(<SetupWizard />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Open guided setup' })).toBeEnabled());
  fireEvent.click(screen.getByRole('button', { name: 'Open guided setup' }));
  fireEvent.click(screen.getByRole('button', { name: 'Next setup step' }));
  const button = await screen.findByRole('button', { name: `Test ${label} connection` });
  await waitFor(() => expect(button).toBeEnabled()); fireEvent.click(button);
  expect(await screen.findByText(new RegExp(notice))).toBeVisible();
  expect(write).toHaveBeenCalledWith(`/api/integrations/${bot === 'streamerBot' ? 'streamerbot' : 'speakerbot'}/test`, 'POST');
  expect(api.saveConfiguration).not.toHaveBeenCalled();
});

it('reports connection test failures without exposing remote bodies', async () => {
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: 'localhost', port: 8080 }, speakerBot: { host: 'localhost', port: 7680 } });
  vi.mocked(write).mockImplementation(async (path) => {
    if (path.endsWith('/test')) throw new Error('private server body');
    return { step: 1, version: 1 };
  });
  render(<SetupWizard />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Open guided setup' })).toBeEnabled());
  fireEvent.click(screen.getByRole('button', { name: 'Open guided setup' }));
  fireEvent.click(screen.getByRole('button', { name: 'Next setup step' }));
  const button = await screen.findByRole('button', { name: 'Test Speaker.bot connection' });
  await waitFor(() => expect(button).toBeEnabled()); fireEvent.click(button);
  expect(await screen.findByText('Unable to test this connection. Check the connection status and try again.')).toBeVisible();
  expect(screen.queryByText('private server body')).not.toBeInTheDocument();
});

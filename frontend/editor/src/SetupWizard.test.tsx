import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SetupWizard } from './SetupWizard';
import { api } from './api';

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

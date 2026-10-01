import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { BotPanel } from './BotPanel';
import { api, bots } from './api';
vi.mock('./api', () => ({ api: { configuration: vi.fn(), saveConfiguration: vi.fn() }, bots: { overview: vi.fn(), discovery: vi.fn(), inspector: vi.fn(), fixture: vi.fn(), replay: vi.fn() } }));
beforeEach(() => {
  vi.mocked(bots.overview).mockResolvedValue({ streamerBot: { state: 'connected', version: 'test' }, speakerBot: { state: 'disabled' } });
  vi.mocked(bots.discovery).mockResolvedValue({ actions: [{ id: 'action-id', name: 'test action', enabled: false }], codeTriggers: [{ name: 'Probe', eventName: 'tdsblive.test' }], events: {} });
  vi.mocked(bots.inspector).mockResolvedValue([{ id: 'test-id', classification: 'normalized', event: { nativeType: 'Twitch.ChatMessage', provenance: 'simulation' } }]);
  vi.mocked(bots.fixture).mockResolvedValue({ synthetic: true });
  vi.mocked(bots.replay).mockResolvedValue({ liveActionsAllowed: false });
  vi.mocked(api.configuration).mockResolvedValue({ streamerBot: { host: '127.0.0.1', port: 8080, enabled: false } });
  vi.mocked(api.saveConfiguration).mockResolvedValue({ restartRequired: true });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); vi.unstubAllGlobals(); });
it('shows live state, discovers actions, searches, pauses and resumes', async () => {
  render(<BotPanel />);
  expect(await screen.findByText(/test action/)).toBeInTheDocument();
  expect(screen.getByLabelText('Streamer.bot connection status')).toHaveTextContent('Connected');
  expect(screen.getByText(/tdsblive.test/)).toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Search events'), { target: { value: 'chat' } });
  await waitFor(() => expect(bots.inspector).toHaveBeenCalledWith('chat'));
  fireEvent.click(screen.getByText('Pause inspector')); expect(screen.getByText('Resume inspector')).toBeVisible();
  fireEvent.click(screen.getByText('Resume inspector'));
  fireEvent.click(screen.getByText('Toggle Streamer.bot connection'));
  expect(await screen.findByText(/Restart TDSBLive/)).toBeVisible();
  expect(api.saveConfiguration).toHaveBeenCalledWith(expect.objectContaining({ streamerBot: expect.objectContaining({ enabled: true }) }));
});
it('inspects samples and replays without live automation', async () => {
  render(<BotPanel />); fireEvent.click(await screen.findByText('Inspect sample'));
  expect(await screen.findByText(/"synthetic"/)).toBeVisible();
  fireEvent.click(screen.getByText('Replay locally'));
  expect(await screen.findByText(/without persistence or live automation/)).toBeVisible();
  expect(bots.replay).toHaveBeenCalledWith('test-id');
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: vi.fn().mockResolvedValue(undefined) } });
  fireEvent.click(screen.getByText('Copy sample')); expect(await screen.findByText(/Sample copied/)).toBeVisible();
});
it('downloads a private fixture and supports copying on HTTP', async () => {
  vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:test'), revokeObjectURL: vi.fn() });
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: undefined });
  Object.defineProperty(document, 'execCommand', { configurable: true, value: vi.fn(() => true) });
  render(<BotPanel />); fireEvent.click(await screen.findByText('Save private fixture'));
  expect(await screen.findByText(/Private fixture saved/)).toBeVisible(); expect(click).toHaveBeenCalled(); click.mockRestore();
  fireEvent.click(screen.getByText('Copy sample')); expect(await screen.findByText(/Sample copied/)).toBeVisible();
});
it('reports errors without exposing remote response bodies', async () => {
  vi.mocked(bots.overview).mockRejectedValue(new Error('private body'));
  render(<BotPanel />); expect(await screen.findByText('Unable to refresh bot connections.')).toBeVisible();
  vi.mocked(api.configuration).mockRejectedValue(new Error('private body'));
  fireEvent.click(screen.getByText('Toggle Speaker.bot connection'));
  expect(await screen.findByText('Unable to save connection settings.')).toBeVisible();
  expect(screen.queryByText('private body')).not.toBeInTheDocument();
});
it('handles sample, replay and clipboard failures', async () => {
  vi.mocked(bots.fixture).mockRejectedValueOnce(new Error());
  vi.mocked(bots.replay).mockRejectedValueOnce(new Error());
  render(<BotPanel />); fireEvent.click(await screen.findByText('Inspect sample'));
  expect(await screen.findByText('Unable to load this sample.')).toBeVisible();
  fireEvent.click(screen.getByText('Replay locally')); expect(await screen.findByText('Unable to replay this event.')).toBeVisible();
  fireEvent.click(screen.getByText('Inspect sample')); await screen.findByText(/"synthetic"/);
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: vi.fn().mockRejectedValue(new Error()) } });
  fireEvent.click(screen.getByText('Copy sample')); expect(await screen.findByText(/Copy unavailable/)).toBeVisible();
});

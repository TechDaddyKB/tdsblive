import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { AutomationPanel } from './AutomationPanel';
const mocks = vi.hoisted(() => ({ rules: vi.fn(), executions: vi.fn(), save: vi.fn(), remove: vi.fn(), moderate: vi.fn(), simulate: vi.fn(), language: vi.fn(), capabilities: vi.fn(), temporaryEffects: vi.fn(), resolveRestored: vi.fn() }));
vi.mock('./automationApi', () => ({ automation: mocks }));
vi.mock('./api', () => ({ bots: { discovery: vi.fn().mockResolvedValue({ actions: [], events: {}, codeTriggers: [] }) } }));
beforeEach(() => { vi.clearAllMocks(); mocks.rules.mockResolvedValue([]); mocks.executions.mockResolvedValue([]); mocks.temporaryEffects.mockResolvedValue([]); mocks.capabilities.mockResolvedValue({ streamerBotState: 'disabled', speakerBotState: 'disabled', issues: [] }); mocks.save.mockImplementation(async value => ({ ...value, version: 1 })); });
afterEach(cleanup);

it('requires external restoration confirmation before resolving an uncertain temporary effect', async () => {
  const effect = { actionId: 'owned-effect', version: 3, state: 'uncertain' };
  mocks.temporaryEffects.mockResolvedValue([effect]); mocks.resolveRestored.mockResolvedValue(undefined);
  render(<AutomationPanel />);
  const button = await screen.findByRole('button', { name: 'Resolve restored effect owned-effect' });
  expect(button).toBeDisabled();
  expect(mocks.resolveRestored).not.toHaveBeenCalled();
  fireEvent.click(screen.getByLabelText('I inspected and restored the external state for owned-effect'));
  fireEvent.click(button);
  await waitFor(() => expect(mocks.resolveRestored).toHaveBeenCalledWith(effect));
});

it('shows prepared speech and requires an explicit allowed language selection before resolving review', async () => {
  const receipt = { id: 'owned-review', state: 'language-review', version: 2, queueGroup: 'tts',
    json: JSON.stringify({ speechText: 'Owned sanitized speech', action: { speech: { allowedLanguages: ['en'] } } }) };
  mocks.executions.mockResolvedValue([receipt]);
  mocks.language.mockResolvedValue(undefined);
  render(<AutomationPanel />);
  await waitFor(() => expect(screen.getByLabelText('Prepared speech')).toHaveTextContent('Owned sanitized speech'));
  expect(screen.queryByRole('button', { name: 'Approve owned-review' })).not.toBeInTheDocument();
  const verify = screen.getByRole('button', { name: 'Verify language owned-review' });
  expect(verify).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Verified message language'), { target: { value: 'en' } });
  fireEvent.click(verify);
  await waitFor(() => expect(mocks.language).toHaveBeenCalledWith(receipt, 'en'));
  expect(mocks.moderate).not.toHaveBeenCalled();
});

it('simulates a disabled draft without saving it or enabling live automation', async () => {
  mocks.simulate.mockResolvedValue({ actions: [], persisted: false, liveActionsAllowed: false });
  render(<AutomationPanel />);
  fireEvent.click(screen.getByRole('button', { name: 'New speech rule' }));
  fireEvent.change(screen.getByLabelText('Voice alias'), { target: { value: 'Owned voice' } });
  fireEvent.click(screen.getByRole('button', { name: 'Simulate selected rule' }));
  await waitFor(() => expect(mocks.simulate).toHaveBeenCalled());
  expect(mocks.save).not.toHaveBeenCalled();
  expect(mocks.simulate.mock.calls[0][0].rule.enabled).toBe(false);
  expect(mocks.simulate.mock.calls[0][0].event.source).toBe('automation-preview');
  expect(screen.getByLabelText('Enable live automation')).not.toBeChecked();
  await waitFor(() => expect(screen.getByLabelText('Simulation result')).toHaveTextContent('liveActionsAllowed'));
});

it('keeps a new rule disabled and preserves native integer precision on save', async () => {
  render(<AutomationPanel />);
  await waitFor(() => expect(mocks.rules).toHaveBeenCalled());
  fireEvent.click(screen.getByRole('button', { name: 'New speech rule' }));
  expect(screen.getByLabelText('Enable live automation')).not.toBeChecked();
  fireEvent.change(screen.getByLabelText('Voice alias'), { target: { value: 'Owned voice' } });
  fireEvent.click(screen.getByLabelText('Require manual moderation'));
  fireEvent.change(screen.getByLabelText('Maximum characters'), { target: { value: '120' } });
  fireEvent.change(screen.getByLabelText('Blocked words (one per line)'), { target: { value: 'owned-block\nsecond-word' } });
  fireEvent.change(screen.getByLabelText('Native amount in minor units'), { target: { value: '9223372036854775807' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save automation rule' }));
  await waitFor(() => expect(mocks.save).toHaveBeenCalled());
  const saved = mocks.save.mock.calls[0][0];
  expect(saved.enabled).toBe(false);
  expect(saved.condition.value).toBe('9223372036854775807');
  expect(saved.condition.currency).toBe('USD');
  expect(saved.actions[0].speech.voice).toBe('Owned voice');
  expect(saved.actions[0].speech.manualModeration).toBe(true);
  expect(saved.actions[0].speech.maximumCharacters).toBe('120');
  expect(saved.actions[0].speech.blockedWords).toEqual(['owned-block', 'second-word']);
  expect(screen.getByRole('button', { name: 'Delete automation rule' })).toBeInTheDocument();
});

it('does not clear an edited rule when initial loading completes', async () => {
  render(<AutomationPanel />);
  fireEvent.click(screen.getByRole('button', { name: 'New sound rule' }));
  fireEvent.change(screen.getByLabelText('Rule name'), { target: { value: 'Owned sound setup' } });
  await waitFor(() => expect(mocks.executions).toHaveBeenCalled());
  expect(screen.getByLabelText('Rule name')).toHaveValue('Owned sound setup');
});

import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { CombinedChat } from './CombinedChat';
import { defaultSettings, type ChatEvent, type OverlayDefinition } from './chat';

const callbacks = vi.hoisted(() => ({ settings: (value: unknown) => { void value; }, events: (value: unknown) => { void value; }, status: (value: string) => { void value; } }));
vi.mock('./ChatConnection', () => ({ ChatConnection: class {
  constructor(_id: string, _token: string, _preview: boolean, settings: typeof callbacks.settings, events: typeof callbacks.events, status: typeof callbacks.status) { Object.assign(callbacks, { settings, events, status }); }
  start() { callbacks.settings({ id: 'combined-chat', name: 'Streamer Chat', chat: defaultSettings }); callbacks.status('Connected'); }
  stop() {}
} }));
afterEach(() => { cleanup(); localStorage.clear(); vi.useRealTimers(); });
it('renders badge artwork and falls back safely when missing, unsafe or unavailable', () => {
  render(<CombinedChat id="combined-chat" />);
  act(() => callbacks.events([{ ...message, user: { badgeDetails: [
    { name: 'moderator', imageUrl: 'https://example.invalid/mod.png' },
    { name: 'subscriber', imageUrl: 'javascript:alert(1)' }, { name: 'vip' },
  ] } }]));
  const image = screen.getByRole('img', { name: 'moderator' });
  expect(image).toHaveAttribute('src', 'https://example.invalid/mod.png');
  expect(image).toHaveAttribute('referrerpolicy', 'no-referrer');
  expect(screen.queryByText('moderator')).toBeNull(); expect(screen.getByText('subscriber')).toBeVisible(); expect(screen.getByText('vip')).toBeVisible();
  fireEvent.error(image); expect(screen.getByText('moderator')).toBeVisible();
});
const message: ChatEvent = { id: 'synthetic-chat', type: 'chat.message', platform: 'rumble', receivedAt: new Date().toISOString(), occurredAt: new Date().toISOString(), provenance: 'live', user: { displayName: 'Viewer', badges: ['moderator'], avatarUrl: 'https://example.invalid/avatar.png' }, message: { text: '<img onerror=alert(1)>hello' } };
it('renders duplicate snapshots once, escapes text, shows metadata and obeys display settings', () => {
  render(<CombinedChat id="combined-chat" />);
  act(() => callbacks.events([message, message])); expect(screen.getAllByText(message.message!.text!)).toHaveLength(1); expect(screen.getByText('moderator')).toBeVisible(); expect(screen.getByLabelText('rumble')).toBeVisible();
  expect(document.querySelectorAll('img')).toHaveLength(1); fireEvent.error(document.querySelector('img')!); expect(document.querySelector('img')).not.toBeVisible();
  act(() => callbacks.settings({ id: 'combined-chat', chat: { ...defaultSettings, showPlatformIcon: false, showAvatar: false, showBadges: false, showUsername: false, showMessage: false, showTimestamp: true } } as OverlayDefinition));
  expect(screen.queryByText('moderator')).toBeNull(); expect(screen.queryByText(message.message!.text!)).toBeNull(); expect(document.querySelector('time')).not.toBeNull();
});
it('provides a responsive streamer view with a stored light/dark toggle and persistent messages', () => {
  const view = render(<CombinedChat id="combined-chat" streamer />); expect(screen.getByRole('heading', { name: 'Streamer Chat' })).toBeVisible();
  act(() => callbacks.events([{ ...message, receivedAt: new Date(0).toISOString() }])); expect(screen.getByText(message.message!.text!)).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Switch to light mode' })); expect(document.querySelector('[data-theme=light]')).not.toBeNull(); expect(localStorage.getItem('tdsblive.chat.theme')).toBe('light');
  view.unmount(); render(<CombinedChat id="combined-chat" streamer />); fireEvent.click(screen.getByRole('button', { name: 'Switch to dark mode' })); expect(localStorage.getItem('tdsblive.chat.theme')).toBe('dark');
});
it('expires overlay messages, labels test preview and exposes a sign-in recovery link', async () => {
  vi.useFakeTimers(); vi.setSystemTime(new Date(message.receivedAt)); render(<CombinedChat id="combined-chat" preview />);
  act(() => callbacks.settings({ chat: { ...defaultSettings, messageDurationSeconds: 1, animationOut: 'slide' } })); act(() => callbacks.events([message]));
  await act(async () => vi.advanceTimersByTimeAsync(1000)); expect(document.querySelector('.exit-slide')).not.toBeNull();
  await act(async () => vi.advanceTimersByTimeAsync(500)); expect(screen.queryByText(message.message!.text!)).toBeNull();
  expect(screen.getByText('Test preview · includes simulation/replay')).toBeVisible(); act(() => callbacks.status('Sign in or use an overlay token')); expect(screen.getByRole('link', { name: 'Sign in to this host' })).toHaveAttribute('href', '/login');
});

import { expect, it } from 'vitest';
import { ChatBuffer, accepts, defaultSettings, safeAvatar, type ChatEvent } from './chat';

const event = (id: string, changes: Partial<ChatEvent> = {}): ChatEvent => ({ id, type: 'chat.message', platform: 'rumble', occurredAt: new Date(0).toISOString(), receivedAt: new Date(0).toISOString(), provenance: 'live', user: { login: 'Viewer', badges: ['moderator'] }, message: { text: 'hello' }, ...changes });
it('deduplicates repeated delivery/history, bounds the visible list and orders both directions', () => {
  const buffer = new ChatBuffer(); const settings = { ...defaultSettings, persistent: true, maximumMessages: 2 };
  expect(buffer.ingest([event('a'), event('a'), event('b')], settings)).toHaveLength(2);
  expect(buffer.ingest([event('b'), event('c')], settings).map(e => e.id)).toEqual(['b', 'c']);
  expect(buffer.visible({ ...settings, newestOnTop: true }).map(e => e.id)).toEqual(['c', 'b']);
  expect(buffer.ingest([event('a')], settings).map(e => e.id)).toEqual(['b', 'c']);
});
it('expires timed messages after their exit animation but retains persistent messages', () => {
  const buffer = new ChatBuffer(); const settings = { ...defaultSettings, messageDurationSeconds: 1 };
  expect(buffer.ingest([event('a')], settings, 0)).toHaveLength(1);
  expect(buffer.visible(settings, 1100)).toHaveLength(1);
  expect(buffer.visible(settings, 1300)).toHaveLength(0);
  expect(new ChatBuffer().ingest([event('b')], { ...settings, persistent: true }, 86400000)).toHaveLength(1);
});
it('filters platforms, identities, exact prefixes and bots without interpreting HTML', () => {
  expect(accepts(event('a'), { ...defaultSettings, platforms: ['kick'] })).toBe(false);
  expect(accepts(event('a'), { ...defaultSettings, ignoredUsers: ['viewer'] })).toBe(false);
  expect(accepts(event('a', { message: { text: '!command' } }), { ...defaultSettings, ignoredPrefixes: ['!'] })).toBe(false);
  expect(accepts(event('a', { user: { login: 'NightBot' } }), { ...defaultSettings, hideBotMessages: true })).toBe(false);
  expect(accepts(event('a', { user: { isBot: true } }), { ...defaultSettings, hideBotMessages: true })).toBe(false);
  expect(accepts(event('a', { type: 'support.rant' }), defaultSettings)).toBe(false);
  expect(accepts(event('a', { message: null }), defaultSettings)).toBe(false);
  expect(accepts(event('a', { message: { text: '<script>hello</script>' } }), defaultSettings)).toBe(true);
});
it('bounds retained identities at 10000 and ignores invalid timestamps', () => {
  const buffer = new ChatBuffer(); const settings = { ...defaultSettings, persistent: true };
  buffer.ingest(Array.from({ length: 10001 }, (_, n) => event(String(n))), settings);
  expect(buffer.ingest([event('10001', { receivedAt: 'invalid' })], settings)).toHaveLength(100);
  expect(buffer.ingest([event('0', { receivedAt: new Date(1).toISOString() })], settings).some(e => e.id === '0')).toBe(true);
});
it('allows only credential-free HTTP avatars and local assets', () => {
  expect(safeAvatar('/assets/icon')).toContain('/assets/icon');
  expect(safeAvatar('https://example.invalid/avatar.png')).toBe('https://example.invalid/avatar.png');
  for (const value of [null, 'javascript:alert(1)', 'data:image/svg+xml,hello', 'ftp://example.invalid/a', 'https://user:pass@example.invalid/a', 'http://[']) expect(safeAvatar(value)).toBeUndefined();
});

import { defaultSettings, type ChatSettings, type OverlayDefinition } from './chat';
export interface AlertSettings {
  eventTypes: string[]; platforms: string[]; template: string; group: string; priority: number; durationMs: number; cooldownMs: number;
  concurrency: number; maximumQueueLength: number; interruptible: boolean; interruptPolicy: 'never' | 'higher-priority';
  overflowPolicy: 'drop-oldest' | 'drop-newest'; animation: 'none' | 'fade' | 'slide'; mediaAssetId: string | null; soundAssetId: string | null;
}
export interface Widget {
  id: string; name: string; kind: 'text' | 'image' | 'video' | 'audio' | 'chat' | 'alert'; x: number; y: number; width: number; height: number;
  rotation: number; locked: boolean; hidden: boolean; text: string; color: string; fontSize: number; assetId: string | null;
  volume: number; loop: boolean; muted: boolean; chat: ChatSettings; alert: AlertSettings;
}
export interface Scene extends OverlayDefinition { canvasEnabled: boolean; revisionLimit: number; widgets: Widget[] }
export const defaultAlert: AlertSettings = {
  eventTypes: ['community.follow'], platforms: ['twitch', 'youtube', 'kick', 'rumble'], template: '{user} · {type}', group: 'main-alerts', priority: 0,
  durationMs: 5000, cooldownMs: 0, concurrency: 1, maximumQueueLength: 50, interruptible: true, interruptPolicy: 'never',
  overflowPolicy: 'drop-oldest', animation: 'fade', mediaAssetId: null, soundAssetId: null,
};
// crypto.randomUUID requires a secure context. HTTP LAN is supported.
export function widgetId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16)); bytes[6] = (bytes[6] & 15) | 64; bytes[8] = (bytes[8] & 63) | 128;
  const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
export function createWidget(kind: Widget['kind']): Widget {
  return { id: widgetId(), name: kind === 'chat' ? 'Combined Chat' : kind === 'alert' ? 'AlertBox' : kind[0].toUpperCase() + kind.slice(1),
    kind, x: 40, y: 40, width: kind === 'chat' ? 600 : 400, height: kind === 'chat' ? 700 : 200, rotation: 0, locked: false, hidden: false,
    text: 'Hello, stream!', color: '#ffffff', fontSize: 32, assetId: null, volume: .75, loop: true, muted: true,
    chat: structuredClone(defaultSettings), alert: structuredClone(defaultAlert) };
}
export const alertPresets = [
  ['Follow', 'community.follow', 'twitch'], ['Subscription', 'support.subscription', 'twitch'], ['Gift Subscription', 'support.gift', 'twitch'], ['Bits', 'support.bits', 'twitch'],
  ['Super Chat', 'support.donation', 'youtube'], ['Membership', 'support.subscription', 'youtube'], ['Gift Membership', 'support.gift', 'youtube'],
  ['Kick Subscription', 'support.subscription', 'kick'], ['Ko-fi Donation', 'support.donation', 'kofi'], ['Rumble Rant', 'support.rant', 'rumble'],
  ['Rumble Follow', 'community.follow', 'rumble'], ['Rumble Subscription (unverified)', 'support.subscription', 'rumble'],
  ['Rumble Gift (unverified)', 'support.gift', 'rumble'], ['Custom Streamer.bot Trigger', 'integration.custom', 'general'],
] as const;

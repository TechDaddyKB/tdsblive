import { defaultCustom, type CustomSettings } from './custom';
import { defaultSettings, type ChatSettings, type OverlayDefinition } from './chat';
export interface AlertSettings {
  eventTypes: string[]; platforms: string[]; template: string; group: string; priority: number; durationMs: number; cooldownMs: number;
  concurrency: number; maximumQueueLength: number; interruptible: boolean; interruptPolicy: 'never' | 'higher-priority';
  overflowPolicy: 'drop-oldest' | 'drop-newest'; animation: 'none' | 'fade' | 'slide'; mediaAssetId: string | null; soundAssetId: string | null;
}
export interface Widget {
  id: string; name: string; kind: 'text' | 'image' | 'video' | 'audio' | 'chat' | 'alert' | 'donor-crown' | 'donor-leaderboard' | 'latest-supporter' | 'current-stream-leader' | 'current-stream-total' | 'event-list' | 'goal-bar' | 'progress-bar' | 'custom'; x: number; y: number; width: number; height: number;
  groupId?: string | null; rotation: number; locked: boolean; hidden: boolean; text: string; color: string; fontSize: number; assetId: string | null;
  volume: number; loop: boolean; muted: boolean; chat: ChatSettings; alert: AlertSettings; donor?: DonorSettings; eventList?: EventListSettings; progress?: ProgressSettings; custom?: CustomSettings;
}
export interface DonorSettings {
  period: string; platforms: string[]; eventTypes: string[]; minimumUsdMinor: string; count: number;
  customStart: string | null; customEndExclusive: string | null; showName: boolean; showAvatar: boolean;
  showPlatformBadges: boolean; showAmount: boolean; showCrown: boolean; template: string; fontFamily: string;
  animation: 'none' | 'fade' | 'slide'; transitionMs: number; crownAssetId: string | null; fontAssetId: string | null;
}
export const defaultDonor: DonorSettings = {
  period: 'all-time', platforms: [], eventTypes: [], minimumUsdMinor: '0', count: 10, customStart: null, customEndExclusive: null,
  showName: true, showAvatar: true, showPlatformBadges: true, showAmount: true, showCrown: true,
  template: '{name} · {amount}', fontFamily: 'sans-serif', animation: 'fade', transitionMs: 300, crownAssetId: null, fontAssetId: null,
};
export const donorKinds: Widget['kind'][] = ['donor-crown', 'donor-leaderboard', 'latest-supporter', 'current-stream-leader', 'current-stream-total'];
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
    kind, groupId: null, x: 40, y: 40, width: kind === 'chat' ? 600 : 400, height: kind === 'chat' ? 700 : 200, rotation: 0, locked: false, hidden: false,
    text: 'Hello, stream!', color: '#ffffff', fontSize: 32, assetId: null, volume: .75, loop: true, muted: true,
    custom: structuredClone(defaultCustom), eventList: structuredClone(defaultEventList), progress: structuredClone(defaultProgress), chat: structuredClone(defaultSettings), alert: structuredClone(defaultAlert), donor: { ...structuredClone(defaultDonor), template: kind === 'current-stream-total' ? '{amount}' : defaultDonor.template } };
}
export const alertPresets = [
  ['Follow', 'community.follow', 'twitch'], ['Subscription', 'support.subscription', 'twitch'], ['Gift Subscription', 'support.gift', 'twitch'], ['Bits', 'support.bits', 'twitch'],
  ['Super Chat', 'support.donation', 'youtube'], ['Membership', 'support.subscription', 'youtube'], ['Gift Membership', 'support.gift', 'youtube'],
  ['Kick Subscription', 'support.subscription', 'kick'], ['Ko-fi Donation', 'support.donation', 'kofi'], ['Rumble Rant', 'support.rant', 'rumble'],
  ['Rumble Follow', 'community.follow', 'rumble'], ['Rumble Subscription (unverified)', 'support.subscription', 'rumble'],
  ['Rumble Gift (unverified)', 'support.gift', 'rumble'], ['Custom Streamer.bot Trigger', 'integration.custom', 'general'],
] as const;

export function inheritGroupSettings(widget: Widget, widgets: Widget[]): Widget {
  if (widget.kind !== 'alert') return widget;
  const peer = widgets.find(w => w.kind === 'alert' && w.id !== widget.id && w.alert.group === widget.alert.group);
  return peer ? { ...widget, alert: { ...widget.alert, concurrency: peer.alert.concurrency,
    maximumQueueLength: peer.alert.maximumQueueLength, overflowPolicy: peer.alert.overflowPolicy } } : widget;
}

export interface EventListSettings { eventTypes: string[]; platforms: string[]; ignoredUsers: string[]; count: number; durationMs: number; persistent: boolean; newestOnTop: boolean; template: string; font: string }
export const defaultEventList: EventListSettings = { eventTypes: ['community.follow', 'support.subscription', 'support.gift', 'support.bits', 'support.donation', 'support.rant'], platforms: [], ignoredUsers: [], count: 10, durationMs: 60000, persistent: true, newestOnTop: true, template: '{user} · {type}', font: 'Arial' };
export interface ProgressSettings { source: 'manual' | 'ledger-usd'; label: string; value: number; target: number; fillColor: string; trackColor: string; showValue: boolean; showPercent: boolean; orientation: 'horizontal' | 'vertical' }
export const defaultProgress: ProgressSettings = { source: 'manual', label: 'Stream goal', value: 0, target: 100, fillColor: '#60baff', trackColor: '#24262c', showValue: true, showPercent: true, orientation: 'horizontal' };

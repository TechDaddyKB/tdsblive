export type Platform = 'twitch' | 'youtube' | 'kick' | 'rumble';
export const platforms: Platform[] = ['twitch', 'youtube', 'kick', 'rumble'];
export interface ChatSettings extends WireChatSettings {
  platforms: Platform[]; showPlatformIcon: boolean; showAvatar: boolean; showBadges: boolean; showUsername: boolean; showMessage: boolean; showTimestamp: boolean;
  messageDurationSeconds: number; maximumMessages: number; persistent: boolean; newestOnTop: boolean; animationIn: 'none' | 'fade' | 'slide'; animationOut: 'none' | 'fade' | 'slide';
  backgroundOpacity: number; font: string; fontAssetId: string | null; fontSize: number; platformColors: Record<Platform, string>;
  ignoredUsers: string[]; ignoredPrefixes: string[]; hideBotMessages: boolean; botUsers: string[];
}
export interface OverlayDefinition extends WireOverlayDefinition { id: string; name: string; width: number; height: number; background: string; version: number; chat: ChatSettings; canvasEnabled?: boolean }
export interface ChatEvent {
  alertWidgetIds?: string[];
  id: string; type: string; platform: Platform; occurredAt: string; receivedAt: string; provenance: 'live' | 'simulation' | 'replay';
  user?: { platformUserId?: string | null; login?: string | null; displayName?: string | null; avatarUrl?: string | null; badges?: string[] | null; isBot?: boolean; badgeDetails?: { name: string; imageUrl?: string | null; version?: string | null }[] | null } | null;
  message?: { text?: string | null; parts?: { kind: string; text: string; imageUrl?: string | null; source?: string | null; zeroWidth?: boolean }[] | null } | null;
}
export const defaultSettings: ChatSettings = {
  platforms: [...platforms], showPlatformIcon: true, showAvatar: true, showBadges: true, showUsername: true, showMessage: true, showTimestamp: false,
  messageDurationSeconds: 30, maximumMessages: 100, persistent: false, newestOnTop: false, animationIn: 'fade', animationOut: 'fade',
  backgroundOpacity: .35, font: 'Arial', fontAssetId: null, fontSize: 24,
  platformColors: { twitch: '#c4a1ff', youtube: '#ff6b6b', kick: '#72e346', rumble: '#b8df5c' }, ignoredUsers: [], ignoredPrefixes: [], hideBotMessages: false,
  botUsers: ['nightbot', 'moobot', 'streamelements', 'streamlabs'],
};
export function accepts(event: ChatEvent, settings: ChatSettings): boolean {
  if (event.type !== 'chat.message' || typeof event.message?.text !== 'string' || !settings.platforms.includes(event.platform)) return false;
  const names = [event.user?.platformUserId, event.user?.login, event.user?.displayName].filter((n): n is string => typeof n === 'string').map(n => n.toLowerCase());
  if (settings.ignoredUsers.some(n => names.includes(n.toLowerCase())) || settings.ignoredPrefixes.some(p => event.message!.text!.startsWith(p))) return false;
  return !settings.hideBotMessages || !(event.user?.isBot || settings.botUsers.some(n => names.includes(n.toLowerCase())));
}
export class ChatBuffer {
  private readonly seen = new Set<string>();
  private messages: ChatEvent[] = [];
  ingest(events: ChatEvent[], settings: ChatSettings, now = Date.now()): ChatEvent[] {
    for (const event of events) {
      if (typeof event.id !== 'string' || this.seen.has(event.id) || !accepts(event, settings) || !Number.isFinite(Date.parse(event.receivedAt))) continue;
      this.seen.add(event.id);
      if (this.seen.size > 10_000) this.seen.delete(this.seen.values().next().value!);
      this.messages.push(event);
    }
    return this.visible(settings, now);
  }
  visible(settings: ChatSettings, now = Date.now()): ChatEvent[] {
    this.messages.sort((a, b) => Date.parse(a.receivedAt) - Date.parse(b.receivedAt) || a.id.localeCompare(b.id));
    this.messages = this.messages.slice(-500);
    const shown = this.messages.filter(e => accepts(e, settings) && (settings.persistent || now < Date.parse(e.receivedAt) + settings.messageDurationSeconds * 1000 + 300))
      .slice(-settings.maximumMessages);
    return settings.newestOnTop ? shown.reverse() : shown;
  }
}
export function safeAvatar(value: string | null | undefined): string | undefined {
  if (!value) return undefined;
  try { const url = new URL(value, location.origin); return ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password ? url.href : undefined; }
  catch { return undefined; }
}
import type { components } from '../../editor/src/generated/api-types';
type WireChatSettings = components['schemas']['ChatSettings'];
type WireOverlayDefinition = components['schemas']['OverlayDefinition'];

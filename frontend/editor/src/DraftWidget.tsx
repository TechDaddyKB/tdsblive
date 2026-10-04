import { WidgetPresentation } from '../../overlay-runtime/src/CanvasRuntime';
import type { ChatEvent } from '../../overlay-runtime/src/chat';
import type { Scene, Widget } from '../../overlay-runtime/src/scene';
import type { DonorSnapshot } from '../../overlay-runtime/src/DonorWidget';
export function sampleEvent(type = 'community.follow', platform: string = 'twitch'): ChatEvent {
  return { id: 'sample-design', type, platform: platform as ChatEvent['platform'], provenance: 'simulation', occurredAt: new Date().toISOString(), receivedAt: new Date().toISOString(), user: { displayName: 'Sample viewer' }, message: { text: 'Thanks for the stream!' } };
}
export function DraftWidget({ widget, scene, event, audio = false, showAlert = true }: { widget: Widget; scene: Scene; event?: ChatEvent; audio?: boolean; showAlert?: boolean }) {
  const sample = event ?? sampleEvent(widget.alert.eventTypes[0], widget.alert.platforms[0]);
  const snapshot: DonorSnapshot = { widgetId: widget.id, state: 'ready', generatedAt: new Date().toISOString(), totalUsdMinor: '7500', unknownCount: 0, gatedCount: 0, estimatedCount: 0,
    rows: [{ supporterId: 'sample-viewer', name: 'Sample supporter', usdAmountMinor: '2500', platforms: ['twitch'], unknownCount: 0, estimatedCount: 0, latestAt: new Date().toISOString(), hasKnownAmount: true }] };
  const chat = [sampleEvent('chat.message')];
  return <div className="draft-widget runtime-widget" style={{ width: '100%', height: '100%' }}>
    <WidgetPresentation widget={widget} scene={scene} events={chat} listEvents={[sample]} snapshot={snapshot} preview draft silent={!audio}
      customEvents={widget.custom?.permissions.includes('chat') && (widget.custom.subscriptions.includes('*') || widget.custom.subscriptions.includes('chat.message')) ? [{ widgetId: widget.id, event: { ...chat[0] } }] : []}
      active={widget.kind === 'alert' && showAlert ? [{ key: widget.id + sample.id, widget, event: sample, enqueued: 0 }] : []} />
    {widget.kind === 'audio' && <span className="audio-design-label">♫ {widget.name} · {audio ? 'Preview audio enabled' : 'Silent preview'}</span>}
  </div>;
}

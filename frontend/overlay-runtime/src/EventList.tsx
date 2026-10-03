import type { ChatEvent } from './chat';
import { alertText } from './AlertQueue';
import { defaultEventList, type Widget } from './scene';

export function eventListAccepts(event: ChatEvent, widget: Widget): boolean {
  const settings = widget.eventList ?? defaultEventList;
  const names = [event.user?.login, event.user?.displayName, event.user?.platformUserId];
  return (settings.eventTypes.includes('*') || settings.eventTypes.includes(event.type)) &&
    (!settings.platforms.length || settings.platforms.includes(event.platform)) &&
    !names.some(name => name && settings.ignoredUsers.some(v => v.toLowerCase() === name.toLowerCase()));
}
export function EventList({ widget, events, now }: { widget: Widget; events: ChatEvent[]; now: number }) {
  const settings = widget.eventList ?? defaultEventList;
  const visible = events.filter(e => eventListAccepts(e, widget) && (settings.persistent || now < Date.parse(e.receivedAt) + settings.durationMs))
    .sort((a, b) => Date.parse(b.receivedAt) - Date.parse(a.receivedAt) || b.id.localeCompare(a.id)).slice(0, settings.count);
  if (!settings.newestOnTop) visible.reverse();
  return <ol aria-label="Event list" style={{ fontFamily: settings.font, margin: 0, padding: 0, listStyle: 'none' }}>
    {visible.map(event => <li key={event.id} data-event-id={event.id}>{alertText(settings.template, event)}</li>)}
  </ol>;
}

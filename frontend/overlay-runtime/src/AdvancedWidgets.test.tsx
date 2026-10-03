import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { EventList } from './EventList';
import { ProgressWidget } from './ProgressWidget';
import { createWidget, defaultEventList, defaultProgress } from './scene';
import type { ChatEvent } from './chat';
afterEach(cleanup);
const events: ChatEvent[] = ['One', 'Two', 'Ignored'].map((name, i) => ({ id: String(i), type: 'community.follow', platform: 'twitch', receivedAt: new Date(i * 1000).toISOString(), occurredAt: new Date(i * 1000).toISOString(), provenance: 'live', user: { displayName: name }, message: { text: '<script>escaped</script>' } }));
it('filters and bounds event lists, orders entries and expires transient history', () => {
  const widget = { ...createWidget('event-list'), eventList: { ...defaultEventList, ignoredUsers: ['ignored'], count: 1, template: '{user} {message}' } };
  const view = render(<EventList widget={widget} events={events} now={3000} />);
  expect(screen.getByRole('listitem')).toHaveTextContent('Two <script>escaped</script>'); expect(document.querySelector('script')).toBeNull();
  view.rerender(<EventList widget={{ ...widget, eventList: { ...widget.eventList, count: 10, newestOnTop: false } }} events={events} now={3000} />);
  expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('One');
  view.rerender(<EventList widget={{ ...widget, eventList: { ...widget.eventList, persistent: false, durationMs: 100 } }} events={events} now={3000} />); expect(screen.queryByRole('listitem')).toBeNull();
  view.rerender(<EventList widget={{ ...widget, eventList: { ...widget.eventList, platforms: ['rumble'] } }} events={events} now={3000} />); expect(screen.queryByRole('listitem')).toBeNull();
  view.rerender(<EventList widget={{ ...widget, eventList: { ...widget.eventList, eventTypes: ['support.rant'] } }} events={events} now={3000} />); expect(screen.queryByRole('listitem')).toBeNull();
});
it('clamps bar rendering while displaying actual overshoot and ledger estimate states', () => {
  const widget = { ...createWidget('goal-bar'), progress: { ...defaultProgress, value: 150, target: 100 } };
  const view = render(<ProgressWidget widget={widget} />); expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100'); expect(screen.getByText(/150 \/ 100/)).toBeInTheDocument();
  view.rerender(<ProgressWidget widget={{ ...widget, progress: { ...widget.progress, source: 'ledger-usd', orientation: 'vertical' } }} snapshot={{ widgetId: widget.id, state: 'ready', rows: [], generatedAt: '', totalUsdMinor: '1250', unknownCount: 1, gatedCount: 0, estimatedCount: 1 }} />);
  expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '12.5'); expect(screen.getByText(/includes estimates/)).toHaveTextContent('pending values excluded');
  view.rerender(<ProgressWidget widget={{ ...widget, progress: { ...widget.progress, source: 'ledger-usd' } }} snapshot={{ widgetId: widget.id, state: 'preview', rows: [], generatedAt: '', totalUsdMinor: '0', unknownCount: 0, gatedCount: 0, estimatedCount: 0 }} />); expect(screen.getByText('preview')).toBeInTheDocument();
});

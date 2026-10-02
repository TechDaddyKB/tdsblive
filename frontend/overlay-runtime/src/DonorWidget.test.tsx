import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { DonorWidget, donorMoney, type DonorSnapshot } from './DonorWidget';
import { createWidget, defaultDonor } from './scene';
afterEach(() => { cleanup(); vi.useRealTimers(); });
const snapshot: DonorSnapshot = { widgetId: 'owned', state: 'ready', generatedAt: '2026-10-01T12:00:00Z', totalUsdMinor: '9223372036854775808', unknownCount: 1, gatedCount: 1, estimatedCount: 1,
  rows: [{ supporterId: 'alice', name: '<Alice>', usdAmountMinor: '9223372036854775808', platforms: ['twitch'], unknownCount: 1, estimatedCount: 1, latestAt: '2026-10-01T12:00:00Z' }] };
it('formats integer totals beyond safe number and signed values exactly', () => {
  expect(donorMoney('9223372036854775808')).toBe('$92,233,720,368,547,758.08');
  expect(donorMoney('-1')).toBe('-$0.01'); expect(donorMoney('1.5')).toBe('Unknown');
});
it('updates leader without refresh, escapes names and labels uncertain amounts', () => {
  vi.useFakeTimers();
  const widget = createWidget('donor-crown'); const view = render(<DonorWidget widget={widget} snapshot={snapshot} />);
  expect(screen.getByText('<Alice> · $92,233,720,368,547,758.08')).toBeVisible(); expect(document.querySelector('alice')).toBeNull();
  expect(screen.getByText('Estimated')).toBeVisible(); expect(screen.getByText('Unverified support excluded')).toBeVisible();
  view.rerender(<DonorWidget widget={widget} snapshot={{ ...snapshot, rows: [{ ...snapshot.rows[0], supporterId: 'bob', name: 'Bob', usdAmountMinor: '123' }] }} />);
  expect(screen.getByText('<Alice> · $92,233,720,368,547,758.08')).toBeVisible();
  expect(document.querySelector('.exit-fade')).not.toBeNull();
  act(() => vi.advanceTimersByTime(300));
  expect(screen.getByText('Bob · $1.23')).toBeVisible(); expect(screen.queryByText(/<Alice>/)).toBeNull();
});
it('latest unvalued support displays its name without inventing a zero valuation', () => {
  render(<DonorWidget widget={createWidget('latest-supporter')} snapshot={{ ...snapshot, rows: [{ ...snapshot.rows[0], usdAmountMinor: '0', hasKnownAmount: false }] }} />);
  expect(screen.getByText('<Alice> · Awaiting valuation')).toBeVisible(); expect(screen.queryByText(/\$0.00/)).toBeNull();
});
it('respects visibility and shows current stream total', () => {
  const widget = { ...createWidget('donor-leaderboard'), donor: { ...defaultDonor, showName: false, showAmount: false, showCrown: false, showPlatformBadges: false } };
  const view = render(<DonorWidget widget={widget} snapshot={snapshot} />);
  expect(screen.queryByText(/Alice/)).toBeNull(); expect(screen.queryByLabelText('Crown')).toBeNull(); expect(screen.queryByText('twitch')).toBeNull();
  view.rerender(<DonorWidget widget={createWidget('current-stream-total')} snapshot={snapshot} />);
  expect(screen.getByText('$92,233,720,368,547,758.08')).toBeVisible(); expect(screen.getByText('Includes estimates')).toBeVisible();
});
it.each(['preview', 'pending', 'gated', 'empty', 'period-unavailable'])('renders %s without inventing a supporter', state => {
  render(<DonorWidget widget={createWidget('donor-crown')} snapshot={{ ...snapshot, state }} />);
  expect(screen.getByRole('status')).toBeVisible(); expect(screen.queryByText(/Alice/)).toBeNull();
});

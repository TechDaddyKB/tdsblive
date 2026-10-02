import { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { WidgetProperties } from './WidgetProperties';
import { createWidget, type Widget } from '../../overlay-runtime/src/scene';
afterEach(cleanup);
function Harness({ kind = 'donor-leaderboard' }: { kind?: Widget['kind'] }) {
  const [widget, setWidget] = useState(() => createWidget(kind));
  return <><WidgetProperties widget={widget} assets={[
    { id: 'a'.repeat(64), filename: 'Crown PNG', mime: 'image/png' },
    { id: 'b'.repeat(64), filename: 'Owned font', mime: 'font/woff2' },
  ]} change={setWidget} /><output data-testid="donor-settings">{JSON.stringify(widget.donor)}</output></>;
}
const value = () => JSON.parse(screen.getByTestId('donor-settings').textContent!);
const change = (label: string, next: string) => fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value: next } });
it('configures periods, source filters, exact large thresholds, styles and assets', () => {
  render(<Harness />);
  change('Donor period', 'custom'); change('Start date', '2026-10-01'); change('End date (exclusive)', '2026-11-01');
  change('Donor platforms', 'twitch, kofi'); change('Donor support kinds', 'bits, donation');
  change('Minimum USD cents', '9007199254740993'); change('Ranked supporters', '25');
  change('Donor template', '{rank} {name}: {amount}'); change('Donor font family', 'Arial');
  change('Leader animation', 'slide'); change('Transition milliseconds', '1000');
  change('Crown image', 'a'.repeat(64)); change('Font asset', 'b'.repeat(64));
  for (const label of ['Show name', 'Show avatar', 'Show platform badges', 'Show amount', 'Show crown']) fireEvent.click(screen.getByLabelText(label));
  expect(value()).toMatchObject({ period: 'custom', customStart: '2026-10-01', customEndExclusive: '2026-11-01', platforms: ['twitch', 'kofi'], eventTypes: ['bits', 'donation'],
    minimumUsdMinor: '9007199254740993', count: 25, template: '{rank} {name}: {amount}', fontFamily: 'Arial', animation: 'slide', transitionMs: 1000,
    crownAssetId: 'a'.repeat(64), fontAssetId: 'b'.repeat(64), showName: false, showAvatar: false, showPlatformBadges: false, showAmount: false, showCrown: false });
  change('Minimum USD cents', '9223372036854775808'); change('Minimum USD cents', '1.2'); change('Ranked supporters', '26'); change('Transition milliseconds', '-1');
  expect(value()).toMatchObject({ minimumUsdMinor: '9007199254740993', count: 25, transitionMs: 1000 });
});
it.each(['current-stream-leader', 'current-stream-total'] as const)('locks %s to the persisted stream period', kind => {
  render(<Harness kind={kind} />); expect(screen.getByLabelText('Donor period')).toBeDisabled(); expect(screen.getByLabelText('Donor period')).toHaveValue('current-stream');
  expect(screen.queryByLabelText('Ranked supporters')).toBeNull();
});

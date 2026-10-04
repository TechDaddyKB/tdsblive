import { describe, expect, it } from 'vitest';
import { createWidget, type Scene } from '../../overlay-runtime/src/scene';
import { alertReason, selectAlertDesigns } from './alertPreview';
import { decimalToInteger, integerToDecimal } from './TriggerControls';
import { readFileSync } from 'node:fs';
interface Vector { operator: string; value: string; upper?: string; amount?: string | null; currency?: string; digits?: number; gated?: boolean; giftRole?: string; expected: boolean }
it('shares the backend matching specification including Int64 boundaries', () => {
  const vectors = JSON.parse(readFileSync('tests/fixtures/alert-matching.json', 'utf8')) as Vector[];
  for (const v of vectors) { const w = createWidget('alert'); w.alert = { ...w.alert, eventTypes: ['support.donation'], platforms: ['kofi'], condition: { unit: 'native-money', operator: v.operator, value: v.value, upperExclusive: v.upper, currency: 'USD', minorUnitDigits: 2 } };
    expect(alertReason(w, { type: 'support.donation', platform: 'kofi', amount: v.amount ?? undefined, currency: v.currency, digits: v.digits, gated: v.gated, giftRole: v.giftRole }) === null).toBe(v.expected);
  }
});
describe('Native alert design selection', () => {
  const widget = { ...createWidget('alert'), alert: { ...createWidget('alert').alert, eventTypes: ['support.donation'], platforms: ['kofi'], condition: { unit: 'native-money', operator: 'range', value: '500', upperExclusive: '1000', currency: 'USD', minorUnitDigits: 2 } } };
  const sample = { type: 'support.donation', platform: 'kofi', amount: '500', currency: 'USD', digits: 2 };
  it.each([['499', false], ['500', true], ['999', true], ['1000', false]])('matches half-open amount %s', (amount, matches) => expect(alertReason(widget, { ...sample, amount }) === null).toBe(matches));
  it('fails closed on unavailable or incompatible facts', () => {
    for (const patch of [{ amount: undefined }, { currency: 'EUR' }, { digits: 0 }, { gated: true }, { giftRole: 'recipient' }, { amount: '9223372036854775808' }]) expect(alertReason(widget, { ...sample, ...patch })).not.toBeNull();
  });
  it('chooses explicit set order while preserving independent alerts', () => {
    const second = { ...widget, id: 'second' }; const independent = { ...widget, id: 'legacy', alert: { ...widget.alert, condition: null } };
    const scene = { widgets: [widget, second, independent], alertSets: [{ id: 'set', name: 'Tiers', widgetIds: ['second', widget.id], selection: 'first' }] } as Scene;
    expect(selectAlertDesigns(scene, sample).filter(r => !r.reason).map(r => r.id)).toEqual(['second', 'legacy']);
    expect(selectAlertDesigns({ ...scene, alertSets: [{ ...scene.alertSets![0], selection: 'all' }] }, sample).filter(r => !r.reason)).toHaveLength(3);
  });
  it('uses reliable incoming identity and respects hidden designs', () => {
    expect(alertReason({ ...widget, hidden: true }, sample)).toBeTruthy();
    expect(alertReason({ ...widget, alert: { ...widget.alert, customTriggerKey: 'owned.sparkle' } }, sample)).toBe('Different custom trigger');
  });
});
describe('Currency entry uses bounded integer arithmetic', () => {
  it.each([['5.00', 2, '500'], ['0.05', 2, '5'], ['10', 0, '10'], ['1.234', 3, '1234'], ['92233720368547758.07', 2, '9223372036854775807']])('converts %s at scale %s', (value, digits, result) => { expect(decimalToInteger(value, digits)).toBe(result); expect(decimalToInteger(integerToDecimal(result, digits), digits)).toBe(result); });
  it.each(['-1', '1e3', '5.001', '', 'Infinity', '92233720368547758.08'])('rejects %s', value => expect(decimalToInteger(value, 2)).toBeNull());
});

import { describe, expect, it } from 'vitest';
import { majorToMinor, minorToMajor, money, valuationLabel } from './financialMoney';

describe('financial display precision', () => {
  it('preserves integers beyond JavaScript safe integer range', () => {
    expect(money('9223372036854775807')).toBe('$92,233,720,368,547,758.07');
    expect(money('18446744073709551614')).toBe('$184,467,440,737,095,516.14');
  });
  it('keeps explicit zero separate from unavailable money', () => {
    expect(money('0')).toBe('$0.00');
    expect(money(null)).toBe('Unknown');
    expect(money('NaN')).toBe('Unknown');
  });
  it('respects currencies with different minor units', () => {
    expect(money('12345', 'JPY', 0)).toBe('JPY 12,345');
    expect(money('12345', 'KWD', 3)).toBe('KWD 12.345');
    expect(money('1', 'USD', 9)).toBe('Unknown');
  });
  it('moves decimal points without floating point rounding', () => {
    expect(majorToMinor('0.0125')).toBe('1.25');
    expect(minorToMajor('1.25')).toBe('0.0125');
    expect(majorToMinor('92233720368547758.07')).toBe('9223372036854775807');
    expect(() => majorToMinor('-1')).toThrow();
    expect(() => majorToMinor('1e3')).toThrow();
  });
  it('discloses estimates and excluded or gated contributions', () => {
    expect(valuationLabel('fx', true, 'counted')).toBe('Estimated conversion');
    expect(valuationLabel('configured_nominal', true, 'counted')).toBe('Configured estimate');
    expect(valuationLabel('exact', false, 'excluded')).toBe('Excluded notification');
    expect(valuationLabel('unknown', false, 'gated')).toBe('Needs evidence');
  });
});

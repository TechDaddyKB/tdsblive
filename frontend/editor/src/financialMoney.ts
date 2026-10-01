/** Decimal point movement uses strings; authoritative money never passes through Number. */
function decimalParts(value: string): [string, string] {
  if (value.length > 128 || !/^\d+(?:\.\d+)?$/.test(value)) throw new Error('Enter a nonnegative decimal value.');
  const [whole, fraction = ''] = value.split('.');
  return [whole.replace(/^0+(?=\d)/, ''), fraction];
}

export function majorToMinor(value: string): string {
  const [whole, fraction] = decimalParts(value.trim());
  const integer = (whole + fraction.padEnd(2, '0').slice(0, 2)).replace(/^0+(?=\d)/, '');
  const remainder = fraction.slice(2).replace(/0+$/, '');
  return integer + (remainder ? '.' + remainder : '');
}

export function minorToMajor(value: string): string {
  const [whole, fraction] = decimalParts(value);
  const padded = whole.padStart(3, '0');
  const remainder = (padded.slice(-2) + fraction).replace(/0+$/, '');
  return padded.slice(0, -2) + (remainder ? '.' + remainder : '');
}

export function money(value: string | null, currency = 'USD', digits = 2): string {
  if (value === null) return 'Unknown';
  if (!/^\d+$/.test(value) || !Number.isInteger(digits) || digits < 0 || digits > 4) return 'Unknown';
  const padded = value.padStart(digits + 1, '0');
  const integer = digits ? padded.slice(0, -digits) : padded;
  const major = new Intl.NumberFormat('en-US').format(BigInt(integer));
  const amount = major + (digits ? '.' + padded.slice(-digits) : '');
  return currency === 'USD' ? '$' + amount : currency + ' ' + amount;
}

export function valuationLabel(method: string, estimated: boolean, state: string): string {
  if (state === 'excluded') return 'Excluded notification';
  if (state === 'gated') return 'Needs evidence';
  if (method === 'configured_nominal') return 'Configured estimate';
  if (method === 'fx') return estimated ? 'Estimated conversion' : 'Converted';
  return method === 'exact' ? 'Reported amount' : 'Unknown value';
}

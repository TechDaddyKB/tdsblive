import { useState } from 'react';
import { finance, type CachedRate, type FinancialOperation } from './financialApi';

export function FinancialRatesPanel({ rates, operation, busy }: { rates: CachedRate[]; operation: FinancialOperation; busy: boolean }) {
  const [currency, setCurrency] = useState('EUR');
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [value, setValue] = useState('');
  const [result, setResult] = useState('');
  return <fieldset disabled={busy}>
    <legend>Currency rates</legend>
    <p>Rates convert one native currency unit into USD. Manual rates apply to the chosen UTC event date. Changes leave accepted history frozen until you reconcile selected entries.</p>
    <label>Rate currency <input value={currency} maxLength={3} pattern="[A-Z]{3}" onChange={event => setCurrency(event.target.value.toUpperCase())} /></label>
    <label>Rate date <input type="date" value={date} onChange={event => setDate(event.target.value)} /></label>
    <button onClick={() => void operation(async () => {
      const lookup = await finance.lookupRate(currency, date);
      setResult(lookup.rate ? `${lookup.rate.usdPerNativeUnit} USD · ${lookup.rate.provider} · ${lookup.rate.rateDate}${lookup.rate.estimated ? ' (estimate)' : ''}` : 'Unavailable; contributions remain pending.');
    }, 'Rate lookup finished.')}>Look up rate</button>
    <button onClick={() => void operation(async () => {
      const refresh = await finance.refreshRate(currency, date); setResult(refresh.refreshed ? 'Provider observation refreshed.' : 'Provider unavailable; cached rate preserved.');
    }, 'Rate refresh finished; history is unchanged.')}>Refresh provider rate</button>
    {result && <p role="status">{result}</p>}
    <form onSubmit={event => { event.preventDefault(); void operation(() => finance.overrideRate(currency, date, value), 'Dated manual rate saved; history is unchanged.'); }}>
      <label>Manual USD rate <input inputMode="decimal" required value={value} onChange={event => setValue(event.target.value)} /></label>
      <button type="submit">Save manual rate</button>
    </form>
    {rates.length === 0 ? <p>No cached observations yet.</p> : <ul>{rates.map(row => <li key={JSON.stringify([row.currency, row.requestedDate, row.origin])}>
      {row.currency} · event date {row.requestedDate} · {row.usdPerNativeUnit} USD · observed {row.rateDate} · {row.provider}{row.estimated ? ' · estimate' : ''}{' '}
      {row.origin === 'manual' && <button onClick={() => void operation(() => finance.removeOverride(row.currency, row.requestedDate), 'Manual override removed; history is unchanged.')}>Remove {row.currency} override for {row.requestedDate}</button>}
    </li>)}</ul>}
  </fieldset>;
}

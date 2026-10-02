import { useState } from 'react';
import { finance, type FinancialSettings, type FinancialOperation } from './financialApi';

export function FinancialSettingsPanel({ settings, operation, busy }: { settings: FinancialSettings; operation: FinancialOperation; busy: boolean }) {
  const [zone, setZone] = useState(settings.timeZone);
  const [start, setStart] = useState(settings.currentStreamStartUtc ?? '');
  return <fieldset disabled={busy}>
    <legend>Periods and stream start</legend>
    <form onSubmit={event => {
      event.preventDefault();
      void operation(() => {
        const date = start ? new Date(start) : null;
        if (date && (Number.isNaN(date.getTime()) || !/(?:Z|[+-]\d{2}:\d{2})$/.test(start))) throw new Error('Enter an explicit UTC offset');
        return finance.saveSettings({ timeZone: zone, currentStreamStartUtc: date?.toISOString() ?? null, version: settings.version });
      }, 'Financial periods saved.');
    }}>
      <label>Financial timezone <input value={zone} required onChange={event => setZone(event.target.value)} /></label>
      <button type="button" onClick={() => setZone(Intl.DateTimeFormat().resolvedOptions().timeZone)}>Use browser timezone</button>
      <label>Current stream start <input placeholder="2026-01-05T12:00:00Z" value={start} onChange={event => setStart(event.target.value)} /></label>
      <p>Enter a time with its UTC offset. This start is shared across platforms. Daily periods use the financial timezone; weeks start Monday.</p>
      <button type="button" onClick={() => setStart(new Date().toISOString())}>Use current time</button>
      <button type="button" onClick={() => setStart('')}>Clear stream start</button>
      <button type="submit">Save financial periods</button>
    </form>
    <p>Saved timezone: {settings.timeZone}. Saved stream start: {settings.currentStreamStartUtc ?? 'Not set'}.</p>
  </fieldset>;
}

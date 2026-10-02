import { useEffect, useRef, useState } from 'react';
import { finance, type FinancialPage, type FinancialSettings, type FinancialTotals, type FinancialIdentity, type NominalRule, type CachedRate, type FinancialSelection, type FinancialOperation } from './financialApi';
import { money, valuationLabel } from './financialMoney';
import { FinancialSettingsPanel } from './FinancialSettingsPanel';
import { FinancialRulesPanel } from './FinancialRulesPanel';
import { FinancialRatesPanel } from './FinancialRatesPanel';
import { FinancialIdentitiesPanel } from './FinancialIdentitiesPanel';

export function FinancialPanel() {
  const [settings, setSettings] = useState<FinancialSettings>();
  const [page, setPage] = useState<FinancialPage>();
  const [totals, setTotals] = useState<FinancialTotals>();
  const [identities, setIdentities] = useState<FinancialIdentity[]>([]);
  const [rules, setRules] = useState<NominalRule[]>([]);
  const [rates, setRates] = useState<CachedRate[]>([]);
  const [selected, setSelected] = useState<FinancialSelection[]>([]);
  const [state, setState] = useState('all');
  const [offset, setOffset] = useState(0);
  const [period, setPeriod] = useState('all-time');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [busy, setBusy] = useState(false);
  const writing = useRef(false);
  const [message, setMessage] = useState('');
  const [reconciliation, setReconciliation] = useState('');
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const configure = async () => {
    const [configuration, people, values, currencies] = await Promise.all([finance.settings(), finance.identities(), finance.rules(), finance.rates()]);
    setSettings(configuration); setIdentities(people); setRules(values); setRates(currencies);
  };
  useEffect(() => {
    let active = true;
    void Promise.all([finance.settings(), finance.identities(), finance.rules(), finance.rates()]).then(([configuration, people, values, currencies]) => {
      if (active) { setSettings(configuration); setIdentities(people); setRules(values); setRates(currencies); }
    }).catch(() => { if (active) setError('Unable to load financial configuration.'); });
    return () => { active = false; };
  }, []);
  const ready = !!settings && (period !== 'current-stream' || !!settings.currentStreamStartUtc) && (period !== 'custom' || (/^\d{4}-\d{2}-\d{2}$/.test(start) && /^\d{4}-\d{2}-\d{2}$/.test(end) && start < end));
  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const [ledger, aggregate] = await Promise.all([finance.ledger(offset, state, controller.signal), ready ? finance.totals(period, start, end, controller.signal) : Promise.resolve(undefined)]);
        if (!controller.signal.aborted) { setPage(ledger); setTotals(aggregate); }
      } catch { if (!controller.signal.aborted) setError('Unable to refresh financial data.'); }
      finally { if (!controller.signal.aborted) timer = setTimeout(() => { void poll(); }, 2000); }
    };
    void poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [offset, state, period, start, end, ready, revision]);
  const operation: FinancialOperation = async (action, success) => {
    if (writing.current) return;
    writing.current = true; setBusy(true); setError(''); setMessage('');
    try { await action(); await configure(); setMessage(success); }
    catch { setError('Unable to apply this change. Refresh and review the values before retrying.'); }
    finally { writing.current = false; setBusy(false); setRevision(value => value + 1); }
  };
  return <section id="financial-ledger" aria-labelledby="financial-heading">
    <h2 id="financial-heading">Financial ledger</h2>
    <p>Reported amounts, conversions, configured estimates, and unknown values remain distinct. Accepted valuations stay frozen until explicitly reconciled. Estimates do not establish platform revenue.</p>
    {error && <p role="alert">{error}</p>}{message && <p role="status">{message}</p>}
    {reconciliation && <p role="status">Reconciliation outcomes: {reconciliation}</p>}
    <button disabled={busy} onClick={() => { void operation(async () => {}, 'Financial data refreshed.'); }}>Refresh financial data</button>
    {settings && <FinancialSettingsPanel settings={settings} operation={operation} busy={busy} />}
    <label>Totals period <select value={period} onChange={event => { setPeriod(event.target.value); setTotals(undefined); }}>{['all-time', 'current-stream', 'today', 'week', 'month', 'year', 'custom'].map(value => <option key={value}>{value}</option>)}</select></label>
    {period === 'custom' && <><label>Start date <input type="date" value={start} onChange={event => setStart(event.target.value)} /></label><label>End date (exclusive) <input type="date" value={end} onChange={event => setEnd(event.target.value)} /></label></>}
    {!ready && <p>Set the stream start or a valid custom date range to view that period.</p>}
    {totals && <><p>Timezone: {totals.timeZone}. Valued totals include estimates; unknown contributions are counted separately.</p><table><thead><tr><th>Supporter</th><th>Valued total</th><th>Reported USD</th><th>Converted</th><th>Configured estimate</th><th>Unknown</th><th>Needs evidence</th></tr></thead><tbody>{totals.supporters.map(row => <tr key={row.supporterId}><td>{row.name}</td><td>{money(row.usdAmountMinor)}</td><td>{money(row.exactAmountMinor)}</td><td>{money(row.fxAmountMinor)}</td><td>{money(row.nominalAmountMinor)}</td><td>{row.unknownCount}</td><td>{row.gatedCount}</td></tr>)}</tbody></table></>}
    <h3>Contributions</h3>
    <label>Ledger state <select value={state} onChange={event => { setState(event.target.value); setOffset(0); setSelected([]); }}>{['all', 'counted', 'pending', 'gated', 'excluded'].map(value => <option key={value}>{value}</option>)}</select></label>
    <button disabled={busy || !selected.length} onClick={() => { void operation(async () => { const results = await finance.reconcile(selected); setSelected([]); setReconciliation(results.map(row => row.outcome).join(', ')); }, 'Reconciliation finished. Pending values remain unknown when no rate or rule is available.'); }}>Reconcile selected valuations</button>
    {page && <><table><thead><tr><th>Select</th><th>When / source</th><th>Supporter / support</th><th>Native amount</th><th>USD valuation</th><th>Evidence</th></tr></thead><tbody>{page.items.map(row => <tr key={row.id}>
      <td><input type="checkbox" aria-label={`Select contribution ${row.id}`} disabled={busy || row.accountingState !== 'counted'} checked={selected.some(value => value.id === row.id)} onChange={event => setSelected(values => event.target.checked ? [...values, { id: row.id, version: row.version }] : values.filter(value => value.id !== row.id))} /></td>
      <td>{row.occurredAt}<br />{row.platform}</td><td>{row.supporterName}<br />{row.type} × {row.quantity} {row.tier}</td>
      <td>{money(row.nativeAmountMinor, row.nativeCurrency ?? 'USD', Number(row.nativeMinorUnitDigits ?? 2))}</td>
      <td>{money(row.usdAmountMinor)}<br />{valuationLabel(row.valuationMethod, row.estimated, row.accountingState)}{row.pendingReason && <p>{row.pendingReason}</p>}</td>
      <td>{row.fxRate && <p>{row.fxRate} USD per native unit · {row.fxRateDate} · {row.fxProvider}</p>}<details><summary>Source facts</summary><pre>{row.metadataJson}</pre></details></td>
    </tr>)}</tbody></table><p>{page.totalCount} contributions; page starting at {offset + 1}.</p>
      <button disabled={!offset || busy} onClick={() => { setOffset(value => Math.max(0, value - 50)); setSelected([]); }}>Previous contributions</button>
      <button disabled={offset + 50 >= Number(page.totalCount) || busy} onClick={() => { setOffset(value => value + 50); setSelected([]); }}>Next contributions</button></>}
    <FinancialRulesPanel rules={rules} operation={operation} busy={busy} />
    <FinancialRatesPanel rates={rates} operation={operation} busy={busy} />
    <FinancialIdentitiesPanel identities={identities} operation={operation} busy={busy} />
  </section>;
}

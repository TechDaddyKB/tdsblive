import { useState } from 'react';
import { finance, type NominalRule, type FinancialOperation } from './financialApi';
import { majorToMinor, minorToMajor } from './financialMoney';

export function FinancialRulesPanel({ rules, operation, busy }: { rules: NominalRule[]; operation: FinancialOperation; busy: boolean }) {
  const [platform, setPlatform] = useState('twitch');
  const [type, setType] = useState('bits');
  const [tier, setTier] = useState('');
  const [value, setValue] = useState('');
  const existing = rules.find(row => row.platform === platform && row.type === type && row.tier === tier);
  return <fieldset disabled={busy}>
    <legend>Nominal supporter values</legend>
    <p>These are estimates of supporter value, not viewer spend or creator revenue. Saving or removing a rule leaves accepted history unchanged. No values are assumed until you choose them.</p>
    <form onSubmit={event => { event.preventDefault(); void operation(() => finance.saveRule({ platform, type, tier,
      usdMinorPerUnit: majorToMinor(value), expectedVersion: existing?.version ?? 0 }), 'Nominal value saved; history is unchanged.'); }}>
      <label>Rule platform <select value={platform} onChange={event => setPlatform(event.target.value)}>{['twitch', 'youtube', 'kick', 'kofi', 'rumble'].map(item => <option key={item}>{item}</option>)}</select></label>
      <label>Support type <select value={type} onChange={event => setType(event.target.value)}>{['bits', 'subscription', 'membership', 'gift'].map(item => <option key={item}>{item}</option>)}</select></label>
      <label>Tier <input value={tier} maxLength={64} placeholder="Blank for Bits/Kick; 1000 or prime for Twitch" onChange={event => setTier(event.target.value)} /></label>
      <label>USD per unit <input inputMode="decimal" required value={value} onChange={event => setValue(event.target.value)} /></label>
      <button type="submit">Save nominal value</button>
    </form>
    {rules.length === 0 ? <p>All nominal values are unconfigured.</p> : <ul>{rules.map(row => <li key={JSON.stringify([row.platform, row.type, row.tier])}>
      {row.platform} · {row.type} · {row.tier || 'Any untiered event'}: {row.enabled ? '$' + minorToMajor(row.usdMinorPerUnit) + ' per unit (estimate)' : 'Unconfigured'}{' '}
      <button onClick={() => { setPlatform(row.platform); setType(row.type); setTier(row.tier); setValue(minorToMajor(row.usdMinorPerUnit)); }}>Edit {row.platform} {row.type} {row.tier}</button>{' '}
      {row.enabled && <button onClick={() => void operation(() => finance.removeRule({ platform: row.platform, type: row.type, tier: row.tier, expectedVersion: row.version }), 'Rule removed; future values are unconfigured.')}>Remove {row.platform} {row.type} {row.tier}</button>}
    </li>)}</ul>}
  </fieldset>;
}

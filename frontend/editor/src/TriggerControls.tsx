import { useEffect, useState } from 'react';
import { request } from './api';
import { alertPresets, type AlertCondition, type AlertSettings } from '../../overlay-runtime/src/scene';
export interface TriggerChoice { id: string; label: string; platform: string; eventType: string; description: string; availability: string; units: string[]; nativeType?: string | null; customTriggerKey?: string | null }
export const eventLabels: Record<string, string> = { '*': 'All events', 'community.follow': 'Follow / new subscriber', 'support.subscription': 'Subscription / membership', 'support.gift': 'Gift subscription / membership', 'support.bits': 'Bits', 'support.donation': 'Donation / paid support', 'support.rant': 'Rumble Rant', 'chat.message': 'Chat message', 'integration.custom': 'Custom event', 'integration.unknown': 'Other incoming event', 'stream.online': 'Stream started', 'stream.offline': 'Stream ended' };
export const platformLabels: Record<string, string> = { twitch: 'Twitch', youtube: 'YouTube', kick: 'Kick', rumble: 'Rumble', kofi: 'Ko-fi', general: 'Streamer.bot general', custom: 'Custom' };
export const fallbackTriggers: TriggerChoice[] = alertPresets.map(p => ({ id: `${p[2]}:${p[1]}`, label: `${platformLabels[p[2]] ?? p[2]} · ${p[0]}`, platform: p[2], eventType: p[1], description: 'Incoming event. Verify your platform connection separately.', availability: p[0].includes('unverified') ? 'unverified' : 'configured', units: p[1].startsWith('support.') ? ['quantity', 'native-money'] : [] }));
export function useTriggerCatalog() {
  const [choices, setChoices] = useState(fallbackTriggers);
  useEffect(() => { let active = true; void request<TriggerChoice[]>('/api/alert-triggers').then(value => { if (active && Array.isArray(value) && value.every(c => typeof c.id === 'string')) setChoices(value); }).catch(() => {}); return () => { active = false; }; }, []);
  return choices;
}
export function NamedPicker({ label, values, options, change }: { label: string; values: string[]; options: Record<string, string>; change: (values: string[]) => void }) {
  const [search, setSearch] = useState(''); const merged = { ...options, ...Object.fromEntries(values.filter(v => !options[v]).map(v => [v, `${v} (configured)`])) };
  return <fieldset className="named-picker"><legend>{label}</legend><label>Search {label.toLowerCase()}<input value={search} onChange={e => setSearch(e.target.value)} /></label>
    <div className="choice-list">{Object.entries(merged).filter(([key, name]) => (name + key).toLowerCase().includes(search.toLowerCase())).map(([key, name]) => <label key={key}><input type="checkbox" checked={values.includes(key)} onChange={e => change(e.target.checked ? [...values, key] : values.filter(v => v !== key))} />{name}</label>)}</div>
  </fieldset>;
}
export function TriggerSelect({ alert, change }: { alert: AlertSettings; change: (patch: Partial<AlertSettings>) => void }) {
  const choices = useTriggerCatalog(); const [search, setSearch] = useState('');
  const selected = choices.find(c => alert.eventTypes.length === 1 && alert.platforms.length === 1 && c.platform === alert.platforms[0] && c.eventType === alert.eventTypes[0] && (c.nativeType ?? null) === (alert.nativeType ?? null) && (c.customTriggerKey ?? null) === (alert.customTriggerKey ?? null));
  const filtered = choices.filter(c => c.id === selected?.id || c.label.toLowerCase().includes(search.toLowerCase()));
  return <><label>Find a trigger<input aria-label="Find a trigger" placeholder="Try follow, donation or Rant" value={search} onChange={e => setSearch(e.target.value)} /></label>
    <label>Choose trigger<select aria-label="Choose trigger" value={selected?.id ?? ''} onChange={e => { const c = choices.find(c => c.id === e.target.value); if (c) change({ eventTypes: [c.eventType], platforms: [c.platform], nativeType: c.nativeType ?? null, customTriggerKey: c.customTriggerKey ?? null }); }}><option value="">{alert.eventTypes.length > 1 || alert.platforms.length > 1 ? 'Multiple configured triggers — see Advanced' : 'Choose an incoming trigger…'}</option>{filtered.map(c => <option key={c.id} value={c.id}>{c.label}{c.availability === 'unverified' && !c.label.includes('unverified') ? ' (unverified)' : ''}</option>)}</select></label>
    {selected ? <p className="field-help">{selected.description}{selected.units.length > 0 ? ' Supports quantity and reported money conditions when the incoming event provides those facts.' : ' This trigger has no maintained support amount or quantity capability.'}</p> : <p className="field-help">Configured: {alert.platforms.map(p => platformLabels[p] ?? p).join(', ')} · {alert.eventTypes.map(t => eventLabels[t] ?? `${t} (configured)`).join(', ')}. Existing filters are retained until you change them.</p>}<p>Incoming triggers select designs. Streamer.bot actions and executable code triggers are separate.</p>
  </>;
}
export function decimalToInteger(value: string, digits: number): string | null {
  if (!Number.isInteger(digits) || digits < 0 || digits > 4 || value.length > 32 || !/^\d+(?:\.\d*)?$/.test(value)) return null;
  const [whole, fraction = ''] = value.split('.'); if (fraction.length > digits) return null;
  const result = BigInt(whole + fraction.padEnd(digits, '0')); return result <= 9223372036854775807n ? String(result) : null;
}
export function integerToDecimal(value: string | number, digits: number): string {
  if (!/^\d+$/.test(String(value))) return '';
  const text = String(value).padStart(digits + 1, '0'); return digits ? `${text.slice(0, -digits)}.${text.slice(-digits)}` : text;
}
export function AmountField({ label, value, digits, change }: { label: string; value: string | number; digits: number; change: (value: string) => void }) {
  const [text, setText] = useState(() => integerToDecimal(value, digits)); const [invalid, setInvalid] = useState(false);
  useEffect(() => { setText(integerToDecimal(value, digits)); setInvalid(false); }, [value, digits]);
  return <label>{label}<input aria-label={label} inputMode="decimal" aria-invalid={invalid} value={text} onChange={e => { const input = e.target.value; setText(input); const parsed = decimalToInteger(input, digits); setInvalid(parsed === null); if (parsed !== null) change(parsed); }} />{invalid && <span role="alert">Enter a nonnegative amount with up to {digits} decimal places. The previous valid amount is retained.</span>}</label>;
}
export function CurrencyField({ label, currency, digits, value, upper, change }: { label: string; currency: string; digits: number; value: string | number; upper?: string | number | null; change: (patch: { currency: string; minorUnitDigits: number; value: string; upperExclusive?: string | null }) => void }) {
  const [text, setText] = useState(currency); const [error, setError] = useState(''); const list = label.replaceAll(' ', '-') + '-currencies';
  useEffect(() => { setText(currency); }, [currency]);
  return <label>{label}<input aria-label={label} list={list} value={text} maxLength={3} onChange={e => {
    const code = e.target.value.toUpperCase(); setText(code); setError(''); if (!/^[A-Z]{3}$/.test(code)) { setError('Choose a currency or enter its three-letter code. The previous currency is retained.'); return; }
    const scale = new Intl.NumberFormat('en', { style: 'currency', currency: code }).resolvedOptions().maximumFractionDigits ?? 2;
    const rescale = (amount: string | number) => {
      if (!/^\d+$/.test(String(amount)) || digits < 0 || digits > 4) return null;
      const source = 10n ** BigInt(digits), scaled = BigInt(amount) * 10n ** BigInt(scale);
      if (scaled % source !== 0n) return null;
      const result = scaled / source;
      return result <= 9223372036854775807n ? String(result) : null;
    };
    const amount = rescale(value), maximum = upper == null ? null : rescale(upper);
    if (amount === null || upper != null && maximum === null) { setError(`Enter amounts with up to ${scale} decimal places before choosing ${code}. The previous currency is retained.`); return; }
    change({ currency: code, minorUnitDigits: scale, value: amount, ...(upper !== undefined ? { upperExclusive: maximum } : {}) });
  }} /><datalist id={list}>{Object.entries({ USD: 'US dollar', EUR: 'Euro', GBP: 'British pound', CAD: 'Canadian dollar', AUD: 'Australian dollar', JPY: 'Japanese yen', KWD: 'Kuwaiti dinar' }).map(([code, name]) => <option key={code} value={code}>{name}</option>)}</datalist>{error && <span role="alert">{error}</span>}</label>;
}
export function ConditionFields({ condition, change }: { condition?: AlertCondition | null; change: (value: AlertCondition | null) => void }) {
  return <fieldset><legend>When should this design run?</legend><label>Condition<select aria-label="Alert condition" value={condition?.operator ?? 'any'} onChange={e => change(e.target.value === 'any' ? null : { ...(condition ?? { unit: 'quantity', value: '1' }), operator: e.target.value, upperExclusive: e.target.value === 'range' ? String(BigInt(condition?.value ?? 1) + 1n) : null })}><option value="any">Every matching event</option><option value="exact">Exactly</option><option value="minimum">At least</option><option value="range">Between (upper limit excluded)</option><option value="multiple">Every multiple of</option></select></label>
    {condition && <><label>Measure<select aria-label="Alert measure" value={condition.unit} onChange={e => change({ ...condition, unit: e.target.value, currency: e.target.value === 'native-money' ? 'USD' : null, minorUnitDigits: e.target.value === 'native-money' ? 2 : null })}><option value="quantity">Quantity (Bits, gifts, subscriptions)</option><option value="native-money">Reported money amount</option></select></label>
      {condition.unit === 'native-money' && <><CurrencyField label="Alert currency" currency={condition.currency ?? 'USD'} digits={condition.minorUnitDigits ?? 2} value={condition.value} upper={condition.upperExclusive} change={patch => change({ ...condition, ...patch })} /><details><summary>Currency scale</summary><label>Decimal places<input aria-label="Alert decimal places" type="number" min={0} max={4} value={condition.minorUnitDigits ?? 2} onChange={e => { const n = e.target.valueAsNumber; if (Number.isInteger(n) && n >= 0 && n <= 4) change({ ...condition, minorUnitDigits: n }); }} /></label></details></>}
      <AmountField label={condition.unit === 'native-money' ? 'Alert amount' : 'Alert quantity'} value={condition.value} digits={condition.unit === 'native-money' ? condition.minorUnitDigits ?? 2 : 0} change={value => change({ ...condition, value })} />
      {condition.operator === 'range' && <AmountField label="Alert upper limit (excluded)" value={condition.upperExclusive ?? '2'} digits={condition.unit === 'native-money' ? condition.minorUnitDigits ?? 2 : 0} change={upperExclusive => change({ ...condition, upperExclusive })} />}
      <p>Missing or unverified support facts never satisfy this condition. Amounts use the reported currency, not estimated supporter totals.</p></>}
  </fieldset>;
}

import { useEffect, useState, type ReactNode } from 'react';
import type { Widget } from '../../overlay-runtime/src/scene';
import { AmountField, ConditionFields, eventLabels, platformLabels, integerToDecimal, TriggerSelect } from './TriggerControls';
import type { AlertSample } from './alertPreview';
const steps = ['Trigger', 'Conditions', 'Design', 'Test', 'Finish'];
export function AlertEditor({ widget, change, design, advanced, test, results = [] }: { widget: Widget; change: (w: Widget) => void; design: ReactNode; advanced: ReactNode; test?: (sample: AlertSample) => void; results?: { id: string; name: string; reason: string | null }[] }) {
  const [step, setStep] = useState(0); const a = widget.alert;
  const [sampleQuantity, setSampleQuantity] = useState(() => String(a.condition?.unit === 'quantity' ? a.condition.value : 1));
  const [sampleAmount, setSampleAmount] = useState(() => String(a.condition?.unit === 'native-money' ? a.condition.value : 500));
  const [tested, setTested] = useState(false);
  const digits = a.condition?.minorUnitDigits ?? 2, currency = a.condition?.currency ?? 'USD';
  useEffect(() => {
    setSampleQuantity(String(a.condition?.unit === 'quantity' ? a.condition.value : 1));
    setSampleAmount(String(a.condition?.unit === 'native-money' ? a.condition.value : 500));
  }, [a.condition?.unit, a.condition?.value]);
  useEffect(() => { setTested(false); }, [a.eventTypes.join(','), a.platforms.join(','), a.nativeType, a.customTriggerKey, a.condition?.unit, a.condition?.operator, a.condition?.value, a.condition?.upperExclusive, currency, digits]);
  return <section className="alert-editor" aria-label="Guided alert design"><nav aria-label="Alert setup steps">{steps.map((label, i) => <button key={label} aria-current={step === i ? 'step' : undefined} onClick={() => setStep(i)}>{i + 1}. {label}</button>)}</nav>
    <p className="field-help">Changes save automatically. You can revisit any step.</p>
    <div hidden={step !== 0}><h3>Choose what starts this alert</h3><TriggerSelect alert={a} change={patch => change({ ...widget, alert: { ...a, ...patch } })} /><details><summary>Advanced trigger filters</summary>{advanced}</details></div>
    <div hidden={step !== 1}><ConditionFields condition={a.condition} change={condition => change({ ...widget, alert: { ...a, condition } })} /></div>
    <div hidden={step !== 2}><h3>Make it yours</h3>{design}</div>
    <div hidden={step !== 3}><h3>Try your design</h3><p>The canvas uses sample data. Isolated tests never write supporter totals or run live actions.</p>
      {a.eventTypes.some(type => type.startsWith('support.')) && <><AmountField label="Test quantity" value={sampleQuantity} digits={0} change={value => { setSampleQuantity(value); setTested(false); }} /><AmountField label={`Test amount (${currency})`} value={sampleAmount} digits={digits} change={value => { setSampleAmount(value); setTested(false); }} /></>}
      <button onClick={() => { test?.({ type: a.eventTypes[0], platform: a.platforms[0], nativeType: a.nativeType ?? undefined, customTriggerKey: a.customTriggerKey ?? undefined, ...(a.eventTypes.some(type => type.startsWith('support.')) ? { quantity: sampleQuantity, amount: sampleAmount, currency, digits } : {}) }); setTested(true); }} disabled={!test}>Test this design with sample data</button>
      <ul aria-label="Guided matching results">{tested && results.map(result => <li key={result.id}>{result.name}: {result.reason ?? 'Matches this event'}</li>)}</ul>
      <p>Test events below the canvas also lets you compare other triggers and currencies in the saved preview.</p></div>
    <div hidden={step !== 4}><h3>Your alert is ready</h3><p>{a.platforms.map(p => platformLabels[p] ?? p).join(', ')} · {a.eventTypes.map(t => eventLabels[t] ?? t).join(', ')}</p><p>{a.condition ? `${({ exact: 'Exactly', minimum: 'At least', range: 'From', multiple: 'Each multiple of' } as Record<string, string>)[a.condition.operator] ?? a.condition.operator} ${integerToDecimal(a.condition.value, a.condition.unit === 'native-money' ? a.condition.minorUnitDigits ?? 2 : 0)} ${a.condition.unit === 'native-money' ? a.condition.currency : 'items'}${a.condition.operator === 'range' ? `, up to ${integerToDecimal(a.condition.upperExclusive ?? '0', a.condition.unit === 'native-money' ? a.condition.minorUnitDigits ?? 2 : 0)} (excluded)` : ''}` : 'Every matching event'} · {a.durationMs / 1000} seconds</p><p>Use Copy OBS URL after saving. Add alert designs to an ordered alert set to choose one design per event.</p></div>
    <div className="step-actions"><button disabled={step === 0} onClick={() => setStep(step - 1)}>Previous alert step</button><button disabled={step === 4} onClick={() => setStep(step + 1)}>Next alert step</button></div>
  </section>;
}

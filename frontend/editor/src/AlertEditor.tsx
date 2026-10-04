import { useState, type ReactNode } from 'react';
import type { Widget } from '../../overlay-runtime/src/scene';
import { ConditionFields, eventLabels, platformLabels, integerToDecimal, TriggerSelect } from './TriggerControls';
const steps = ['Trigger', 'Conditions', 'Design', 'Test', 'Finish'];
export function AlertEditor({ widget, change, design, advanced, test }: { widget: Widget; change: (w: Widget) => void; design: ReactNode; advanced: ReactNode; test?: () => void }) {
  const [step, setStep] = useState(0); const a = widget.alert;
  return <section className="alert-editor" aria-label="Guided alert design"><nav aria-label="Alert setup steps">{steps.map((label, i) => <button key={label} aria-current={step === i ? 'step' : undefined} onClick={() => setStep(i)}>{i + 1}. {label}</button>)}</nav>
    <p className="field-help">Changes save automatically. You can revisit any step.</p>
    <div hidden={step !== 0}><h3>Choose what starts this alert</h3><TriggerSelect alert={a} change={patch => change({ ...widget, alert: { ...a, ...patch } })} /><details><summary>Advanced trigger filters</summary>{advanced}</details></div>
    <div hidden={step !== 1}><ConditionFields condition={a.condition} change={condition => change({ ...widget, alert: { ...a, condition } })} /></div>
    <div hidden={step !== 2}><h3>Make it yours</h3>{design}</div>
    <div hidden={step !== 3}><h3>Try your design</h3><p>The canvas uses sample data. Isolated tests never write supporter totals or run live actions.</p><button onClick={test} disabled={!test}>Test this design with sample data</button><p>Use Test events below the canvas to check which designs match a particular amount or trigger.</p></div>
    <div hidden={step !== 4}><h3>Your alert is ready</h3><p>{a.platforms.map(p => platformLabels[p] ?? p).join(', ')} · {a.eventTypes.map(t => eventLabels[t] ?? t).join(', ')}</p><p>{a.condition ? `${({ exact: 'Exactly', minimum: 'At least', range: 'From', multiple: 'Each multiple of' } as Record<string, string>)[a.condition.operator] ?? a.condition.operator} ${integerToDecimal(a.condition.value, a.condition.unit === 'native-money' ? a.condition.minorUnitDigits ?? 2 : 0)} ${a.condition.unit === 'native-money' ? a.condition.currency : 'items'}${a.condition.operator === 'range' ? `, up to ${integerToDecimal(a.condition.upperExclusive ?? '0', a.condition.unit === 'native-money' ? a.condition.minorUnitDigits ?? 2 : 0)} (excluded)` : ''}` : 'Every matching event'} · {a.durationMs / 1000} seconds</p><p>Use Copy OBS URL after saving. Add alert designs to an ordered alert set to choose one design per event.</p></div>
    <div className="step-actions"><button disabled={step === 0} onClick={() => setStep(step - 1)}>Previous alert step</button><button disabled={step === 4} onClick={() => setStep(step + 1)}>Next alert step</button></div>
  </section>;
}

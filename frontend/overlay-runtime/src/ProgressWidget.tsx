import { defaultProgress, type Widget } from './scene';
import { donorMoney } from './DonorWidget';
import type { DonorSnapshot } from './DonorWidget';

export function ProgressWidget({ widget, snapshot }: { widget: Widget; snapshot?: DonorSnapshot }) {
  const settings = widget.progress ?? defaultProgress;
  const manual = settings.source === 'manual';
  const ledgerMinor = BigInt(snapshot?.totalUsdMinor ?? '0');
  const targetMinor = BigInt(Math.max(1, Math.round(settings.target * 100)));
  const amount = manual ? settings.value : Number(ledgerMinor < targetMinor ? ledgerMinor : targetMinor) / 100;
  const value = Number.isFinite(amount) ? Math.max(0, amount) : 0;
  const percent = Math.min(100, Math.max(0, value / settings.target * 100));
  const vertical = settings.orientation === 'vertical';
  const formatted = (n: number) => manual ? n.toLocaleString() : n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
  const state = !manual && (!snapshot || !['ready', 'empty'].includes(snapshot.state)) ? snapshot?.state ?? 'Connecting' : '';
  return <div className="progress-widget" style={{ width: '100%', height: '100%' }}>
    <div>{settings.label}{settings.showValue && ` · ${manual ? formatted(value) : donorMoney(snapshot?.totalUsdMinor ?? '0')} / ${formatted(settings.target)}`}{settings.showPercent && ` · ${percent.toFixed(1)}%`}</div>
    <div role="progressbar" aria-label={settings.label || widget.name} aria-valuemin={0} aria-valuemax={settings.target} aria-valuenow={Math.min(value, settings.target)}
      style={{ background: settings.trackColor, height: vertical ? '70%' : 24, width: vertical ? 24 : '100%', position: 'relative', overflow: 'hidden' }}>
      <div style={{ background: settings.fillColor, position: 'absolute', bottom: 0, left: 0, width: vertical ? '100%' : `${percent}%`, height: vertical ? `${percent}%` : '100%' }} />
    </div>
    {!manual && <small>{state}{snapshot?.estimatedCount ? ' · includes estimates' : ''}{snapshot?.unknownCount ? ' · pending values excluded' : ''}</small>}
  </div>;
}

import type { Scene, Widget } from '../../overlay-runtime/src/scene';
export interface AlertSample { type: string; platform: string; nativeType?: string; customTriggerKey?: string; quantity?: string; amount?: string; currency?: string; digits?: number; gated?: boolean; giftRole?: string }
export function alertReason(w: Widget, sample: AlertSample): string | null {
  const a = w.alert;
  if (w.hidden) return 'Design is hidden';
  if (!a.eventTypes.includes('*') && !a.eventTypes.includes(sample.type)) return 'Different event';
  if (!a.platforms.includes(sample.platform)) return 'Different platform';
  if (a.nativeType && a.nativeType !== sample.nativeType) return 'Different incoming event';
  if (a.customTriggerKey && a.customTriggerKey !== sample.customTriggerKey) return 'Different custom trigger';
  const c = a.condition; if (!c) return null;
  if (sample.gated || sample.giftRole === 'recipient') return 'Support facts are not eligible';
  if (c.unit === 'native-money' && (c.currency !== sample.currency || c.minorUnitDigits !== sample.digits)) return 'Different currency or scale';
  const text = c.unit === 'native-money' ? sample.amount : sample.quantity;
  if (text === undefined || !/^\d+$/.test(text) || BigInt(text) > 9223372036854775807n) return 'Missing valid support facts';
  const value = BigInt(text), lower = BigInt(c.value);
  const match = c.operator === 'exact' ? value === lower : c.operator === 'minimum' ? value >= lower : c.operator === 'range' ? value >= lower && c.upperExclusive != null && value < BigInt(c.upperExclusive) : c.operator === 'multiple' && lower > 0n && value > 0n && value % lower === 0n;
  return match ? null : 'Amount or quantity does not match';
}
export function selectAlertDesigns(scene: Scene, sample: AlertSample) {
  const results = scene.widgets.filter(w => w.kind === 'alert').map(w => ({ id: w.id, name: w.name, reason: alertReason(w, sample) }));
  for (const set of scene.alertSets ?? []) if (set.selection === 'first') {
    const winner = set.widgetIds.find(id => results.some(r => r.id === id && r.reason === null));
    for (const result of results) if (set.widgetIds.includes(result.id) && result.id !== winner && result.reason === null) result.reason = 'An earlier design in this set matched';
  }
  return results;
}

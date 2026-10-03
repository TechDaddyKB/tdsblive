import type { EditorAsset } from './WidgetProperties';

export type FieldType = 'text' | 'textarea' | 'number' | 'slider' | 'checkbox' | 'dropdown' | 'multiselect' | 'color' | 'font' | 'image' | 'audio' | 'video' | 'duration' | 'event' | 'action' | 'user' | 'platform' | 'button' | 'hidden' | 'group';
export interface SettingsField {
  key: string; label: string; type: FieldType; min?: number; max?: number; step?: number; maxLength?: number;
  options?: { value: string; label: string }[]; multiple?: boolean; children?: SettingsField[];
}
export type SettingValue = string | number | boolean | string[] | null;
export interface SettingsContext {
  assets?: EditorAsset[]; events?: string[]; actions?: { id: string; name: string }[]; users?: string[]; platforms?: string[];
  button?: (key: string) => void;
}
export function SettingsFields({ fields, values, change, context = {} }: {
  fields: SettingsField[]; values: Record<string, SettingValue>; change: (key: string, value: SettingValue) => void; context?: SettingsContext;
}) {
  return <>{fields.map(field => {
    const value = values[field.key];
    const { key, label, type } = field;
    if (type === 'hidden') return null;
    if (type === 'group') return <fieldset key={key}><legend>{label}</legend><SettingsFields fields={field.children ?? []} values={values} change={change} context={context} /></fieldset>;
    if (type === 'button') return <button key={key} type="button" onClick={() => context.button?.(key)}>{label}</button>;
    if (type === 'checkbox') return <label key={key}><input type="checkbox" checked={value === true} onChange={e => change(key, e.target.checked)} />{label}</label>;
    const text = typeof value === 'string' ? value : '';
    if (type === 'textarea') return <label key={key}>{label}<textarea aria-label={label} maxLength={field.maxLength ?? 4096} value={text} onChange={e => change(key, e.target.value)} /></label>;
    if (['number', 'slider', 'duration'].includes(type)) return <label key={key}>{label}<input aria-label={label} type={type === 'slider' ? 'range' : 'number'}
      min={field.min} max={field.max} step={field.step ?? 1} value={typeof value === 'number' ? value : 0} onChange={e => {
        const next = e.target.valueAsNumber;
        if (Number.isFinite(next) && (field.min === undefined || next >= field.min) && (field.max === undefined || next <= field.max)) change(key, next);
      }} /></label>;
    const assetType = ['image', 'audio', 'video'].includes(type);
    const options = assetType ? (context.assets ?? []).filter(a => a.mime.startsWith(`${type}/`)).map(a => ({ value: a.id, label: a.filename })) :
      type === 'event' ? (context.events ?? []).map(v => ({ value: v, label: v })) :
      type === 'action' ? (context.actions ?? []).map(a => ({ value: a.id, label: a.name })) :
      type === 'platform' ? (context.platforms ?? ['twitch', 'youtube', 'kick', 'rumble', 'kofi', 'general', 'custom']).map(v => ({ value: v, label: v })) :
      type === 'user' ? (context.users ?? []).map(v => ({ value: v, label: v })) : field.options;
    if (options) {
      const multiple = type === 'multiselect' || field.multiple === true;
      // Keep configured stable IDs visible when an integration is temporarily disconnected.
      const configured = Array.isArray(value) ? value : text ? [text] : [];
      const available = [...options, ...configured.filter(v => !options.some(o => o.value === v)).map(v => ({ value: v, label: `${v} (configured)` }))];
      return <label key={key}>{label}<select aria-label={label} multiple={multiple} value={multiple ? configured : text} onChange={e => change(key,
        multiple ? Array.from(e.target.selectedOptions, o => o.value) : assetType && !e.target.value ? null : e.target.value)}>
        {!multiple && <option value="">Choose…</option>}{available.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select></label>;
    }
    return <label key={key}>{label}<input aria-label={label} type={type === 'color' ? 'color' : 'text'} maxLength={field.maxLength ?? 128} value={text}
      onChange={e => { if (type !== 'font' || /^[\w -]{0,64}$/.test(e.target.value)) change(key, e.target.value); }} /></label>;
  })}</>;
}

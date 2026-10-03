import { SettingsFields, type SettingsField, type SettingValue } from './SettingsFields';
import { defaultEventList, defaultProgress, type Widget } from '../../overlay-runtime/src/scene';
import { alertPresets } from '../../overlay-runtime/src/scene';
import { DonorProperties } from './DonorProperties';
import type { EditorAsset } from './WidgetProperties';

const eventFields: SettingsField[] = [
  { key: 'filters', label: 'Event list filters', type: 'group', children: [
    { key: 'eventTypes', label: 'List event types', type: 'event', multiple: true },
    { key: 'platforms', label: 'List platforms (empty means all)', type: 'platform', multiple: true },
    { key: 'ignoredUsers', label: 'Ignored users (one per line)', type: 'textarea' },
  ] },
  { key: 'count', label: 'Maximum list entries', type: 'number', min: 1, max: 100 },
  { key: 'durationMs', label: 'Entry duration (ms)', type: 'duration', min: 100, max: 86400000 },
  { key: 'persistent', label: 'Keep event entries', type: 'checkbox' },
  { key: 'newestOnTop', label: 'Newest event first', type: 'checkbox' },
  { key: 'template', label: 'Event entry template', type: 'textarea' },
  { key: 'font', label: 'Event list font', type: 'font' },
];
const progressFields: SettingsField[] = [
  { key: 'source', label: 'Progress source', type: 'dropdown', options: [{ value: 'manual', label: 'Manual value' }, { value: 'ledger-usd', label: 'Supporter total (USD)' }] },
  { key: 'label', label: 'Progress label', type: 'text', maxLength: 256 },
  { key: 'value', label: 'Current value', type: 'number', min: 0, max: 1e12, step: .01 },
  { key: 'target', label: 'Target value', type: 'number', min: .01, max: 1e12, step: .01 },
  { key: 'fillColor', label: 'Progress fill', type: 'color' }, { key: 'trackColor', label: 'Progress track', type: 'color' },
  { key: 'showValue', label: 'Show progress values', type: 'checkbox' }, { key: 'showPercent', label: 'Show progress percent', type: 'checkbox' },
  { key: 'orientation', label: 'Progress orientation', type: 'dropdown', options: [{ value: 'horizontal', label: 'Horizontal' }, { value: 'vertical', label: 'Vertical' }] },
  { key: 'reset', label: 'Reset manual progress', type: 'button' },
];
export function AdvancedWidgetProperties({ widget, assets, change }: { widget: Widget; assets: EditorAsset[]; change: (w: Widget) => void }) {
  if (widget.kind === 'event-list') {
    const settings = widget.eventList ?? defaultEventList;
    return <><SettingsFields fields={eventFields} values={{ ...settings, ignoredUsers: settings.ignoredUsers.join('\n') }} context={{ events: ['*', ...new Set(alertPresets.map(p => p[1])), 'chat.message'] }}
      change={(key, value) => change({ ...widget, eventList: { ...settings, [key]: key === 'ignoredUsers' ? String(value).split('\n').map(v => v.trim()).filter(Boolean) : value } })} />
      <p>Placeholders: {'{user}, {type}, {platform}, {message}'}. Empty platform selection includes all platforms.</p></>;
  }
  const settings = widget.progress ?? defaultProgress;
  const update = (key: string, value: SettingValue) => change({ ...widget, progress: { ...settings, [key]: value } });
  return <><SettingsFields fields={progressFields} values={{ ...settings }} change={update} context={{ button: () => update('value', 0) }} />
    {settings.source === 'ledger-usd' && <><p>The target uses USD. Pending values are excluded; estimates remain marked. Preview does not read production totals.</p><DonorProperties widget={widget} assets={assets} change={change} /></>}
  </>;
}

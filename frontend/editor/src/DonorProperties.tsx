import { defaultDonor, type DonorSettings, type Widget } from '../../overlay-runtime/src/scene';
import type { EditorAsset } from './WidgetProperties';

export function DonorProperties({ widget, assets, change }: { widget: Widget; assets: EditorAsset[]; change: (widget: Widget) => void }) {
  const settings = widget.donor ?? defaultDonor;
  const update = (patch: Partial<DonorSettings>) => change({ ...widget, donor: { ...settings, ...patch } });
  const current = widget.kind.startsWith('current-stream-');
  return <fieldset><legend>Supporter settings</legend>
    <label>Period<select aria-label="Donor period" value={current ? 'current-stream' : settings.period} disabled={current} onChange={e => update({ period: e.target.value })}>
      {['all-time', 'current-stream', 'today', 'week', 'month', 'year', 'custom'].map(period => <option key={period}>{period}</option>)}
    </select></label>
    {settings.period === 'custom' && !current && <>
      <label>Start date<input type="date" value={settings.customStart ?? ''} onChange={e => update({ customStart: e.target.value || null })} /></label>
      <label>End date (exclusive)<input type="date" value={settings.customEndExclusive ?? ''} onChange={e => update({ customEndExclusive: e.target.value || null })} /></label>
    </>}
    <p>Periods use your financial timezone. Current stream requires a saved stream start.</p>
    <label>Platforms<input aria-label="Donor platforms" value={settings.platforms.join(',')} onChange={e => update({ platforms: e.target.value.split(',').map(p => p.trim()).filter(Boolean) })} /></label>
    <label>Support kinds<input aria-label="Donor support kinds" value={settings.eventTypes.join(',')} onChange={e => update({ eventTypes: e.target.value.split(',').map(p => p.trim()).filter(Boolean) })} /></label>
    <p>Leave filters empty for all. Kinds: donation, bits, subscription, membership, gift, rant.</p>
    <label>Minimum USD cents<input aria-label="Minimum USD cents" inputMode="numeric" value={settings.minimumUsdMinor} onChange={e => {
      const value = e.target.value; if (/^\d+$/.test(value) && BigInt(value) <= 9223372036854775807n) update({ minimumUsdMinor: value });
    }} /></label>
    {widget.kind === 'donor-leaderboard' && <label>Ranked supporters<input aria-label="Ranked supporters" type="number" min={1} max={25} value={settings.count} onChange={e => {
      const value = e.target.valueAsNumber; if (Number.isInteger(value) && value >= 1 && value <= 25) update({ count: value });
    }} /></label>}
    {(['showName', 'showAvatar', 'showPlatformBadges', 'showAmount', 'showCrown'] as const).map(key => <label key={key}><input type="checkbox" checked={settings[key]} onChange={e => update({ [key]: e.target.checked })} />{({ showName: 'Show name', showAvatar: 'Show avatar', showPlatformBadges: 'Show platform badges', showAmount: 'Show amount', showCrown: 'Show crown' })[key]}</label>)}
    <label>Donor template<textarea aria-label="Donor template" maxLength={4096} value={settings.template} onChange={e => update({ template: e.target.value })} /></label>
    <p>Placeholders: {'{name}, {amount}, {rank}'}</p>
    <label>Font family<input aria-label="Donor font family" maxLength={128} value={settings.fontFamily} onChange={e => update({ fontFamily: e.target.value })} /></label>
    <label>Leader animation<select aria-label="Leader animation" value={settings.animation} onChange={e => update({ animation: e.target.value as DonorSettings['animation'] })}><option>none</option><option>fade</option><option>slide</option></select></label>
    <label>Transition milliseconds<input aria-label="Transition milliseconds" type="number" min={0} max={10000} value={settings.transitionMs} onChange={e => {
      const value = e.target.valueAsNumber; if (Number.isInteger(value) && value >= 0 && value <= 10000) update({ transitionMs: value });
    }} /></label>
    {(['crownAssetId', 'fontAssetId'] as const).map(key => <label key={key}>{key === 'crownAssetId' ? 'Crown image' : 'Font asset'}<select value={settings[key] ?? ''} onChange={e => update({ [key]: e.target.value || null })}><option value="">Default</option>{assets.filter(asset => asset.mime.startsWith(key === 'crownAssetId' ? 'image/' : 'font/')).map(asset => <option key={asset.id} value={asset.id}>{asset.filename}</option>)}</select></label>)}
  </fieldset>;
}

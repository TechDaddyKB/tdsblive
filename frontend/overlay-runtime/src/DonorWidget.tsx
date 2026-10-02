import { defaultDonor, type DonorSettings, type Widget } from './scene';
import { useDonorAsset, useDonorFont } from './DonorAssets';
import { useEffect, useRef, useState } from 'react';
import { safeAvatar } from './chat';
export interface DonorRow { supporterId: string; name: string; usdAmountMinor: string; platforms: string[]; unknownCount: number; estimatedCount: number; latestAt: string; hasKnownAmount?: boolean; avatarUrl?: string | null }
export interface DonorSnapshot { widgetId: string; state: string; generatedAt: string; rows: DonorRow[]; totalUsdMinor: string; unknownCount: number; gatedCount: number; estimatedCount: number }
export function donorMoney(minor: string): string {
  if (!/^-?\d+$/.test(minor)) return 'Unknown';
  const value = BigInt(minor); const magnitude = value < 0n ? -value : value;
  return `${value < 0n ? '-' : ''}$${(magnitude / 100n).toLocaleString('en-US')}.${String(magnitude % 100n).padStart(2, '0')}`;
}
function donorText(settings: DonorSettings, name: string, amount: string, rank = ''): string {
  const values: Record<string, string> = { name: settings.showName ? name : '', amount: settings.showAmount ? amount : '', rank };
  return settings.template.replace(/\{(name|amount|rank)\}/g, (_, key: string) => values[key]);
}
export function DonorWidget({ widget, snapshot, overlay = '', token = '' }: { widget: Widget; snapshot?: DonorSnapshot; overlay?: string; token?: string }) {
  const settings = widget.donor ?? defaultDonor;
  const crown = useDonorAsset(settings.crownAssetId, 'image/', overlay, token);
  const font = useDonorAsset(settings.fontAssetId, 'font/', overlay, token);
  const fontFamily = useDonorFont(font, widget.id);
  const [displayed, setDisplayed] = useState(snapshot);
  const [leaving, setLeaving] = useState(false);
  const latest = useRef(snapshot);
  const transition = useRef<{ timer?: ReturnType<typeof setTimeout>; target?: string }>({});
  useEffect(() => {
    latest.current = snapshot;
    const oldLeader = displayed?.rows[0]?.supporterId; const newLeader = snapshot?.rows[0]?.supporterId;
    if (settings.animation !== 'none' && settings.transitionMs > 0 && displayed?.state === 'ready' && snapshot?.state === 'ready' && oldLeader !== newLeader) {
      if (transition.current.timer !== undefined && transition.current.target === newLeader) return;
      clearTimeout(transition.current.timer);
      setLeaving(true);
      transition.current.target = newLeader;
      transition.current.timer = setTimeout(() => {
        transition.current.timer = undefined; setDisplayed(latest.current); setLeaving(false);
      }, settings.transitionMs);
      return;
    }
    clearTimeout(transition.current.timer); transition.current.timer = undefined;
    setDisplayed(snapshot); setLeaving(false);
  }, [snapshot, settings.animation, settings.transitionMs, displayed]);
  useEffect(() => () => clearTimeout(transition.current.timer), []);
  if (!snapshot) return <output>Connecting supporter totals…</output>;
  if (snapshot.state !== 'ready') return <output>{({ preview: 'Test preview · no production totals', pending: 'Support awaiting valuation', gated: 'Support awaiting evidence', empty: 'No matching support', 'period-unavailable': 'Set current stream start in financial settings' } as Record<string, string>)[snapshot.state] ?? 'Support totals unavailable'}</output>;
  return <div className="donor-widget" style={{ fontFamily: fontFamily ?? settings.fontFamily }}>
    {widget.kind === 'current-stream-total' ? <span>{donorText(settings, 'Current stream', donorMoney(snapshot.totalUsdMinor))}</span> : (displayed ?? snapshot).rows.map((row, index) => <div key={row.supporterId} className={`donor-row ${leaving ? 'exit' : 'enter'}-${settings.animation}`} style={{ animationDuration: `${settings.transitionMs}ms` }}>
      {widget.kind === 'donor-leaderboard' && <span>{index + 1}. </span>}
      {settings.showAvatar && (safeAvatar(row.avatarUrl) ? <img className="donor-avatar" src={safeAvatar(row.avatarUrl)} alt={`${row.name} avatar`} referrerPolicy="no-referrer" /> : <span className="donor-avatar-placeholder" aria-label="Avatar unavailable">●</span>)}
      {settings.showCrown && index === 0 && <span aria-label="Crown">{crown ? <img className="donor-crown" src={crown} alt="" /> : '👑 '}</span>}
      <span>{donorText(settings, row.name, row.hasKnownAmount === false ? 'Awaiting valuation' : donorMoney(row.usdAmountMinor), String(index + 1))}</span>
      {settings.showPlatformBadges && row.platforms.map(platform => <span className="donor-platform" key={platform}>{platform}</span>)}
      {row.estimatedCount > 0 && <small>Estimated</small>}
      {row.unknownCount > 0 && <small>Additional support awaiting valuation</small>}
    </div>)}
    {snapshot.unknownCount > 0 && <small>Unvalued support excluded from amounts</small>}
    {snapshot.gatedCount > 0 && <small>Unverified support excluded</small>}
    {widget.kind === 'current-stream-total' && snapshot.estimatedCount > 0 && <small>Includes estimates</small>}
  </div>;
}

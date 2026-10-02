import { useEffect, useState } from 'react';
import { automation, type AutomationAction, type AutomationAudioAsset, type AutomationSoundOverlay } from './automationApi';

export function AutomationSoundPicker({ action, change }: Readonly<{
  action: AutomationAction; change: (patch: Partial<AutomationAction>) => void;
}>) {
  const [assets, setAssets] = useState<AutomationAudioAsset[]>([]);
  const [overlays, setOverlays] = useState<AutomationSoundOverlay[]>([]);
  const [status, setStatus] = useState('Loading audio and overlays…');
  const [refresh, setRefresh] = useState(0);
  const selected = action.soundAssetIds ?? [];
  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([automation.soundAssets(controller.signal), automation.soundOverlays(controller.signal)]).then(([library, canvases]) => {
      if (controller.signal.aborted) return;
      setAssets(library.filter(asset => asset.mime.startsWith('audio/')));
      setOverlays(canvases.filter(overlay => overlay.canvasEnabled));
      setStatus('Choose imported audio. Multiple selections provide random variants.');
    }).catch(() => { if (!controller.signal.aborted) setStatus('Audio and overlays unavailable. Refresh to retry.'); });
    return () => controller.abort();
  }, [refresh]);
  const missingOverlay = action.overlayId && !overlays.some(overlay => overlay.id === action.overlayId);
  const missingAssets = selected.filter(id => !assets.some(asset => asset.id === id));
  function choose(id: string, checked: boolean) {
    change({ soundAssetIds: checked ? [...selected, id] : selected.filter(value => value !== id) });
  }
  return <fieldset><legend>Sound target and audio</legend>
    <output aria-live="polite">{status}</output>
    <button type="button" onClick={() => setRefresh(value => value + 1)}>Refresh audio and overlays</button>
    <label>Target canvas overlay <select required value={action.overlayId ?? ''} onChange={event => change({ overlayId: event.target.value })}>
      <option value="">Select a canvas overlay</option>
      {missingOverlay && <option value={action.overlayId ?? ''} disabled>Unavailable overlay: {action.overlayId}</option>}
      {overlays.map(overlay => <option key={overlay.id} value={overlay.id}>{overlay.name}</option>)}
    </select></label>
    {!overlays.length && <p>Create a canvas overlay in the visual editor, then refresh.</p>}
    {!assets.length && <p>Import audio in the asset library, then refresh.</p>}
    {assets.map(asset => <label key={asset.id}><input type="checkbox" checked={selected.includes(asset.id)}
      disabled={!selected.includes(asset.id) && selected.length >= 32} onChange={event => choose(asset.id, event.target.checked)} />Use audio: {asset.filename}</label>)}
    {missingAssets.map(id => <label key={id}><input type="checkbox" checked onChange={() => choose(id, false)} />Unavailable audio: {id}</label>)}
    <p>{selected.length} audio variants selected (maximum 32).</p>
  </fieldset>;
}

import type { AutomationAction } from './automationApi';
import type { Discovery } from './api';

export function AutomationActionSettings({ action, change, discovery }: Readonly<{ action: AutomationAction; change: (patch: Partial<AutomationAction>) => void; discovery: Discovery | null }>) {
  const speech = action.speech;
  return <>
    {action.kind === 'speech' && <fieldset><legend>Speech safety and moderation</legend>
      <label>Maximum characters <input type="number" min={1} max={2000} value={speech?.maximumCharacters ?? 300} onChange={event => change({ speech: { ...speech, maximumCharacters: event.target.value } })} /></label>
      <label>Maximum repeated characters <input type="number" min={1} max={20} value={speech?.maximumRepeatedCharacters ?? 3} onChange={event => change({ speech: { ...speech, maximumRepeatedCharacters: event.target.value } })} /></label>
      <label>Maximum punctuation run <input type="number" min={1} max={20} value={speech?.maximumPunctuationRun ?? 3} onChange={event => change({ speech: { ...speech, maximumPunctuationRun: event.target.value } })} /></label>
      {(['speakUsername', 'speakAmount', 'speakMessage', 'stripUrls', 'ignoreAnonymousMessage', 'speakerBadWordFilter', 'manualModeration'] as const).map(key => {
        const labels = { speakUsername: 'Speak username', speakAmount: 'Speak amount', speakMessage: 'Speak message', stripUrls: 'Strip URLs',
          ignoreAnonymousMessage: 'Ignore anonymous message', speakerBadWordFilter: 'Speaker.bot bad-word filter', manualModeration: 'Require manual moderation' };
        return <label key={key}><input type="checkbox" checked={speech?.[key] ?? key !== 'manualModeration'} onChange={event => change({ speech: { ...speech, [key]: event.target.checked } })} />{labels[key]}</label>;
      })}
      <label>Blocked words (one per line) <textarea value={speech?.blockedWords?.join('\n') ?? ''} onChange={event => change({ speech: { ...speech, blockedWords: event.target.value.split('\n').map(word => word.trim()).filter(Boolean) } })} /></label>
      <label>Allowed languages (one per line) <textarea value={speech?.allowedLanguages?.join('\n') ?? ''} onChange={event => change({ speech: { ...speech, allowedLanguages: event.target.value.split(/\s+/).filter(Boolean) } })} /></label>
      <p>Restricted languages require verified metadata or moderator review. Missing language information cannot authorize speech.</p>
      <p>Speaker.bot owns its speech queue. These queue policies control pending TDSBLive dispatches; they do not stop speech already accepted by Speaker.bot.</p>
    </fieldset>}
    {action.kind === 'sound' && <fieldset><legend>Sound playback</legend>
      <label>Sound volume (0–1) <input type="number" min={0} max={1} step="0.05" value={action.volume ?? 1} onChange={event => change({ volume: event.target.value })} /></label>
      <label>Ducking volume metadata (0–1) <input type="number" min={0} max={1} step="0.05" value={action.duckingVolume ?? 1} onChange={event => change({ duckingVolume: event.target.value })} /></label>
      <label>Playback timeout seconds <input type="number" min={1} max={3600} value={action.playbackTimeoutSeconds ?? 120} onChange={event => change({ playbackTimeoutSeconds: event.target.value })} /></label>
      <p>One configured audio variant is selected randomly. Playback occurs in the target OBS Browser Source.</p>
    </fieldset>}
    {action.kind === 'streamerbot' && <fieldset><legend>Temporary effect</legend>
      <label>Effect duration seconds (0 disables reversion) <input type="number" min={0} max={86400} value={action.durationSeconds ?? 0} onChange={event => change({ durationSeconds: event.target.value,
        ...(Number(event.target.value) === 0 ? { revertActionId: null } : {}) })} /></label>
      <label>Revert action <select disabled={Number(action.durationSeconds ?? 0) === 0} value={action.revertActionId ?? ''} onChange={event => change({ revertActionId: event.target.value || null })}>
        <option value="">Repeat the selected toggle</option>{discovery?.actions.map(value => <option key={value.id} value={value.id} disabled={!value.enabled}>{value.name}</option>)}</select></label>
      <label>Repeated effect policy <select value={action.stackPolicy ?? 'extend'} onChange={event => change({ stackPolicy: event.target.value })}>{['extend', 'restart', 'ignore', 'queue'].map(value => <option key={value}>{value}</option>)}</select></label>
      <p>Select and allow both actions in Streamer.bot settings. An acknowledgment confirms dispatch; verify the model effect separately.</p>
    </fieldset>}
  </>;
}

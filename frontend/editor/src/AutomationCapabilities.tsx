import { useEffect, useState } from 'react';
import { automation, type AutomationCapabilityReport } from './automationApi';

export function AutomationCapabilities() {
  const [report, setReport] = useState<AutomationCapabilityReport | null>(null);
  const [status, setStatus] = useState('Loading integration checks…');
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    void automation.capabilities(controller.signal).then(value => {
      if (!controller.signal.aborted) { setReport(value); setStatus('Read-only checks; no actions executed.'); }
    }).catch(() => { if (!controller.signal.aborted) setStatus('Integration checks unavailable. Refresh to retry.'); });
    return () => controller.abort();
  }, [refresh]);
  return <section aria-label="Automation integration checks">
    <h3>Integration checks</h3><p>{status}</p>
    <button type="button" onClick={() => { setStatus('Loading integration checks…'); setRefresh(value => value + 1); }}>Refresh integration checks</button>
    {report && <><p>Streamer.bot: {report.streamerBotState} · Speaker.bot: {report.speakerBotState}</p>
      <ul>{report.issues?.map((issue, index) => <li key={`${issue.ruleId}-${issue.actionId}-${issue.code}-${index}`}>
        {issue.message} <small>Rule {issue.ruleId}, action {issue.actionId}</small>
      </li>)}</ul></>}
  </section>;
}

import { useState } from 'react';
import { automation, type AutomationExecution } from './automationApi';

export function AutomationReceiptReview({ receipt, busy, operation }: Readonly<{
  receipt: AutomationExecution; busy: boolean;
  operation: (task: () => Promise<void>, message: string) => Promise<void>;
}>) {
  const [language, setLanguage] = useState('');
  let speech = ''; let languages: string[] = [];
  try {
    const payload: unknown = JSON.parse(receipt.json ?? '{}');
    if (payload && typeof payload === 'object' && 'speechText' in payload && typeof payload.speechText === 'string') speech = payload.speechText;
    if (payload && typeof payload === 'object' && 'action' in payload && payload.action && typeof payload.action === 'object' && 'speech' in payload.action) {
      const settings = payload.action.speech;
      if (settings && typeof settings === 'object' && 'allowedLanguages' in settings && Array.isArray(settings.allowedLanguages))
        languages = settings.allowedLanguages.filter((value): value is string => typeof value === 'string');
    }
  } catch { /* An unreadable receipt cannot authorize dispatch. */ }
  return <>
    <p aria-label="Prepared speech">{speech || 'No prepared speech available.'}</p>
    {receipt.state === 'language-review' && <>
      <label>Verified message language <select value={language} disabled={busy} onChange={event => setLanguage(event.target.value)}>
        <option value="">Select after reviewing the message</option>{languages.map(value => <option key={value} value={value}>{value}</option>)}
      </select></label>
      <button disabled={busy || !language} onClick={() => void operation(() => automation.language(receipt, language), 'Language verified')}>Verify language {receipt.id}</button>
    </>}
    {receipt.state === 'moderation-pending' && <button disabled={busy || !speech} onClick={() => void operation(() => automation.moderate(receipt, true), 'Approved')}>Approve {receipt.id}</button>}
    <button disabled={busy} onClick={() => void operation(() => automation.moderate(receipt, false), 'Rejected')}>Reject {receipt.id}</button>
  </>;
}

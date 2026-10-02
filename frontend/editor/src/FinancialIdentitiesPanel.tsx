import { useState } from 'react';
import { finance, type FinancialIdentity, type FinancialOperation } from './financialApi';

export function FinancialIdentitiesPanel({ identities, operation, busy }: { identities: FinancialIdentity[]; operation: FinancialOperation; busy: boolean }) {
  const [source, setSource] = useState('');
  const [target, setTarget] = useState('');
  const identity = identities.find(row => row.id === source);
  const supporters = [...new Map(identities.map(row => [row.supporterId, row])).values()];
  return <fieldset disabled={busy}>
    <legend>Supporter identities</legend>
    <p>Names do not automatically link people across platforms. Linking moves this identity and its historical contributions to the selected supporter; unlinking separates them again.</p>
    <label>Identity to move <select value={source} onChange={event => setSource(event.target.value)}><option value="">Choose identity</option>{identities.map(row => <option key={row.id} value={row.id}>{row.platform} · {row.displayName} · {row.identityKey} → {row.supporterName}</option>)}</select></label>
    <label>Link to supporter <select value={target} onChange={event => setTarget(event.target.value)}><option value="">Choose supporter</option>{supporters.map(row => <option key={row.supporterId} value={row.supporterId}>{row.supporterName} · {row.platform} · {row.identityKey}</option>)}</select></label>
    <button disabled={!identity || !target || target === identity.supporterId} onClick={() => identity && void operation(() => finance.link(identity, target), 'Identity linked; supporter totals refreshed.')}>Link identity</button>
    <button disabled={!identity} onClick={() => identity && void operation(() => finance.unlink(identity), 'Identity separated; supporter totals refreshed.')}>Unlink identity</button>
    <p>{identities.length} identities loaded (up to 1,000).</p>
  </fieldset>;
}

import { useEffect, useState } from 'react';
import { request } from './api';
interface Health { database: string; eventCount: number; pendingDeliveries: number; sqliteLogFailures: number; secretStorage: string }
export function DiagnosticsPanel() {
  const [health, setHealth] = useState<Health>(); const [message, setMessage] = useState('');
  const refresh = () => request<Health>('/api/diagnostics').then(setHealth).catch(() => setMessage('Unable to load diagnostics. Check whether TDSBLive is running.'));
  useEffect(() => { void refresh(); }, []);
  return <section aria-label="Application diagnostics"><h2>Check application health</h2><p>Use these details when troubleshooting connections, saving or recovery.</p>
    {health && <dl><dt>Saved data</dt><dd>{health.database === 'ready' ? 'Database ready' : health.database}</dd><dt>Saved events</dt><dd>{health.eventCount}</dd><dt>Deliveries waiting</dt><dd>{health.pendingDeliveries}</dd><dt>Log write failures</dt><dd>{health.sqliteLogFailures}</dd></dl>}
    <button onClick={() => { void refresh(); }}>Refresh diagnostics</button><button onClick={async () => { try {
      const response = await fetch('/api/diagnostics/export', { credentials: 'same-origin' }); if (!response.ok) throw new Error();
      const url = URL.createObjectURL(await response.blob()); const a = document.createElement('a'); a.href = url; a.download = 'TDSBLive-diagnostics.json'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); setMessage('Diagnostics download started. Aggregate counts only; configuration, logs and messages are excluded.');
    } catch { setMessage('Unable to download diagnostics. Try again when the host is available.'); } }}>Export sanitized diagnostics</button>
    <p><a href="#connections">Inspect incoming events in Connections</a></p><details><summary>Technical details and local logs</summary><pre>{health && JSON.stringify(health, null, 2)}</pre><a href="/api/diagnostics" target="_blank" rel="noreferrer">Open full local diagnostics</a><p><a href="/api/diagnostics/logs">Download today's local log</a></p><p>Local logs may include private event details. Use sanitized diagnostics when asking for help.</p></details><output role="status">{message}</output>
  </section>;
}

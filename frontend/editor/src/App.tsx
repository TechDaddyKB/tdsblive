import { ConnectionIndicator } from './ConnectionIndicator';
import { useEffect, useState } from 'react';
import { api, ApiError, type Status } from './api';
import { Login } from './Login';
import { BotPanel } from './BotPanel';
import { RumblePanel } from './RumblePanel';
import { ChatPanel } from './ChatPanel';
import { VisualEditor } from './VisualEditor';
import { FinancialPanel } from './FinancialPanel';
import { AutomationPanel } from './AutomationPanel';
import { MaintenancePanel } from './MaintenancePanel';
import { SetupWizard } from './SetupWizard';
import { LanPanel } from './LanPanel';
import './application.css';

export function App() {
  const [status, setStatus] = useState<Status | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    if (window.location.pathname === '/login') return;
    const controller = new AbortController();
    void api.status(controller.signal).then(setStatus).catch((failure: unknown) => {
      if (controller.signal.aborted) return;
      setError(failure instanceof ApiError && failure.status === 401 ? 'Sign in to access this computer.' : 'Unable to reach the local host.');
    });
    return () => controller.abort();
  }, []);
  if (window.location.pathname === '/login') return <Login />;
  return <main className="tdsblive-editor">
    <h1>{status?.name ?? 'TDSBLive'}</h1>
    <p>Local HTTP is supported. HTTPS is optional.</p>
    <nav aria-label="Application"><a href="/editor">Overview</a> <a href="/api/diagnostics">Diagnostics</a></nav>
    {!status && <><ConnectionIndicator integration="Streamer.bot" state="disconnected" />
    <ConnectionIndicator integration="Speaker.bot" state="disconnected" /></>}
    {status && <p role="status">Host ready. {status.lanEnabled ? 'Authenticated LAN access enabled.' : 'Loopback access only.'}</p>}
    {error && <p role="alert">{error} <a href="/login">Sign in</a></p>}
    {status && <SetupWizard />}
    {status && <BotPanel />}
    {status && <RumblePanel />}
    {status && <VisualEditor />}
    {status && <ChatPanel />}
    {status && <FinancialPanel />}
    {status && <AutomationPanel />}
    {status && <MaintenancePanel />}
    {status && <LanPanel />}
  </main>;
}

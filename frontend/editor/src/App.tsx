import { ConnectionIndicator } from './ConnectionIndicator';
import { useEffect, useRef, useState } from 'react';
import { api, ApiError, bots, type Status, type BotOverview } from './api';
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
import { MediaLibrary } from './MediaLibrary';
import { DiagnosticsPanel } from './DiagnosticsPanel';
import { canNavigate } from './navigation';
import './application.css';
const pages = [['overview', 'Overview'], ['overlays', 'Overlays & Alerts'], ['chat', 'Chat'], ['automation', 'Automation'], ['supporters', 'Supporters'], ['media', 'Media'], ['connections', 'Connections'], ['settings', 'Settings'], ['diagnostics', 'Diagnostics'], ['help', 'Help']] as const;
type Page = typeof pages[number][0];
function currentPage(): Page { const value = location.hash.slice(1); return pages.find(p => p[0] === value)?.[0] ?? 'overview'; }
function initialTheme(): string { try { return localStorage.getItem('tdsblive.theme') ?? 'system'; } catch { return 'system'; } }
export function App() {
  const [status, setStatus] = useState<Status | null>(null); const [connections, setConnections] = useState<BotOverview | null>(null);
  const [error, setError] = useState(''); const [page, setPage] = useState<Page>(currentPage);
  const [visited, setVisited] = useState<Set<Page>>(() => new Set([currentPage()]));
  const [theme, setTheme] = useState(initialTheme); const [darkSystem, setDarkSystem] = useState(() => window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false);
  const [save, setSave] = useState('No overlay open'); const [menu, setMenu] = useState(false); const route = useRef(page); const navigation = useRef(0);
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => { document.querySelectorAll<HTMLMediaElement>('.app-content [hidden] audio, .app-content [hidden] video').forEach(media => media.pause()); }, [page]);
  useEffect(() => {
    if (window.location.pathname === '/login') return;
    const controller = new AbortController();
    void api.status(controller.signal).then(setStatus).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure instanceof ApiError && failure.status === 401 ? 'Sign in to access this computer.' : 'Unable to reach the local host.');
    });
    const refresh = () => { void bots.overview().then(value => { if (!controller.signal.aborted) setConnections(value); }).catch(() => {}); };
    refresh(); const timer = setInterval(refresh, 5000); return () => { controller.abort(); clearInterval(timer); };
  }, []);
  useEffect(() => { const media = window.matchMedia?.('(prefers-color-scheme: dark)'); const change = () => setDarkSystem(media?.matches ?? false); media?.addEventListener('change', change); return () => media?.removeEventListener('change', change); }, []);
  useEffect(() => {
    const change = async () => { if (location.hash === '#workspace-content') return; const target = currentPage(); const id = ++navigation.current;
      if (target === route.current) return;
      if (!await canNavigate()) { if (id === navigation.current) { history.replaceState(null, '', `#${route.current}`); setError('Your overlay changes are retained. Resolve the save error or conflict before switching pages.'); } return; }
      if (id !== navigation.current) return; route.current = target; setPage(target); setVisited(old => new Set([...old, target])); setMenu(false); setError('');
      requestAnimationFrame(() => heading.current?.focus());
    };
    const saved = (event: Event) => setSave((event as CustomEvent<string>).detail);
    window.addEventListener('hashchange', change); window.addEventListener('tdsblive:save-status', saved);
    return () => { window.removeEventListener('hashchange', change); window.removeEventListener('tdsblive:save-status', saved); };
  }, []);
  if (window.location.pathname === '/login') return <Login />;
  const mode = theme === 'system' ? darkSystem ? 'dark' : 'light' : theme;
  const connectionState = (value?: string) => value === 'connected' ? 'connected' : value === 'authenticationFailed' ? 'error' : ['connecting', 'discovering', 'authenticating', 'reconnecting'].includes(value ?? '') ? 'connecting' : 'disconnected';
  return <main className="tdsblive-editor" data-theme={mode}>
    <a className="skip-link" href="#workspace-content">Skip to workspace</a>
    <header className="app-header"><div className="brand"><h1>{status?.name ?? 'TDSBLive'}</h1><span>Your streaming workspace</span></div>
      <div className="global-status"><ConnectionIndicator integration="Streamer.bot" state={connectionState(connections?.streamerBot.state)} /><ConnectionIndicator integration="Speaker.bot" state={connectionState(connections?.speakerBot.state)} /><output aria-label="Global editor save status" aria-live="polite">{save}</output></div>
      <label>Theme<select aria-label="Application theme" value={theme} onChange={e => { setTheme(e.target.value); try { localStorage.setItem('tdsblive.theme', e.target.value); } catch { /* Session preference remains available. */ } }}><option value="system">Use system theme</option><option value="light">Light</option><option value="dark">Dark</option></select></label>
      <button className="nav-toggle" aria-expanded={menu} aria-controls="app-navigation" onClick={() => setMenu(!menu)}>Menu</button>
    </header>
    <div className="app-layout"><nav id="app-navigation" className={`app-navigation ${menu ? 'is-open' : ''}`} aria-label="Application">{pages.map(([id, title]) => <a key={id} href={`#${id}`} aria-current={page === id ? 'page' : undefined}>{title}</a>)}</nav>
      <div className="app-content" id="workspace-content"><h2 ref={heading} tabIndex={-1} className="workspace-heading">{pages.find(p => p[0] === page)?.[1]}</h2>
        {error && <p role="alert">{error} {!status && <a href="/login">Sign in</a>}</p>}
        {!status && !error && <p role="status">Connecting to TDSBLive…</p>}
        {status && <>
          {visited.has('overview') && <div hidden={page !== 'overview'}><section className="welcome-card"><h2>Ready for your next stream</h2><p role="status">Host ready. {status.lanEnabled ? 'Authenticated LAN access enabled.' : 'Loopback access only.'}</p><p>Create your overlay, choose your alerts, then copy its address into OBS.</p><a className="primary-link" href="#overlays">Create or edit an overlay</a> <a href="#connections">Check connections</a></section><SetupWizard /></div>}
          {visited.has('overlays') && <div hidden={page !== 'overlays'}><VisualEditor active={page === 'overlays'} /></div>}
          {visited.has('chat') && <div hidden={page !== 'chat'}><ChatPanel /></div>}
          {visited.has('automation') && <div hidden={page !== 'automation'}><AutomationPanel /></div>}
          {visited.has('supporters') && <div hidden={page !== 'supporters'}><FinancialPanel /></div>}
          {visited.has('media') && <div hidden={page !== 'media'}><MediaLibrary /></div>}
          {visited.has('connections') && <div hidden={page !== 'connections'}><BotPanel /><RumblePanel /></div>}
          {visited.has('settings') && <div hidden={page !== 'settings'}><MaintenancePanel /><LanPanel /><section><h2>Connection setup</h2><p>Change addresses, authentication, branding, and timezone in guided setup.</p><a href="#overview">Open guided setup</a><p>Local HTTP is supported. HTTPS is optional.</p></section></div>}
          {visited.has('diagnostics') && <div hidden={page !== 'diagnostics'}><DiagnosticsPanel /></div>}
          {visited.has('help') && <div hidden={page !== 'help'}><section><h2>A simple way to get started</h2><ol><li>Check your bot connections.</li><li>Create an overlay and add an Alert Box.</li><li>Choose a trigger and customize its design.</li><li>Test with sample data, then copy the OBS URL.</li></ol><a href="https://github.com/TechDaddyKB/tdsblive/wiki" target="_blank" rel="noreferrer">Open the user guide</a><p>Advanced settings remain available beside the feature they control. Preview tests never run live automation.</p></section></div>}
        </>}
      </div>
    </div>
  </main>;
}

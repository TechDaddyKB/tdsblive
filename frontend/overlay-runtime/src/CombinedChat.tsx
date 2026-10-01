import { useEffect, useId, useRef, useState } from 'react';
import { ChatBuffer, defaultSettings, safeAvatar, type ChatEvent, type ChatSettings, type OverlayDefinition } from './chat';
import { ChatConnection } from './ChatConnection';
import './chat.css';
import { ChatMessage } from './ChatMessage';

function initialTheme(): string {
  try { return localStorage.getItem('tdsblive.chat.theme') === 'light' ? 'light' : 'dark'; } catch { return 'dark'; }
}
function ChatBadge({ name, imageUrl }: { name: string; imageUrl?: string | null }) {
  const [failed, setFailed] = useState(false);
  const url = safeAvatar(imageUrl);
  return url && !failed ? <img className="badge-image" src={url} alt={name.slice(0, 64)} title={name.slice(0, 64)} referrerPolicy="no-referrer" onError={() => setFailed(true)} />
    : <span className="badge">{name.slice(0, 64)}</span>;
}
export function CombinedChat({ id, streamer = false, preview = false, token = '', feed }: { id: string; streamer?: boolean; preview?: boolean; token?: string; feed?: { definition: OverlayDefinition; events: ChatEvent[] } }) {
  const fontName = `TDSBLiveCustom${useId().replace(/[^a-z0-9]/gi, '')}`;
  const [definition, setDefinition] = useState<OverlayDefinition | null>(null);
  const [messages, setMessages] = useState<ChatEvent[]>([]);
  const [status, setStatus] = useState('Connecting');
  const [theme, setTheme] = useState(initialTheme);
  const [now, setNow] = useState(Date.now);
  const feedMode = Boolean(feed);
  const feedBuffer = useRef(new ChatBuffer());
  const list = useRef<HTMLDivElement>(null);
  const nearEnd = useRef(true);
  const settings = useRef<ChatSettings>(defaultSettings);
  useEffect(() => {
    if (feedMode) return;
    const buffer = new ChatBuffer();
    const connection = new ChatConnection(id, token, preview, value => {
      settings.current = streamer ? { ...value.chat, persistent: true } : value.chat;
      setDefinition(value); setMessages(buffer.visible(settings.current));
    }, events => setMessages(buffer.ingest(events, settings.current)), setStatus);
    void connection.start();
    const timer = setInterval(() => { if (!settings.current.persistent) { setNow(Date.now()); setMessages(buffer.visible(settings.current)); } }, 250);
    return () => { clearInterval(timer); connection.stop(); };
  }, [id, token, preview, streamer, feedMode]);
  useEffect(() => {
    if (!feed) return;
    settings.current = feed.definition.chat; setDefinition(feed.definition); setMessages(feedBuffer.current.ingest(feed.events, settings.current));
  }, [feed]);
  useEffect(() => {
    if (!feedMode) return;
    const timer = setInterval(() => { if (!settings.current.persistent) { setNow(Date.now()); setMessages(feedBuffer.current.visible(settings.current)); } }, 250);
    return () => clearInterval(timer);
  }, [feedMode]);
  useEffect(() => {
    const element = list.current;
    if (element && nearEnd.current) element.scrollTop = settings.current.newestOnTop ? 0 : element.scrollHeight;
  }, [messages]);
  useEffect(() => {
    const fontId = definition?.chat.fontAssetId;
    if (!fontId || fontId.length !== 64 || !/^[0-9a-f]{64}$/.test(fontId)) return;
    const controller = new AbortController(); let face: FontFace | undefined;
    void fetch(`/assets/${encodeURIComponent(fontId)}`, { headers: token ? { Authorization: `Bearer ${token}`, 'X-TDSBLive-Overlay': id } : {}, signal: controller.signal })
      .then(async response => { if (!response.ok) { throw new Error('Font unavailable.'); } face = new FontFace(fontName, await response.arrayBuffer()); await face.load(); if (!controller.signal.aborted) document.fonts.add(face); })
      .catch(() => { /* Use the configured system font if the asset cannot load. */ });
    return () => { controller.abort(); if (face) document.fonts.delete(face); };
  }, [definition?.chat.fontAssetId, id, token, fontName]);
  const s = settings.current;
  const toggleTheme = () => { const next = theme === 'dark' ? 'light' : 'dark'; setTheme(next); try { localStorage.setItem('tdsblive.chat.theme', next); } catch { /* Session preference still works. */ } };
  return <section className={`combined-chat ${streamer ? 'streamer-chat' : 'obs-chat'}`} data-theme={theme} aria-label="Combined chat"
    style={{ fontFamily: s.fontAssetId ? `${fontName}, ${s.font}, sans-serif` : `${s.font}, sans-serif`, fontSize: s.fontSize }}>
    {streamer && <header><h1>{definition?.name ?? 'Combined Chat'}</h1><button onClick={toggleTheme} aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}>{theme === 'dark' ? 'Light mode' : 'Dark mode'}</button><output>{status}</output></header>}
    {preview && <div className="preview-label">Test preview · includes simulation/replay</div>}
    {status.includes('Sign in') && <a href="/login">Sign in to this host</a>}
    <div ref={list} className="chat-list" role="log" aria-live={streamer ? 'polite' : 'off'} aria-label="Chat messages" onScroll={() => {
      const el = list.current!; nearEnd.current = s.newestOnTop ? el.scrollTop < 40 : el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    }}>
      {messages.map(event => {
        const avatar = safeAvatar(event.user?.avatarUrl);
        const leaving = !s.persistent && now >= Date.parse(event.receivedAt) + s.messageDurationSeconds * 1000;
        return <article key={event.id} data-event-id={event.id} data-platform={event.platform} className={`chat-row enter-${s.animationIn} ${leaving ? `exit-${s.animationOut}` : ''}`}
          style={{ backgroundColor: `rgba(0,0,0,${s.backgroundOpacity})`, borderColor: s.platformColors[event.platform] }} dir="auto">
          {s.showPlatformIcon && <span className="platform-icon" title={event.platform} aria-label={event.platform} style={{ backgroundColor: s.platformColors[event.platform] }}>{event.platform[0].toUpperCase()}</span>}
          {s.showAvatar && avatar && <img className="avatar" src={avatar} alt="" referrerPolicy="no-referrer" onError={e => { e.currentTarget.hidden = true; }} />}
          <div className="chat-content">
            {s.showTimestamp && <time dateTime={event.occurredAt}>{new Date(event.occurredAt).toLocaleTimeString()}</time>}
            {s.showBadges && (event.user?.badgeDetails ?? (event.user?.badges ?? []).map(name => ({ name }))).slice(0, 20).map((badge, index) => <ChatBadge key={`${badge.name}-${index}`} {...badge} />)}
            {s.showUsername && <strong className="username" style={{ color: s.platformColors[event.platform] }}>{event.user?.displayName ?? event.user?.login ?? 'Viewer'}: </strong>}
            {s.showMessage && <span className="message-text"><ChatMessage message={event.message} /></span>}
          </div>
        </article>;
      })}
    </div>
  </section>;
}

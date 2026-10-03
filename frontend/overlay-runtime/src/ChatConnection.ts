import type { ChatEvent, OverlayDefinition } from './chat';
import type { DonorSnapshot } from './DonorWidget';
import type { AutomationSoundCommand, AutomationSoundState } from './AutomationSound';

export class ChatConnection {
  private socket: WebSocket | null = null;
  private retry: ReturnType<typeof setTimeout> | undefined;
  private heartbeat: ReturnType<typeof setInterval> | undefined;
  private readonly abort = new AbortController();
  private failures = 0;
  private lastReply = 0;
  constructor(private readonly id: string, private readonly token: string, private readonly preview: boolean,
    private readonly settings: (value: OverlayDefinition) => void, private readonly events: (value: ChatEvent[], delivery: 'socket' | 'history') => void, private readonly status: (value: string) => void, private readonly canvas = false, private readonly donors?: (value: DonorSnapshot[]) => void,
    private readonly sound?: (value: AutomationSoundCommand) => void, private readonly stopSound?: (executionId: string) => void) {}
  private headers(): HeadersInit { return this.token ? { Authorization: `Bearer ${this.token}`, 'X-TDSBLive-Overlay': this.id } : {}; }
  async start(): Promise<void> {
    this.status('Connecting');
    try {
      const result = await fetch(`/api/overlays/${this.id}`, { headers: this.headers(), signal: this.abort.signal });
      if (!result.ok) { this.status(result.status === 401 ? 'Sign in or use an overlay token' : 'Overlay unavailable'); this.schedule(); return; }
      const definition = await result.json() as OverlayDefinition;
      if (this.abort.signal.aborted) return;
      this.settings(definition);
      const url = new URL(`/ws/overlay/${this.id}${this.preview ? '?preview=1' : ''}`, location.href);
      url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
      const socket = new WebSocket(url, this.token ? ['tdsblive.overlay.v1', this.token] : ['tdsblive.overlay.v1']);
      this.socket = socket;
      socket.onopen = () => { this.lastReply = Date.now(); socket.send(JSON.stringify({ op: 'subscribe', types: this.canvas && definition.canvasEnabled ? ['*'] : ['chat.message'] })); };
      socket.onmessage = event => {
        try {
          const message = JSON.parse(String(event.data)) as { op: string; event?: ChatEvent; settings?: OverlayDefinition; widgets?: DonorSnapshot[]; command?: AutomationSoundCommand; executionId?: string };
          this.lastReply = Date.now();
          if (message.op === 'subscribed') { this.failures = 0; this.status('Connected'); void this.history(); }
          if (message.op === 'event' && message.event) this.events([message.event], 'socket');
          if (message.op === 'settings' && message.settings) { this.settings(message.settings); void this.history(); }
          if (message.op === 'donors' && message.widgets) this.donors?.(message.widgets);
          if (message.op === 'sound' && message.command && this.canvas && !this.preview) this.sound?.(message.command);
          if (message.op === 'sound-stop' && message.executionId && this.canvas && !this.preview) this.stopSound?.(message.executionId);
        } catch { this.status('Invalid event ignored'); }
      };
      socket.onerror = () => { this.status('Reconnecting'); socket.close(); };
      socket.onclose = () => { this.socket = null; clearInterval(this.heartbeat); this.status('Reconnecting'); this.schedule(); };
      this.heartbeat = setInterval(() => {
        if (Date.now() - this.lastReply > 45_000) socket.close();
        else if (socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ op: 'ping' }));
      }, 15_000);
    } catch { if (!this.abort.signal.aborted) { this.status('Reconnecting'); this.schedule(); } }
  }
  reportSound(executionId: string, state: AutomationSoundState): void {
    if (this.canvas && !this.preview && this.socket?.readyState === WebSocket.OPEN)
      this.socket.send(JSON.stringify({ op: 'sound-result', executionId, state }));
  }
  private async history(): Promise<void> {
    try {
      const response = await fetch(`/api/overlays/${this.id}/chat`, { headers: this.headers(), signal: this.abort.signal });
      if (this.canvas && !this.preview) {
        const history = await fetch(`/api/overlays/${this.id}/events`, { headers: this.headers(), signal: this.abort.signal });
        if (history.ok && !this.abort.signal.aborted) this.events(await history.json() as ChatEvent[], 'history');
      }
      if (response.ok) {
        const events = await response.json() as ChatEvent[];
        if (!this.abort.signal.aborted) this.events(events, 'history');
      }
      else this.status(response.status === 401 ? 'Sign in or use an overlay token' : 'History unavailable');
    } catch { if (!this.abort.signal.aborted) this.status('History unavailable'); }
  }
  private schedule(): void {
    if (this.abort.signal.aborted || this.retry !== undefined) return;
    const jitter = crypto.getRandomValues(new Uint32Array(1))[0] / 0x1_0000_0000;
    this.retry = setTimeout(() => { this.retry = undefined; void this.start(); }, Math.min(30_000, 1000 * 2 ** Math.min(this.failures++, 5)) * (1 + jitter * .1));
  }
  stop(): void {
    this.abort.abort(); clearTimeout(this.retry); clearInterval(this.heartbeat);
    if (this.socket) { this.socket.onclose = null; this.socket.close(); this.socket = null; }
  }
}

export interface AutomationSoundCommand { executionId: string; assetId: string; volume: number; timeoutSeconds: number; duckingVolume: number }
export type AutomationSoundState = 'started' | 'completed' | 'failed' | 'timeout' | 'interrupted';

export class AutomationSoundPlayer {
  private readonly seen = new Set<string>();
  private readonly running = new Map<string, () => void>();
  constructor(private readonly overlay: string, private readonly token: string,
    private readonly report: (id: string, state: AutomationSoundState) => void,
    private readonly createAudio: (url: string) => HTMLAudioElement = url => new Audio(url)) {}

  async play(command: AutomationSoundCommand): Promise<void> {
    if (!/^[0-9a-f-]{36}$/i.test(command.executionId) || !/^[0-9a-f]{64}$/.test(command.assetId) ||
      !Number.isFinite(command.volume) || command.volume < 0 || command.volume > 1 ||
      !Number.isFinite(command.duckingVolume) || command.duckingVolume < 0 || command.duckingVolume > 1 ||
      !Number.isInteger(command.timeoutSeconds) || command.timeoutSeconds < 1 || command.timeoutSeconds > 3600) return;
    if (this.seen.has(command.executionId)) return;
    this.seen.add(command.executionId);
    if (this.seen.size > 10000) this.seen.delete(this.seen.values().next().value!);
    const controller = new AbortController();
    let audio: HTMLAudioElement | undefined;
    let url: string | undefined;
    let finished = false;
    const finish = (state: AutomationSoundState) => {
      if (finished) return;
      finished = true; controller.abort(); clearTimeout(timer);
      if (audio) { audio.onended = null; audio.onerror = null; audio.pause(); audio.removeAttribute('src'); audio.load(); }
      if (url) URL.revokeObjectURL(url);
      this.running.delete(command.executionId);
      this.report(command.executionId, state);
    };
    const timer = setTimeout(() => finish('timeout'), command.timeoutSeconds * 1000);
    this.running.set(command.executionId, () => finish('interrupted'));
    try {
      const response = await fetch(`/assets/${command.assetId}`, { signal: controller.signal,
        headers: this.token ? { Authorization: `Bearer ${this.token}`, 'X-TDSBLive-Overlay': this.overlay } : {} });
      if (!response.ok || !response.headers.get('Content-Type')?.startsWith('audio/')) throw new Error('Audio unavailable');
      const blob = await response.blob();
      if (finished) return;
      url = URL.createObjectURL(blob); audio = this.createAudio(url); audio.volume = command.volume;
      audio.onended = () => finish('completed'); audio.onerror = () => finish('failed');
      await audio.play();
      if (!finished) this.report(command.executionId, 'started');
    } catch { if (!finished) finish('failed'); }
  }

  interrupt(id: string): void { this.running.get(id)?.(); }
  stop(): void { for (const cancel of [...this.running.values()]) cancel(); }
}

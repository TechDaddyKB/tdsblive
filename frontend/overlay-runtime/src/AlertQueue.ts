import type { ChatEvent } from './chat';
import type { Widget } from './scene';
export interface AlertJob { key: string; widget: Widget; event: ChatEvent; enqueued: number; started?: number; ends?: number }
export class AlertQueue {
  private pending: AlertJob[] = [];
  private running: AlertJob[] = [];
  private readonly seen = new Set<string>();
  private readonly cooldown = new Map<string, number>();
  private sequence = 0;
  dropped = 0; interrupted = 0; completed = 0;
  enqueue(widget: Widget, event: ChatEvent, now: number): void {
    const a = widget.alert;
    if (widget.hidden || widget.kind !== 'alert' || !(a.eventTypes.includes('*') || a.eventTypes.includes(event.type)) || !a.platforms.includes(event.platform)) return;
    const key = `${widget.id}:${event.id}`;
    if (this.seen.has(key)) return;
    this.seen.add(key); if (this.seen.size > 10000) this.seen.delete(this.seen.values().next().value!);
    if (now < (this.cooldown.get(widget.id) ?? -Infinity)) { this.dropped++; return; }
    const job = { key, widget, event, enqueued: this.sequence++ };
    const queued = this.pending.filter(j => j.widget.alert.group === a.group);
    if (queued.length >= a.maximumQueueLength) {
      this.dropped++;
      if (a.overflowPolicy === 'drop-newest') return;
      const oldest = queued.reduce((l, r) => l.enqueued < r.enqueued ? l : r);
      this.pending = this.pending.filter(j => j !== oldest);
    }
    const active = this.running.filter(j => j.widget.alert.group === a.group);
    if (active.length >= a.concurrency && a.interruptPolicy === 'higher-priority') {
      const victim = active.filter(j => j.widget.alert.interruptible && j.widget.alert.priority < a.priority)
        .sort((l, r) => l.widget.alert.priority - r.widget.alert.priority || l.enqueued - r.enqueued)[0];
      if (victim) { this.running = this.running.filter(j => j !== victim); this.interrupted++; }
    }
    this.pending.push(job); this.cooldown.set(widget.id, now + a.cooldownMs); this.tick(now);
  }
  tick(now: number): AlertJob[] {
    const expired = this.running.filter(j => (j.ends ?? 0) <= now); this.completed += expired.length;
    this.running = this.running.filter(j => (j.ends ?? 0) > now);
    this.pending.sort((l, r) => r.widget.alert.priority - l.widget.alert.priority || l.enqueued - r.enqueued);
    const waiting: AlertJob[] = [];
    for (const job of this.pending) {
      const a = job.widget.alert;
      if (this.running.filter(j => j.widget.alert.group === a.group).length < a.concurrency)
        this.running.push({ ...job, started: now, ends: now + a.durationMs });
      else waiting.push(job);
    }
    this.pending = waiting; return [...this.running];
  }
  get queued(): number { return this.pending.length; }
  reconcile(widgets: Widget[]): void {
    const live = new Map(widgets.filter(w => !w.hidden && w.kind === 'alert').map(w => [w.id, JSON.stringify(w)]));
    const valid = (job: AlertJob) => live.get(job.widget.id) === JSON.stringify(job.widget);
    this.pending = this.pending.filter(valid); this.running = this.running.filter(valid);
    for (const id of this.cooldown.keys()) if (!live.has(id)) this.cooldown.delete(id);
  }
  clear(): void { this.pending = []; this.running = []; this.cooldown.clear(); }
}
export function alertText(template: string, event: ChatEvent): string {
  const values: Record<string, string> = { user: event.user?.displayName ?? event.user?.login ?? 'Viewer', type: event.type, platform: event.platform,
    message: event.message?.text ?? '' };
  return template.replace(/\{(user|type|platform|message)\}/g, (_match, key: string) => values[key]).slice(0, 8192);
}

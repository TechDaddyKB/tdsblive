import { describe, expect, it } from 'vitest';
import { AlertQueue, alertText } from './AlertQueue';
import { createWidget } from './scene';
import type { ChatEvent } from './chat';
const event = (id: string): ChatEvent => ({ id, type: 'community.follow', platform: 'twitch', occurredAt: new Date(0).toISOString(), receivedAt: new Date(0).toISOString(), provenance: 'simulation', user: { displayName: '<Viewer>' } });
const widget = () => createWidget('alert');
describe('bounded alert scheduling', () => {
  it('runs independent groups together and serializes each group by priority then FIFO', () => {
    const q = new AlertQueue(); const a = widget(), b = widget(); b.alert.group = 'sounds';
    q.enqueue(a, event('first'), 0); q.enqueue(a, event('second'), 0);
    const high = { ...a, id: 'higher', alert: { ...a.alert, priority: 10 } }; q.enqueue(high, event('high'), 0); q.enqueue(b, event('sound'), 0);
    expect(q.tick(0).map(j => j.event.id)).toEqual(['first', 'sound']); expect(q.queued).toBe(2);
    expect(q.tick(5000).map(j => j.event.id)).toEqual(['high']); expect(q.tick(10000).map(j => j.event.id)).toEqual(['second']);
    expect(q.tick(15000)).toEqual([]); expect(q.completed).toBe(4);
  });
  it('enforces concurrency, dedupe and admission cooldown with exact boundary', () => {
    const q = new AlertQueue(); const a = widget(); a.alert.concurrency = 2; a.alert.cooldownMs = 1000;
    q.enqueue(a, event('1'), 0); q.enqueue(a, event('1'), 1000); q.enqueue(a, event('2'), 999); q.enqueue(a, event('3'), 1000);
    expect(q.tick(1000)).toHaveLength(2); expect(q.dropped).toBe(1); expect(q.queued).toBe(0);
    expect(q.tick(5000).map(j => j.event.id)).toEqual(['3']);
  });
  it.each(['drop-oldest', 'drop-newest'] as const)('bounds pending jobs using %s', policy => {
    const q = new AlertQueue(); const a = widget(); a.alert.maximumQueueLength = 2; a.alert.overflowPolicy = policy;
    for (let i = 0; i < 4; i++) q.enqueue(a, event(String(i)), 0);
    expect(q.queued).toBe(2); expect(q.dropped).toBe(1);
    expect(q.tick(5000)[0].event.id).toBe(policy === 'drop-oldest' ? '2' : '1');
  });
  it('interrupts only lower-priority interruptible jobs when explicitly enabled', () => {
    const q = new AlertQueue(); const a = widget(); a.alert.interruptible = false; q.enqueue(a, event('locked'), 0);
    const b = widget(); b.alert.priority = 10; b.alert.interruptPolicy = 'higher-priority'; q.enqueue(b, event('high'), 1);
    expect(q.tick(1)[0].event.id).toBe('locked'); expect(q.interrupted).toBe(0);
    const c = widget(); c.alert.priority = 20; c.alert.interruptPolicy = 'higher-priority';
    q.tick(5000); q.enqueue(c, event('higher'), 5001); expect(q.tick(5001)[0].event.id).toBe('higher'); expect(q.interrupted).toBe(1);
  });
  it('completes expired jobs before admitting a new interrupting job', () => {
    const q = new AlertQueue(); const a = widget(); q.enqueue(a, event('expired'), 0);
    q.enqueue({ ...a, id: 'high', alert: { ...a.alert, priority: 50, interruptPolicy: 'higher-priority' } }, event('new'), 5000);
    expect(q.tick(5000)[0].event.id).toBe('new'); expect(q.completed).toBe(1); expect(q.interrupted).toBe(0);
  });
  it('does not interrupt an active job when overflow rejects the incoming higher-priority job', () => {
    const q = new AlertQueue(); const a = widget(); a.alert.maximumQueueLength = 1; a.alert.overflowPolicy = 'drop-newest';
    q.enqueue(a, event('active'), 0); q.enqueue(a, event('waiting'), 0);
    q.enqueue({ ...a, id: 'high', alert: { ...a.alert, priority: 50, interruptPolicy: 'higher-priority' } }, event('rejected'), 0);
    expect(q.tick(0)[0].event.id).toBe('active'); expect(q.queued).toBe(1); expect(q.interrupted).toBe(0); expect(q.dropped).toBe(1);
  });
  it('drops hidden/nonmatching jobs and cancels media on changed or removed widget settings', () => {
    const q = new AlertQueue(); const a = widget();
    q.enqueue({ ...a, hidden: true }, event('hidden'), 0); q.enqueue(a, { ...event('chat'), type: 'chat.message' }, 0);
    expect(q.tick(0)).toEqual([]); q.enqueue(a, event('first'), 0); q.enqueue(a, event('queued'), 0);
    q.reconcile([{ ...a, volume: .5 }]); expect(q.tick(0)).toEqual([]); expect(q.queued).toBe(0);
    q.enqueue(a, event('next'), 0); q.clear(); expect(q.tick(0)).toEqual([]);
  });
  it('applies changed cooldown policy immediately after canceling old widget jobs', () => {
    const q = new AlertQueue(); const a = widget(); a.alert.cooldownMs = 10000; q.enqueue(a, event('old'), 0);
    const changed = { ...a, alert: { ...a.alert, cooldownMs: 0 } }; q.reconcile([changed]); q.enqueue(changed, event('new'), 1);
    expect(q.tick(1).map(j => j.event.id)).toEqual(['new']); q.clear(); q.enqueue(changed, event('new'), 2); expect(q.tick(2)).toHaveLength(1);
  });
  it('formats only recognized text placeholders without treating viewer content as markup', () => {
    expect(alertText('{user} {message} {type} {platform} {unknown}', { ...event('1'), message: { text: '<script>' } }))
      .toBe('<Viewer> <script> community.follow twitch {unknown}');
  });
});

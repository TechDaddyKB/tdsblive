import { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { WidgetProperties } from './WidgetProperties';
import { createWidget, type Widget } from '../../overlay-runtime/src/scene';
const assets = [ { id: 'a'.repeat(64), filename: 'Owned GIF', mime: 'image/gif' }, { id: 'b'.repeat(64), filename: 'Owned video', mime: 'video/webm' }, { id: 'c'.repeat(64), filename: 'Owned sound', mime: 'audio/wav' } ];
function Harness({ kind }: { kind: Widget['kind'] }) { const [widget, setWidget] = useState(() => createWidget(kind)); return <><WidgetProperties widget={widget} assets={assets} change={setWidget} /><output data-testid="widget-json">{JSON.stringify(widget)}</output></>; }
const value = (): Widget => JSON.parse(screen.getByTestId('widget-json').textContent!);
const change = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value } });
afterEach(cleanup);
it('edits text, bounded geometry, rotation, appearance and lock/hide without accepting invalid dimensions', () => {
  render(<Harness kind="text" />); change('Layer name', 'Title'); change('Widget text', '<script>escaped</script>'); change('Text color', '#123456');
  for (const [label, n] of [['X', '-20'], ['Y', '40'], ['Width', '500'], ['Height', '300'], ['Rotation', '45'], ['Font size', '48']]) change(label, n);
  fireEvent.click(screen.getByLabelText('Locked')); fireEvent.click(screen.getByLabelText('Hidden'));
  expect(value()).toMatchObject({ name: 'Title', text: '<script>escaped</script>', color: '#123456', x: -20, y: 40, width: 500, height: 300, rotation: 45, fontSize: 48, locked: true, hidden: true });
  change('Width', '0'); change('X', '9000'); change('Height', ''); change('Font size', '48.5'); expect(value().fontSize).toBe(48); expect(value().width).toBe(500); expect(value().x).toBe(-20); expect(value().height).toBe(300);
});
it.each(['image', 'video', 'audio'] as const)('offers only compatible %s assets and playback controls', kind => {
  render(<Harness kind={kind} />); const id = assets.find(a => a.mime.startsWith(`${kind}/`))!.id; change('Media asset', id); expect(value().assetId).toBe(id);
  expect(screen.getByLabelText('Media asset').querySelectorAll('option')).toHaveLength(2);
  if (kind !== 'image') { change('Volume', '0.25'); fireEvent.click(screen.getByLabelText('Mute media')); fireEvent.click(screen.getByLabelText('Loop media')); expect(value()).toMatchObject({ volume: .25, muted: false, loop: false }); }
});
it('edits chat selection and persistence without mutating unrelated chat defaults', () => {
  render(<Harness kind="chat" />); const select = screen.getByLabelText('Chat platforms') as HTMLSelectElement; for (const option of select.options) option.selected = option.value === 'rumble'; fireEvent.change(select);
  change('Chat font size', '36'); fireEvent.click(screen.getByLabelText('Persistent chat')); expect(value().chat).toMatchObject({ platforms: ['rumble'], fontSize: 36, persistent: true, maximumMessages: 100 }); change('Chat font size', '36.5'); expect(value().chat.fontSize).toBe(36);
});
it('configures alert presets, templates, media and complete bounded queue policies', () => {
  render(<Harness kind="alert" />); change('Alert preset', '3'); expect(value().alert).toMatchObject({ eventTypes: ['support.bits'], platforms: ['twitch'] });
  change('Event types', 'support.bits, support.gift'); change('Alert platforms', 'twitch,kick'); change('Alert template', '{user}: {message}'); change('Queue group', 'sounds');
  change('Alert media', assets[0].id); change('Alert sound', assets[2].id); change('Volume', '0.5');
  for (const [label, n] of [['Priority', '10'], ['Duration (ms)', '1000'], ['Cooldown (ms)', '2000'], ['Concurrency', '2'], ['Maximum queue length', '3']]) change(label, n);
  fireEvent.click(screen.getByLabelText('Interruptible')); change('Interrupt policy', 'higher-priority'); change('Overflow policy', 'drop-newest'); change('Animation', 'slide');
  expect(value().alert).toMatchObject({ eventTypes: ['support.bits', 'support.gift'], platforms: ['twitch', 'kick'], template: '{user}: {message}', group: 'sounds', priority: 10, durationMs: 1000, cooldownMs: 2000, concurrency: 2, maximumQueueLength: 3, interruptible: false, interruptPolicy: 'higher-priority', overflowPolicy: 'drop-newest', animation: 'slide', mediaAssetId: assets[0].id, soundAssetId: assets[2].id });
  change('Concurrency', '9'); change('Priority', '-101'); expect(value().alert.concurrency).toBe(2); expect(value().alert.priority).toBe(10);
});

it('accepts both native and forwarded custom Streamer.bot trigger routes', () => {
  render(<Harness kind="alert" />); change('Alert preset', '13'); expect(value().alert).toMatchObject({ eventTypes: ['integration.custom'], platforms: ['general', 'custom'] });
});

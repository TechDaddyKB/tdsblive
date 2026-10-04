import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { AmountField, ConditionFields, CurrencyField, NamedPicker, TriggerSelect } from './TriggerControls';
import { AlertEditor } from './AlertEditor';
import { AlertSets } from './AlertSets';
import { Dialog } from './ui';
import { canNavigate, navigationGuard } from './navigation';
import { defaultSettings } from '../../overlay-runtime/src/chat';
import { createWidget, type Scene, type AlertCondition } from '../../overlay-runtime/src/scene';
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it('tests guided amounts with the chosen incoming identity and explains matching results', () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));
  const widget = createWidget('alert'); widget.alert = { ...widget.alert, eventTypes: ['support.donation'], platforms: ['kofi'], nativeType: 'Kofi.Donation', condition: { unit: 'native-money', operator: 'minimum', value: '500', currency: 'USD', minorUnitDigits: 2 } };
  const test = vi.fn(); render(<AlertEditor widget={widget} change={vi.fn()} design={null} advanced={null} test={test} results={[{ id: widget.id, name: 'Donation', reason: 'Amount or quantity does not match' }]} />);
  fireEvent.click(screen.getByRole('button', { name: '4. Test' })); fireEvent.change(screen.getByLabelText('Test amount (USD)'), { target: { value: '4.99' } }); fireEvent.click(screen.getByRole('button', { name: 'Test this design with sample data' }));
  expect(test).toHaveBeenCalledWith({ type: 'support.donation', platform: 'kofi', nativeType: 'Kofi.Donation', customTriggerKey: undefined, quantity: '1', amount: '499', currency: 'USD', digits: 2 }); expect(screen.getByRole('list', { name: 'Guided matching results' })).toHaveTextContent('Donation: Amount or quantity does not match');
});
it('chooses currency scale automatically while preserving exact ordinary amounts', () => {
  const change = vi.fn(); const view = render(<CurrencyField label="Currency" currency="USD" digits={2} value="500" change={change} />);
  fireEvent.change(screen.getByLabelText('Currency'), { target: { value: 'JPY' } }); expect(change).toHaveBeenCalledWith({ currency: 'JPY', minorUnitDigits: 0, value: '5' });
  view.rerender(<CurrencyField label="Currency" currency="USD" digits={2} value="505" change={change} />); fireEvent.change(screen.getByLabelText('Currency'), { target: { value: 'EUR' } }); expect(change).toHaveBeenLastCalledWith({ currency: 'EUR', minorUnitDigits: 2, value: '505' });
  change.mockClear(); fireEvent.change(screen.getByLabelText('Currency'), { target: { value: 'JPY' } }); expect(change).not.toHaveBeenCalled(); expect(screen.getByRole('alert')).toHaveTextContent('previous currency is retained');
});
it('selects a friendly catalog trigger without replacing unknown identifiers merely by loading', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [{ id: 'owned', label: 'Owned incoming event', platform: 'general', eventType: 'integration.custom', customTriggerKey: 'owned.sparkle', description: 'Observed incoming identity', availability: 'observed', units: [] }] }));
  const change = vi.fn(); const w = createWidget('alert'); w.alert.eventTypes = ['future.event'];
  render(<TriggerSelect alert={w.alert} change={change} />);
  await screen.findByRole('option', { name: 'Owned incoming event' }); expect(change).not.toHaveBeenCalled();
  fireEvent.change(screen.getByLabelText('Find a trigger'), { target: { value: 'Owned' } });
  fireEvent.change(screen.getByLabelText('Choose trigger'), { target: { value: 'owned' } });
  expect(change).toHaveBeenCalledWith({ eventTypes: ['integration.custom'], platforms: ['general'], nativeType: null, customTriggerKey: 'owned.sparkle' });
});
it('keeps the valid amount while explaining invalid input, and preserves large exact integers', () => {
  const change = vi.fn(); const view = render(<AmountField label="Amount" value="500" digits={2} change={change} />);
  fireEvent.change(screen.getByLabelText('Amount'), { target: { value: '5.001' } }); expect(change).not.toHaveBeenCalled(); expect(screen.getByRole('alert')).toHaveTextContent('previous valid amount is retained');
  fireEvent.change(screen.getByLabelText('Amount'), { target: { value: '92233720368547758.07' } }); expect(change).toHaveBeenLastCalledWith('9223372036854775807');
  view.rerender(<AmountField label="Amount" value="1" digits={0} change={change} />); expect(screen.getByLabelText('Amount')).toHaveValue('1');
});
it('offers conditions, native currency and bounded ranges as editable controls', () => {
  let result: AlertCondition | null = null;
  function Form() { const [value, setValue] = useState<AlertCondition | null>(null); return <ConditionFields condition={value} change={v => { result = v; setValue(v); }} />; }
  render(<Form />); fireEvent.change(screen.getByLabelText('Alert condition'), { target: { value: 'range' } });
  fireEvent.change(screen.getByLabelText('Alert measure'), { target: { value: 'native-money' } });
  fireEvent.change(screen.getByLabelText('Alert amount'), { target: { value: '5.00' } }); fireEvent.change(screen.getByLabelText('Alert upper limit (excluded)'), { target: { value: '10.00' } });
  expect(result).toMatchObject({ value: '500', upperExclusive: '1000', currency: 'USD', minorUnitDigits: 2 });
  fireEvent.change(screen.getByLabelText('Alert currency'), { target: { value: 'EUR' } }); expect(result).toMatchObject({ currency: 'EUR' });
  fireEvent.change(screen.getByLabelText('Alert condition'), { target: { value: 'any' } }); expect(result).toBeNull();
});
it('retains configured unknown choices and offers search', () => {
  const change = vi.fn(); render(<NamedPicker label="Events" values={['future.custom']} options={{ 'community.follow': 'Follow' }} change={change} />);
  fireEvent.click(screen.getByLabelText('future.custom (configured)')); expect(change).toHaveBeenCalledWith([]);
  fireEvent.change(screen.getByLabelText('Search events'), { target: { value: 'Follow' } }); expect(screen.queryByLabelText('future.custom (configured)')).not.toBeInTheDocument(); fireEvent.click(screen.getByLabelText('Follow')); expect(change).toHaveBeenLastCalledWith(['future.custom', 'community.follow']);
});
it('keeps alert setup steps keyboard reachable and runs only an explicit sample test', () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline'))); const test = vi.fn();
  render(<AlertEditor widget={createWidget('alert')} change={vi.fn()} design={<p>Design controls</p>} advanced={<p>Legacy controls</p>} test={test} />);
  fireEvent.click(screen.getByRole('button', { name: 'Next alert step' })); expect(screen.getByLabelText('Alert condition')).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: '3. Design' })); expect(screen.getByText('Design controls')).toBeVisible(); expect(test).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('button', { name: '4. Test' })); fireEvent.click(screen.getByRole('button', { name: 'Test this design with sample data' })); expect(test).toHaveBeenCalledOnce();
  fireEvent.click(screen.getByRole('button', { name: '5. Finish' })); expect(screen.getByText('Your alert is ready')).toBeVisible(); fireEvent.click(screen.getByRole('button', { name: 'Previous alert step' })); expect(screen.getByText('Try your design')).toBeVisible();
});
it('orders alert sets explicitly, supports all matches and dissolves without losing designs', () => {
  const a = createWidget('alert'), b = createWidget('alert'); a.name = 'Small'; b.name = 'Large'; let value: Scene = { id: 'owned', name: 'Owned', version: 1, width: 1920, height: 1080, background: 'transparent', canvasEnabled: true, revisionLimit: 50, chat: structuredClone(defaultSettings), widgets: [a,b], alertSets: [] };
  function Form() { const [scene, set] = useState(value); return <AlertSets scene={scene} change={next => { value = next; set(next); }} />; }
  render(<Form />); fireEvent.click(screen.getByText('Choose between alert designs')); fireEvent.click(screen.getByRole('button', { name: 'Create alert set' }));
  expect(value.alertSets![0].selection).toBe('first'); fireEvent.change(screen.getByLabelText(/Add design/), { target: { value: b.id } });
  fireEvent.click(screen.getByRole('button', { name: 'Move design 2 up' })); expect(value.alertSets![0].widgetIds).toEqual([b.id,a.id]);
  fireEvent.click(screen.getByRole('button', { name: 'Move design 1 down' })); fireEvent.change(screen.getByLabelText(/Design selection/), { target: { value: 'all' } }); expect(value.alertSets![0].selection).toBe('all');
  fireEvent.change(screen.getByLabelText('Set name'), { target: { value: 'Tiers' } }); fireEvent.click(screen.getByRole('button', { name: 'Remove design 2' })); expect(value.alertSets![0].widgetIds).toEqual([a.id]);
  fireEvent.click(screen.getByRole('button', { name: 'Dissolve Tiers' })); expect(value.alertSets).toEqual([]); expect(value.widgets).toHaveLength(2);
});
it('restores focus to the opener when a dialog closes', () => {
  const opener = document.createElement('button'); document.body.append(opener); opener.focus(); const close = vi.fn();
  const view = render(<Dialog title="Owned dialog" close={close}><input aria-label="Name" autoFocus /></Dialog>);
  fireEvent.click(screen.getByRole('button', { name: 'Close Owned dialog' })); expect(close).toHaveBeenCalledOnce(); view.unmount(); expect(document.activeElement).toBe(opener); opener.remove();
});
it('flushes navigation guards and refuses unresolved conflicts', async () => {
  const first = vi.fn().mockResolvedValue(true), second = vi.fn().mockResolvedValue(false); const disposeFirst = navigationGuard(first), disposeSecond = navigationGuard(second);
  expect(await canNavigate()).toBe(false); expect(first).toHaveBeenCalledOnce(); disposeSecond(); expect(await canNavigate()).toBe(true); disposeFirst(); await waitFor(() => expect(first).toHaveBeenCalledTimes(2));
});

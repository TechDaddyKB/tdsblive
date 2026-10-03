import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SettingsFields, type SettingsField } from './SettingsFields';
afterEach(cleanup);
it('supports every typed field while preserving stable integration IDs and filtering asset types', () => {
  const fields: SettingsField[] = [
    { key: 'group', label: 'Appearance', type: 'group', children: [{ key: 'text', label: 'Title', type: 'text' }, { key: 'font', label: 'Typeface', type: 'font' }] },
    { key: 'textarea', label: 'Template', type: 'textarea' }, { key: 'number', label: 'Count', type: 'number', min: 1, max: 10 },
    { key: 'slider', label: 'Size', type: 'slider', min: 1, max: 100 }, { key: 'duration', label: 'Duration', type: 'duration', min: 100, max: 1000 },
    { key: 'checkbox', label: 'Enabled', type: 'checkbox' }, { key: 'color', label: 'Color', type: 'color' },
    { key: 'dropdown', label: 'Mode', type: 'dropdown', options: [{ value: 'one', label: 'One' }] },
    { key: 'multiselect', label: 'Tags', type: 'multiselect', options: [{ value: 'one', label: 'One' }, { value: 'two', label: 'Two' }] },
    ...(['image', 'audio', 'video', 'event', 'action', 'user', 'platform'] as const).map(type => ({ key: type, label: type, type })),
    { key: 'button', label: 'Reset', type: 'button' }, { key: 'hidden', label: 'Internal', type: 'hidden' },
  ];
  const change = vi.fn(), button = vi.fn();
  render(<SettingsFields fields={fields} values={{ text: 'Hello', font: 'Arial', textarea: 'Body', number: 2, slider: 50, duration: 500, checkbox: false, color: '#ffffff', dropdown: 'one', multiselect: ['one'], image: null, audio: null, video: null, action: 'disconnected-id', hidden: 'private' }} change={change} context={{
    assets: ['image', 'audio', 'video'].map((type, i) => ({ id: String(i), mime: `${type}/synthetic`, filename: type })), events: ['community.follow'], actions: [{ id: 'stable-id', name: 'Owned action' }], users: ['Viewer'], button,
  }} />);
  expect(screen.queryByText('Internal')).toBeNull(); expect(screen.getByRole('group')).toHaveAccessibleName('Appearance');
  for (const [label, value] of [['Title', 'New title'], ['Template', 'New body'], ['Typeface', 'Open Sans'], ['Color', '#ff0000'], ['Count', '3'], ['Size', '75'], ['Duration', '700'], ['Mode', 'one'], ['image', '0'], ['audio', '1'], ['video', '2'], ['event', 'community.follow'], ['action', 'stable-id'], ['user', 'Viewer'], ['platform', 'rumble']]) fireEvent.change(screen.getByLabelText(label, { exact: true }), { target: { value } });
  expect(change).toHaveBeenCalledWith('duration', 700); expect(change).toHaveBeenCalledWith('action', 'stable-id');
  expect(screen.getByLabelText('action', { exact: true })).toHaveValue('disconnected-id');
  const tags = screen.getByLabelText('Tags') as HTMLSelectElement; for (const option of tags.options) option.selected = true; fireEvent.change(tags); expect(change).toHaveBeenCalledWith('multiselect', ['one', 'two']);
  fireEvent.click(screen.getByLabelText('Enabled')); expect(change).toHaveBeenCalledWith('checkbox', true);
  fireEvent.click(screen.getByText('Reset')); expect(button).toHaveBeenCalledWith('button');
  fireEvent.change(screen.getByLabelText('image', { exact: true }), { target: { value: '' } }); expect(change).toHaveBeenCalledWith('image', null);
  change.mockClear(); fireEvent.change(screen.getByLabelText('Count'), { target: { value: '11' } }); fireEvent.change(screen.getByLabelText('Duration'), { target: { value: '99' } }); fireEvent.change(screen.getByLabelText('Typeface'), { target: { value: 'url(private)' } }); expect(change).not.toHaveBeenCalled();
  expect((screen.getByLabelText('image', { exact: true }) as HTMLSelectElement).options).toHaveLength(2);
});

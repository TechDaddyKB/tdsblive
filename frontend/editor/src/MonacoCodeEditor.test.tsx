import { cleanup, render } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
const owned = vi.hoisted(() => {
  const model = { dispose: vi.fn() }; let value = ''; let notify = () => {};
  const instance = { getValue: () => value, setValue: vi.fn((next: string) => { value = next; notify(); }), getModel: () => model, dispose: vi.fn(),
    onDidChangeModelContent: vi.fn((handler: () => void) => { notify = handler; return { dispose: vi.fn() }; }) };
  return { model, instance, create: vi.fn((_container: unknown, options: { value: string }) => { value = options.value; return instance; }) };
});
vi.mock('monaco-editor/editor/editor.api.js', () => ({ editor: { create: owned.create } }));
vi.mock('monaco-editor/basic-languages/monaco.contribution.js', () => ({}));
vi.mock('monaco-editor/language/json/monaco.contribution.js', () => ({}));
vi.mock('monaco-editor/editor/editor.worker?worker', () => ({ default: class { kind = 'editor'; } }));
vi.mock('monaco-editor/language/json/json.worker?worker', () => ({ default: class { kind = 'json'; } }));
import MonacoCodeEditor from './MonacoCodeEditor';
afterEach(() => { cleanup(); vi.clearAllMocks(); });
it('updates the Monaco model and callback without recreating it, then disposes models on language changes', () => {
  const change = vi.fn(); const view = render(<MonacoCodeEditor language="html" value="<p>owned</p>" change={change} />);
  expect(owned.create).toHaveBeenCalledWith(expect.anything(), expect.objectContaining({ language: 'html', ariaLabel: 'html source', value: '<p>owned</p>' }));
  const later = vi.fn(); view.rerender(<MonacoCodeEditor language="html" value="<p>changed</p>" change={later} />);
  expect(owned.create).toHaveBeenCalledTimes(1); expect(owned.instance.setValue).toHaveBeenCalledWith('<p>changed</p>'); expect(later).toHaveBeenCalledWith('<p>changed</p>');
  view.rerender(<MonacoCodeEditor language="css" value="body {}" change={later} />); expect(owned.model.dispose).toHaveBeenCalledTimes(1); expect(owned.create).toHaveBeenCalledTimes(2);
  view.unmount(); expect(owned.model.dispose).toHaveBeenCalledTimes(2); expect(owned.instance.dispose).toHaveBeenCalledTimes(2);
});
it('uses packaged local worker constructors for JSON and editor languages', () => {
  const environment = (globalThis as unknown as { MonacoEnvironment: { getWorker: (id: string, label: string) => { kind: string } } }).MonacoEnvironment;
  expect(environment.getWorker('owned', 'json').kind).toBe('json'); expect(environment.getWorker('owned', 'html').kind).toBe('editor');
});

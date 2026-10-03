import { useEffect, useRef } from 'react';
import * as monaco from 'monaco-editor/editor/editor.api.js';
import 'monaco-editor/basic-languages/monaco.contribution.js';
import 'monaco-editor/language/json/monaco.contribution.js';
import EditorWorker from 'monaco-editor/editor/editor.worker?worker';
import JsonWorker from 'monaco-editor/language/json/json.worker?worker';

(globalThis as typeof globalThis & { MonacoEnvironment: monaco.Environment }).MonacoEnvironment = { getWorker: (_id, label) => label === 'json' ? new JsonWorker() : new EditorWorker() };
export default function MonacoCodeEditor({ language, value, change }: { language: string; value: string; change: (value: string) => void }) {
  const container = useRef<HTMLDivElement>(null); const editor = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);
  const changed = useRef(change); changed.current = change;
  const current = useRef(value); current.current = value;
  useEffect(() => {
    if (!container.current) return;
    const instance = monaco.editor.create(container.current, { value: current.current, language, automaticLayout: true, minimap: { enabled: false }, theme: 'vs-dark', ariaLabel: `${language} source`, scrollBeyondLastLine: false });
    editor.current = instance; const subscription = instance.onDidChangeModelContent(() => changed.current(instance.getValue()));
    return () => { subscription.dispose(); const model = instance.getModel(); instance.dispose(); model?.dispose(); editor.current = null; };
  }, [language]);
  useEffect(() => { const instance = editor.current; if (instance && instance.getValue() !== value) instance.setValue(value); }, [value]);
  return <div ref={container} style={{ height: 360, minWidth: 280 }} />;
}

import type { Scene } from '../../overlay-runtime/src/scene';
export interface EditorState { document: Scene; status: 'saved' | 'unsaved' | 'saving' | 'conflict' | 'error'; undo: boolean; redo: boolean }
const fingerprint = (value: Scene) => JSON.stringify({ ...value, version: 0 });
export class EditorSession {
  private state: EditorState;
  private baseline: string;
  private past: Scene[] = [];
  private future: Scene[] = [];
  private readonly listeners = new Set<(state: EditorState) => void>();
  private timer: ReturnType<typeof setTimeout> | undefined;
  private work: Promise<void> | undefined;
  private disposed = false;
  constructor(document: Scene, private readonly persist: (snapshot: Scene) => Promise<Scene>) {
    this.state = { document: structuredClone(document), status: 'saved', undo: false, redo: false }; this.baseline = fingerprint(document);
  }
  get snapshot(): EditorState { return this.state; }
  subscribe(listener: (state: EditorState) => void): () => void { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; }
  private update(document: Scene, status: EditorState['status']): void {
    this.state = { document, status, undo: this.past.length > 0, redo: this.future.length > 0 };
    for (const listener of this.listeners) listener(this.state);
  }
  edit(document: Scene): void {
    if (this.disposed || fingerprint(document) === fingerprint(this.state.document)) return;
    this.past.push(structuredClone(this.state.document)); this.past = this.past.slice(-100); this.future = [];
    this.update(structuredClone(document), this.state.status === 'conflict' ? 'conflict' : 'unsaved'); this.schedule();
  }
  undo(): void { this.move(this.past, this.future); }
  redo(): void { this.move(this.future, this.past); }
  private move(from: Scene[], to: Scene[]): void {
    const target = from.pop(); if (!target || this.disposed) return;
    to.push(structuredClone(this.state.document));
    this.update({ ...target, version: this.state.document.version }, this.state.status === 'conflict' ? 'conflict' : 'unsaved'); this.schedule();
  }
  private schedule(): void {
    clearTimeout(this.timer);
    if (!this.disposed && this.state.status !== 'conflict') this.timer = setTimeout(() => { void this.save(); }, 750);
  }
  private save(): Promise<void> {
    if (this.work) return this.work;
    if (this.disposed || this.state.status === 'conflict' || fingerprint(this.state.document) === this.baseline) {
      if (!this.disposed && this.state.status !== 'conflict') this.update(this.state.document, 'saved');
      return Promise.resolve();
    }
    const sent = structuredClone(this.state.document); this.update(this.state.document, 'saving');
    this.work = Promise.resolve().then(() => this.persist(sent)).then(saved => {
      if (this.disposed) return;
      this.baseline = fingerprint(saved);
      // The server may supply newly introduced defaults to an older document.
      // Accept that normalization only when no edit happened during the save.
      const document = fingerprint(this.state.document) === fingerprint(sent) ? structuredClone(saved) : { ...this.state.document, version: saved.version };
      this.update(document, fingerprint(document) === this.baseline ? 'saved' : 'unsaved');
    }).catch((error: unknown) => {
      if (!this.disposed) this.update(this.state.document, error instanceof Error && 'status' in error && error.status === 409 ? 'conflict' : 'error');
    }).finally(() => { this.work = undefined; if (this.state.status === 'unsaved') this.schedule(); });
    return this.work;
  }
  async flush(): Promise<boolean> {
    clearTimeout(this.timer); await this.save();
    if (this.state.status === 'unsaved') return this.flush();
    return this.state.status === 'saved';
  }
  dispose(): void { this.disposed = true; clearTimeout(this.timer); this.listeners.clear(); }
}

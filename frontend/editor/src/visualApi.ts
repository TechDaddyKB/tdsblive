import { request, write } from './api';
import type { Scene } from '../../overlay-runtime/src/scene';
export interface Revision { version: number; savedAt: string; name: string }
function idPath(id: string): string {
  if (id.length > 64 || !/^[a-z0-9][a-z0-9-]*$/.test(id)) throw new Error('Invalid overlay identifier');
  return `/api/overlays/${encodeURIComponent(id)}`;
}
export const visualApi = {
  list: () => request<Scene[]>('/api/overlays'),
  load: (id: string) => request<Scene>(idPath(id)),
  create: (value: Scene) => { idPath(value.id); return write<Scene>('/api/overlays', 'POST', value); },
  save: (value: Scene) => write<Scene>(idPath(value.id), 'PUT', value),
  revisions: (id: string) => request<Revision[]>(`${idPath(id)}/revisions`),
  restore: (id: string, version: number, expectedVersion: number) => {
    if (!Number.isSafeInteger(version) || version < 1 || !Number.isSafeInteger(expectedVersion) || expectedVersion < 1) throw new Error('Invalid revision');
    return write<Scene>(`${idPath(id)}/revisions/${version}/restore`, 'POST', { expectedVersion });
  },
  preview: (id: string, value: { type: string; platform: string; user: string; message: string; raw?: unknown; mode?: 'synthetic' | 'native' }) => write<{ persisted: boolean; liveActionsAllowed: boolean }>(`${idPath(id)}/preview-events`, 'POST', value),
};

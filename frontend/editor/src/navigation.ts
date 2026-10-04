// Guards flush draft overlays before hash/history navigation. Hidden workspaces stay mounted.
const guards = new Set<() => Promise<boolean>>();
export function navigationGuard(guard: () => Promise<boolean>): () => void { guards.add(guard); return () => { guards.delete(guard); }; }
export async function canNavigate(): Promise<boolean> { for (const guard of guards) if (!await guard()) return false; return true; }

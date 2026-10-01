import type { components } from './generated/api-types';

export type Status = components['schemas']['StatusResponse'];
export type Configuration = components['schemas']['ApplicationConfiguration'];

export class ApiError extends Error {
  constructor(public readonly status: number) { super(`Request failed (${status})`); }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(path, { credentials: 'same-origin', ...init });
  if (!response.ok) throw new ApiError(response.status);
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

async function write<T>(path: string, method: string, body?: unknown): Promise<T> {
  const csrf = await request<components['schemas']['CsrfResponse']>('/api/auth/csrf');
  if (!csrf.requestToken) throw new Error('Unable to initialize request protection');
  return request<T>(path, {
    method,
    headers: { 'Content-Type': 'application/json', 'X-TDSBLive-CSRF': csrf.requestToken },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}

export const api = {
  status: (signal?: AbortSignal) => request<Status>('/api/status', { signal }),
  configuration: () => request<Configuration>('/api/configuration'),
  saveConfiguration: (configuration: Configuration) => write<{ restartRequired: boolean }>('/api/configuration', 'PUT', configuration),
  login: (credential: string) => write<void>('/api/auth/login', 'POST', { credential } satisfies components['schemas']['AdminLogin']),
  logout: () => write<void>('/api/auth/logout', 'POST'),
};

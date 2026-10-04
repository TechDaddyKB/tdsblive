import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { App } from './App';
import { navigationGuard } from './navigation';
vi.mock('./SetupWizard', () => ({ SetupWizard: () => <p>Owned guide</p> }));
vi.mock('./VisualEditor', () => ({ VisualEditor: () => <section><h2>Owned overlay workspace</h2><input aria-label="Retained draft" defaultValue="" /></section> }));
beforeEach(() => {
  history.replaceState(null, '', '/editor#overview'); localStorage.clear();
  vi.stubGlobal('fetch', vi.fn().mockImplementation(async (url: string) => ({ ok: true, json: async () => url === '/api/status' ? { name: 'TDSBLive', lanEnabled: false } : url === '/api/integrations' ? { streamerBot: { state: 'connected' }, speakerBot: { state: 'authenticationFailed' } } : [] })));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); history.replaceState(null, '', '/'); });
it('navigates by hash, retains mounted drafts and follows browser history', async () => {
  render(<App />); await screen.findByText('Host ready. Loopback access only.');
  act(() => { location.hash = '#overlays'; }); await screen.findByText('Owned overlay workspace'); fireEvent.change(screen.getByLabelText('Retained draft'), { target: { value: 'Unfinished design' } });
  act(() => { location.hash = '#help'; }); await screen.findByText('A simple way to get started'); expect(screen.getByLabelText('Retained draft')).not.toBeVisible();
  act(() => { history.back(); }); await waitFor(() => expect(screen.getByLabelText('Retained draft')).toBeVisible()); expect(screen.getByLabelText('Retained draft')).toHaveValue('Unfinished design');
  expect(screen.getByLabelText('Streamer.bot connection status')).toHaveTextContent('Connected'); expect(screen.getByLabelText('Speaker.bot connection status')).toHaveTextContent('Connection failed');
});
it('refuses a page switch when saving conflicts and retains its current route', async () => {
  const dispose = navigationGuard(async () => false);
  try { render(<App />); await screen.findByText('Owned guide'); act(() => { location.hash = '#overlays'; }); await screen.findByRole('alert'); expect(location.hash).toBe('#overview'); expect(screen.queryByText('Owned overlay workspace')).not.toBeInTheDocument(); }
  finally { dispose(); }
});
it('persists theme overrides and exposes keyboard-reachable mobile navigation', async () => {
  render(<App />); fireEvent.change(screen.getByLabelText('Application theme'), { target: { value: 'dark' } }); expect(localStorage.getItem('tdsblive.theme')).toBe('dark'); expect(screen.getByRole('main')).toHaveAttribute('data-theme', 'dark');
  fireEvent.click(screen.getByRole('button', { name: 'Menu' })); expect(screen.getByRole('button', { name: 'Menu' })).toHaveAttribute('aria-expanded', 'true');
  act(() => { window.dispatchEvent(new CustomEvent('tdsblive:save-status', { detail: 'saving' })); }); expect(screen.getByLabelText('Global editor save status')).toHaveTextContent('saving');
  fireEvent.change(screen.getByLabelText('Application theme'), { target: { value: 'light' } }); expect(screen.getByRole('main')).toHaveAttribute('data-theme', 'light');
  act(() => { location.hash = '#workspace-content'; }); await waitFor(() => expect(screen.getByRole('link', { name: 'Overview' })).toHaveAttribute('aria-current', 'page'));
});

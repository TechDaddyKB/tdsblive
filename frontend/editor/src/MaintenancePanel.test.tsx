import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { MaintenancePanel } from './MaintenancePanel';

afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it('requires a checked backup and explicit replacement confirmation', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'test-token' })))
    .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'checked-backup', expiresAt: '2099-01-01' })));
  vi.stubGlobal('fetch', fetch);
  render(<MaintenancePanel />);
  expect(screen.getByRole('button', { name: 'Check backup' })).toBeDisabled();
  expect(screen.queryByRole('button', { name: 'Restore checked backup' })).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText('Backup ZIP'), { target: { files: [new File(['backup'], 'backup.zip')] } });
  fireEvent.click(screen.getByRole('button', { name: 'Check backup' }));
  const restore = await screen.findByRole('button', { name: 'Restore checked backup' });
  expect(restore).toBeDisabled();
  fireEvent.click(screen.getByRole('checkbox'));
  expect(restore).toBeEnabled();
  expect(fetch.mock.calls[1]?.[0]).toBe('/api/recovery/validate');
  fireEvent.change(screen.getByLabelText('Backup ZIP'), { target: { files: [new File(['different'], 'different.zip')] } });
  expect(screen.queryByRole('button', { name: 'Restore checked backup' })).not.toBeInTheDocument();
});

it('explains owner-only restrictions without suggesting the backup was restored', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'test-token' })))
    .mockResolvedValueOnce(new Response('', { status: 403 })));
  render(<MaintenancePanel />);
  fireEvent.change(screen.getByLabelText('Backup ZIP'), { target: { files: [new File(['backup'], 'backup.zip')] } });
  fireEvent.click(screen.getByRole('button', { name: 'Check backup' }));
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Open the editor on the computer running TDSBLive'));
  expect(screen.queryByRole('button', { name: 'Restore checked backup' })).not.toBeInTheDocument();
});

it('imports connection settings and explains the required restart', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'test-token' })))
    .mockResolvedValueOnce(new Response(JSON.stringify({ restartRequired: true })));
  vi.stubGlobal('fetch', fetch);
  render(<MaintenancePanel />);
  fireEvent.change(screen.getByLabelText('Connection settings JSON'), { target: { files: [new File(['{}'], 'settings.json')] } });
  fireEvent.click(screen.getByRole('button', { name: 'Import connection settings' }));
  expect(await screen.findByText(/Connection settings imported. Restart TDSBLive/)).toBeVisible();
  expect(fetch.mock.calls[1]?.[0]).toBe('/api/configuration/import');
  expect(screen.getByRole('button', { name: 'Restart TDSBLive' })).toBeEnabled();
});

it.each(['Restart', 'Quit'])('%s queues the operation once and disables further changes', async operation => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'test-token' })))
    .mockResolvedValueOnce(new Response('{}', { status: 202 }));
  vi.stubGlobal('fetch', fetch);
  render(<MaintenancePanel />);
  fireEvent.click(screen.getByRole('button', { name: `${operation} TDSBLive` }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Download backup' })).toBeDisabled());
  expect(fetch.mock.calls[1]?.[0]).toBe(`/api/application/${operation.toLowerCase()}`);
  expect(screen.getByRole('status')).toHaveTextContent(operation === 'Quit' ? 'TDSBLive is closing' : 'TDSBLive is restarting');
});

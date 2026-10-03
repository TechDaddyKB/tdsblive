import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { PortableControls } from './PortableControls';
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it('downloads both package kinds after flushing, excluding auth from URLs', async () => {
  const fetcher = vi.fn().mockResolvedValue(new Response('owned')); vi.stubGlobal('fetch', fetcher);
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn().mockReturnValue('blob:owned') });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() }); vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  const flush = vi.fn().mockResolvedValue(true); render(<PortableControls overlay="owned" widget="selected" flush={flush} imported={vi.fn()} />);
  fireEvent.click(screen.getByText('Export overlay')); await screen.findByText('Portable package exported. Widget state and access tokens are excluded.');
  expect(fetcher).toHaveBeenCalledWith('/api/overlays/owned/export'); expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:owned');
  fireEvent.click(screen.getByText('Export selected widget')); await waitFor(() => expect(fetcher).toHaveBeenCalledWith('/api/overlays/owned/export?widgetId=selected'));
  expect(flush).toHaveBeenCalledTimes(2);
});
it('imports a selected ZIP with CSRF and installs returned scene', async () => {
  const fetcher = vi.fn().mockResolvedValueOnce(Response.json({ requestToken: 'owned-csrf' })).mockResolvedValueOnce(Response.json({ id: 'import-owned', widgets: [] })); vi.stubGlobal('fetch', fetcher);
  const imported = vi.fn().mockResolvedValue(undefined); render(<PortableControls overlay="owned" flush={async () => true} imported={imported} />);
  const file = new File(['owned zip'], 'owned.sbxoverlay'); fireEvent.change(screen.getByLabelText('Import portable package'), { target: { files: [file] } });
  await screen.findByText('Package imported with new identities. Review custom code and grant permissions explicitly.'); expect(imported).toHaveBeenCalledWith({ id: 'import-owned', widgets: [] });
  expect(fetcher.mock.calls[1][1]).toMatchObject({ method: 'POST', body: file, headers: { 'X-Package-Filename': 'owned.sbxoverlay', 'X-TDSBLive-CSRF': 'owned-csrf' } });
});
it('rejects invalid files, unsaved state and failed endpoints with actionable errors', async () => {
  const fetcher = vi.fn().mockResolvedValue(new Response('', { status: 400 })); vi.stubGlobal('fetch', fetcher);
  const view = render(<PortableControls overlay="owned" widget="selected" flush={async () => false} imported={vi.fn()} />);
  fireEvent.click(screen.getByText('Export overlay')); await screen.findByText(/Export failed/); expect(fetcher).not.toHaveBeenCalled();
  view.rerender(<PortableControls overlay="owned" flush={async () => true} imported={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Import portable package'), { target: { files: [new File(['bad'], 'bad.zip')] } }); await screen.findByText(/Import failed/); expect(fetcher).not.toHaveBeenCalled();
  fireEvent.click(screen.getByText('Export overlay')); await waitFor(() => expect(fetcher).toHaveBeenCalled());
});

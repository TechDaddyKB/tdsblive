import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { FinancialPanel } from './FinancialPanel';
import { finance } from './financialApi';

vi.mock('./financialApi', () => ({ finance: {
  settings: vi.fn(), identities: vi.fn(), rules: vi.fn(), rates: vi.fn(), ledger: vi.fn(), totals: vi.fn(), reconcile: vi.fn(),
} }));
beforeEach(() => {
  vi.mocked(finance.settings).mockResolvedValue({ timeZone: 'UTC', version: 0 });
  vi.mocked(finance.identities).mockResolvedValue([]);
  vi.mocked(finance.rules).mockResolvedValue([]);
  vi.mocked(finance.rates).mockResolvedValue([]);
  vi.mocked(finance.ledger).mockResolvedValue({ items: [], totalCount: 0, offset: 0, limit: 50 });
  vi.mocked(finance.totals).mockResolvedValue({ period: 'all-time', timeZone: 'UTC', range: { startInclusive: null, endExclusive: null }, supporters: [] });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('loads operator controls without silently configuring nominal prices', async () => {
  render(<FinancialPanel />);
  expect(await screen.findByText(/Saved timezone: UTC/)).toBeVisible();
  expect(screen.getByRole('button', { name: 'Reconcile selected valuations' })).toBeDisabled();
  expect(screen.getByText(/Estimates do not establish platform revenue/)).toBeVisible();
  expect(finance.rules).toHaveBeenCalledOnce();
});

it('does not query a current stream period without its explicit start', async () => {
  render(<FinancialPanel />);
  await screen.findByText(/Saved timezone: UTC/);
  await waitFor(() => expect(finance.totals).toHaveBeenCalled());
  vi.mocked(finance.totals).mockClear();
  fireEvent.change(screen.getByLabelText('Totals period'), { target: { value: 'current-stream' } });
  await waitFor(() => expect(screen.getByText(/Set the stream start/)).toBeVisible());
  expect(finance.totals).not.toHaveBeenCalled();
});

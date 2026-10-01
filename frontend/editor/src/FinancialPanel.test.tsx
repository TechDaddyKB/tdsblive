import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { FinancialPanel } from './FinancialPanel';
import { finance, type FinancialEntry } from './financialApi';

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

const contribution: FinancialEntry = {
  id: 'owned-contribution', eventId: 'owned-event', supporterId: 'owned-person', identityId: 'owned-identity',
  supporterName: 'Owned viewer', platform: 'twitch', type: 'bits', source: 'owned-fixture', nativeEventId: 'owned-native',
  occurredAt: '2026-01-05T12:00:00Z', quantity: '100', nativeAmountMinor: null, nativeCurrency: null,
  nativeMinorUnitDigits: null, usdAmountMinor: null, valuationMethod: 'unknown', estimated: false,
  fxRate: null, fxRateDate: null, fxProvider: null, accountingState: 'counted', pendingReason: 'nominal_unconfigured',
  tier: '', giftRole: 'none', metadataJson: '{"owned":"<script>escaped</script>"}', version: 1,
};

it('retains the selected version across live polling and exposes reconciliation conflicts', async () => {
  vi.mocked(finance.ledger).mockResolvedValue({ items: [contribution], totalCount: 1, offset: 0, limit: 50 });
  vi.mocked(finance.reconcile).mockResolvedValue([{ id: contribution.id, outcome: 'conflict', version: 2 }]);
  render(<FinancialPanel />);
  const checkbox = await screen.findByLabelText('Select contribution owned-contribution');
  fireEvent.click(checkbox);
  vi.mocked(finance.ledger).mockResolvedValue({ items: [{ ...contribution, version: 2, quantity: '200' }], totalCount: 1, offset: 0, limit: 50 });
  await waitFor(() => expect(screen.getByText(/bits × 200/)).toBeVisible(), { timeout: 4000 });
  fireEvent.click(screen.getByRole('button', { name: 'Reconcile selected valuations' }));
  expect(await screen.findByText('Reconciliation outcomes: conflict')).toBeVisible();
  expect(finance.reconcile).toHaveBeenCalledWith([{ id: contribution.id, version: 1 }]);
  expect(screen.getByRole('button', { name: 'Reconcile selected valuations' })).toBeDisabled();
  expect(document.querySelector('pre script')).toBeNull();
});

it('prevents reconciliation of gated entries and reports fetch failures without payload details', async () => {
  vi.mocked(finance.ledger).mockResolvedValue({ items: [{ ...contribution, accountingState: 'gated' }], totalCount: 1, offset: 0, limit: 50 });
  render(<FinancialPanel />);
  expect(await screen.findByLabelText('Select contribution owned-contribution')).toBeDisabled();
  expect(screen.getByText('Needs evidence')).toBeVisible();
  vi.mocked(finance.ledger).mockRejectedValue(new Error('private upstream detail'));
  fireEvent.change(screen.getByLabelText('Ledger state'), { target: { value: 'gated' } });
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to refresh financial data.');
  expect(screen.queryByText('private upstream detail')).toBeNull();
});

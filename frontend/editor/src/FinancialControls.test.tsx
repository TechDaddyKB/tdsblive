import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { finance, type FinancialOperation } from './financialApi';
import { FinancialRulesPanel } from './FinancialRulesPanel';
import { FinancialRatesPanel } from './FinancialRatesPanel';
import { FinancialSettingsPanel } from './FinancialSettingsPanel';
import { FinancialIdentitiesPanel } from './FinancialIdentitiesPanel';

vi.mock('./financialApi', () => ({ finance: {
  saveRule: vi.fn(), removeRule: vi.fn(), saveSettings: vi.fn(), lookupRate: vi.fn(), refreshRate: vi.fn(), overrideRate: vi.fn(), removeOverride: vi.fn(), link: vi.fn(), unlink: vi.fn(),
} }));
const operation: FinancialOperation = async action => { await action(); };
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('edits a disabled rule with its current version and preserves fractional minor units', async () => {
  render(<FinancialRulesPanel rules={[{ platform: 'twitch', type: 'bits', tier: '', usdMinorPerUnit: '1.25', version: 4, enabled: false }]} busy={false} operation={operation} />);
  fireEvent.click(screen.getByRole('button', { name: 'Edit twitch bits' }));
  expect(screen.getByLabelText('USD per unit')).toHaveValue('0.0125');
  fireEvent.change(screen.getByLabelText('Rule platform'), { target: { value: 'kick' } });
  fireEvent.change(screen.getByLabelText('Support type'), { target: { value: 'subscription' } });
  fireEvent.change(screen.getByLabelText('Tier'), { target: { value: 'owned-tier' } });
  fireEvent.change(screen.getByLabelText('Rule platform'), { target: { value: 'twitch' } });
  fireEvent.change(screen.getByLabelText('Support type'), { target: { value: 'bits' } });
  fireEvent.change(screen.getByLabelText('Tier'), { target: { value: '' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save nominal value' }).closest('form')!);
  await waitFor(() => expect(finance.saveRule).toHaveBeenCalledWith({ platform: 'twitch', type: 'bits', tier: '', usdMinorPerUnit: '1.25', expectedVersion: 4 }));
});

it('removes a configured rule with its version rather than turning it into zero', async () => {
  render(<FinancialRulesPanel rules={[{ platform: 'kick', type: 'gift', tier: '', usdMinorPerUnit: '0', version: 3, enabled: true }]} busy={false} operation={operation} />);
  expect(screen.getByText(/\$0 per unit/)).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Remove kick gift' }));
  await waitFor(() => expect(finance.removeRule).toHaveBeenCalledWith({ platform: 'kick', type: 'gift', tier: '', expectedVersion: 3 }));
  expect(finance.saveRule).not.toHaveBeenCalled();
});

it('keeps exact rate strings and discloses unavailable and estimated observations', async () => {
  vi.mocked(finance.lookupRate).mockResolvedValueOnce({ available: false, rate: null }).mockResolvedValueOnce({ available: true, rate: { currency: 'EUR', requestedDate: '2026-01-05', rateDate: '2026-01-04', usdPerNativeUnit: '1.23456789', provider: 'owned-provider', estimated: true, origin: 'frankfurter' } });
  vi.mocked(finance.refreshRate).mockResolvedValue({ refreshed: false });
  render(<FinancialRatesPanel rates={[{ currency: 'EUR', requestedDate: '2026-01-05', rateDate: '2026-01-05', usdPerNativeUnit: '1.1', provider: 'manual', estimated: false, origin: 'manual' }]} busy={false} operation={operation} />);
  fireEvent.change(screen.getByLabelText('Rate currency'), { target: { value: 'eur' } });
  fireEvent.change(screen.getByLabelText('Rate date'), { target: { value: '2026-01-05' } });
  fireEvent.click(screen.getByRole('button', { name: 'Look up rate' }));
  expect(await screen.findByText('Unavailable; contributions remain pending.')).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Look up rate' }));
  expect(await screen.findByText(/1.23456789 USD · owned-provider · 2026-01-04 \(estimate\)/)).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Refresh provider rate' }));
  expect(await screen.findByText('Provider unavailable; cached rate preserved.')).toBeVisible();
  fireEvent.change(screen.getByLabelText('Manual USD rate'), { target: { value: '1.23456789' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save manual rate' }).closest('form')!);
  await waitFor(() => expect(finance.overrideRate).toHaveBeenCalledWith('EUR', '2026-01-05', '1.23456789'));
  fireEvent.click(screen.getByRole('button', { name: 'Remove EUR override for 2026-01-05' }));
  await waitFor(() => expect(finance.removeOverride).toHaveBeenCalledWith('EUR', '2026-01-05'));
});

it('saves explicit-offset stream starts in UTC without losing settings versions', async () => {
  render(<FinancialSettingsPanel settings={{ timeZone: 'UTC', currentStreamStartUtc: null, version: 8 }} busy={false} operation={operation} />);
  fireEvent.click(screen.getByRole('button', { name: 'Use browser timezone' }));
  fireEvent.click(screen.getByRole('button', { name: 'Use current time' }));
  expect(screen.getByLabelText('Current stream start')).not.toHaveValue('');
  fireEvent.click(screen.getByRole('button', { name: 'Clear stream start' }));
  expect(screen.getByLabelText('Current stream start')).toHaveValue('');
  fireEvent.change(screen.getByLabelText('Financial timezone'), { target: { value: 'America/Chicago' } });
  fireEvent.change(screen.getByLabelText('Current stream start'), { target: { value: '2026-01-05T14:00:00+02:00' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Save financial periods' }).closest('form')!);
  await waitFor(() => expect(finance.saveSettings).toHaveBeenCalledWith({ timeZone: 'America/Chicago', currentStreamStartUtc: '2026-01-05T12:00:00.000Z', version: 8 }));
});

it('links equal display names only through selected identity and supporter IDs', async () => {
  const twitch = { id: 'identity-a', supporterId: 'person-a', supporterName: 'Same name', displayName: 'Same name', identityKey: 'id:one', platform: 'twitch' };
  const kofi = { id: 'identity-b', supporterId: 'person-b', supporterName: 'Same name', displayName: 'Same name', identityKey: 'id:two', platform: 'kofi' };
  render(<FinancialIdentitiesPanel identities={[twitch, kofi]} busy={false} operation={operation} />);
  expect(screen.getByRole('button', { name: 'Link identity' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Identity to move'), { target: { value: twitch.id } });
  fireEvent.change(screen.getByLabelText('Link to supporter'), { target: { value: twitch.supporterId } });
  expect(screen.getByRole('button', { name: 'Link identity' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Link to supporter'), { target: { value: kofi.supporterId } });
  fireEvent.click(screen.getByRole('button', { name: 'Link identity' }));
  await waitFor(() => expect(finance.link).toHaveBeenCalledWith(twitch, kofi.supporterId));
  fireEvent.click(screen.getByRole('button', { name: 'Unlink identity' }));
  await waitFor(() => expect(finance.unlink).toHaveBeenCalledWith(twitch));
});

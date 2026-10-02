import { request, write } from './api';
import type { components } from './generated/api-types';

type Schema = components['schemas'];
export type FinancialEntry = Schema['FinancialEntry'];
export type FinancialSettings = Schema['FinancialSettings'];
export type FinancialIdentity = Schema['FinancialIdentity'];
export type NominalRule = Schema['NominalValuationRule'];
export type CachedRate = Schema['CachedRateEntry'];
export type FinancialTotals = Schema['FinancialTotalsResponse'];
export type FinancialDiagnostics = Schema['FinancialProjectionDiagnostics'];
export type FinancialPage = Schema['FinancialLedgerPage'];
export type FinancialSelection = Schema['FinancialVersionRef'];

const root = '/api/financial';
export const finance = {
  settings: () => request<FinancialSettings>(`${root}/settings`),
  saveSettings: (value: FinancialSettings) => write<FinancialSettings>(`${root}/settings`, 'PUT', value),
  ledger: (offset: number, state: string, signal?: AbortSignal) => request<FinancialPage>(`${root}/ledger?limit=50&offset=${offset}&state=${encodeURIComponent(state)}`, { signal }),
  totals: (period: string, start: string, end: string, signal?: AbortSignal) => request<FinancialTotals>(`${root}/totals?${new URLSearchParams({ period, ...(period === 'custom' ? { start, endExclusive: end } : {}) })}`, { signal }),
  identities: (search = '') => request<FinancialIdentity[]>(`${root}/identities?limit=1000&search=${encodeURIComponent(search)}`),
  rules: () => request<NominalRule[]>(`${root}/rules`),
  saveRule: (value: Schema['NominalRuleUpdate']) => write<void>(`${root}/rules`, 'PUT', value),
  removeRule: (value: Schema['NominalRuleRemoval']) => write<void>(`${root}/rules`, 'DELETE', value),
  rates: () => request<CachedRate[]>(`${root}/rates`),
  lookupRate: (currency: string, date: string) => write<Schema['RateLookupResponse']>(`${root}/rates/lookup`, 'POST', { currency, date }),
  refreshRate: (currency: string, date: string) => write<Schema['RateRefreshResponse']>(`${root}/rates/refresh`, 'POST', { currency, date }),
  overrideRate: (currency: string, date: string, usdPerNativeUnit: string) => write<void>(`${root}/rates/override`, 'PUT', { currency, date, usdPerNativeUnit }),
  removeOverride: (currency: string, date: string) => write<void>(`${root}/rates/override`, 'DELETE', { currency, date }),
  link: (identity: FinancialIdentity, targetSupporterId: string) => write<Schema['IdentityTransferResponse']>(`${root}/identities/${encodeURIComponent(identity.id)}/link`, 'POST', { expectedSupporterId: identity.supporterId, targetSupporterId }),
  unlink: (identity: FinancialIdentity) => write<Schema['IdentityTransferResponse']>(`${root}/identities/${encodeURIComponent(identity.id)}/unlink`, 'POST', { expectedSupporterId: identity.supporterId }),
  reconcile: (selected: FinancialSelection[]) => write<Schema['FinancialReconcileResult'][]>(`${root}/reconcile`, 'POST', { selected }),
  diagnostics: (signal?: AbortSignal) => request<FinancialDiagnostics>(`${root}/diagnostics`, { signal }),
};

export type FinancialOperation = (action: () => Promise<unknown>, success: string) => Promise<void>;

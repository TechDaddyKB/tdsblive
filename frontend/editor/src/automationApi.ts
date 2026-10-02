import { request, write } from './api';
import type { components } from './generated/api-types';

type Schema = components['schemas'];
export type AutomationRule = Schema['AutomationRule'];
export type AutomationAction = Schema['AutomationAction'];
export type AutomationExecution = Schema['AutomationExecution'];
export type AutomationCapabilityReport = Schema['AutomationCapabilityReport'];
export type AutomationTemporaryEffect = Schema['AutomationTemporaryEffect'];
export type AutomationAudioAsset = Schema['AssetInfo'];
export type AutomationSoundOverlay = Schema['OverlayDefinition'];
export const automation = {
  soundAssets: (signal?: AbortSignal) => request<AutomationAudioAsset[]>('/api/assets', { signal }),
  soundOverlays: (signal?: AbortSignal) => request<AutomationSoundOverlay[]>('/api/overlays', { signal }),
  capabilities: (signal?: AbortSignal) => request<AutomationCapabilityReport>('/api/automation/capabilities', { signal }),
  temporaryEffects: (signal?: AbortSignal) => request<AutomationTemporaryEffect[]>('/api/automation/temporary-effects', { signal }),
  resolveRestored: (effect: AutomationTemporaryEffect) => write<void>(`/api/automation/temporary-effects/${encodeURIComponent(effect.actionId!)}/resolve`, 'POST', { version: effect.version, externalStateRestored: true }),
  rules: (signal?: AbortSignal) => request<AutomationRule[]>('/api/automation/rules', { signal }),
  save: (rule: AutomationRule) => write<AutomationRule>(`/api/automation/rules/${encodeURIComponent(rule.id!)}`, 'PUT', rule),
  remove: (rule: AutomationRule) => write<void>(`/api/automation/rules/${encodeURIComponent(rule.id!)}?version=${rule.version}`, 'DELETE'),
  executions: (signal?: AbortSignal) => request<AutomationExecution[]>('/api/automation/executions', { signal }),
  moderate: (receipt: AutomationExecution, approve: boolean) => write<void>(`/api/automation/executions/${encodeURIComponent(receipt.id!)}/moderate`, 'POST', { version: receipt.version, approve, state: receipt.state }),
  language: (receipt: AutomationExecution, language: string) => write<void>(`/api/automation/executions/${encodeURIComponent(receipt.id!)}/language`, 'POST', { version: receipt.version, language }),
  simulate: (value: Schema['AutomationPreviewRequest'] & { rule?: AutomationRule }) => write<unknown>('/api/automation/simulate', 'POST', value),
};

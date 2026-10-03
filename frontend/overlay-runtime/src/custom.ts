export interface CustomSettings {
  manifestVersion: number; packageVersion: string; author: string; html: string; css: string; javaScript: string;
  fields: import('../../editor/src/SettingsFields').SettingsField[];
  config: Record<string, import('../../editor/src/SettingsFields').SettingValue>;
  subscriptions: string[]; permissions: string[]; networkDomains: string[]; assetIds: string[];
}
export const defaultCustom: CustomSettings = {
  manifestVersion: 1, packageVersion: '1.0.0', author: '', html: '<div id="message">Custom widget</div>',
  css: 'body { color: white; background: transparent; }',
  javaScript: "SBX.on('chat.message', e => { document.getElementById('message').textContent = e.message?.text ?? ''; });",
  fields: [], config: {}, subscriptions: ['chat.message'], permissions: [], networkDomains: [], assetIds: [],
};
export interface CustomDelivery { widgetId: string; event: Record<string, unknown> }

// Every request is scoped to the specific iframe instance. A message from a
// sibling frame, a navigation replacement, or a previous generation is rejected.
export function validFrameMessage(event: MessageEvent, frame: Window | null, channel: string): boolean {
  if (!frame || event.source !== frame || event.origin !== 'null' || !event.data || typeof event.data !== 'object') return false;
  const data = event.data as Record<string, unknown>;
  try { return data.channel === channel && typeof data.op === 'string' && ['ready', 'store', 'error'].includes(data.op) && JSON.stringify(data).length <= 40000; } catch { return false; }
}

export function customCsp(settings: CustomSettings, nonce: string): string {
  // Exact HTTPS origins only; never wildcard/local hosts or application origin.
  const domains = settings.permissions.includes('network') ? settings.networkDomains.filter(d =>
    /^[a-z0-9]+(?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,}$/.test(d) && !/\.(local|localhost|internal)$/.test(d)).map(d => `https://${d}`).join(' ') : '';
  return `default-src 'none'; script-src 'nonce-${nonce}'; style-src 'unsafe-inline'; worker-src blob:; connect-src ${domains || "'none'"}; img-src data: blob: ${domains}; media-src ${settings.permissions.includes('audio') ? 'data: blob: ' + domains : "'none'"}; font-src data: blob:; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'`;
}

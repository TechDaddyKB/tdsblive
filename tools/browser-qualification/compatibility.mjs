import { editorUI } from './editor-ui.mjs';
import assert from 'node:assert/strict';

export async function qualifyCompatibility(page, origin, writeHeaders) {
  const scene = await (await fetch(`${origin}/api/overlays/g12-browser`)).json();
  const widget = scene.widgets[0];
  widget.custom.permissions = ['chat', 'storage'];
  widget.custom.networkDomains = [];
  widget.custom.subscriptions = ['chat.message', 'future.available'];
  widget.custom.config = { label: 'Local compatibility' };
  widget.custom.html = '<div id="load"></div><div id="event"></div><div id="session"></div><div id="store"></div><div id="warning"></div>';
  widget.custom.javaScript = `
    const SE_API = SBX.enableStreamElements();
    window.addEventListener('onWidgetLoad', e => document.getElementById('load').textContent = e.detail.fieldData.label);
    window.addEventListener('onSessionUpdate', e => document.getElementById('session').textContent = e.detail.isEditorMode ? 'Preview' : 'Runtime');
    window.addEventListener('onEventReceived', e => {
      if (e.detail.listener === 'message') document.getElementById('event').textContent = e.detail.event.message.text;
    });
    SE_API.store.get('owned').then(s => document.getElementById('store').textContent = 'Restored '+(s?.count ?? 0));
    SBX.on('future.available', async () => { await SE_API.store.set('owned', { count: 9 }); document.getElementById('store').textContent = 'Saved 9'; });
    try { SE_API.counters.get('remote'); } catch (e) { document.getElementById('warning').textContent = e.message; }
  `;
  assert.equal((await fetch(`${origin}/api/overlays/g12-browser`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(scene) })).status, 200);
  const runtime = await page.context().newPage();
  try {
    await runtime.goto(`${origin}/overlay/g12-browser?preview=1`);
    const frame = runtime.frameLocator('iframe[title="Custom"]');
    await frame.locator('#load').getByText('Local compatibility', { exact: true }).waitFor();
    await frame.locator('#session').getByText('Preview', { exact: true }).waitFor();
    await frame.locator('#store').getByText('Restored 0', { exact: true }).waitFor();
    await frame.locator('#warning').getByText(/counters is unsupported locally/).waitFor();
    const send = async (type, message) => {
      assert.equal((await fetch(`${origin}/api/overlays/g12-browser/preview-events`, { method: 'POST', headers: writeHeaders,
        body: JSON.stringify({ type, platform: 'general', user: 'Owned', message }) })).status, 200);
    };
    await send('chat.message', 'Shim chat delivered'); await frame.locator('#event').getByText('Shim chat delivered', { exact: true }).waitFor();
    await send('future.available', 'Store fixture'); await frame.locator('#store').getByText('Saved 9', { exact: true }).waitFor();
    await runtime.reload(); await frame.locator('#store').getByText('Restored 9', { exact: true }).waitFor();
    const state = await (await fetch(`${origin}/api/overlays/g12-browser/widgets/${widget.id}/store?preview=1`)).json();
    assert.deepEqual(state['se:owned'], { count: 9 });
    const diagnostics = await (await fetch(`${origin}/api/diagnostics/export`)).json();
    assert.equal(diagnostics.includesUserData, false); assert.equal(diagnostics.includesLogs, false);
    await page.goto(`${origin}/editor#settings`);
    const download = page.waitForEvent('download'); await page.getByRole('button', { name: 'Export sanitized diagnostics', exact: true }).click();
    assert.equal((await download).suggestedFilename(), 'TDSBLive-diagnostics.json');
    console.log('G13 rendered local compatibility passed: lifecycle, canonical chat, scoped store/reload, unsupported warning and aggregate diagnostic download');
  } finally { await runtime.close(); }
}

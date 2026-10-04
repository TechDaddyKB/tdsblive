import { editorUI } from './editor-ui.mjs';
import assert from 'node:assert/strict';

export async function qualifyCustomWidgets(page, origin, writeHeaders) {
  await page.goto(`${origin}/editor#overlays`);
  const editor = editorUI(page);
  await editor.getByLabel('New overlay ID').fill('g12-browser'); await editor.getByLabel('New overlay name').fill('Custom widget qualification');
  await editor.getByRole('button', { name: 'Create overlay', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay"]')?.value === 'g12-browser');
  await editor.getByRole('button', { name: 'Add custom', exact: true }).click();
  await page.locator('.monaco-editor').waitFor();
  await editor.getByLabel('Allow chat', { exact: true }).check(); await editor.getByLabel('Allow storage', { exact: true }).check();
  await editor.getByLabel('Custom subscriptions').fill('chat.message,future.available');
  await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  const read = async () => (await fetch(`${origin}/api/overlays/g12-browser`)).json();
  let scene = await read(); const widget = scene.widgets[0];
  assert.equal(widget.kind, 'custom'); assert.deepEqual(widget.custom.permissions, ['chat', 'storage']);
  // Exercise real Monaco model editing rather than replacing a textarea facade.
  await editor.getByRole('button', { name: 'CSS', exact: true }).click();
  await page.getByRole('textbox', { name: 'css source', exact: true }).focus(); await page.keyboard.press('Control+a');
  await page.keyboard.insertText('body { color: rgb(0,255,0); background: transparent; }');
  await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  await page.waitForFunction(async () => (await (await fetch('/api/overlays/g12-browser')).json()).widgets[0].custom.css.includes('rgb(0,255,0)'));
  assert.match((await read()).widgets[0].custom.css, /rgb\(0,255,0\)/);
  // Add hostile markup and worker probes using the ordinary save endpoint.
  scene = await read();
  scene.widgets[0].custom.html = '<div id="message">Waiting</div><script>parent.document.body.innerHTML="escaped"</script><iframe src="https://example.com/frame"></iframe><a href="https://example.com/navigate">bad</a><img src="https://example.com/image" onerror="parent.postMessage({op:\'store\'},\'*\')">';
  scene.widgets[0].custom.javaScript = `
    const out = document.getElementById('message');
    const checks = [];
    try { parent.document; checks.push('parent-leak'); } catch { checks.push('parent-blocked'); }
    checks.push(typeof location.assign === 'undefined' && typeof location.replace === 'undefined' ? 'navigation-blocked' : 'navigation-leak');
    checks.push(typeof document.cookie === 'undefined' ? 'cookies-blocked' : 'cookie-leak');
    fetch('https://example.com/exfil').then(() => checks.push('network-leak')).catch(() => checks.push('network-blocked'));
    SBX.store.get().then(s => { out.textContent = 'Restored '+(s.count ?? 0); });
    SBX.on('future.available', async e => { await SBX.store.set({ count: 7 }); out.textContent = e.message.text+' '+checks.join(' '); });
    SBX.on('chat.message', e => out.textContent = e.message.text);
  `;
  assert.equal((await fetch(`${origin}/api/overlays/g12-browser`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(scene) })).status, 200);
  let external = 0; const runtime = await page.context().newPage();
  runtime.on('request', r => { if (r.url().startsWith('https://example.com/')) external++; });
  await runtime.goto(`${origin}/overlay/g12-browser?preview=1`);
  const iframe = runtime.frameLocator('iframe[title="Custom"]');
  await iframe.locator('#message').getByText('Restored 0', { exact: true }).waitFor();
  assert.equal(await runtime.locator('iframe').getAttribute('sandbox'), 'allow-scripts');
  assert.equal(await iframe.locator('iframe,a,[onerror]').count(), 0);
  assert.equal(await iframe.locator('img').getAttribute('src'), null);
  const probe = await runtime.frames().find(f => f.parentFrame())?.evaluate(() => {
    try { return { parent: parent.document.body.textContent, cookies: document.cookie }; } catch { return { blocked: true }; }
  });
  assert.deepEqual(probe, { blocked: true });
  const send = async (type, message, raw = {}) => {
    assert.equal((await fetch(`${origin}/api/overlays/g12-browser/preview-events`, { method: 'POST', headers: writeHeaders,
      body: JSON.stringify({ type, platform: 'general', user: 'Owned', message, raw }) })).status, 200);
  };
  await send('future.available', 'Available arbitrary event');
  await iframe.locator('#message').getByText('Available arbitrary event parent-blocked navigation-blocked cookies-blocked network-blocked', { exact: true }).waitFor();
  assert.equal(external, 0, 'Denied network request reached the network');
  const stored = await (await fetch(`${origin}/api/overlays/g12-browser/widgets/${widget.id}/store?preview=1`)).json(); assert.deepEqual(stored, { count: 7 });
  assert.deepEqual(await (await fetch(`${origin}/api/overlays/g12-browser/widgets/${widget.id}/store`)).json(), {});
  // Wrong source/channel spoof must not write storage.
  await runtime.evaluate(() => window.postMessage({ channel: 'spoofed', op: 'store', id: 1, method: 'set', value: { count: 99 } }, location.origin));
  await runtime.reload(); await iframe.locator('#message').getByText('Restored 7', { exact: true }).waitFor();
  assert.equal((await (await fetch(`${origin}/api/overlays/g12-browser/widgets/${widget.id}/store?preview=1`)).json()).count, 7);
  // Disable chat/raw/financial/audio: arbitrary unprivileged events remain available.
  scene = await read(); scene.widgets[0].custom.permissions = ['storage'];
  assert.equal((await fetch(`${origin}/api/overlays/g12-browser`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(scene) })).status, 200);
  await iframe.locator('#message').getByText('Restored 7', { exact: true }).waitFor();
  await send('chat.message', 'Denied chat');
  await send('future.available', 'Still permitted');
  await iframe.locator('#message').getByText('Still permitted parent-blocked navigation-blocked cookies-blocked network-blocked', { exact: true }).waitFor();
  // An explicit exact-domain grant permits only that origin, without parent credentials.
  await page.context().route('https://example.com/**', route => route.request().url().endsWith('/permitted') ? route.fulfill({ status: 200, headers: { 'Access-Control-Allow-Origin': '*' }, contentType: 'text/plain', body: 'Allowlisted domain reached' }) : route.abort());
  scene = await read(); scene.widgets[0].custom.permissions = ['storage', 'network']; scene.widgets[0].custom.networkDomains = ['example.com'];
  scene.widgets[0].custom.html += '<p id="network">Network pending</p>';
  scene.widgets[0].custom.javaScript += "; fetch('https://example.com/permitted').then(r=>r.text()).then(t=>document.getElementById('network').textContent=t);";
  assert.equal((await fetch(`${origin}/api/overlays/g12-browser`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(scene) })).status, 200);
  await iframe.locator('#network').getByText('Allowlisted domain reached', { exact: true }).waitFor();
  await page.context().unroute('https://example.com/**');
  await runtime.close();
  // Portable overlay and widget are created by UI and imported through UI.
  await page.reload(); await editor.getByLabel('Overlay', { exact: true }).selectOption('g12-browser');
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay"]')?.value === 'g12-browser');
  await editor.getByRole('button', { name: 'Custom', exact: true }).click();
  let download = page.waitForEvent('download'); await editor.getByRole('button', { name: 'Export overlay', exact: true }).click();
  const overlay = await download; const overlayPath = await overlay.path(); assert.ok(overlayPath);
  await editor.getByLabel('Import portable package').setInputFiles({ name: 'owned.sbxoverlay', mimeType: 'application/zip', buffer: await (await import('node:fs/promises')).readFile(overlayPath) });
  await page.getByText('Package imported with new identities. Review custom code and grant permissions explicitly.', { exact: true }).waitFor();
  let imported = (await (await fetch(`${origin}/api/overlays`)).json()).find(o => o.id.startsWith('import-'));
  assert.ok(imported); assert.equal(imported.widgets[0].custom.javaScript, scene.widgets[0].custom.javaScript); assert.deepEqual(imported.widgets[0].custom.permissions, []);
  await editor.getByRole('button', { name: 'Custom', exact: true }).click();
  download = page.waitForEvent('download'); await editor.getByRole('button', { name: 'Export selected widget', exact: true }).click();
  const selected = await download; const selectedPath = await selected.path(); assert.ok(selectedPath);
  await editor.getByLabel('Import portable package').setInputFiles({ name: 'owned.sbxwidget', mimeType: 'application/zip', buffer: await (await import('node:fs/promises')).readFile(selectedPath) });
  await page.waitForFunction(() => document.querySelectorAll('.canvas-widget').length === 2);
  imported = await (await fetch(`${origin}/api/overlays/${imported.id}`)).json(); assert.equal(imported.widgets.length, 2); assert.notEqual(imported.widgets[0].id, imported.widgets[1].id);
  console.log('G12 real-browser qualification passed: Monaco, opaque frame/worker, denied networking/navigation/parent access, arbitrary subscriptions, durable preview-isolated storage, message spoofing and portable package UI round-trips');
}

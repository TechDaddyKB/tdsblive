// Owns only a fresh local host and temporary data; never runs live integrations.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, writeFile, rm, realpath } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from 'playwright';

const mode = process.argv[2] ?? 'managed';
assert.ok(['managed', 'portable', 'installed'].includes(mode), 'Choose managed, portable or installed qualification');
const directory = await mkdtemp(path.join(tmpdir(), 'tdsblive-process-recovery-'));
const data = path.join(directory, 'data');
const { mkdir } = await import('node:fs/promises');
await mkdir(data);
const port = await new Promise((resolve, reject) => {
  const server = createServer(); server.on('error', reject);
  server.listen(0, '127.0.0.1', () => { const port = server.address().port; server.close(() => resolve(port)); });
});
await writeFile(path.join(data, 'configuration.json'), JSON.stringify({ server: { host: '127.0.0.1', port } }));
const origin = `http://127.0.0.1:${port}`;
let executable = path.join(process.env.DOTNET_ROOT, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
if (mode !== 'managed') {
  assert.equal(process.platform, 'win32', 'Packaged qualification requires Windows');
  const packageRoot = path.resolve(process.env.TDSBLIVE_PACKAGE_CHECK_ROOT);
  const relative = path.relative(await realpath(process.env.RUNNER_TEMP), packageRoot);
  assert.match(relative, /^tdsblive-package-check-[a-f0-9]{32}$/,
    'Only the owned Windows package qualification directory can be used');
  const candidate = path.join(packageRoot, mode === 'portable' ? 'portable' : 'installed', 'TDSBLive.exe');
  assert.equal(await realpath(candidate), candidate, 'Packaged executable must not resolve through a symbolic link');
  executable = candidate;
}
const argumentsList = ['--TDSBLive:DataDirectory', data, '--TDSBLive:OpenEditor=false', '--TDSBLive:DesktopMode=off'];
if (mode === 'managed') argumentsList.unshift(path.resolve('src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'));
const child = spawn(executable, argumentsList, { stdio: 'ignore' });
let launchError = false;
child.on('error', () => { launchError = true; });
let cookie = ''; let token = ''; let generation;
let browser; let subscriptions = 0;
async function subscribedAfter(previous) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (subscriptions > previous) return;
    await delay(100);
  }
  throw new Error('Owned overlay browser did not connect or reconnect');
}
async function get(route) {
  const response = await fetch(origin + route, { signal: AbortSignal.timeout(2000) });
  assert.ok(response.ok); return response.json();
}
async function ready(previous) {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    assert.ok(!launchError, 'Owned host executable could not launch');
    try { const state = await get('/api/application/status'); if (state.generation !== previous) return state.generation; }
    catch { /* The owned process is starting or restarting. */ }
    await delay(100);
  }
  throw new Error('Owned host did not start a new generation');
}
async function protect() {
  const response = await fetch(origin + '/api/auth/csrf'); assert.ok(response.ok);
  cookie = response.headers.getSetCookie().map(value => value.split(';')[0]).join('; ');
  token = (await response.json()).requestToken; assert.ok(token);
}
async function write(route, body, method = 'POST', raw = false) {
  const response = await fetch(origin + route, { method, signal: AbortSignal.timeout(30000),
    headers: { Cookie: cookie, Origin: origin, 'X-TDSBLive-CSRF': token,
      'Content-Type': raw ? 'application/zip' : 'application/json' },
    ...(body === undefined ? {} : { body: raw ? body : JSON.stringify(body) }) });
  assert.ok(response.ok, `Owned request failed: ${route} (${response.status})`); return response;
}
try {
  generation = await ready(); await protect();
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  page.on('websocket', socket => socket.on('framereceived', frame => {
    try { if (JSON.parse(String(frame.payload)).op === 'subscribed') subscriptions++; }
    catch { /* Other browser traffic is not a subscription acknowledgment. */ }
  }));
  const imageUpload = await fetch(origin + '/api/assets', { method: 'POST', headers: { Cookie: cookie, Origin: origin, 'X-TDSBLive-CSRF': token,
    'Content-Type': 'image/gif', 'X-Asset-Filename': 'owned-recovery.gif' }, body: Buffer.from('47494638396101000100800000ff00000000ff2c00000000010001000002024401003b', 'hex') });
  assert.ok(imageUpload.ok); const image = await imageUpload.json();
  const groupId = crypto.randomUUID();
  const advanced = await (await write('/api/overlays', { id: 'g11-recovery', name: 'Owned advanced recovery', width: 1920, height: 1080, canvasEnabled: true, widgets: [
    { id: crypto.randomUUID(), name: 'Owned event list', kind: 'event-list', groupId, x: 100, y: 100, rotation: 15 },
    { id: crypto.randomUUID(), name: 'Owned progress', kind: 'goal-bar', groupId, x: 600, y: 100, rotation: 15, progress: { value: 25, target: 50 } },
    { id: crypto.randomUUID(), name: 'Owned image', kind: 'image', x: 100, y: 400, assetId: image.id },
    { id: crypto.randomUUID(), name: 'Owned custom state', kind: 'custom', x: 600, y: 400, custom: {
      permissions: ['storage'], subscriptions: ['future.available'], html: '<p id="counter">Loading state</p><p id="legacy">Loading compatibility</p>',
      javaScript: 'const SE_API = SBX.enableStreamElements(); function refresh() { SBX.store.get().then(s => document.getElementById("counter").textContent = "State "+s.count); SE_API.store.get("owned").then(s => document.getElementById("legacy").textContent = "Local SE state "+s.count); } refresh(); SBX.on("sbx:session", s => { if (s.connected) refresh(); });'
    } },
  ] })).json();
  const widgetStorePath = `/api/overlays/g11-recovery/widgets/${advanced.widgets[3].id}/store`;
  await write(widgetStorePath, { count: 7, 'se:owned': { count: 7 } }, 'PUT');
  await page.goto(origin + '/overlay/g11-recovery');
  await subscribedAfter(0);
  await page.getByRole('progressbar').waitFor();
  await page.waitForFunction(() => document.querySelector('.runtime-widget > img')?.naturalWidth === 1);
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('State 7', { exact: true }).waitFor();
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('Local SE state 7', { exact: true }).waitFor();
  await write('/api/setup', { step: 2, reviewed: false, version: 0 }, 'PUT');
  const backup = Buffer.from(await (await write('/api/recovery/backup')).arrayBuffer());
  await write('/api/overlays/g11-recovery', { ...advanced, widgets: advanced.widgets.map(w => w.kind === 'goal-bar' ? { ...w, progress: { ...w.progress, value: 40 } } : w) }, 'PUT');
  await write(widgetStorePath, { count: 9, 'se:owned': { count: 9 } }, 'PUT');
  await write('/api/setup', { step: 4, reviewed: false, version: 1 }, 'PUT');
  let previousSubscriptions = subscriptions;
  await write('/api/application/restart');
  generation = await ready(generation); await protect();
  await subscribedAfter(previousSubscriptions);
  assert.equal((await get('/api/setup')).step, 4);
  assert.equal((await get(widgetStorePath)).count, 9);
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('State 9', { exact: true }).waitFor();
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('Local SE state 9', { exact: true }).waitFor();
  assert.equal((await get('/api/overlays/g11-recovery')).widgets[1].progress.value, 40);
  await page.waitForFunction(() => document.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow') === '40');
  const preview = await (await write('/api/recovery/validate', backup, 'POST', true)).json();
  previousSubscriptions = subscriptions;
  await write('/api/recovery/restore', { id: preview.id, confirm: true });
  generation = await ready(generation); await protect();
  await subscribedAfter(previousSubscriptions);
  assert.equal((await get('/api/setup')).step, 2);
  assert.equal((await get(widgetStorePath)).count, 7);
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('State 7', { exact: true }).waitFor();
  await page.frameLocator('iframe[title="Owned custom state"]').getByText('Local SE state 7', { exact: true }).waitFor();
  const restored = await get('/api/overlays/g11-recovery');
  assert.deepEqual(restored.widgets, advanced.widgets);
  await page.waitForFunction(() => document.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow') === '25');
  await page.waitForFunction(() => document.querySelector('.runtime-widget > img')?.naturalWidth === 1);
  assert.equal(await page.locator('.runtime-widget').first().evaluate(node => node.style.transform), 'rotate(15deg)');
  const configuration = await get('/api/configuration');
  assert.equal(configuration.streamerBot.enabled, false);
  assert.equal(configuration.speakerBot.enabled, false);
  assert.equal(configuration.rumble.enabled, false);
  assert.equal(configuration.server.enableLan, false);
  await write('/api/application/quit');
  const deadline = Date.now() + 10000;
  let stopped = false;
  while (Date.now() < deadline) {
    try { await get('/api/application/status'); } catch { stopped = true; break; }
    await delay(100);
  }
  assert.ok(stopped, 'Owned host did not quit');
  console.log('G10/G11/G12/G13 real-process restart/restore passed: open overlay browser reconnects, SBX and local SE storage, grouped transforms, advanced settings and referenced media restored, new generations, safety-paused integrations and quit');
} finally {
  // Stop only the host at the random port belonging to this temporary data root.
  try { await protect(); await write('/api/application/quit'); await delay(500); } catch { /* Already stopped. */ }
  await browser?.close();
  if (child.exitCode === null) child.kill();
  // HTTP stops accepting requests before the final SQLite handles close on Windows.
  // Retry only the owned directory; persistent locks still fail qualification.
  await rm(directory, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
}

// CI-only fresh-browser qualification; never controls the user's personal browser.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const dotnetRoot = process.env.DOTNET_ROOT;
assert.ok(dotnetRoot && path.isAbsolute(dotnetRoot), 'CI must supply an absolute setup-dotnet installation directory');
const dotnetExecutable = path.join(dotnetRoot, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
const directory = await mkdtemp(path.join(tmpdir(), 'tdsblive-browser-test-'));
const port = await new Promise((resolve, reject) => {
  const server = createServer();
  server.on('error', reject);
  server.listen(0, '127.0.0.1', () => {
    const port = server.address().port;
    server.close(() => resolve(port));
  });
});
await writeFile(path.join(directory, 'configuration.json'), JSON.stringify({ server: { host: '127.0.0.1', port } }));
const origin = `http://127.0.0.1:${port}`;
const host = spawn(dotnetExecutable, [path.join(root, 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
  '--TDSBLive:DataDirectory', directory], { stdio: 'ignore' });
let spawnFailed = false;
host.on('error', () => { spawnFailed = true; });
let browser;
try {
  const deadline = Date.now() + 30_000;
  let ready = false;
  while (Date.now() < deadline) {
    assert.ok(!spawnFailed && host.exitCode === null, 'Isolated host failed before readiness');
    try { ready = (await fetch(`${origin}/api/status`)).ok; } catch { /* Await this live child only. */ }
    if (ready) break;
    await delay(100);
  }
  assert.ok(ready, 'Isolated host readiness timed out');
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  let pageErrors = 0;
  page.on('pageerror', () => { pageErrors++; });
  assert.equal((await page.goto(`${origin}/editor`)).status(), 200);
  await page.getByRole('heading', { name: 'TDSBLive', exact: true }).waitFor();
  await page.getByText('Host ready. Loopback access only.', { exact: true }).waitFor();
  assert.match(await page.getByLabel('Streamer.bot connection status').innerText(), /Disconnected/);
  await mkdir(path.join(root, 'artifacts'), { recursive: true });
  await page.screenshot({ path: path.join(root, 'artifacts/g02-editor.png') });
  assert.equal((await page.goto(`${origin}/login`)).status(), 200);
  await page.getByRole('heading', { name: 'TDSBLive sign in', exact: true }).waitFor();
  assert.equal(await page.getByLabel('Admin credential').getAttribute('type'), 'password');
  await page.addInitScript(() => {
    const Original = window.WebSocket;
    window.tdsbliveTestSockets = [];
    window.WebSocket = class extends Original {
      constructor(...args) {
        super(...args); window.tdsbliveTestSockets.push(this);
        this.addEventListener('message', event => { try { if (JSON.parse(event.data).op === 'subscribed') this.g05Subscribed = true; } catch { /* Malformed-frame qualification is covered by unit tests. */ } });
      }
    };
  });
  assert.equal((await page.goto(`${origin}/overlay/combined-chat?preview=1`)).status(), 200);
  await page.getByLabel('Combined chat', { exact: true }).waitFor();
  assert.equal(await page.evaluate(() => getComputedStyle(document.body).backgroundColor), 'rgba(0, 0, 0, 0)');
  await page.waitForFunction(() => window.tdsbliveTestSockets?.[0]?.g05Subscribed);
  const csrfResponse = await fetch(`${origin}/api/auth/csrf`);
  const csrfCookie = csrfResponse.headers.getSetCookie().map(value => value.split(';')[0]).join('; ');
  const csrfToken = (await csrfResponse.json()).requestToken;
  const writeHeaders = { 'Content-Type': 'application/json', 'Origin': origin, 'Cookie': csrfCookie, 'X-TDSBLive-CSRF': csrfToken };
  assert.ok(csrfToken);
  let overlay = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  overlay.chat.persistent = true;
  overlay.chat.maximumMessages = 10;
  assert.equal((await fetch(`${origin}/api/overlays/combined-chat`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(overlay) })).status, 200);
  for (const platform of ['twitch', 'youtube', 'kick', 'rumble']) {
    assert.equal((await fetch(`${origin}/api/test-event`, { method: 'POST', headers: writeHeaders, body: JSON.stringify({ event: {
      occurredAt: new Date().toISOString(), source: 'g05-browser-test', platform, type: 'chat.message', nativeType: 'SyntheticChat', dedupeKey: platform,
      user: { login: 'synthetic-viewer', displayName: `${platform} viewer`, badges: ['moderator'] }, message: { text: `${platform} synthetic <script>escaped</script>` },
    } }) })).status, 200);
    await page.getByText(`${platform} synthetic <script>escaped</script>`, { exact: true }).waitFor();
  }
  assert.equal(await page.locator('.chat-row').count(), 4);
  assert.equal(await page.locator('.chat-content script').count(), 0);
  assert.equal(await page.evaluate(() => window.tdsbliveTestSockets.length), 1, 'One shared socket must serve the overlay');
  await page.evaluate(() => window.tdsbliveTestSockets[0].close());
  await page.waitForFunction(() => window.tdsbliveTestSockets.length === 2 && window.tdsbliveTestSockets[1].g05Subscribed);
  assert.equal(await page.locator('.chat-row').count(), 4, 'Reconnect must retain messages without duplicate DOM entries');
  overlay = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  overlay.chat.maximumMessages = 2;
  assert.equal((await fetch(`${origin}/api/overlays/combined-chat`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(overlay) })).status, 200);
  await page.waitForFunction(() => document.querySelectorAll('.chat-row').length === 2);
  await page.screenshot({ path: path.join(root, 'artifacts/g05-overlay.png') });
  assert.equal((await page.goto(`${origin}/chat/combined-chat?preview=1`)).status(), 200);
  await page.getByRole('button', { name: 'Switch to light mode' }).click();
  assert.equal(await page.locator('.streamer-chat').getAttribute('data-theme'), 'light');
  await page.reload();
  await page.getByRole('button', { name: 'Switch to dark mode' }).waitFor();
  await page.getByRole('button', { name: 'Switch to dark mode' }).click();
  assert.equal(await page.locator('.streamer-chat').getAttribute('data-theme'), 'dark');
  await page.getByText('Connected', { exact: true }).waitFor();
  assert.equal((await fetch(`${origin}/api/test-event`, { method: 'POST', headers: writeHeaders, body: JSON.stringify({ event: {
    occurredAt: new Date().toISOString(), source: 'g05-browser-test', platform: 'rumble', type: 'chat.message', nativeType: 'SyntheticChat', dedupeKey: 'dock-check',
    user: { displayName: 'Synthetic Viewer' }, message: { text: 'Streamer dock receives the shared chat feed' },
  } }) })).status, 200);
  await page.getByText('Streamer dock receives the shared chat feed', { exact: true }).waitFor();
  await page.screenshot({ path: path.join(root, 'artifacts/g05-streamer-chat.png') });
  assert.equal(pageErrors, 0, 'Rendered pages raised JavaScript errors');
  console.log('G02/G05 fresh-browser qualification passed: HTTP editor/login, transparent escaped four-platform chat, bounded DOM, one socket, reconnect, saved settings and persistent light/dark streamer view');
} finally {
  await browser?.close();
  if (host.exitCode === null && !spawnFailed) {
    const exited = new Promise(resolve => host.once('exit', resolve));
    host.kill();
    await exited;
  }
  await rm(directory, { recursive: true, force: true }); // Only this generated test directory.
}

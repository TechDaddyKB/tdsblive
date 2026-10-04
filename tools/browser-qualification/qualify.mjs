// CI-only fresh-browser qualification; never controls the user's personal browser.
import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, rm, realpath } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { qualifyVisualEditor } from './visual-editor.mjs';
import { qualifyFinancial } from './financial.mjs';
import { qualifyDonors } from './donors.mjs';
import { qualifyAutomation } from './automation.mjs';
import { qualifyCustomWidgets } from './custom-widgets.mjs';
import { qualifyAdvancedEditor } from './advanced-editor.mjs';
import { qualifyUiRedesign } from './ui-redesign.mjs';
import { qualifyUiAcceptance } from './ui-acceptance.mjs';
import { qualifyCompatibility } from './compatibility.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const dotnetRoot = process.env.DOTNET_ROOT;
assert.ok(dotnetRoot && path.isAbsolute(dotnetRoot), 'CI must supply an absolute setup-dotnet installation directory');
const mode = process.argv[2] ?? 'managed';
assert.ok(['managed', 'portable', 'installed'].includes(mode), 'Choose managed, portable or installed qualification');
let executable = path.join(dotnetRoot, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
const launchArguments = [];
if (mode === 'managed') launchArguments.push(path.join(root, 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'));
else {
  assert.equal(process.platform, 'win32', 'Packaged browser qualification requires native Windows');
  const packageRoot = path.resolve(process.env.TDSBLIVE_PACKAGE_CHECK_ROOT);
  assert.match(path.relative(await realpath(process.env.RUNNER_TEMP), packageRoot), /^tdsblive-package-check-[a-f0-9]{32}$/, 'Only the owned Windows package check directory can be used');
  // CLI input selects a fixed target; it must never become part of an executable path.
  const packageTargets = new Map([
    ['portable', path.join(packageRoot, 'portable', 'TDSBLive.exe')],
    ['installed', path.join(packageRoot, 'installed', 'TDSBLive.exe')],
  ]);
  const candidate = packageTargets.get(mode);
  assert.ok(candidate, 'Choose a named packaged target');
  assert.equal(await realpath(candidate), candidate, 'The shipped executable must not resolve through a symbolic link');
  executable = candidate;
}
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
const host = spawn(executable, [...launchArguments, '--TDSBLive:DataDirectory', directory, '--TDSBLive:OpenEditor=false'], { stdio: 'ignore' });
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
  const context = await browser.newContext();
  const page = await context.newPage();
  page.on('response', response => {
    if (response.request().method() === 'PUT' && response.url().includes('/api/overlays/') && response.status() >= 400)
      console.error(`Isolated overlay save returned HTTP ${response.status()}`);
  });
  await page.route(`${origin}/g05-badge.svg`, route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18"><rect width="18" height="18" fill="green"/></svg>' }));
  // Owned two-frame GIF fixture: red/blue pixels, 100 ms frames, infinite loop.
  const animatedGif = Buffer.from('47494638396101000100800000ff00000000ff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f904000a0000002c00000000010001000002024c01003b', 'hex');
  await page.route(`${origin}/g05-media.gif`, route => route.fulfill({ contentType: 'image/gif', body: animatedGif }));
  let pageErrors = 0;
  page.on('pageerror', error => { pageErrors++; console.error('Isolated browser error:', error.message); });
  assert.equal((await page.goto(`${origin}/editor`)).status(), 200);
  await page.getByRole('heading', { name: 'TDSBLive', exact: true }).waitFor();
  await page.getByText('Host ready. Loopback access only.', { exact: true }).waitFor();
  assert.match(await page.getByLabel('Streamer.bot connection status').innerText(), /Disconnected/);
  // First-run guide saves progress without enabling integrations merely by navigation.
  await page.getByRole('button', { name: 'Close guided setup' }).waitFor();
  for (let step = 2; step <= 6; step++) {
    await page.getByRole('button', { name: 'Next setup step' }).click();
    await page.getByText(new RegExp(`^Step ${step} of 6:`)).waitFor();
  }
  await page.getByRole('button', { name: 'Finish setup review' }).click();
  await page.getByRole('button', { name: 'Open guided setup' }).waitFor();
  await page.reload();
  await page.getByRole('button', { name: 'Open guided setup' }).waitFor();
  const setupConfiguration = await (await fetch(`${origin}/api/configuration`)).json();
  assert.equal(setupConfiguration.streamerBot.enabled, false);
  assert.equal(setupConfiguration.speakerBot.enabled, false);
  // Exercise the ordinary release UI against real configuration persistence.
  await page.goto(`${origin}/editor#connections`);
  await page.getByText('Streamer.bot action permissions', { exact: true }).click();
  await page.getByRole('button', { name: 'Load action permissions', exact: true }).click();
  await page.getByRole('button', { name: 'Save action permissions', exact: true }).waitFor();
  assert.equal(await page.getByLabel('Allow qualified live event forwarding to Streamer.bot', { exact: true }).isChecked(), false);
  await page.getByRole('button', { name: 'Save action permissions', exact: true }).click();
  await page.getByText('Permissions saved. Restart TDSBLive to apply them, then review live rules and trigger bindings.', { exact: true }).waitFor();
  await page.goto(`${origin}/editor#settings`);
  assert.equal(await page.getByLabel('Enable authenticated LAN access', { exact: true }).isChecked(), false);
  await page.waitForFunction(expected => Number(document.querySelector('#lan-title')?.parentElement.querySelector('input[type=number]')?.value) === expected, port);
  assert.equal(Number(await page.getByLabel('HTTP port', { exact: true }).inputValue()), port);
  await page.getByRole('button', { name: 'Save access settings', exact: true }).click();
  await page.getByText('Access settings saved. Restart TDSBLive to apply them. Update OBS URLs if you changed the port.', { exact: true }).waitFor();
  const permissionsConfiguration = await (await fetch(`${origin}/api/configuration`)).json();
  assert.equal(permissionsConfiguration.server.port, port);
  assert.equal(permissionsConfiguration.server.enableLan, false);
  assert.equal(permissionsConfiguration.streamerBot.forwardLiveEvents, false);
  assert.deepEqual(permissionsConfiguration.streamerBot.allowedActionIds, []);
  assert.equal(permissionsConfiguration.streamerBot.enabled, false);
  assert.equal(permissionsConfiguration.speakerBot.enabled, false);
  const backupDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download backup', exact: true }).click();
  const backup = await backupDownload;
  const backupPath = await backup.path();
  assert.ok(backupPath);
  await page.getByLabel('Backup ZIP', { exact: true }).setInputFiles(backupPath);
  await page.getByRole('button', { name: 'Check backup', exact: true }).click();
  await page.getByText('Backup checked. Nothing has been replaced yet.', { exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Restore checked backup' }).isEnabled(), false);
  await page.getByLabel('I want to replace my current saved data with this backup.').check();
  assert.equal(await page.getByRole('button', { name: 'Restore checked backup' }).isEnabled(), true);
  // No shutdown/restore request here: the remaining qualifiers own this running host.
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
      user: { login: 'synthetic-viewer', displayName: `${platform} viewer`, badges: ['moderator'], badgeDetails: [{ name: 'moderator', imageUrl: `${origin}/g05-badge.svg` }] }, message: { text: `${platform} synthetic <script>escaped</script>`, parts: [
        { kind: 'text', text: `${platform} synthetic <script>escaped</script>` },
        { kind: 'emote', text: '', source: { twitch: 'Twitch', youtube: '7TV', kick: 'BTTV', rumble: 'FFZ' }[platform], imageUrl: `${origin}/g05-media.gif` },
        { kind: 'gif', text: '', source: 'Twitch', imageUrl: `${origin}/g05-media.gif` },
      ] },
    } }) })).status, 200);
    await page.getByText(`${platform} synthetic <script>escaped</script>`, { exact: true }).waitFor();
  }
  assert.equal(await page.locator('.chat-row').count(), 4);
  assert.equal(await page.locator('.chat-content script').count(), 0);
  await page.waitForFunction(() => Array.from(document.querySelectorAll('.badge-image')).length === 4 && Array.from(document.querySelectorAll('.badge-image')).every(image => image.complete && image.naturalWidth > 0));
  await page.waitForFunction(() => document.querySelectorAll('.chat-emote').length === 4 && document.querySelectorAll('.chat-gif').length === 4 && Array.from(document.querySelectorAll('.chat-media')).every(image => image.complete && image.naturalWidth > 0));
  assert.equal(await page.evaluate(() => window.tdsbliveTestSockets.length), 1, 'One shared socket must serve the overlay');
  await page.evaluate(() => window.tdsbliveTestSockets[0].close());
  await page.waitForFunction(() => window.tdsbliveTestSockets.length === 2 && window.tdsbliveTestSockets[1].g05Subscribed);
  assert.equal(await page.locator('.chat-row').count(), 4, 'Reconnect must retain messages without duplicate DOM entries');
  overlay = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  overlay.chat.maximumMessages = 2;
  assert.equal((await fetch(`${origin}/api/overlays/combined-chat`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(overlay) })).status, 200);
  await page.waitForFunction(() => document.querySelectorAll('.chat-row').length === 2);
  overlay = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  overlay.chat.maximumMessages = 4;
  assert.equal((await fetch(`${origin}/api/overlays/combined-chat`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(overlay) })).status, 200);
  await page.waitForFunction(() => document.querySelectorAll('.chat-row').length === 4);
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
    user: { displayName: 'Synthetic Viewer', badgeDetails: [{ name: 'moderator', imageUrl: `${origin}/g05-badge.svg` }] }, message: { text: 'Streamer dock receives the shared chat feed' },
  } }) })).status, 200);
  await page.getByText('Streamer dock receives the shared chat feed', { exact: true }).waitFor();
  await page.waitForFunction(() => document.querySelector('.badge-image')?.naturalWidth > 0);
  await page.screenshot({ path: path.join(root, 'artifacts/g05-streamer-chat.png') });
  await qualifyVisualEditor(page, origin, writeHeaders, root);
  await qualifyAdvancedEditor(page, origin, writeHeaders);
  await qualifyCustomWidgets(page, origin, writeHeaders);
  await qualifyCompatibility(page, origin, writeHeaders);
  await qualifyAutomation(page, origin, writeHeaders);
  execFileSync(process.platform === 'win32' ? 'python' : 'python3', [path.join(root, 'tools/seed_financial_browser.py'), directory], { stdio: 'pipe' });
  await qualifyFinancial(page, origin);
  await qualifyDonors(page, origin, writeHeaders, root);
  await qualifyUiRedesign(page, origin, writeHeaders, root);
  await qualifyUiAcceptance(origin, root, mode);
  assert.equal(pageErrors, 0, 'Rendered pages raised JavaScript errors');
  console.log('G02/G05/G06 fresh-browser qualification passed: HTTP editor/login, transparent escaped four-platform chat, bounded DOM, one socket, reconnect, saved settings and persistent light/dark streamer view');
} finally {
  await browser?.close();
  if (host.exitCode === null && !spawnFailed) {
    const exited = new Promise(resolve => host.once('exit', resolve));
    host.kill();
    await exited;
  }
  await rm(directory, { recursive: true, force: true }); // Only this generated test directory.
}

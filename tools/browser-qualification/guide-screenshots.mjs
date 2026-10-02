// Generates public guide illustrations using a fresh host and made-up chat only.
// No personal browser profile, bot connection, credential or production data is used.
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
assert.ok(process.env.DOTNET_ROOT && path.isAbsolute(process.env.DOTNET_ROOT));
const directory = await mkdtemp(path.join(tmpdir(), 'tdsblive-guide-'));
const port = await new Promise((resolve, reject) => {
  const listener = createServer();
  listener.on('error', reject);
  listener.listen(0, '127.0.0.1', () => {
    const value = listener.address().port;
    listener.close(() => resolve(value));
  });
});
await writeFile(path.join(directory, 'configuration.json'), JSON.stringify({ server: { host: '127.0.0.1', port } }));
const origin = `http://127.0.0.1:${port}`;
const output = path.join(root, 'docs/user-guide/images');
const executable = path.join(process.env.DOTNET_ROOT, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
const host = spawn(executable, [path.join(root, 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
  '--TDSBLive:DataDirectory', directory], { stdio: 'ignore' });
let spawnFailed = false;
host.on('error', () => { spawnFailed = true; });
let browser;
try {
  const deadline = Date.now() + 30_000;
  let ready = false;
  while (Date.now() < deadline) {
    assert.ok(!spawnFailed && host.exitCode === null, 'Owned guide host exited before readiness');
    try { ready = (await fetch(`${origin}/api/status`)).ok; } catch { /* Wait for this host. */ }
    if (ready) break;
    await delay(100);
  }
  assert.ok(ready, 'Owned guide host did not become ready');
  await mkdir(output, { recursive: true });
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1000, height: 700 } });
  const page = await context.newPage();
  await page.addInitScript(() => {
    const Original = window.WebSocket;
    window.guideSubscribed = false;
    window.WebSocket = class extends Original {
      constructor(...args) {
        super(...args);
        this.addEventListener('message', event => {
          try { if (JSON.parse(event.data).op === 'subscribed') window.guideSubscribed = true; }
          catch { /* Ignore non-protocol messages in the owned screenshot host. */ }
        });
      }
    };
  });
  const csrf = await fetch(`${origin}/api/auth/csrf`);
  const headers = { 'Content-Type': 'application/json', Origin: origin,
    Cookie: csrf.headers.getSetCookie().map(value => value.split(';')[0]).join('; '),
    'X-TDSBLive-CSRF': (await csrf.json()).requestToken };
  const overlay = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  overlay.chat.persistent = true;
  overlay.chat.showTimestamp = false;
  assert.equal((await fetch(`${origin}/api/overlays/combined-chat`, { method: 'PUT', headers, body: JSON.stringify(overlay) })).status, 200);
  await page.goto(`${origin}/chat/combined-chat?preview=1`);
  await page.getByText('Connected', { exact: true }).waitFor();
  await page.waitForFunction(() => window.guideSubscribed);
  const examples = [
    ['twitch', 'Alex', 'Hello everyone! Ready for today’s stream?'],
    ['youtube', 'Jamie', 'The sound is great. Have a good stream!'],
    ['kick', 'Sam', 'Happy to be here!'],
    ['rumble', 'Taylor', 'Hi from Rumble!'],
  ];
  async function sendExamples(round) {
    for (const [platform, name, text] of examples) {
      assert.equal((await fetch(`${origin}/api/test-event`, { method: 'POST', headers, body: JSON.stringify({ event: {
        occurredAt: '2026-10-02T12:00:00Z', source: 'user-guide', platform, type: 'chat.message',
        nativeType: 'OwnedGuideExample', dedupeKey: `guide-${platform}-${round}`, user: { displayName: name }, message: { text },
      } }) })).status, 200);
      await page.getByText(text, { exact: true }).waitFor();
    }
  }
  await sendExamples('dock');
  await page.waitForFunction(() => document.getAnimations().every(animation => animation.playState === 'finished'));
  await page.getByRole('button', { name: 'Switch to light mode' }).click();
  await page.screenshot({ path: path.join(output, 'streamer-chat-light.png') });
  await page.getByRole('button', { name: 'Switch to dark mode' }).click();
  await page.screenshot({ path: path.join(output, 'streamer-chat-dark.png') });
  await page.goto(`${origin}/overlay/combined-chat?preview=1`);
  await page.waitForFunction(() => window.guideSubscribed);
  await sendExamples('overlay');
  await page.waitForFunction(() => document.getAnimations().every(animation => animation.playState === 'finished'));
  await mkdir(path.join(root, 'artifacts'), { recursive: true });
  await page.screenshot({ path: path.join(root, 'artifacts/g10-transparent-chat.png'), omitBackground: true });
  await page.goto(`${origin}/editor`);
  await page.getByText('Host ready. Loopback access only.', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Close guided setup' }).waitFor();
  await page.getByRole('region', { name: 'Guided setup' }).screenshot({ path: path.join(output, 'guided-setup.png') });
  await page.getByRole('button', { name: 'Close guided setup' }).click();
  await page.getByRole('region', { name: 'Backup and recovery' }).screenshot({ path: path.join(output, 'backup-recovery.png') });
  const card = page.getByRole('region', { name: 'Combined chat setup' });
  await card.getByText('Chat appearance and filters', { exact: true }).click();
  await card.screenshot({ path: path.join(output, 'chat-settings.png') });
  await page.getByRole('region', { name: 'Financial ledger', exact: true }).screenshot({ path: path.join(output, 'supporter-totals.png') });
  const automation = page.getByRole('region', { name: 'Automation rules', exact: true });
  await automation.getByRole('button', { name: 'New speech rule', exact: true }).click();
  await automation.getByLabel('Rule name', { exact: true }).fill('Read Ko-fi messages');
  await automation.getByLabel('Voice alias', { exact: true }).fill('local english');
  assert.equal(await automation.getByLabel('Enable live automation', { exact: true }).isChecked(), false);
  await automation.screenshot({ path: path.join(output, 'automation-rules.png') });
  const editor = page.getByRole('region', { name: 'Visual overlay editor', exact: true });
  await editor.getByLabel('New overlay name').fill('My stream overlay');
  await editor.getByLabel('New overlay ID').fill('my-stream-overlay');
  await editor.getByRole('button', { name: 'Create overlay', exact: true }).click();
  await editor.getByLabel('Overlay canvas').waitFor();
  await editor.getByRole('button', { name: 'Add text', exact: true }).click();
  await editor.getByLabel('Widget text').fill('Welcome to the stream!');
  await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  await editor.screenshot({ path: path.join(output, 'visual-editor.png') });
  console.log('Guide illustrations generated from owned simulation only; inspect and secrets-scan before publishing.');
} finally {
  await browser?.close();
  if (!spawnFailed && host.exitCode === null) {
    const exited = new Promise(resolve => host.once('exit', resolve));
    host.kill();
    await exited;
  }
  await rm(directory, { recursive: true, force: true });
}

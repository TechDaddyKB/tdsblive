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
const host = spawn('dotnet', [path.join(root, 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
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
  assert.equal(pageErrors, 0, 'Rendered pages raised JavaScript errors');
  console.log('G02 fresh-browser qualification passed: HTTP editor, real host status, truthful integration state and login shell');
} finally {
  await browser?.close();
  if (host.exitCode === null && !spawnFailed) {
    const exited = new Promise(resolve => host.once('exit', resolve));
    host.kill();
    await exited;
  }
  await rm(directory, { recursive: true, force: true }); // Only this generated test directory.
}

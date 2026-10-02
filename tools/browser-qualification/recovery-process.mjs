// Owns only a fresh local host and temporary data; never runs live integrations.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, writeFile, rm, realpath, stat } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

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
const packagedExecutable = process.argv[2];
let executable = path.join(process.env.DOTNET_ROOT, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
if (packagedExecutable) {
  assert.equal(process.platform, 'win32', 'Packaged qualification requires Windows');
  const candidate = await realpath(packagedExecutable);
  const relative = path.relative(await realpath(process.env.RUNNER_TEMP), candidate);
  assert.match(relative, /^tdsblive-package-check-[a-f0-9]{32}\\(?:portable|installed)\\TDSBLive\.exe$/,
    'Only the owned Windows package qualification executable can run');
  assert.ok((await stat(candidate)).isFile());
  executable = candidate;
}
const argumentsList = ['--TDSBLive:DataDirectory', data, '--TDSBLive:OpenEditor=false'];
if (!packagedExecutable) argumentsList.unshift(path.resolve('src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'));
const child = spawn(executable, argumentsList, { stdio: 'ignore' });
let launchError = false;
child.on('error', () => { launchError = true; });
let cookie = ''; let token = ''; let generation;
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
  await write('/api/setup', { step: 2, reviewed: false, version: 0 }, 'PUT');
  const backup = Buffer.from(await (await write('/api/recovery/backup')).arrayBuffer());
  await write('/api/setup', { step: 4, reviewed: false, version: 1 }, 'PUT');
  await write('/api/application/restart');
  generation = await ready(generation); await protect();
  assert.equal((await get('/api/setup')).step, 4);
  const preview = await (await write('/api/recovery/validate', backup, 'POST', true)).json();
  await write('/api/recovery/restore', { id: preview.id, confirm: true });
  generation = await ready(generation); await protect();
  assert.equal((await get('/api/setup')).step, 2);
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
  console.log('G10 real-process restart/restore passed: new generations, saved data, safety-paused integrations and quit');
} finally {
  // Stop only the host at the random port belonging to this temporary data root.
  try { await protect(); await write('/api/application/quit'); await delay(500); } catch { /* Already stopped. */ }
  if (child.exitCode === null) child.kill();
  await rm(directory, { recursive: true, force: true });
}

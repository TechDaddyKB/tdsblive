// Owned synthetic acceptance, never a claim of observed nontechnical users.
// Native zoom uses Chromium's tabs API, rather than CSS scaling or a smaller viewport.
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { chromium } from 'playwright';

const destinations = ['overview', 'overlays', 'chat', 'automation', 'supporters', 'media', 'connections', 'settings', 'diagnostics', 'help'];

async function panel(page, name) {
  const tabs = page.getByLabel('Editor panels');
  if (await tabs.isVisible()) await tabs.getByRole('button', { name, exact: true }).click();
}

async function activate(locator, touch) {
  if (touch) await locator.tap(); else await locator.click();
}

async function taskWalkthrough(page, origin, index, touch) {
  const started = Date.now();
  const milestones = {};
  await page.goto(`${origin}/editor`);
  await page.getByRole('heading', { name: 'Ready for your next stream', exact: true }).waitFor();
  const closeSetup = page.getByRole('button', { name: 'Close guided setup', exact: true });
  if (await closeSetup.isVisible()) await activate(closeSetup, touch);
  await activate(page.getByRole('link', { name: 'Create or edit an overlay', exact: true }), touch);
  await activate(page.getByRole('button', { name: 'New overlay', exact: true }), touch);
  const name = `Synthetic usability ${index}`;
  await page.getByLabel('New overlay name', { exact: true }).fill(name);
  await activate(page.getByRole('button', { name: 'Create overlay', exact: true }), touch);
  await page.getByRole('button', { name: 'Add widget', exact: true }).waitFor();
  for (const [layer, trigger, template] of [['Follow greeting', 'Twitch · Follow', 'Welcome to my stream!'], ['Donation thanks', 'Ko-fi · Donation', 'Thank you for supporting my stream!']]) {
    await activate(page.getByRole('button', { name: 'Add widget', exact: true }), touch);
    await activate(page.getByRole('button', { name: 'Add AlertBox', exact: true }), touch);
    await page.getByLabel('Layer name', { exact: true }).fill(layer);
    await page.getByLabel('Choose trigger', { exact: true }).selectOption({ label: trigger });
    await activate(page.getByRole('button', { name: 'Next alert step', exact: true }), touch);
    await activate(page.getByRole('button', { name: 'Next alert step', exact: true }), touch);
    await page.getByLabel('Alert template', { exact: true }).fill(template);
    await activate(page.getByRole('button', { name: 'Next alert step', exact: true }), touch);
    await activate(page.getByRole('button', { name: 'Test this design with sample data', exact: true }), touch);
    await page.getByLabel('Guided matching results').getByText(`${layer}: Matches this event`, { exact: true }).waitFor();
    await activate(page.getByRole('button', { name: 'Next alert step', exact: true }), touch);
    await page.getByRole('heading', { name: 'Your alert is ready', exact: true }).waitFor();
    milestones[layer] = Date.now() - started;
  }
  await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  await activate(page.getByRole('button', { name: 'Copy OBS URL', exact: true }), touch);
  const url = await page.evaluate(() => navigator.clipboard.readText());
  const id = `synthetic-usability-${index}`;
  assert.equal(url, `${origin}/overlay/${id}`, 'OBS handoff copies the existing runtime address');
  const document = await (await fetch(`${origin}/api/overlays/${id}`)).json();
  assert.equal(document.widgets.length, 2);
  assert.notEqual(document.widgets[0].alert.template, document.widgets[1].alert.template);
  assert.deepEqual(document.widgets.map(w => w.alert.eventTypes), [['community.follow'], ['support.donation']]);
  assert.deepEqual(document.widgets.map(w => w.alert.platforms), [['twitch'], ['kofi']]);
  const elapsedMs = Date.now() - started;
  assert.ok(elapsedMs < 600_000, 'Scripted task must finish inside the approved ten-minute ceiling');
  await panel(page, 'Canvas');
  await page.getByLabel('Overlay canvas', { exact: true }).scrollIntoViewIfNeeded();
  const touchGestures = touch ? await qualifyTouchGestures(page, origin, id) : undefined;
  return { scenario: index, viewport: page.viewportSize(), input: touch ? 'emulated touch' : 'pointer', elapsedMs, milestones, internalIdentifiersTyped: false, obsAddressVerified: true, touchGestures };
}

async function qualifyTouchGestures(page, origin, id) {
  const client = await page.context().newCDPSession(page);
  const read = async () => (await (await fetch(`${origin}/api/overlays/${id}`)).json()).widgets.find(widget => widget.name === 'Donation thanks');
  const drag = async (x, y, dx, dy) => {
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y, id: 1 }] });
    for (let i = 1; i <= 5; i++) await client.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x + dx * i / 5, y: y + dy * i / 5, id: 1 }] });
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  };
  try {
    const before = await read();
    const widget = await page.locator('.canvas-widget.selected').boundingBox(); assert.ok(widget);
    await drag(widget.x + 2, widget.y + 2, 20, 12);
    const moved = await read(); assert.notEqual(moved.x, before.x, 'Touch pointer dragging moves the selected widget');
    const resize = await page.getByRole('button', { name: 'Resize widget', exact: true }).boundingBox(); assert.ok(resize);
    await drag(resize.x + resize.width / 2, resize.y + resize.height / 2, 12, 8);
    const resized = await read(); assert.ok(resized.width > moved.width, 'Touch resize changes size through the real pointer handlers');
    assert.equal(resized.x, moved.x, 'Touch resize preserves the moved position');
    return { move: true, resize: true };
  } finally { await client.detach(); }
}

async function keyboardReach(page, locator) {
  assert.ok(await locator.isVisible(), 'Keyboard target must be visible');
  for (let i = 0; i < 250; i++) {
    if (await locator.evaluate(node => node === document.activeElement)) return;
    await page.keyboard.press('Tab');
  }
  assert.fail(`Keyboard cannot reach ${await locator.innerText()}`);
}

async function keyboardAndFocus(page) {
  await keyboardReach(page, page.getByRole('button', { name: 'New overlay', exact: true }));
  await page.keyboard.press('Enter');
  const dialog = page.getByRole('dialog', { name: 'Create overlay', exact: true });
  await dialog.waitFor();
  assert.equal(await dialog.evaluate(node => node.contains(document.activeElement)), true);
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab');
    assert.equal(await dialog.evaluate(node => node.contains(document.activeElement)), true, 'Modal traps keyboard focus');
  }
  await page.keyboard.press('Escape');
  await dialog.waitFor({ state: 'hidden' });
  assert.equal(await page.getByRole('button', { name: 'New overlay', exact: true }).evaluate(node => node === document.activeElement), true, 'Closing a dialog restores opener focus');
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  if (await menu.isVisible()) { await keyboardReach(page, menu); await page.keyboard.press('Enter'); }
  await keyboardReach(page, page.getByRole('link', { name: 'Media', exact: true }));
  await page.keyboard.press('Enter');
  await page.getByRole('heading', { name: 'Your media', exact: true }).waitFor();
  await page.waitForFunction(() => document.querySelector('.workspace-heading') === document.activeElement);
  await page.goBack();
  await page.getByRole('heading', { name: 'Visual Overlay Editor', exact: true }).waitFor();
}

async function accessibilityTree(page, destination) {
  const client = await page.context().newCDPSession(page);
  try {
    const { nodes } = await client.send('Accessibility.getFullAXTree');
    const controls = nodes.filter(node => !node.ignored && ['button', 'link', 'textbox', 'combobox', 'listbox', 'checkbox', 'slider', 'spinbutton', 'switch'].includes(node.role?.value));
    const unnamed = controls.filter(node => !node.name?.value?.trim());
    assert.deepEqual(unnamed.map(node => ({ role: node.role?.value, node: node.backendDOMNodeId })), [], `${destination}: exposed controls need accessible names`);
    assert.ok(controls.length > 0, `${destination}: accessibility tree must expose controls`);
    assert.ok(nodes.some(node => !node.ignored && node.role?.value === 'main'), `${destination}: main landmark exists`);
    return { destination, namedControls: controls.length };
  } finally { await client.detach(); }
}

async function previewAudio(browser, origin) {
  const csrf = await fetch(`${origin}/api/auth/csrf`);
  const headers = { Origin: origin, Cookie: csrf.headers.getSetCookie().map(value => value.split(';')[0]).join('; '), 'X-TDSBLive-CSRF': (await csrf.json()).requestToken };
  const wave = Buffer.alloc(44 + 96000);
  wave.write('RIFF'); wave.writeUInt32LE(wave.length - 8, 4); wave.write('WAVEfmt ', 8); wave.writeUInt32LE(16, 16);
  wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22); wave.writeUInt32LE(48000, 24); wave.writeUInt32LE(96000, 28); wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34);
  wave.write('data', 36); wave.writeUInt32LE(96000, 40);
  for (let i = 0; i < 48000; i++) wave.writeInt16LE(Math.round(Math.sin(i * 2 * Math.PI * 440 / 48000) * 6000), 44 + i * 2);
  const upload = await fetch(`${origin}/api/assets`, { method: 'POST', headers: { ...headers, 'Content-Type': 'audio/wav', 'X-Asset-Filename': 'owned-ui-signal.wav' }, body: wave });
  assert.equal(upload.status, 200);
  const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
  try {
    const page = await context.newPage();
    await page.addInitScript(() => {
      window.uiAudioProbes = [];
      const play = HTMLMediaElement.prototype.play;
      HTMLMediaElement.prototype.play = function (...args) {
        if (this instanceof HTMLAudioElement && !this.uiAudioProbe) {
          const audioContext = new AudioContext();
          const analyser = audioContext.createAnalyser(); analyser.fftSize = 512;
          audioContext.createMediaElementSource(this).connect(analyser); analyser.connect(audioContext.destination);
          const probe = { peakRms: 0 }; this.uiAudioProbe = probe; window.uiAudioProbes.push(probe);
          const values = new Float32Array(analyser.fftSize);
          const sample = () => { analyser.getFloatTimeDomainData(values); probe.peakRms = Math.max(probe.peakRms, Math.sqrt(values.reduce((sum, value) => sum + value * value, 0) / values.length)); requestAnimationFrame(sample); };
          requestAnimationFrame(sample); void audioContext.resume();
        }
        return play.apply(this, args);
      };
    });
    await page.goto(`${origin}/editor#overlays`);
    await page.getByLabel('Overlay', { exact: true }).selectOption('synthetic-usability-1');
    await panel(page, 'Layers'); await page.getByRole('button', { name: 'Follow greeting', exact: true }).click();
    await panel(page, 'Properties'); await page.getByRole('button', { name: '3. Design', exact: true }).click();
    await page.getByLabel('Alert sound', { exact: true }).selectOption({ label: 'owned-ui-signal.wav' });
    await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
    await page.waitForFunction(() => { const audio = document.querySelector('.canvas-widget audio'); return audio?.muted && audio.currentTime > 0.05; });
    assert.equal(await page.evaluate(() => Math.max(0, ...window.uiAudioProbes.map(probe => probe.peakRms))), 0, 'Unsaved/static design output remains silent');
    await page.getByRole('button', { name: 'Preview', exact: true }).click();
    const enabled = page.getByLabel('Enable preview audio', { exact: true });
    assert.equal(await enabled.isChecked(), false);
    await page.frameLocator('iframe[title="Overlay test preview"]').getByText(/Test preview.*Connected/).waitFor();
    const previewFrame = () => page.frames().find(frame => frame.url().includes('/overlay/synthetic-usability-1?preview=1'));
    const sendAndMeasure = async audible => {
      const frame = previewFrame(); assert.ok(frame);
      await frame.getByText(/Test preview.*Connected/).waitFor();
      await page.getByRole('button', { name: 'Send isolated test event', exact: true }).click();
      await frame.waitForFunction(audible => {
        const audio = document.querySelector('.active-alert audio');
        return audio && audio.currentTime > .05 && audio.muted === !audible && (!audible || window.uiAudioProbes.some(probe => probe.peakRms > .01));
      }, audible);
      const peakRms = await frame.evaluate(() => Math.max(0, ...window.uiAudioProbes.map(probe => probe.peakRms)));
      if (!audible) assert.equal(peakRms, 0, 'Saved preview stays silent before explicit opt-in');
      return peakRms;
    };
    const silentRms = await sendAndMeasure(false);
    await enabled.check();
    await page.waitForFunction(() => document.querySelector('iframe[title="Overlay test preview"]')?.getAttribute('src')?.includes('audio=1'));
    await previewFrame().waitForURL(/audio=1/);
    await page.frameLocator('iframe[title="Overlay test preview"]').getByText(/Test preview.*Connected/).waitFor();
    const enabledRms = await sendAndMeasure(true);
    await enabled.uncheck();
    await page.getByRole('link', { name: 'Media', exact: true }).click();
    await page.goBack(); await page.getByRole('heading', { name: 'Visual Overlay Editor', exact: true }).waitFor();
    if (!await enabled.isVisible()) await page.getByRole('button', { name: 'Preview', exact: true }).click();
    assert.equal(await enabled.isChecked(), false, 'Leaving the workspace resets preview audio');
    return { ownedToneHz: 440, silentRms, enabledRms, humanHearingClaimed: false };
  } finally { await context.close(); }
}

async function expandVisibleDetails(page) {
  for (const summary of await page.locator('.app-content summary').all()) {
    if (await summary.isVisible() && !await summary.evaluate(node => node.parentElement.open)) await summary.click();
  }
}

async function nativeZoomAndAccessibility(origin, output) {
  const directory = await mkdtemp(path.join(tmpdir(), 'tdsblive-native-zoom-'));
  const extension = path.join(directory, 'extension');
  await mkdir(extension);
  await writeFile(path.join(extension, 'manifest.json'), JSON.stringify({ manifest_version: 3, name: 'Owned UI zoom qualifier', version: '1.0', permissions: ['tabs'], background: { service_worker: 'worker.js' } }));
  await writeFile(path.join(extension, 'worker.js'), 'chrome.runtime.onInstalled.addListener(() => {});');
  let context;
  const evidence = [];
  try {
    context = await chromium.launchPersistentContext(path.join(directory, 'profile'), { channel: 'chromium', headless: true, viewport: { width: 1280, height: 720 }, args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`] });
    const worker = context.serviceWorkers()[0] ?? await context.waitForEvent('serviceworker');
    const page = await context.newPage();
    await page.goto(`${origin}/editor#overlays`);
    await page.getByLabel('Overlay', { exact: true }).selectOption('synthetic-usability-1');
    await page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
    const before = await (await fetch(`${origin}/api/overlays/synthetic-usability-1`)).json();
    for (const theme of ['light', 'dark']) {
      await page.getByLabel('Application theme').selectOption(theme);
      for (const zoom of [2, 4]) {
        const actual = await worker.evaluate(async ({ origin, zoom }) => {
          const tab = (await chrome.tabs.query({})).find(item => item.url?.startsWith(`${origin}/editor`));
          await chrome.tabs.setZoom(tab.id, zoom);
          return chrome.tabs.getZoom(tab.id);
        }, { origin, zoom });
        assert.ok(Math.abs(actual - zoom) < 0.001, 'Chromium reports the actual requested browser zoom');
        await page.waitForFunction(width => innerWidth === width, 1280 / zoom);
        for (const destination of destinations) {
          await page.goto(`${origin}/editor#${destination}`);
          await page.getByRole('heading', { name: 'TDSBLive', exact: true }).waitFor();
          await page.waitForLoadState('networkidle');
          await expandVisibleDetails(page);
          assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `${theme} ${zoom * 100}% ${destination}: document reflows`);
          evidence.push({ theme, zoom: actual, cssWidth: await page.evaluate(() => innerWidth), ...await accessibilityTree(page, destination) });
        }
        await page.goto(`${origin}/editor#overlays`);
        await page.getByLabel('Overlay', { exact: true }).selectOption('synthetic-usability-1');
        await panel(page, 'Canvas');
        await page.getByLabel('Overlay canvas', { exact: true }).scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(output, `native-zoom-${theme}-${zoom * 100}.png`) });
      }
    }
    assert.deepEqual((await (await fetch(`${origin}/api/overlays/synthetic-usability-1`)).json()).widgets, before.widgets, 'Browser zoom never changes saved design geometry or content');
    await page.emulateMedia({ reducedMotion: 'reduce' });
    assert.equal(await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches), true);
    await keyboardAndFocus(page);
    return evidence;
  } finally { await context?.close(); await rm(directory, { recursive: true, force: true }); }
}

export async function qualifyUiAcceptance(origin, root, target = 'managed') {
  assert.ok(['managed', 'portable', 'installed'].includes(target));
  const output = path.join(root, `artifacts/ui-redesign/acceptance-${target}`);
  await mkdir(output, { recursive: true });
  const readIsolation = () => Promise.all(['/api/events', '/api/financial/ledger?limit=50&offset=0&state=all', '/api/automation/executions'].map(async route => {
    const response = await fetch(`${origin}${route}`); assert.equal(response.status, 200); return response.json();
  }));
  const beforeIsolation = await readIsolation();
  const browser = await chromium.launch({ channel: 'chromium', headless: true, args: ['--mute-audio', '--autoplay-policy=no-user-gesture-required'] });
  const scenarios = [];
  let audio;
  try {
    for (const [index, width, height, touch, theme] of [[1, 1366, 768, false, 'light'], [2, 768, 1024, false, 'dark'], [3, 390, 844, true, 'light']]) {
      const context = await browser.newContext({ viewport: { width, height }, hasTouch: touch, permissions: ['clipboard-read', 'clipboard-write'] });
      try {
        const page = await context.newPage();
        await page.addInitScript(theme => localStorage.setItem('tdsblive.theme', theme), theme);
        scenarios.push(await taskWalkthrough(page, origin, index, touch));
        await page.screenshot({ path: path.join(output, `synthetic-task-${index}.png`) });
      } finally { await context.close(); }
    }
    audio = await previewAudio(browser, origin);
  } finally { await browser.close(); }
  const zoomAndAccessibility = await nativeZoomAndAccessibility(origin, output);
  assert.deepEqual(await readIsolation(), beforeIsolation, 'Synthetic usability and zoom cannot add events, financial entries or automation receipts');
  await writeFile(path.join(output, 'synthetic-acceptance.json'), JSON.stringify({ target, synthetic: true, observedHumanSessions: 0, scenarios, zoomAndAccessibility, audio }, null, 2));
  console.log(`UI synthetic acceptance passed: ${scenarios.length} fresh-project walkthroughs, 200%/400% native browser zoom in both themes across ten destinations, accessibility names, keyboard modal focus and reduced motion. Human usability/screen-reader/physical touch are not claimed.`);
}

import { editorUI } from './editor-ui.mjs';
import assert from 'node:assert/strict';
import path from 'node:path';
import { readFile } from 'node:fs/promises';
export async function qualifyVisualEditor(page, origin, writeHeaders, root) {
  const editor = editorUI(page);
  const gif = Buffer.from('47494638396101000100800000ff00000000ff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f904000a0000002c00000000010001000002024c01003b', 'hex');
  const uploaded = await fetch(`${origin}/api/assets`, { method: 'POST', headers: { ...writeHeaders, 'Content-Type': 'image/gif', 'X-Asset-Filename': 'g06-owned.gif' }, body: gif });
  assert.equal(uploaded.status, 200);
  const asset = await uploaded.json();
  // Source-owned VP9 pattern, scanned with tracked files before CI reads it.
  const video = await readFile(path.join(root, 'tests/fixtures/media/synthetic-pattern.webm'));
  const wave = Buffer.alloc(44 + 48000); wave.write('RIFF', 0); wave.writeUInt32LE(wave.length - 8, 4); wave.write('WAVEfmt ', 8); wave.writeUInt32LE(16, 16);
  wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22); wave.writeUInt32LE(48000, 24); wave.writeUInt32LE(96000, 28); wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34);
  wave.write('data', 36); wave.writeUInt32LE(48000, 40); for (let i = 0; i < 24000; i++) wave.writeInt16LE(Math.round(Math.sin(i * 2 * Math.PI * 440 / 48000) * 3000), 44 + i * 2);
  const upload = async (body, name, mime) => {
    const result = await fetch(`${origin}/api/assets`, { method: 'POST', headers: { ...writeHeaders, 'Content-Type': mime, 'X-Asset-Filename': name }, body });
    assert.equal(result.status, 200); return result.json();
  };
  const videoAsset = await upload(video, 'g06-owned.webm', 'video/webm'); const soundAsset = await upload(wave, 'g06-owned.wav', 'audio/wav');
  await page.goto(`${origin}/editor#overlays`);
  await editor.getByLabel('New overlay name').fill('Browser qualification');
  await editor.getByLabel('New overlay ID').fill('g06-browser');
  await editor.getByLabel('Canvas preset').selectOption('1080x1920');
  await editor.getByRole('button', { name: 'Create overlay', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay"]')?.value === 'g06-browser');
  await editor.getByLabel('Overlay canvas').waitFor();
  // HTTP LAN browsers may omit Clipboard; URL copying must stay usable without HTTPS.
  await page.evaluate(() => Object.defineProperty(navigator, 'clipboard', { configurable: true, value: undefined }));
  await editor.getByRole('button', { name: 'Copy OBS URL', exact: true }).click();
  await editor.getByText(`OBS URL: ${origin}/overlay/g06-browser`, { exact: true }).waitFor();
  await page.evaluate(() => Reflect.deleteProperty(navigator, 'clipboard'));
  const saved = () => page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  await editor.getByRole('button', { name: 'Add text', exact: true }).click();
  await editor.getByLabel('Widget text').fill('G06 <script>escaped text</script>'); await saved();
  let document = await (await fetch(`${origin}/api/overlays/g06-browser`)).json();
  assert.equal(document.width, 1080); assert.equal(document.height, 1920);
  const restoreVersion = document.version;
  const widget = editor.locator(`[data-widget-id="${document.widgets[0].id}"]`);
  await editor.getByLabel('Canvas zoom').fill('0.4');
  const box = await widget.boundingBox(); assert.ok(box);
  await page.mouse.move(box.x + 20, box.y + 20); await page.mouse.down(); await page.mouse.move(box.x + 60, box.y + 40, { steps: 5 }); await page.mouse.up();
  await saved(); assert.equal(await editor.getByLabel('X', { exact: true }).inputValue(), '140'); assert.equal(await editor.getByLabel('Y', { exact: true }).inputValue(), '90');
  const resize = await editor.getByRole('button', { name: 'Resize widget', exact: true }).boundingBox(); assert.ok(resize);
  await page.mouse.move(resize.x + 5, resize.y + 5); await page.mouse.down(); await page.mouse.move(resize.x + 45, resize.y + 25, { steps: 5 }); await page.mouse.up();
  await saved(); assert.equal(await editor.getByLabel('Width', { exact: true }).inputValue(), '500');
  await widget.focus(); await page.keyboard.press('ArrowRight'); await page.keyboard.press('Shift+ArrowDown'); await saved();
  assert.equal(await editor.getByLabel('X', { exact: true }).inputValue(), '141'); assert.equal(await editor.getByLabel('Y', { exact: true }).inputValue(), '100');
  await page.keyboard.press('Control+z'); assert.equal(await editor.getByLabel('Y', { exact: true }).inputValue(), '90');
  await page.keyboard.press('Control+Shift+z'); assert.equal(await editor.getByLabel('Y', { exact: true }).inputValue(), '100'); await saved();
  for (let i = 0; i < 3; i++) {
    await editor.getByRole('button', { name: 'Duplicate', exact: true }).click(); assert.equal(await editor.locator('.canvas-widget').count(), 2);
    await editor.getByRole('button', { name: 'Delete layer', exact: true }).click(); assert.equal(await editor.locator('.canvas-widget').count(), 1);
    await editor.getByRole('button', { name: 'Undo', exact: true }).click(); assert.equal(await editor.locator('.canvas-widget').count(), 2);
    await editor.getByRole('button', { name: 'Redo', exact: true }).click(); assert.equal(await editor.locator('.canvas-widget').count(), 1);
    await editor.getByRole('listitem').getByRole('button').click();
  }
  await saved(); await page.reload(); await editor.getByLabel('Overlay canvas').waitFor();
  await editor.getByRole('listitem').getByRole('button').click(); assert.equal(await editor.getByLabel('X', { exact: true }).inputValue(), '141');
  await editor.getByRole('button', { name: 'Revision history', exact: true }).click();
  await editor.getByRole('button', { name: `Restore v${restoreVersion}`, exact: true }).click();
  await editor.getByText(`Restored revision ${restoreVersion} as a new revision.`, { exact: true }).waitFor(); await saved();
  await editor.getByRole('listitem').getByRole('button').click(); assert.equal(await editor.getByLabel('X', { exact: true }).inputValue(), '40');
  await editor.getByRole('button', { name: 'Add image', exact: true }).click();
  await editor.getByLabel('Media asset').selectOption(asset.id); await saved();
  await editor.getByRole('button', { name: 'Add Combined Chat', exact: true }).click(); await saved();
  await editor.getByRole('button', { name: 'Add AlertBox', exact: true }).click();
  await editor.getByLabel('Alert media').selectOption(videoAsset.id); await editor.getByLabel('Alert sound').selectOption(soundAsset.id);
  await editor.getByLabel('Duration (ms)', { exact: true }).fill('2000');
  await editor.getByLabel('Layer name').fill('Browser alert'); await editor.getByLabel('Concurrency').fill('2');
  await editor.getByLabel('Maximum queue length').fill('3'); await editor.getByLabel('Overflow policy').selectOption('drop-newest'); await saved();
  await editor.getByRole('button', { name: 'Add AlertBox', exact: true }).click();
  assert.equal(await editor.getByLabel('Concurrency').inputValue(), '2'); assert.equal(await editor.getByLabel('Maximum queue length').inputValue(), '3');
  assert.equal(await editor.getByLabel('Overflow policy').inputValue(), 'drop-newest'); await editor.getByLabel('Hidden', { exact: true }).check(); await saved();
  await editor.getByRole('button', { name: 'Browser alert', exact: true }).click();
  await editor.getByRole('button', { name: 'Preview', exact: true }).click();
  const frame = page.frameLocator('iframe[title="Overlay test preview"]');
  await frame.getByLabel('Overlay scene').waitFor();
  await frame.locator('.canvas-preview-label').getByText(/Connected/).waitFor();
  await frame.locator('.runtime-widget > img').waitFor();
  const imageFrame = page.frames().find(f => f.url().includes('/overlay/g06-browser'));
  await imageFrame.waitForFunction(() => document.querySelector('.runtime-widget > img')?.naturalWidth > 0);
  await frame.getByLabel('Combined chat', { exact: true }).waitFor();
  await editor.getByRole('button', { name: 'Send isolated test event', exact: true }).click();
  await frame.getByText('Test viewer · community.follow', { exact: true }).waitFor();
  await imageFrame.waitForFunction(() => {
    const video = document.querySelector('.active-alert video'), sound = document.querySelector('.active-alert audio');
    return video?.videoWidth === 32 && video.currentTime > .1 && sound?.currentTime > .1 && sound.muted;
  });
  await frame.getByText('Test viewer · community.follow', { exact: true }).waitFor({ state: 'hidden' });
  assert.equal(await frame.locator('script:not([src])').count(), 0);
  const native = { event: { source: 'Twitch', type: 'Follow' }, data: { userName: 'Native synthetic viewer' } };
  const result = await fetch(`${origin}/api/overlays/g06-browser/preview-events`, { method: 'POST', headers: writeHeaders,
    body: JSON.stringify({ type: 'community.follow', platform: 'twitch', mode: 'native', raw: native }) });
  assert.equal(result.status, 200); assert.deepEqual(Object.fromEntries(Object.entries(await result.json()).filter(([key]) => key !== 'id')),
    { provenance: 'simulation', persisted: false, liveActionsAllowed: false });
  await frame.getByText('Native synthetic viewer · community.follow', { exact: true }).waitFor();
  const canvasFrame = page.frames().find(f => f.url().includes('/overlay/g06-browser')); assert.ok(canvasFrame);
  assert.equal(await canvasFrame.evaluate(() => window.tdsbliveTestSockets.length), 1, 'Canvas chat and alerts must share a single socket');
  await frame.getByText('Native synthetic viewer · community.follow', { exact: true }).waitFor({ state: 'hidden' });
  const chat = await fetch(`${origin}/api/overlays/g06-browser/preview-events`, { method: 'POST', headers: writeHeaders,
    body: JSON.stringify({ type: 'chat.message', platform: 'twitch', user: 'Synthetic viewer', message: 'Canvas reconnect check' }) });
  assert.equal(chat.status, 200); await frame.getByText('Canvas reconnect check', { exact: true }).waitFor();
  await canvasFrame.evaluate(() => window.tdsbliveTestSockets[0].close());
  await canvasFrame.waitForFunction(() => window.tdsbliveTestSockets.length === 2 && window.tdsbliveTestSockets[1].g05Subscribed);
  assert.equal(await frame.getByText('Canvas reconnect check', { exact: true }).count(), 1);
  assert.equal(await frame.locator('[data-alert-event]').count(), 0, 'Reconnect cannot replay completed support alerts');
  assert.equal(await canvasFrame.evaluate(() => window.tdsbliveTestSockets.filter(socket => socket.readyState === WebSocket.OPEN).length), 1);
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write'], { origin });
  await editor.getByRole('button', { name: 'Copy OBS URL', exact: true }).click();
  await editor.getByText('OBS URL copied.', { exact: true }).waitFor();
  assert.equal(await page.evaluate(() => navigator.clipboard.readText()), `${origin}/overlay/g06-browser`);
  await editor.screenshot({ path: path.join(root, 'artifacts/g06-editor.png') });
  const sourceContext = await page.context().browser().newContext();
  const source = await sourceContext.newPage(); let sourceErrors = 0; source.on('pageerror', () => { sourceErrors++; });
  try {
    await source.goto(`${origin}/overlay/g06-browser`); await source.getByLabel('Overlay scene').waitFor();
    await source.waitForFunction(() => document.querySelector('.runtime-widget > img')?.naturalWidth > 0);
    await editor.getByRole('button', { name: 'Revision history', exact: true }).click();
    await editor.getByRole('button', { name: `Restore v${restoreVersion}`, exact: true }).click();
    await editor.getByText(`Restored revision ${restoreVersion} as a new revision.`, { exact: true }).waitFor();
    await source.waitForFunction(() => document.querySelectorAll('.runtime-widget').length === 1 && document.querySelector('.runtime-widget')?.style.left === '40px');
    assert.equal(await source.locator('img,video,audio,.combined-chat').count(), 0, 'Restoring a text-only revision removes obsolete media/chat widgets');
    assert.equal(await source.locator('.widget-text').innerText(), 'G06 <script>escaped text</script>');
    assert.equal(sourceErrors, 0);
  } finally { await sourceContext.close(); }

  // No injected test may enter durable event history, including native mode.
  const history = await (await fetch(`${origin}/api/events`)).json();
  assert.ok(!JSON.stringify(history).includes('Native synthetic viewer'));
}

// Audit the generated or packaged guide in an isolated browser with networking denied.
import assert from 'node:assert/strict';
import { readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { chromium } from 'playwright';

// The caller scans generated files first. Accept named, fixed repository targets;
// CLI arguments must never become scanner options or filesystem paths.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const targets = new Map([
  ['packaged', path.join(root, 'release/portable/guide')],
  ['candidate', path.join(root, 'release/ui-redesign-offline-guide')],
  ['review', path.join(root, 'release/ui-redesign-offline-guide-wizard')],
  ['tray', path.join(root, 'release/tray-guide-preparation')],
  ['tray-native', path.join(root, 'release/tray-guide-native-launcher-review')],
  ['release-1.0.1', path.join(root, 'release/guide-1.0.1-final-prepublication')],
]);
const directory = targets.get(process.argv[2] ?? 'packaged');
assert.ok(directory, 'Choose a supported packaged or review guide target');
const pages = (await readdir(directory)).filter(name => name.endsWith('.html'));
assert.ok(pages.includes('Adaptive-Editor-and-Guided-Alerts.html'), 'The candidate guide chapter must be packaged');
const browser = await chromium.launch({ headless: true });
try {
  const context = await browser.newContext({ offline: true });
  await context.route(/^https?:/, route => route.abort());
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  for (const filename of pages) {
    await page.goto(pathToFileURL(path.join(directory, filename)).href);
    assert.ok(await page.locator('h1').innerText(), `Missing chapter title: ${filename}`);
    assert.equal(await page.locator('img').evaluateAll(images => images.every(image => image.complete && image.naturalWidth > 0)), true, `Broken screenshot: ${filename}`);
    for (const target of await page.locator('a').evaluateAll(links => links.map(link => link.href))) {
      if (target.startsWith('file:')) {
        const file = fileURLToPath(new URL(target));
        assert.ok(file.startsWith(directory + path.sep), 'Local guide links must remain inside the package');
        assert.ok((await stat(file)).isFile(), `Broken chapter link: ${filename}`);
      }
    }
    for (const width of [320, 390, 1366]) {
      await page.setViewportSize({ width, height: 844 });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `Guide reflow: ${filename} at ${width}px`);
    }
  }
  await page.goto(pathToFileURL(path.join(directory, 'Home.html')).href);
  await page.getByText('Your first working setup', { exact: true }).click();
  await page.getByRole('link', { name: 'Adaptive editor and guided alerts', exact: true }).first().click();
  await page.getByRole('heading', { name: 'Adaptive editor and guided alerts', exact: true }).waitFor();
  if (pages.includes('Tray-and-Desktop-Controls.html')) {
    await page.getByText('Everyday use', { exact: true }).click();
    await page.getByRole('link', { name: 'Find the app and quit', exact: true }).first().click();
    await page.getByRole('heading', { name: 'Find TDSBLive, open the editor and quit', exact: true }).waitFor();
    for (const name of ['tray-windows-menu.png', 'tray-windows-restart.png', 'tray-windows-fallback.png']) {
      assert.equal(await page.locator(`img[src="images/${name}"]`).count(), 1,
        `The tray guide must retain its Windows menu, confirmation and recovery illustrations: ${name}`);
    }
    if (process.argv[2] === 'tray-native') {
      assert.equal(await page.locator('img[src="images/tray-linux-menu.png"]').count(), 1,
        'Native Linux instructions must illustrate the actual native menu');
    }
  } else {
    assert.notEqual(process.argv[2], 'tray', 'Tray preparation must include the new chapter');
  }
  assert.deepEqual(errors, []);
  console.log(`Network-disabled offline guide passed: ${pages.length} chapters, local navigation, images and every chapter at 320/390/1366px`);
} finally { await browser.close(); }

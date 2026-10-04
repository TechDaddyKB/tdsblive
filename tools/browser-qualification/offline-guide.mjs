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
]);
const directory = targets.get(process.argv[2] ?? 'packaged');
assert.ok(directory, 'Choose packaged, candidate or review');
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
  }
  await page.goto(pathToFileURL(path.join(directory, 'Home.html')).href);
  await page.getByText('Your first working setup', { exact: true }).click();
  await page.getByRole('link', { name: 'Adaptive editor and guided alerts', exact: true }).first().click();
  await page.getByRole('heading', { name: 'Adaptive editor and guided alerts', exact: true }).waitFor();
  for (const width of [390, 1366]) {
    await page.setViewportSize({ width, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `Guide reflow at ${width}px`);
  }
  assert.deepEqual(errors, []);
  console.log(`Network-disabled offline guide passed: ${pages.length} chapters, local navigation, images and candidate reflow`);
} finally { await browser.close(); }

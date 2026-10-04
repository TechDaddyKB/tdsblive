// Audit the generated or packaged guide in an isolated browser with networking denied.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { chromium } from 'playwright';

const directory = path.resolve(process.argv[2] ?? 'release/portable/guide');
execFileSync('sonar', ['analyze', 'secrets', directory], { stdio: 'inherit' });
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

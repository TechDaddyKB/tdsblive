import assert from 'node:assert/strict';
import path from 'node:path';
import { mkdir } from 'node:fs/promises';
import { editorUI } from './editor-ui.mjs';

export async function qualifyUiRedesign(page, origin, writeHeaders, root) {
  await page.goto(`${origin}/editor#overlays`);
  const editor = editorUI(page);
  await editor.getByLabel('New overlay name').fill('Friendly alerts');
  await editor.getByRole('button', { name: 'Create overlay', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay"]')?.value === 'friendly-alerts');
  const saved = () => page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  const read = async () => (await fetch(`${origin}/api/overlays/friendly-alerts`)).json();
  for (const [name, trigger, template, threshold] of [['Follow greeting', 'Twitch · Follow', 'Welcome {user}!', null], ['Donation thanks', 'Ko-fi · Donation', 'Thank you {user}!', null], ['Big donation', 'Ko-fi · Donation', 'Big thanks {user}!', '10.00']]) {
    await editor.getByRole('button', { name: 'Add AlertBox', exact: true }).click();
    await editor.getByLabel('Layer name').fill(name);
    await editor.getByLabel('Choose trigger').selectOption({ label: trigger });
    if (threshold) {
      await editor.getByRole('button', { name: '2. Conditions', exact: true }).click();
      await editor.getByLabel('Alert condition').selectOption({ label: 'At least' });
      await editor.getByLabel('Alert measure').selectOption({ label: 'Reported money amount' });
      await editor.getByLabel('Alert amount').fill(threshold);
    }
    await editor.getByLabel('Alert template').fill(template);
    assert.ok(await page.locator('.canvas-widget').getByText(template.replace('{user}', 'Sample viewer'), { exact: true }).isVisible(), 'Unsaved design renders directly on canvas');
    await saved();
  }
  await editor.getByRole('button', { name: 'Create alert set', exact: true }).click();
  await editor.getByLabel('Add design').selectOption({ label: 'Donation thanks' });
  await editor.getByLabel('Add design').selectOption({ label: 'Big donation' });
  await editor.getByRole('button', { name: 'Move design 3 up', exact: true }).click();
  await saved();
  await editor.getByRole('button', { name: '4. Test', exact: true }).click();
  await editor.getByLabel('Test amount (USD)').fill('9.99');
  await editor.getByRole('button', { name: 'Test this design with sample data', exact: true }).click();
  const guidedResults = page.getByLabel('Guided matching results');
  await guidedResults.getByText('Big donation: Amount or quantity does not match', { exact: true }).waitFor();
  await guidedResults.getByText('Donation thanks: Matches this event', { exact: true }).waitFor();
  await editor.getByLabel('Test amount (USD)').fill('10.00');
  await editor.getByRole('button', { name: 'Test this design with sample data', exact: true }).click();
  await guidedResults.getByText('Big donation: Matches this event', { exact: true }).waitFor();
  await editor.getByLabel('Choose trigger').selectOption('native:Kofi.Donation');
  await editor.getByRole('button', { name: '4. Test', exact: true }).click();
  await editor.getByRole('button', { name: 'Test this design with sample data', exact: true }).click();
  await guidedResults.getByText('Big donation: Matches this event', { exact: true }).waitFor();
  await editor.getByLabel('Choose trigger').selectOption('kofi:support.donation');
  await saved();
  await editor.getByLabel('Test event type').selectOption({ label: 'Donation / paid support' });
  await editor.getByLabel('Test platform', { exact: true }).selectOption({ label: 'Ko-fi' });
  await editor.getByLabel('Sample amount', { exact: true }).fill('10.00');
  await editor.getByRole('button', { name: 'Send isolated test event', exact: true }).click();
  const results = page.getByLabel('Alert matching results');
  await results.getByText('Big donation: Matches this event', { exact: true }).waitFor();
  assert.ok((await results.innerText()).includes('Donation thanks: An earlier design in this set matched'));
  await editor.getByLabel('Design selection').selectOption({ label: 'All matching designs' });
  await editor.getByRole('button', { name: 'Send isolated test event', exact: true }).click();
  await results.getByText('Donation thanks: Matches this event', { exact: true }).waitFor();
  await saved();
  assert.equal(String((await read()).widgets[2].alert.condition.value), '1000');
  assert.equal((await read()).alertSets[0].selection, 'all');
  await editor.getByLabel('Design selection').selectOption({ label: 'First matching design' }); await saved();
  const geometry = (await read()).widgets.map(w => [w.id, w.x, w.y, w.width, w.height]);
  await mkdir(path.join(root, 'artifacts/ui-redesign'), { recursive: true });
  for (const theme of ['light', 'dark']) {
    await page.getByLabel('Application theme').selectOption(theme);
    for (const [width, height] of [[320,568],[390,844],[768,1024],[1024,768],[1366,768],[1920,1080],[1024,500]]) {
      await page.setViewportSize({ width, height });
      const panels = page.locator('.workspace-tabs');
      if (await panels.isVisible()) await panels.getByRole('button', { name: 'Canvas', exact: true }).click();
      await editor.getByRole('button', { name: 'Fit canvas to window', exact: true }).click();
      await page.waitForFunction(() => { const canvas = document.querySelector('.editor-canvas'), viewport = document.querySelector('.canvas-scroll'); const a = canvas?.getBoundingClientRect(), b = viewport?.getBoundingClientRect(); return a && b && a.width <= b.width && a.height <= b.height; });
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `${theme} ${width} must contain horizontal scrolling`);
      await editor.getByLabel('Overlay canvas').scrollIntoViewIfNeeded();
      await page.screenshot({ path: path.join(root, `artifacts/ui-redesign/${theme}-${width}x${height}.png`) });
      assert.deepEqual((await read()).widgets.map(w => [w.id, w.x, w.y, w.width, w.height]), geometry, 'Window resizing must not alter overlay geometry');
    }
  }
  // A 320 CSS pixel viewport represents the required reflow width at 400% zoom
  // on a 1280 pixel desktop. Real browser zoom must also be checked manually.
  await page.setViewportSize({ width: 1366, height: 768 });
  await editor.getByLabel('Canvas zoom').fill('0.7');
  await page.setViewportSize({ width: 1024, height: 500 });
  assert.equal(await editor.getByLabel('Canvas zoom').inputValue(), '0.7', 'Manual zoom remains under user control');
  await page.setViewportSize({ width: 1366, height: 768 });
  await editor.getByLabel('Overlay name').fill('Retained across navigation');
  await page.getByRole('link', { name: 'Media', exact: true }).click();
  await page.getByRole('heading', { name: 'Your media', exact: true }).waitFor();
  await page.goBack(); await page.getByRole('heading', { name: 'Visual Overlay Editor', exact: true }).waitFor();
  assert.equal(await editor.getByLabel('Overlay name').inputValue(), 'Retained across navigation');
  assert.equal((await read()).name, 'Retained across navigation');
  await page.reload(); await editor.getByLabel('Overlay', { exact: true }).selectOption('friendly-alerts');
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay name"]')?.value === 'Retained across navigation');
  assert.equal(await page.locator('.tdsblive-editor').getAttribute('data-theme'), 'dark');
  const server = await read(); server.name = 'Concurrent edit';
  assert.equal((await fetch(`${origin}/api/overlays/friendly-alerts`, { method: 'PUT', headers: writeHeaders, body: JSON.stringify(server) })).status, 200);
  await editor.getByLabel('Overlay name').fill('Unsaved conflicting draft');
  await page.getByRole('link', { name: 'Media', exact: true }).click();
  await page.getByRole('alert').getByText(/Resolve the save error or conflict before switching pages/).waitFor();
  assert.equal(await editor.getByLabel('Overlay name').inputValue(), 'Unsaved conflicting draft');
  assert.equal(new URL(page.url()).hash, '#overlays');
  await editor.getByRole('button', { name: 'Reload saved version', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay name"]')?.value === 'Concurrent edit');
  for (const destination of ['overview','overlays','chat','automation','supporters','media','connections','settings','diagnostics','help']) {
    await page.setViewportSize({ width: 320, height: 568 }); await page.goto(`${origin}/editor#${destination}`);
    await page.getByRole('heading', { name: 'TDSBLive', exact: true }).waitFor(); await page.waitForLoadState('networkidle');
    for (const summary of await page.locator('.app-content summary').all()) if (await summary.isVisible() && !await summary.evaluate(n => n.parentElement.open)) await summary.click();
    if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) console.error('Reflow overflow:', destination, await page.evaluate(() => ({scrollX, width:innerWidth, documentWidth:document.documentElement.scrollWidth, bodyWidth:document.body.scrollWidth})), await page.evaluate(() => Array.from(document.querySelectorAll('body *')).filter(n => n.getBoundingClientRect().right + scrollX > innerWidth && n.getBoundingClientRect().width > 0).map(n => ({tag:n.tagName,label:n.getAttribute('aria-label') ?? n.textContent.slice(0,80),right:n.getBoundingClientRect().right, cls:n.className}))));
    if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) { console.error('Overflow metrics:', await page.evaluate(() => Array.from(document.querySelectorAll('body *')).filter(n => n.scrollWidth > n.clientWidth + 2 && n.clientWidth > 0).map(n => ({ tag:n.tagName,cls:n.className, width:n.clientWidth,scroll:n.scrollWidth,overflow:getComputedStyle(n).overflowX,text:n.textContent.slice(0,60) })))); await page.screenshot({path:path.join(root,'artifacts/ui-redesign/overflow.png'),fullPage:true}); }
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, `${destination} must reflow at 320 CSS pixels`);
  }
  // Unsaved custom code uses memory only and cannot inherit network authority.
  const ownedCustom = { id: 'draft-isolation', name: 'Owned draft isolation', canvasEnabled: true, widgets: [{ kind: 'custom', name: 'Owned custom', custom: { permissions: ['storage','network'], networkDomains: ['example.com'],
    html: '<div id="count"></div><div id="network"></div>', javaScript: "SBX.store.get().then(() => SBX.store.set({count:44})).then(() => document.getElementById('count').textContent='Draft memory 44'); fetch('https://example.com/owned-draft').catch(() => document.getElementById('network').textContent='Draft network blocked');" } }] };
  assert.equal((await fetch(`${origin}/api/overlays`, { method: 'POST', headers: writeHeaders, body: JSON.stringify(ownedCustom) })).status, 201);
  const draftScene = await (await fetch(`${origin}/api/overlays/draft-isolation`)).json();
  const context = await page.context().browser().newContext({ viewport: { width: 390, height: 844 }, hasTouch: true }); const touch = await context.newPage(); let external = 0;
  try {
    await touch.route('https://example.com/**', route => { external++; return route.fulfill({ body: 'unexpected external access' }); });
    await touch.goto(`${origin}/editor#overlays`); const touchEditor = editorUI(touch);
    await touchEditor.getByLabel('Overlay', { exact: true }).selectOption('draft-isolation');
    const custom = touch.frameLocator('iframe[title="Owned custom"]'); await custom.locator('#count').getByText('Draft memory 44', { exact: true }).waitFor();
    await custom.locator('#network').getByText('Draft network blocked', { exact: true }).waitFor(); assert.equal(external, 0);
    for (const suffix of ['', '?preview=1']) assert.deepEqual(await (await fetch(`${origin}/api/overlays/draft-isolation/widgets/${draftScene.widgets[0].id}/store${suffix}`)).json(), {}, 'Draft storage must not write either persisted namespace');
    await touchEditor.getByLabel('Overlay', { exact: true }).selectOption('friendly-alerts');
    await touchEditor.getByLabel('Select multiple').check();
    await touchEditor.getByRole('button', { name: 'Follow greeting', exact: true }).focus();
    for (const name of ['Follow greeting','Donation thanks']) { const box = await touchEditor.getByRole('button', { name, exact: true }).boundingBox(); await touch.touchscreen.tap(box.x + box.width / 2, box.y + box.height / 2); }
    assert.equal(await touchEditor.getByLabel('Selected layers').innerText(), '2 selected', 'Touch multiselection requires no modifier keys');
    await touchEditor.getByLabel('Select multiple').uncheck();
    for (const [width,height] of [[1080,1920],[1000,1000],[3840,2160],[1920,1080]]) {
      await touchEditor.getByLabel('Canvas width').fill(String(width)); await touchEditor.getByLabel('Canvas height').fill(String(height));
      await touchEditor.getByRole('button', { name: 'Fit canvas to window', exact: true }).click();
      await touch.waitForFunction(() => { const a = document.querySelector('.editor-canvas')?.getBoundingClientRect(), b = document.querySelector('.canvas-scroll')?.getBoundingClientRect(); return a && b && a.width <= b.width && a.height <= b.height; });
      await touchEditor.getByRole('button', { name: 'Save now', exact: true }).click();
      await touch.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
      const doc = await read(); assert.equal(doc.width, width); assert.equal(doc.height, height); assert.deepEqual(doc.widgets.map(w => [w.id,w.x,w.y,w.width,w.height]), geometry);
    }
  } finally { await context.close(); }
  console.log('UI redesign browser qualification passed: named triggers, ordinary amounts, draft rendering, first/all matching, seven viewports in both themes, fitted/manual zoom, geometry preservation, history navigation and retained conflicts');
}

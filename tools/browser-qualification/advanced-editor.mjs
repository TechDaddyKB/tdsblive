import assert from 'node:assert/strict';

export async function qualifyAdvancedEditor(page, origin, writeHeaders) {
  await page.goto(`${origin}/editor`);
  const editor = page.getByRole('region', { name: 'Visual overlay editor' });
  await editor.getByLabel('New overlay ID').fill('g11-browser');
  await editor.getByLabel('New overlay name').fill('Advanced editor qualification');
  await editor.getByRole('button', { name: 'Create overlay', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay"]')?.value === 'g11-browser' &&
    document.querySelector('[aria-label="Overlay name"]')?.value === 'Advanced editor qualification');
  await editor.getByLabel('Overlay canvas').waitFor();
  const saved = () => page.waitForFunction(() => document.querySelector('[aria-label="Editor save status"]')?.textContent === 'saved');
  const document = async () => (await fetch(`${origin}/api/overlays/g11-browser`)).json();
  for (const [name, x] of [['First', 40], ['Second', 300], ['Third', 800]]) {
    await editor.getByRole('button', { name: 'Add text', exact: true }).click();
    await editor.getByLabel('Layer name').fill(name); await editor.getByLabel('X', { exact: true }).fill(String(x));
    await editor.getByLabel('Width', { exact: true }).fill('100');
  }
  await saved();
  await editor.getByRole('button', { name: 'First', exact: true }).click();
  await editor.getByRole('button', { name: 'Second', exact: true }).click({ modifiers: ['Shift'] });
  await editor.getByRole('button', { name: 'Third', exact: true }).click({ modifiers: ['Shift'] });
  await editor.getByRole('button', { name: 'Distribute horizontally', exact: true }).click(); await saved();
  assert.deepEqual((await document()).widgets.map(w => w.x), [40, 420, 800]);
  for (let i = 0; i < 3; i++) {
    await editor.getByRole('button', { name: 'Group selection', exact: true }).click();
    await editor.getByRole('button', { name: 'Rotate selection 15°', exact: true }).click();
    await editor.getByRole('button', { name: 'Align left', exact: true }).click();
    await editor.getByLabel('Overlay canvas').focus(); await page.keyboard.press('Shift+ArrowRight');
    await editor.getByRole('button', { name: 'Undo', exact: true }).click();
    await editor.getByRole('button', { name: 'Redo', exact: true }).click();
    await editor.getByRole('button', { name: 'Ungroup selection', exact: true }).click();
  }
  await editor.getByRole('button', { name: 'Group selection', exact: true }).click();
  await editor.getByLabel('Overlay canvas').focus(); await page.keyboard.press('Control+c'); await page.keyboard.press('Control+v');
  await saved(); let scene = await document(); assert.equal(scene.widgets.length, 6);
  assert.ok(scene.widgets.slice(0, 3).every(w => w.groupId === scene.widgets[0].groupId));
  assert.ok(scene.widgets.slice(3).every(w => w.groupId === scene.widgets[3].groupId)); assert.notEqual(scene.widgets[0].groupId, scene.widgets[3].groupId);
  await editor.getByRole('button', { name: 'Toggle selection lock', exact: true }).click();
  await editor.getByLabel('Overlay canvas').focus(); await page.keyboard.press('Delete'); assert.equal(await editor.locator('.canvas-widget').count(), 6);
  await editor.getByRole('button', { name: 'Toggle selection lock', exact: true }).click();
  await editor.getByRole('button', { name: 'Toggle selection visibility', exact: true }).click(); assert.equal(await editor.locator('.canvas-widget').count(), 3);
  await editor.getByRole('button', { name: 'Toggle selection visibility', exact: true }).click();
  await editor.getByRole('button', { name: 'Send selection to back', exact: true }).click(); await saved();
  assert.equal((await document()).widgets[0].id, scene.widgets[3].id);
  await editor.getByRole('button', { name: 'Bring selection to front', exact: true }).click(); await saved();
  scene = await document(); await page.reload(); await editor.getByLabel('Overlay canvas').waitFor();
  await editor.getByLabel('Overlay', { exact: true }).selectOption('g11-browser');
  await page.waitForFunction(() => document.querySelector('[aria-label="Overlay name"]')?.value === 'Advanced editor qualification');
  assert.deepEqual((await document()).widgets, scene.widgets);
  await editor.getByRole('button', { name: 'Third (group)', exact: true }).first().click();
  assert.equal(await editor.getByLabel('Selected layers').innerText(), '3 selected');
  await editor.getByLabel('Width', { exact: true }).fill('150'); await saved();
  assert.ok((await document()).widgets.slice(3).every(w => w.width === 150));
  await editor.getByLabel('Canvas zoom').fill('2'); await editor.getByRole('button', { name: 'Pan canvas', exact: true }).click();
  const viewport = editor.locator('.canvas-scroll'); await viewport.scrollIntoViewIfNeeded(); const box = await viewport.boundingBox(); assert.ok(box);
  await viewport.evaluate(node => { node.scrollTop = 300; });
  await page.mouse.move(box.x + 40, box.y + 70); await page.mouse.down(); await page.mouse.move(box.x + 40, box.y + 20, { steps: 5 }); await page.mouse.up();
  assert.ok(await viewport.evaluate(node => node.scrollTop) > 300);
  await editor.getByRole('button', { name: 'Pan canvas', exact: true }).click(); await editor.getByLabel('Canvas zoom').fill('0.4');
  await editor.getByLabel('Show grid', { exact: true }).uncheck(); assert.equal(await editor.locator('.show-grid').count(), 0);
  assert.equal(await editor.getByLabel('Snap to 10px grid').isChecked(), true);
  for (const kind of ['event-list', 'goal-bar', 'progress-bar']) await editor.getByRole('button', { name: `Add ${kind}`, exact: true }).click();
  await editor.getByLabel('Current value').fill('25'); await editor.getByLabel('Target value').fill('50'); await saved();
  await editor.getByRole('button', { name: 'Preview', exact: true }).click();
  const frame = page.frameLocator('iframe[title="Overlay test preview"]'); await frame.getByLabel('Overlay scene').waitFor();
  await frame.locator('.canvas-preview-label').getByText(/Connected/).waitFor();
  assert.equal(await frame.getByRole('progressbar').last().getAttribute('aria-valuenow'), '25');
  assert.equal(await frame.getByRole('progressbar').last().getAttribute('aria-valuemax'), '50');
  for (let i = 0; i < 12; i++) {
    const result = await fetch(`${origin}/api/overlays/g11-browser/preview-events`, { method: 'POST', headers: writeHeaders,
      body: JSON.stringify({ type: 'community.follow', platform: 'rumble', user: `Owned viewer ${i}`, message: 'Owned advanced widget example' }) });
    assert.equal(result.status, 200);
  }
  await frame.getByText('Owned viewer 11 · community.follow', { exact: true }).waitFor(); assert.equal(await frame.getByLabel('Event list').getByRole('listitem').count(), 10);
  const previewFrame = page.frames().find(f => f.url().includes('/overlay/g11-browser')); assert.ok(previewFrame);
  assert.equal(await previewFrame.evaluate(() => window.tdsbliveTestSockets.filter(s => s.readyState === WebSocket.OPEN).length), 1);
  await previewFrame.evaluate(() => window.tdsbliveTestSockets[0].close());
  await previewFrame.waitForFunction(() => window.tdsbliveTestSockets.length === 2 && window.tdsbliveTestSockets[1].g05Subscribed);
  assert.equal(await frame.getByLabel('Event list').getByRole('listitem').count(), 10);
  assert.ok(!JSON.stringify(await (await fetch(`${origin}/api/events`)).json()).includes('Owned advanced widget example'));
  console.log('G11 real-browser qualification passed: repeated grouped transforms/history, independent copy, lock/hide, z-order, resize, pan/grid/zoom, save/reload, Event List bounds/reconnect and manual progress');
}

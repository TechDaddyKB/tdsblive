import assert from 'node:assert/strict';

// Owned fixtures only; this host has all external integrations disabled.
export async function qualifyAutomation(page, origin, writeHeaders) {
  const read = async route => {
    const response = await fetch(`${origin}${route}`);
    assert.equal(response.status, 200);
    return response.json();
  };
  const beforeLedger = await read('/api/financial/ledger?limit=50&offset=0&state=all');
  const beforeExecutions = await read('/api/automation/executions');
  const ruleName = `G09 browser sound ${Date.now()}`;
  const overlayId = `g09-browser-${Date.now()}`;
  assert.equal((await fetch(`${origin}/api/overlays`, { method: 'POST', headers: writeHeaders,
    body: JSON.stringify({ id: overlayId, name: overlayId, canvasEnabled: true }) })).status, 201);
  const assets = [];
  for (const frequency of [440, 660]) {
    const wave = Buffer.alloc(44 + 48000);
    wave.write('RIFF'); wave.writeUInt32LE(wave.length - 8, 4); wave.write('WAVEfmt ', 8);
    wave.writeUInt32LE(16, 16); wave.writeUInt16LE(1, 20); wave.writeUInt16LE(1, 22);
    wave.writeUInt32LE(48000, 24); wave.writeUInt32LE(96000, 28);
    wave.writeUInt16LE(2, 32); wave.writeUInt16LE(16, 34); wave.write('data', 36); wave.writeUInt32LE(48000, 40);
    for (let sample = 0; sample < 24000; sample++) wave.writeInt16LE(Math.round(Math.sin(sample * 2 * Math.PI * frequency / 48000) * 3000), 44 + sample * 2);
    const response = await fetch(`${origin}/api/assets`, { method: 'POST', headers: { ...writeHeaders,
      'Content-Type': 'audio/wav', 'X-Asset-Filename': `G09-owned-${frequency}.wav` }, body: wave });
    assert.equal(response.status, 200); assets.push(await response.json());
  }
  await page.goto(`${origin}/editor`);
  const panel = page.getByRole('region', { name: 'Automation rules', exact: true });
  await panel.getByRole('button', { name: 'New sound rule', exact: true }).click();
  await panel.getByLabel('Rule name', { exact: true }).fill(ruleName);
  await panel.getByRole('combobox', { name: /^Target canvas overlay/ }).selectOption({ label: overlayId });
  for (const asset of assets) await panel.getByLabel(`Use audio: ${asset.filename}`, { exact: true }).check();
  await panel.getByLabel('Sound volume (0–1)', { exact: true }).fill('0.5');
  await panel.getByLabel('Playback timeout seconds', { exact: true }).fill('5');
  await panel.getByRole('button', { name: 'Save automation rule', exact: true }).click();
  await panel.getByRole('button', { name: `Edit ${ruleName}`, exact: true }).waitFor();
  const saved = (await read('/api/automation/rules')).find(rule => rule.name === ruleName);
  assert.ok(saved); assert.equal(saved.enabled, false);
  assert.equal(saved.actions[0].overlayId, overlayId);
  assert.deepEqual(saved.actions[0].soundAssetIds.sort(), assets.map(asset => asset.id).sort());
  assert.equal(Number(saved.actions[0].volume), 0.5);
  await page.reload();
  await panel.getByRole('button', { name: `Edit ${ruleName}`, exact: true }).click();
  for (const asset of assets) assert.equal(await panel.getByLabel(`Use audio: ${asset.filename}`, { exact: true }).isChecked(), true);
  const simulationResponse = page.waitForResponse(response => response.url().endsWith('/api/automation/simulate') && response.request().method() === 'POST');
  await panel.getByRole('button', { name: 'Simulate selected rule', exact: true }).click();
  const simulation = await (await simulationResponse).json();
  assert.equal(simulation.persisted, false); assert.equal(simulation.liveActionsAllowed, false);
  assert.deepEqual(await read('/api/automation/executions'), beforeExecutions);
  assert.deepEqual(await read('/api/financial/ledger?limit=50&offset=0&state=all'), beforeLedger);
  await panel.getByLabel(`Use audio: ${assets[1].filename}`, { exact: true }).uncheck();
  const saveResponse = page.waitForResponse(response => response.url().includes('/api/automation/rules/') && response.request().method() === 'PUT');
  await panel.getByRole('button', { name: 'Save automation rule', exact: true }).click();
  const edited = await (await saveResponse).json();
  assert.ok(Number(edited.version) > Number(saved.version));
  assert.deepEqual(edited.actions[0].soundAssetIds, [assets[0].id]);
  await panel.getByRole('button', { name: 'Delete automation rule', exact: true }).click();
  await panel.getByRole('button', { name: `Edit ${ruleName}`, exact: true }).waitFor({ state: 'detached' });
  assert.equal((await read('/api/automation/rules')).some(rule => rule.id === saved.id), false);
  console.log('G09 browser qualification passed: named canvas/audio selection, random variants, save/reload/edit/delete and side-effect-free simulation');
}

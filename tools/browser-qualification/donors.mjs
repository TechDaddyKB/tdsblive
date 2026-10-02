import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

export async function qualifyDonors(page, origin, writeHeaders) {
  const write = async (path, method, value) => {
    const response = await fetch(`${origin}${path}`, { method, headers: writeHeaders, body: JSON.stringify(value) });
    assert.equal(response.status, method === 'POST' && path === '/api/overlays' ? 201 : path === '/api/financial/rules' ? 204 : 200, path);
    return response.status === 204 ? undefined : response.json();
  };
  const kinds = ['donor-crown', 'donor-leaderboard', 'latest-supporter', 'current-stream-leader', 'current-stream-total'];
  const widgets = kinds.map((kind, i) => ({ id: randomUUID(), kind, name: kind, x: 10, y: i * 180, width: 900, height: 170 }));
  const original = await (await fetch(`${origin}/api/overlays/combined-chat`)).json();
  await write('/api/overlays', 'POST', { ...original, id: 'donor-qualification', name: 'Owned donor qualification', version: 1, canvasEnabled: true, widgets });
  await page.goto(`${origin}/overlay/donor-qualification`);
  const crown = page.locator(`[data-widget-id="${widgets[0].id}"]`);
  const total = page.locator(`[data-widget-id="${widgets[4].id}"]`);
  await crown.getByText(/\$12\.50/).waitFor();
  await total.getByText('$13.75', { exact: true }).waitFor();
  const identities = await (await fetch(`${origin}/api/financial/identities`)).json();
  const twitch = identities.find(row => row.platform === 'twitch');
  const kofi = identities.find(row => row.platform === 'kofi');
  await write(`/api/financial/identities/${twitch.id}/link`, 'POST', { expectedSupporterId: twitch.supporterId, targetSupporterId: kofi.supporterId });
  await crown.getByText(/\$13\.75/).waitFor();
  await write(`/api/financial/identities/${twitch.id}/unlink`, 'POST', { expectedSupporterId: kofi.supporterId });
  await crown.getByText(/\$12\.50/).waitFor();
  let overlay = await (await fetch(`${origin}/api/overlays/donor-qualification`)).json();
  overlay.widgets[0].donor.platforms = ['twitch'];
  await write('/api/overlays/donor-qualification', 'PUT', overlay);
  await crown.getByText(/\$1\.25/).waitFor();
  const rule = (await (await fetch(`${origin}/api/financial/rules`)).json()).find(row => row.platform === 'twitch' && row.type === 'bits');
  await write('/api/financial/rules', 'PUT', { platform: 'twitch', type: 'bits', tier: '', usdMinorPerUnit: '2', expectedVersion: rule.version });
  const ledger = await (await fetch(`${origin}/api/financial/ledger`)).json();
  const bits = ledger.items.find(row => row.nativeEventId === 'browser-bits');
  await write('/api/financial/reconcile', 'POST', { selected: [{ id: bits.id, version: bits.version }] });
  await crown.getByText(/\$2\.00/).waitFor();
  await total.getByText('$14.50', { exact: true }).waitFor();
  const socketsBefore = await page.evaluate(() => window.tdsbliveTestSockets.length);
  await page.evaluate(() => window.tdsbliveTestSockets.at(-1).close());
  await page.waitForFunction(before => window.tdsbliveTestSockets.length > before && window.tdsbliveTestSockets.at(-1).g05Subscribed, socketsBefore);
  await crown.getByText(/\$2\.00/).waitFor();
  await page.goto(`${origin}/overlay/donor-qualification?preview=1`);
  await page.getByText('Test preview · no production totals', { exact: true }).first().waitFor();
  assert.equal(await page.getByText(/\$12\.50|\$14\.50/).count(), 0);
  console.log('G08 real-browser donor snapshots, identity link/unlink, filters, reconciliation, reconnect and preview isolation passed');
}

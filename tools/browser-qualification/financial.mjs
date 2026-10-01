import assert from 'node:assert/strict';

export async function qualifyFinancial(page, origin) {
  await page.goto(`${origin}/editor`);
  const panel = page.getByRole('region', { name: 'Financial ledger' });
  await panel.getByText('All nominal values are unconfigured.', { exact: true }).waitFor();
  await panel.getByLabel('Financial timezone').fill('America/Chicago');
  await panel.getByLabel('Current stream start').fill('2026-01-05T12:00:00Z');
  await panel.getByRole('button', { name: 'Save financial periods', exact: true }).click();
  await panel.getByText('Financial periods saved.', { exact: true }).waitFor();
  const settings = await (await fetch(`${origin}/api/financial/settings`)).json();
  assert.equal(settings.timeZone, 'America/Chicago');
  assert.equal(Date.parse(settings.currentStreamStartUtc), Date.parse('2026-01-05T12:00:00Z'));
  await panel.getByLabel('USD per unit').fill('0.0125');
  await panel.getByRole('button', { name: 'Save nominal value', exact: true }).click();
  await panel.getByText('Nominal value saved; history is unchanged.', { exact: true }).waitFor();
  let rules = await (await fetch(`${origin}/api/financial/rules`)).json();
  assert.equal(rules[0].usdMinorPerUnit, '1.25');
  await panel.getByLabel('Rate date').fill('2026-01-05');
  await panel.getByLabel('Manual USD rate').fill('1.23456789');
  await panel.getByRole('button', { name: 'Save manual rate', exact: true }).click();
  await panel.getByText('Dated manual rate saved; history is unchanged.', { exact: true }).waitFor();
  const rates = await (await fetch(`${origin}/api/financial/rates`)).json();
  assert.equal(rates[0].usdPerNativeUnit, '1.23456789');
  await page.reload();
  await panel.getByText(/Saved timezone: America\/Chicago/).waitFor();
  await panel.getByRole('button', { name: 'Remove twitch bits', exact: true }).click();
  await panel.getByText('Rule removed; future values are unconfigured.', { exact: true }).waitFor();
  rules = await (await fetch(`${origin}/api/financial/rules`)).json();
  assert.equal(rules[0].enabled, false);
  await panel.getByRole('button', { name: 'Remove EUR override for 2026-01-05', exact: true }).click();
  await panel.getByText('Manual override removed; history is unchanged.', { exact: true }).waitFor();
  await panel.getByLabel('Totals period').selectOption('custom');
  await panel.getByLabel('Start date', { exact: true }).fill('2026-01-01');
  await panel.getByLabel('End date (exclusive)').fill('2026-02-01');
  await panel.getByText(/Timezone: America\/Chicago/).waitFor();
  assert.equal(await panel.getByRole('button', { name: 'Reconcile selected valuations' }).isDisabled(), true);
  console.log('G07 isolated browser settings/rule/manual-FX precision and reload qualification passed');
}

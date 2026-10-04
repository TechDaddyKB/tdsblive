// Reveal redesigned controls through user-facing buttons and summaries.
// Existing qualification assertions keep exercising the original capabilities.
export function editorUI(page) {
  const root = page.getByRole('region', { name: 'Visual overlay editor' });
  async function reveal(locator, query) {
    if (query?.startsWith('New overlay') && !await locator.count()) await root.getByRole('button', { name: 'New overlay', exact: true }).click();
    if (query?.startsWith('Add ') && query !== 'Add widget' && !await locator.count()) await root.getByRole('button', { name: 'Add widget', exact: true }).click();
    await locator.first().waitFor({ state: 'attached' });
    const place = await locator.first().evaluate(node => ({ layers: !!node.closest('.layers-panel'), properties: !!node.closest('.properties-panel'), canvas: !!node.closest('.canvas-scroll'), step: node.closest('.alert-editor') ? Array.from(node.closest('.alert-editor').children).filter(n => n.tagName === 'DIV' && !n.classList.contains('step-actions')).findIndex(n => n.contains(node)) : -1 }));
    await openPanel(place);
    if (place.step >= 0) await root.getByRole('navigation', { name: 'Alert setup steps' }).getByRole('button').nth(place.step).click();
    const summaries = await locator.first().evaluate(node => {
      const titles = [];
      for (let parent = node.parentElement; parent; parent = parent.parentElement) {
        if (parent.tagName === 'DETAILS' && !parent.open) titles.unshift(parent.querySelector(':scope > summary').textContent);
      }
      return titles;
    });
    for (const title of summaries) await root.locator('summary').getByText(title, { exact: true }).click();
  }
  async function openPanel(place) {
    const panels = root.locator('.workspace-tabs');
    if (!await panels.isVisible()) return;
    let name = '';
    if (place.layers) name = 'Layers';
    else if (place.properties) name = 'Properties';
    else if (place.canvas) name = 'Canvas';
    if (!name) return;
    const button = panels.getByRole('button', { name, exact: true });
    if (await button.getAttribute('aria-pressed') !== 'true') await button.click();
  }
  function nextQuery(name, args, query) {
    if (name === 'getByLabel') return args[0];
    if (name === 'getByRole') return args[1]?.name;
    return query;
  }
  const chainMethods = new Set(['locator', 'getByRole', 'getByLabel', 'getByText', 'getByTestId', 'getByPlaceholder', 'filter', 'first', 'last', 'nth']);
  const actions = new Set(['click', 'fill', 'selectOption', 'check', 'uncheck', 'setInputFiles', 'focus', 'boundingBox', 'screenshot', 'scrollIntoViewIfNeeded', 'waitFor']);
  function wrap(locator, query) { return new Proxy(locator, { get(target, name) {
    if (chainMethods.has(name)) return (...args) => { if (name === 'getByRole') { args[1] = { ...args[1], includeHidden: true }; }
      return wrap(target[name](...args), nextQuery(name, args, query)); };
    if (actions.has(name)) return async (...args) => { if (name !== 'waitFor' || args[0]?.state !== 'hidden') { await reveal(target, typeof query === 'string' ? query : undefined); }
      return target[name](...args); };
    const value = target[name]; return typeof value === 'function' ? value.bind(target) : value;
  } }); }
  return wrap(root);
}

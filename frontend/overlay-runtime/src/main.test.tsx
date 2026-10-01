import { act, screen } from '@testing-library/react';
import { expect, it } from 'vitest';

it('starts a transparent overlay page without importing the editor', async () => {
  document.body.innerHTML = '<div id="root"></div>';
  await act(async () => { await import('./main'); });
  expect(document.body).toHaveStyle({ background: 'transparent' });
  expect(screen.getByLabelText('Overlay')).toBeVisible();
  expect(screen.queryByRole('heading', { name: 'TDSBLive' })).toBeNull();
});

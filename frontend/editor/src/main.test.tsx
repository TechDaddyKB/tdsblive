import { act, screen } from '@testing-library/react';
import { expect, it } from 'vitest';

it('starts the editor in its HTML mount point with truthful initial connection state', async () => {
  document.body.innerHTML = '<div id="root"></div>';
  await act(async () => { await import('./main'); });
  expect(screen.getByRole('heading', { name: 'TDSBLive' })).toBeVisible();
  expect(screen.getByLabelText('Streamer.bot connection status')).toHaveTextContent('Disconnected');
});

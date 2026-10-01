import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { App } from './App';

afterEach(cleanup);

it('does not report a connected integration before setup', () => {
  render(<App />);
  expect(screen.getByRole('heading', { name: 'TDSBLive' })).toBeVisible();
  expect(screen.getByLabelText('Streamer.bot connection status')).toHaveTextContent('Disconnected');
});

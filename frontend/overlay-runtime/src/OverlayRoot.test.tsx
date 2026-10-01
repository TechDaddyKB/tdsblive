import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { OverlayRoot } from './OverlayRoot';

afterEach(cleanup);

it('preserves transparent composition while rendering widget content', () => {
  render(<OverlayRoot><span>Synthetic widget</span></OverlayRoot>);
  expect(screen.getByLabelText('Overlay')).toHaveStyle({ background: 'transparent' });
  expect(screen.getByText('Synthetic widget')).toBeVisible();
});

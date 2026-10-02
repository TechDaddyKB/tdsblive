import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { PlatformBadge } from './PlatformBadge';
afterEach(cleanup);
it.each([['twitch', 'Twitch'], ['youtube', 'YouTube'], ['kick', 'Kick'], ['rumble', 'Rumble'], ['ko-fi', 'Ko-fi'], ['streamlabs', 'Streamlabs'], ['patreon', 'Patreon']])('renders the %s logo with its accessible name', (platform, name) => {
  render(<PlatformBadge platform={platform} />);
  const image = screen.getByRole('img', { name: `${name} platform` });
  expect(image).toHaveAttribute('viewBox', '0 0 24 24');
  expect(image.querySelector('path')?.getAttribute('d')?.length).toBeGreaterThan(20);
  expect(image.querySelector('circle')).toBeNull();
  expect(screen.getByTitle(name)).toHaveTextContent('');
});
it.each(['<unknown>', 'constructor'])('uses an honest generic badge for unknown platform %s', platform => {
  render(<PlatformBadge platform={platform} />);
  const image = screen.getByRole('img', { name: `${platform} platform` });
  expect(image.querySelector('circle')).not.toBeNull();
  expect(document.querySelector('unknown')).toBeNull();
});

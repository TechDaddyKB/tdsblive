import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { ChatMessage } from './ChatMessage';
afterEach(cleanup);
it('renders all provider artwork, animated image URLs and zero-width emotes without HTML', () => {
  render(<ChatMessage message={{ text: 'Hello', parts: [{ kind: 'text', text: '<script>Hello</script> ' }, ...['twitch', '7TV', 'BTTV', 'FFZ'].map(source => ({ kind: 'emote', text: source, source, imageUrl: `https://example.invalid/${source}.gif`, zeroWidth: source === '7TV' }))] }} />);
  expect(document.querySelector('script')).toBeNull(); expect(screen.getByText('<script>Hello</script>')).toBeVisible();
  for (const source of ['twitch', '7TV', 'BTTV', 'FFZ']) expect(screen.getByRole('img', { name: source })).toHaveAttribute('referrerpolicy', 'no-referrer');
  expect(screen.getByRole('img', { name: '7TV' })).toHaveClass('zero-width');
  fireEvent.error(screen.getByRole('img', { name: 'BTTV' })); expect(document.body.textContent).toContain('BTTV'); expect(screen.queryByRole('img', { name: 'BTTV' })).toBeNull();
});
it('renders bounded GIF attachments with safe failure and unsafe URL fallback', () => {
  render(<ChatMessage message={{ text: '', parts: [{ kind: 'gif', text: '', imageUrl: 'https://example.invalid/reaction.gif' }, { kind: 'gif', text: 'unsafe', imageUrl: 'javascript:alert(1)' }] }} />);
  const gif = screen.getByRole('img', { name: '[GIF]' }); expect(gif).toHaveClass('chat-gif'); expect(screen.getByText('unsafe')).toBeVisible();
  fireEvent.error(gif); expect(document.body.textContent).toContain('[GIF]'); expect(screen.queryByRole('img', { name: '[GIF]' })).toBeNull();
});
it('uses original escaped text for absent or oversized media parts', () => {
  const view = render(<ChatMessage message={{ text: 'Plain message' }} />); expect(screen.getByText('Plain message')).toBeVisible();
  view.rerender(<ChatMessage message={{ text: 'Original', parts: Array.from({ length: 257 }, () => ({ kind: 'text', text: 'Ignored' })) }} />);
  expect(screen.getByText('Original')).toBeVisible(); expect(screen.queryByText('Ignored')).toBeNull();
});

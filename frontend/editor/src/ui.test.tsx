import { useState } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Dialog } from './ui';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

function Example() {
  const [open, setOpen] = useState(false);
  return <><button onClick={() => setOpen(true)}>Open sample</button>{open && <Dialog title="Sample" close={() => setOpen(false)}>
    <label>Sample content<textarea /></label><button>Done</button><button disabled>Unavailable</button><div hidden><button>Hidden control</button></div>
  </Dialog>}</>;
}

it('keeps modal tab focus on visible enabled controls in both directions and restores the opener', () => {
  // jsdom has no layout; model only whether a control participates in rendering.
  vi.spyOn(HTMLElement.prototype, 'getClientRects').mockImplementation(function (this: HTMLElement) {
    return (this.closest('[hidden]') ? [] : [new DOMRect(0, 0, 44, 44)]) as unknown as DOMRectList;
  });
  render(<Example />);
  const opener = screen.getByRole('button', { name: 'Open sample' }); opener.focus(); fireEvent.click(opener);
  const first = screen.getByRole('button', { name: 'Close Sample' });
  const last = screen.getByRole('button', { name: 'Done' });
  last.focus(); expect(fireEvent.keyDown(last, { key: 'Tab' })).toBe(false); expect(first).toHaveFocus();
  expect(fireEvent.keyDown(first, { key: 'Tab', shiftKey: true })).toBe(false); expect(last).toHaveFocus();
  const content = screen.getByLabelText('Sample content'); content.focus();
  expect(fireEvent.keyDown(content, { key: 'Tab' })).toBe(true); expect(content).toHaveFocus();
  expect(fireEvent.keyDown(content, { key: 'ArrowRight' })).toBe(true);
  fireEvent(screen.getByRole('dialog', { name: 'Sample' }), new Event('cancel', { cancelable: true }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); expect(opener).toHaveFocus();
});

import type { ReactNode } from 'react';

export function OverlayRoot({ children }: { children?: ReactNode }) {
  return <div aria-label="Overlay" style={{ background: 'transparent' }}>{children}</div>;
}

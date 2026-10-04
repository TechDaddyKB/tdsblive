import { useEffect, useRef, type ReactNode } from 'react';
export function Dialog({ title, close, children }: { title: string; close: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  const previous = useRef(document.activeElement as HTMLElement | null);
  useEffect(() => { const element = ref.current;
    if (element?.showModal) element.showModal(); else element?.setAttribute('open', '');
    return () => { element?.close?.(); previous.current?.focus(); };
  }, []);
  return <dialog ref={ref} className="ui-dialog" aria-label={title} onCancel={e => { e.preventDefault(); close(); }} onKeyDown={e => {
    if (e.key !== 'Tab') return;
    const controls = Array.from(e.currentTarget.querySelectorAll<HTMLElement>('button, input, select, textarea, a[href], summary, [tabindex]'))
      .filter(node => node.tabIndex >= 0 && !node.matches(':disabled') && node.getClientRects().length > 0);
    const target = e.shiftKey ? controls.at(-1) : controls[0];
    if (document.activeElement === (e.shiftKey ? controls[0] : controls.at(-1))) { e.preventDefault(); target?.focus(); }
  }}>
    <header><h2>{title}</h2><button type="button" onClick={close} aria-label={`Close ${title}`}>Close</button></header>{children}
  </dialog>;
}
export function FieldGroup({ title, children, open = false }: { title: string; children: ReactNode; open?: boolean }) {
  return <details className="field-group" open={open}><summary>{title}</summary><div className="field-group-body">{children}</div></details>;
}

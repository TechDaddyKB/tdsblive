import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ConnectionIndicator, type ConnectionState } from './ConnectionIndicator';

afterEach(cleanup);

describe('connection health presentation', () => {
  it.each<[ConnectionState, string]>([
    ['disconnected', 'Disconnected'], ['connecting', 'Connecting'],
    ['connected', 'Connected'], ['error', 'Connection failed'],
  ])('reports %s without claiming another connection state', (state, label) => {
    render(<ConnectionIndicator integration="Rumble" state={state} />);
    const status = screen.getByLabelText('Rumble connection status');
    expect(status).toHaveTextContent(`Rumble: ${label}`);
    expect(status).toHaveAttribute('aria-live', 'polite');
  });

  it('renders an integration name as text rather than executable markup', () => {
    const { container } = render(<ConnectionIndicator integration="<script>synthetic-name</script>" state="disconnected" />);
    expect(container.querySelector('script')).toBeNull();
    expect(container).toHaveTextContent('<script>synthetic-name</script>');
  });
});

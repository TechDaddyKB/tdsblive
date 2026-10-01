export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error';

const labels: Record<ConnectionState, string> = {
  disconnected: 'Disconnected',
  connecting: 'Connecting',
  connected: 'Connected',
  error: 'Connection failed',
};

export function ConnectionIndicator({ integration, state }: { integration: string; state: ConnectionState }) {
  return <output aria-live="polite" aria-label={`${integration} connection status`} data-state={state}>
    {integration}: {labels[state]}
  </output>;
}

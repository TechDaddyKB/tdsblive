import { ConnectionIndicator } from './ConnectionIndicator';

export function App() {
  return <main>
    <h1>TDSBLive</h1>
    <ConnectionIndicator integration="Streamer.bot" state="disconnected" />
  </main>;
}

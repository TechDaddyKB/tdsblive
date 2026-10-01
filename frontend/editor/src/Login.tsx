import { useState } from 'react';
import { api } from './api';

export function Login({ onAuthenticated = () => window.location.assign('/editor') }: Readonly<{ onAuthenticated?: () => void }>) {
  const [credential, setCredential] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  return <main>
    <h1>TDSBLive sign in</h1>
    <p>Use the admin credential generated on the streaming computer.</p>
    <form onSubmit={async event => {
      event.preventDefault();
      setError(''); setBusy(true);
      try { await api.login(credential); setCredential(''); onAuthenticated(); }
      catch { setError('Sign in failed. Check your credential and try again.'); }
      finally { setBusy(false); }
    }}>
      <label>Admin credential <input type="password" autoComplete="current-password" value={credential}
        onChange={event => setCredential(event.target.value)} required /></label>
      <button type="submit" disabled={busy}>Sign in</button>
    </form>
    {error && <p role="alert">{error}</p>}
  </main>;
}

import { createRoot } from 'react-dom/client';
import { OverlayRoot } from './OverlayRoot';
import { CombinedChat } from './CombinedChat';

document.body.style.background = 'transparent';
const path = location.pathname.split('/').filter(Boolean);
const id = path[1] ?? 'combined-chat';
const token = new URLSearchParams(location.hash.slice(1)).get('token') ?? '';
createRoot(document.getElementById('root')!).render(<OverlayRoot><CombinedChat id={id} streamer={path[0] === 'chat'}
  token={token} preview={new URLSearchParams(location.search).get('preview') === '1'} /></OverlayRoot>);

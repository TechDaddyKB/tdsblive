import { createRoot } from 'react-dom/client';
import { OverlayRoot } from './OverlayRoot';
import { CombinedChat } from './CombinedChat';
import { OverlayView } from './CanvasRuntime';

document.body.style.background = 'transparent';
const path = location.pathname.split('/').filter(Boolean);
const id = path[1] ?? 'combined-chat';
const token = new URLSearchParams(location.hash.slice(1)).get('token') ?? '';
const preview = new URLSearchParams(location.search).get('preview') === '1';
const previewAudio = new URLSearchParams(location.search).get('audio') === '1';
createRoot(document.getElementById('root')!).render(<OverlayRoot>{path[0] === 'chat' ? <CombinedChat id={id} streamer token={token} preview={preview} /> : <OverlayView id={id} token={token} preview={preview} previewAudio={previewAudio} />}</OverlayRoot>);

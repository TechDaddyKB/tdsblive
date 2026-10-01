import { createRoot } from 'react-dom/client';
import { OverlayRoot } from './OverlayRoot';

document.body.style.background = 'transparent';
createRoot(document.getElementById('root')!).render(<OverlayRoot />);

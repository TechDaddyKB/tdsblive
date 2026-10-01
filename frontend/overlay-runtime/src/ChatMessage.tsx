import { useState } from 'react';
import { safeAvatar, type ChatEvent } from './chat';

type Part = NonNullable<NonNullable<ChatEvent['message']>['parts']>[number];
function MediaPart({ part }: { part: Part }) {
  const [failed, setFailed] = useState(false);
  const url = safeAvatar(part.imageUrl);
  if (failed || !url || !['emote', 'gif'].includes(part.kind)) return <>{part.text || (part.kind === 'gif' ? '[GIF]' : '')}</>;
  return <img className={`chat-media ${part.kind === 'gif' ? 'chat-gif' : 'chat-emote'} ${part.zeroWidth ? 'zero-width' : ''}`} src={url}
    alt={part.text || (part.kind === 'gif' ? '[GIF]' : '[Emote]')} title={`${part.text || part.kind}${part.source ? ` · ${part.source}` : ''}`} referrerPolicy="no-referrer" onError={() => setFailed(true)} />;
}
export function ChatMessage({ message }: { message: ChatEvent['message'] }) {
  if (!message?.parts?.length || message.parts.length > 256) return <>{message?.text}</>;
  return <>{message.parts.map((part, index) => <MediaPart key={`${index}-${part.imageUrl ?? ''}`} part={part} />)}</>;
}

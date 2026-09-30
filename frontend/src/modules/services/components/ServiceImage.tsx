import { useState } from 'react';
import { NoPhoto } from '../../../shared/ui/NoPhoto';

export function ServiceImage({ src, alt, name, className }: { src: string | null; alt: string | null; name: string; className?: string }) {
  const [failed, setFailed] = useState(false);
  return src && !failed
    ? <img src={src} alt={alt ?? ''} className={className} onError={() => setFailed(true)} />
    : <NoPhoto name={name} context="Servicio" ratio="wide" />;
}

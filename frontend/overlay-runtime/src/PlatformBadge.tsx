import { platformLogos } from './platform-logos';

export function PlatformBadge({ platform }: { platform: string }) {
  const key = platform.toLowerCase().replace(/[^a-z0-9]/g, '');
  const logo = Object.hasOwn(platformLogos, key) ? platformLogos[key] : undefined;
  const name = logo?.name ?? platform;
  return <span className="donor-platform" title={name}>
    <svg viewBox="0 0 24 24" role="img" aria-label={`${name} platform`} fill="currentColor">
      {logo ? <path d={logo.path} /> : <><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" strokeWidth="2" /><path d="M11 7h2v2h-2zm0 4h2v6h-2z" /></>}
    </svg>
  </span>;
}

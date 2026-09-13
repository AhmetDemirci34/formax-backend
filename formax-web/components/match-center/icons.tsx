/**
 * Maç Detay Merkezi — inline SVG ikon seti.
 * Proje standardı: inline SVG (Lucide/shadcn kurulu değil). Ölçü 24px, stroke 2,
 * renk `currentColor` (design token uyumlu). Görsel referans: Stitch SCREEN_4/5.
 */

interface IconProps {
  className?: string;
  size?: number;
}

function base(size = 24) {
  return {
    width: size,
    height: size,
    viewBox: "0 0 24 24",
    fill: "none" as const,
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  };
}

// ── Header ───────────────────────────────────────────────────────────────────
export function BackIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M15 18l-6-6 6-6" />
    </svg>
  );
}

export function BellIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.7 21a2 2 0 0 1-3.4 0" />
    </svg>
  );
}

export function CloseIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  );
}

// ── Action ikonları (Stitch) ─────────────────────────────────────────────────
// AI Maç Analizi — ampul
export function BulbIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M9 18h6M10 21h4" />
      <path d="M12 3a6 6 0 0 0-4 10.5c.6.6 1 1.4 1 2.5h6c0-1.1.4-1.9 1-2.5A6 6 0 0 0 12 3z" />
    </svg>
  );
}

// Form Durumları — yükselen trend
export function TrendIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M3 17l5-5 3 3 7-8" />
      <path d="M18 7h3v3" />
    </svg>
  );
}

// Kadro Bilgisi — 2x2 grid
export function GridIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <rect x="4" y="4" width="7" height="7" rx="1.5" />
      <rect x="13" y="4" width="7" height="7" rx="1.5" />
      <rect x="4" y="13" width="7" height="7" rx="1.5" />
      <rect x="13" y="13" width="7" height="7" rx="1.5" />
    </svg>
  );
}

// Canlı Takip — yayın dalgaları
export function BroadcastIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <circle cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
      <path d="M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7" />
      <path d="M6 6a9 9 0 0 0 0 12M18 6a9 9 0 0 1 0 12" />
    </svg>
  );
}

// Son Dakika — haber/döküman
export function NewsIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M4 5h12v14H5a1 1 0 0 1-1-1z" />
      <path d="M16 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-2 0" />
      <path d="M7 9h6M7 12h6M7 15h4" />
    </svg>
  );
}

// ── Bottom nav ───────────────────────────────────────────────────────────────
export function CompassIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <polygon points="16 8 13 13 8 16 11 11" fill="currentColor" stroke="none" />
    </svg>
  );
}
export function BallIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7.5l3.2 2.3-1.2 3.7h-4l-1.2-3.7z" />
      <path d="M12 7.5V4.5M14 13.5l2.4 1.8M10 13.5l-2.4 1.8" />
    </svg>
  );
}
export function RadioIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <circle cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
      <path d="M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7" />
      <path d="M6 6a9 9 0 0 0 0 12M18 6a9 9 0 0 1 0 12" />
    </svg>
  );
}
export function ClipboardIcon({ className, size }: IconProps) {
  // Tahminlerim — pano / kontrol listesi
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <rect x="6" y="4" width="12" height="17" rx="2" />
      <path d="M9 5.5h6" />
      <path d="M9.5 11l1.6 1.6 3.4-3.6M9.5 16.5h5" />
    </svg>
  );
}
export function HeartIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <path d="M12 20s-7-4.3-7-9.2A4.2 4.2 0 0 1 12 8a4.2 4.2 0 0 1 7 2.8C19 15.7 12 20 12 20z" />
    </svg>
  );
}
export function UserIcon({ className, size }: IconProps) {
  return (
    <svg {...base(size)} className={className} aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1" />
    </svg>
  );
}

// Hesabım (SCREEN_12) — mevcut setlerde bulunmayan ikonlar.
// Proje standardı: inline SVG (Lucide/shadcn kurulu değil), currentColor.
import type { SVGProps } from "react";

type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Base({ size = 24, children, ...rest }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      {...rest}
    >
      {children}
    </svg>
  );
}

/** Header geri butonu — `arrow_back_ios` (ince sol chevron). */
export function BackIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M15 5l-7 7 7 7" />
    </Base>
  );
}

/** Avatar overlay — profil fotoğrafını değiştir (`photo_camera`). */
export function CameraIcon(p: IconProps) {
  return (
    <Base {...p} strokeWidth={2}>
      <path d="M4 8.5h3l1.4-2h7.2l1.4 2h3v10H4z" />
      <circle cx="12" cy="13" r="3.2" />
    </Base>
  );
}

/** Hesabı Sil — `delete` (çöp kutusu). */
export function TrashIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 7h16" />
      <path d="M9.5 7V5.5A1.5 1.5 0 0 1 11 4h2a1.5 1.5 0 0 1 1.5 1.5V7" />
      <path d="M6.5 7l.8 12a1.5 1.5 0 0 0 1.5 1.4h6.4a1.5 1.5 0 0 0 1.5-1.4l.8-12" />
      <path d="M10.5 11v6M13.5 11v6" />
    </Base>
  );
}

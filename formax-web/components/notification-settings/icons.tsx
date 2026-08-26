// Bildirim Tercihleri (SCREEN_04) — mevcut setlerde bulunmayan ikonlar.
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

/** Sistem Duyuruları — megafon. */
export function MegaphoneIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 10v4a1.5 1.5 0 0 0 1.5 1.5H8l8 4.5V5L8 9.5H5.5A1.5 1.5 0 0 0 4 11z" />
      <path d="M8 15.5V20h3" />
      <path d="M19 10.5v3" />
    </Base>
  );
}

/** Sessiz Saatler — ay (dark_mode). */
export function MoonIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M20 14.5A8.5 8.5 0 0 1 9.5 4a8.5 8.5 0 1 0 10.5 10.5z" />
    </Base>
  );
}

/**
 * Bilgi kartındaki büyük çan illüstrasyonu (Stitch görseli): çalan çan —
 * gövde hafif eğik, iki yanında ses dalgası yayları.
 */
export function BellOutlineIcon(p: IconProps) {
  return (
    <Base {...p} strokeWidth={1.5}>
      <path d="M17.2 9.4a5.2 5.2 0 1 0-10.4 0c0 5.6-2.2 7.4-2.2 7.4h14.8s-2.2-1.8-2.2-7.4" />
      <path d="M13.6 19.8a2 2 0 0 1-3.2 0" />
      <path d="M20.6 5.6a8.6 8.6 0 0 1 1.7 3.1M3.4 5.6a8.6 8.6 0 0 0-1.7 3.1" />
    </Base>
  );
}

/** Sessiz Saatler — başlangıç/bitiş satırı saat ikonu (clock). */
export function ClockIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 1.8" />
    </Base>
  );
}

/** Bilgi kartı — dolu amber info rozeti (Stitch'te amber dolu daire + beyaz i). */
export function InfoFilledIcon(p: IconProps) {
  const { size = 24, ...rest } = p;
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden {...rest}>
      <circle cx="12" cy="12" r="10" fill="currentColor" />
      <circle cx="12" cy="8" r="1.15" fill="#000" />
      <rect x="11" y="10.6" width="2" height="6" rx="1" fill="#000" />
    </svg>
  );
}

/** Maç kategorisi rozeti — küçük saha/top glifi. */
export function MatchGlyphIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 6.5l3.6 2.6-1.4 4.3h-4.4L8.4 9.1z" />
    </Base>
  );
}

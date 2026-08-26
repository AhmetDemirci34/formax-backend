// Profil Merkezi (SCREEN_15) — mevcut ikon setinde BULUNMAYAN ikonlar.
// Proje standardı: inline SVG (Lucide/shadcn kurulu değil), currentColor, ince stroke.
// Zaten var olanlar (UserIcon, BellIcon, SettingsIcon, GlobeIcon, LifeBuoyIcon,
// ChevronRightIcon) `@/components/discover/icons` üzerinden yeniden kullanılır —
// burada tekrarlanmaz.
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

/** Header sağ aksiyon — "Düzenle" (UI Spec §6: Edit Icon, 24px). */
export function PencilIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 20h4l10-10a2.5 2.5 0 0 0-3.5-3.5L4.5 16.5z" />
      <path d="M13.5 7l3.5 3.5" />
    </Base>
  );
}

/** Ödemeler & Abonelik — Material "payments". */
export function PaymentsIcon(p: IconProps) {
  return (
    <Base {...p}>
      <rect x="3" y="6" width="18" height="12" rx="2.5" />
      <path d="M3 10h18" />
      <path d="M7 14.5h3" />
    </Base>
  );
}

/** Tahmin Geçmişim — Material "history". */
export function HistoryIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M3.5 12a8.5 8.5 0 1 0 2.6-6.1" />
      <path d="M3.5 4.5V9H8" />
      <path d="M12 7.5V12l3 1.8" />
    </Base>
  );
}

/** Dil Seçimi — Material "language" (Globe'dan ayrı, harf tabanlı çeviri ikonu). */
export function LanguageIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="9" />
      <path d="M3.6 9h16.8M3.6 15h16.8" />
      <path d="M12 3c2.2 2.4 3.4 5.6 3.4 9S14.2 18.6 12 21c-2.2-2.4-3.4-5.6-3.4-9S9.8 5.4 12 3z" />
    </Base>
  );
}

/** Çıkış Yap — Material "logout" (destructive satır). */
export function LogoutIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M14 4h3.5A2.5 2.5 0 0 1 20 6.5v11a2.5 2.5 0 0 1-2.5 2.5H14" />
      <path d="M10 8l-4 4 4 4" />
      <path d="M6 12h9" />
    </Base>
  );
}

/** Tema — açık/koyu kontrast ikonu. */
export function ThemeIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 3.5a8.5 8.5 0 0 0 0 17z" fill="currentColor" stroke="none" />
    </Base>
  );
}

/** Bottom nav "Predictions" — görseldeki çerçeveli sütun grafik ikonu. */
export function BarChartIcon(p: IconProps) {
  return (
    <Base {...p}>
      <rect x="3.5" y="3.5" width="17" height="17" rx="3.5" />
      <path d="M8 16v-3.5M12 16V8.5M16 16v-5.5" />
    </Base>
  );
}

/** Yardım ve Destek — trailing "launch" (harici tarayıcı). */
export function LaunchIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M14 5h5v5" />
      <path d="M19 5l-7.5 7.5" />
      <path d="M18 14.5V18a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h3.5" />
    </Base>
  );
}

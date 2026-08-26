"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

// ── İkonlar (inline SVG — proje standardı; Lucide yok) ───────────────────────
function CompassIcon({ active }: { active: boolean }) {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={active ? 2.2 : 1.8} strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <polygon points="16 8 13 13 8 16 11 11" fill="currentColor" stroke="none" />
    </svg>
  );
}
function BallIcon({ active }: { active: boolean }) {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={active ? 2.2 : 1.8} strokeLinejoin="round">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7.5l3.2 2.3-1.2 3.7h-4l-1.2-3.7z" />
      <path d="M12 7.5V4.5M14 13.5l2.4 1.8M10 13.5l-2.4 1.8" />
    </svg>
  );
}
function ClipboardIcon({ active }: { active: boolean }) {
  // Tahminlerim — pano / kontrol listesi
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={active ? 2.2 : 1.8} strokeLinecap="round" strokeLinejoin="round">
      <rect x="6" y="4" width="12" height="17" rx="2" />
      <path d="M9 4a1.5 1.5 0 0 1 1.5-1.5h3A1.5 1.5 0 0 1 15 4v.5a1 1 0 0 1-1 1h-4a1 1 0 0 1-1-1z" fill={active ? "currentColor" : "none"} />
      <path d="M9.5 11l1.6 1.6 3.4-3.6M9.5 16.5h5" />
    </svg>
  );
}
function HeartIcon({ active }: { active: boolean }) {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill={active ? "currentColor" : "none"} stroke="currentColor" strokeWidth={active ? 0 : 1.8} strokeLinejoin="round">
      <path d="M12 20s-7-4.3-7-9.2A4.2 4.2 0 0 1 12 8a4.2 4.2 0 0 1 7 2.8C19 15.7 12 20 12 20z" />
    </svg>
  );
}
function UserIcon({ active }: { active: boolean }) {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill={active ? "currentColor" : "none"} stroke="currentColor" strokeWidth={active ? 0 : 1.8} strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1" />
    </svg>
  );
}

// "/profile" — Profil Merkezi (SCREEN_15) Stitch görselinde 4 sekmeli KENDİ
// navigasyonunu taşır (components/profile/ProfileBottomNav.tsx); global 5 sekmeli
// nav orada gizlenir, aksi halde iki nav üst üste biner.
// "/account" — Hesabım (SCREEN_12) bir ALT ekrandır; Stitch'te alt navigasyon
// yoktur, geri butonuyla Profil'e dönülür.
const HIDE_ON = [
  "/auth/login",
  "/auth/register",
  "/onboarding",
  "/match/",
  "/profile",
  "/account",
];

const TABS = [
  { href: "/", label: "Keşfet", icon: CompassIcon },
  { href: "/maclar", label: "Maçlar", icon: BallIcon },
  { href: "/predictions", label: "Tahminlerim", icon: ClipboardIcon },
  { href: "/following", label: "Takip", icon: HeartIcon },
  // Profil sekmesi, Profil modülünün alt ekranlarında da AKTİF kalır
  // (Bildirim Tercihleri SCREEN_04 ve altındaki Sessiz Saatler SCREEN_07).
  // /profile ve /account zaten HIDE_ON'da (nav gizli); bu prefiksler nav'ın
  // GÖRÜNDÜĞÜ alt ekranları kapsar.
  {
    href: "/profile",
    label: "Profil",
    icon: UserIcon,
    activeOn: ["/notifications/settings"] as const,
  },
];

export function BottomNav() {
  const pathname = usePathname();
  if (HIDE_ON.some((p) => pathname.startsWith(p))) return null;

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 border-t border-white/10 bg-bg-deep/95 backdrop-blur-md">
      <div className="mx-auto flex max-w-[var(--app-max-width)] px-2 pb-[max(var(--safe-bottom),22px)] pt-2.5">
        {TABS.map((tab) => {
          const active =
            tab.href === "/"
              ? pathname === "/"
              : pathname.startsWith(tab.href) ||
                (tab.activeOn?.some((p) => pathname.startsWith(p)) ?? false);
          const Icon = tab.icon;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              className={`flex flex-1 flex-col items-center gap-1.5 py-1 transition-colors ${
                active ? "text-neon" : "text-text-muted hover:text-text-secondary"
              }`}
            >
              <span className={active ? "fx-icon-glow-green" : undefined}>
                <Icon active={active} />
              </span>
              <span className="text-[10px] font-semibold leading-none tracking-wide">{tab.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

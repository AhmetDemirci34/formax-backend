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
function RadioIcon({ active }: { active: boolean }) {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={active ? 2.2 : 1.8} strokeLinecap="round">
      <circle cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
      <path d="M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7" />
      <path d="M6 6a9 9 0 0 0 0 12M18 6a9 9 0 0 1 0 12" />
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

const HIDE_ON = ["/auth/login", "/auth/register", "/onboarding", "/match/"];

const TABS = [
  { href: "/", label: "Keşfet", icon: CompassIcon },
  { href: "/maclar", label: "Maçlar", icon: BallIcon },
  { href: "/radar", label: "Radar", icon: RadioIcon },
  { href: "/following", label: "Takip", icon: HeartIcon },
  { href: "/profile", label: "Profil", icon: UserIcon },
];

export function BottomNav() {
  const pathname = usePathname();
  if (HIDE_ON.some((p) => pathname.startsWith(p))) return null;

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 border-t border-white/10 bg-bg-deep/95 backdrop-blur-md">
      <div className="mx-auto flex max-w-[var(--app-max-width)] px-2 pb-[max(var(--safe-bottom),22px)] pt-2.5">
        {TABS.map((tab) => {
          const active = tab.href === "/" ? pathname === "/" : pathname.startsWith(tab.href);
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

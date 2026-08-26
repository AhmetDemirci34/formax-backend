"use client";

import Link from "next/link";
import {
  CompassIcon,
  BallIcon,
  ClipboardIcon,
  HeartIcon,
  UserIcon,
} from "./icons";

/**
 * Maç Detay Merkezi · Bottom Navigation (Fixed, z-50 — Teknik Doküman §11).
 * Bu ekran bir ana sekme DEĞİLDİR → hiçbir sekme aktif görünmez; tüm ikon ve
 * yazılar sönük (Keşfet ekranındaki pasif nav ile aynı). Global BottomNav
 * `/match/` üzerinde gizli olduğundan bu ekrana özel işlevsel nav render edilir.
 * Rotalar uygulamanın gerçek sekmeleriyle uyumludur; safe-area padding zorunlu.
 */
const TABS = [
  { href: "/", label: "Keşfet", Icon: CompassIcon },
  { href: "/maclar", label: "Maçlar", Icon: BallIcon },
  { href: "/predictions", label: "Tahminlerim", Icon: ClipboardIcon },
  { href: "/following", label: "Takip", Icon: HeartIcon },
  { href: "/profile", label: "Profil", Icon: UserIcon },
];

export function MatchCenterBottomNav() {
  return (
    <nav className="z-50 shrink-0 border-t border-white/10 bg-goalai-surface/95 backdrop-blur-md">
      <div className="mx-auto flex max-w-[var(--app-max-width)] items-start px-2 pb-[max(var(--safe-bottom),12px)] pt-2.5">
        {TABS.map(({ href, label, Icon }) => (
          <Link
            key={href}
            href={href}
            className="flex flex-1 flex-col items-center gap-1.5 py-1 text-white/40 transition-opacity hover:opacity-80"
          >
            <Icon size={24} />
            <span className="text-[10px] font-semibold leading-none tracking-wide">
              {label}
            </span>
          </Link>
        ))}
      </div>
    </nav>
  );
}

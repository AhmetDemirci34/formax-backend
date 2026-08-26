"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { HomeIcon, TrophyIcon, UserIcon } from "@/components/discover/icons";
import { BarChartIcon } from "./icons";
import { haptic } from "@/lib/utils/haptics";

interface Tab {
  label: string;
  href: string | null;
  icon: (p: { size?: number; className?: string }) => React.ReactElement;
}

/**
 * Sekmeler — Stitch görselinden BİREBİR: 4 sekme, İngilizce etiketler,
 * aktif sekme "Profile" ve lime (#CCFF00).
 *
 * TODO(route): "Leagues" sekmesinin rotası projede YOK — uydurulmadı, `href`
 * null bırakıldı (satır render edilir, dokunma no-op'tur). Rota eklendiğinde
 * yalnızca buradaki `href` doldurulacak.
 */
const TABS: Tab[] = [
  { label: "Home", href: "/", icon: HomeIcon },
  { label: "Predictions", href: "/predictions", icon: BarChartIcon },
  { label: "Leagues", href: null, icon: TrophyIcon },
  { label: "Profile", href: "/profile", icon: UserIcon },
];

/**
 * ProfileBottomNav — Profil Merkezi'nin sabit alt navigasyonu.
 *
 * GEOMETRİ KAYNAĞI: Stitch görseli. Görselde 4 sekme vardır; projenin global
 * 5 sekmeli `BottomNav`'ı bu ekranda gizlenir (bkz. components/ui/BottomNav.tsx
 * HIDE_ON) — böylece iki nav üst üste binmez.
 *
 * Yükseklik 83px (safe area dahil) · ikon 24px · etiket 11px Medium ·
 * aktif #CCFF00, pasif #808080 · basışta ikon %95 küçülür.
 */
export function ProfileBottomNav() {
  const pathname = usePathname();

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 bg-profile-surface">
      <div className="mx-auto flex max-w-[var(--app-max-width)] px-2 pb-[max(var(--safe-bottom),16px)] pt-2.5">
        {TABS.map((tab) => {
          const active = tab.href ? pathname === tab.href : false;
          const Icon = tab.icon;
          const content = (
            <>
              <Icon size={24} />
              <span className="text-[11px] font-medium leading-none">{tab.label}</span>
            </>
          );
          const className = `flex flex-1 flex-col items-center gap-1.5 py-1 transition-transform duration-150 active:scale-95 ${
            active ? "text-profile-accent" : "text-profile-muted"
          }`;

          return tab.href ? (
            <Link key={tab.label} href={tab.href} onClick={() => haptic()} className={className}>
              {content}
            </Link>
          ) : (
            <button key={tab.label} type="button" onClick={() => haptic()} className={className}>
              {content}
            </button>
          );
        })}
      </div>
    </nav>
  );
}

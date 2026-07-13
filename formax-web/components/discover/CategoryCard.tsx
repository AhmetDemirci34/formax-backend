import type { ReactNode } from "react";

interface CategoryCardProps {
  icon: ReactNode;
  title: string;
  active: boolean;
  onClick: () => void;
}

/**
 * FORMAX · CategoryCard (02, Home)
 * AICategorySelector kartı — ikon + başlık. Aktif = neon glow, pasif = glass.
 * Hero tasarım dili (glass/glow/border/token) aynen; yeni dil yok.
 */
export function CategoryCard({ icon, title, active, onClick }: CategoryCardProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`flex min-w-0 flex-1 flex-col items-center gap-1.5 rounded-2xl px-1 py-2.5 transition-all active:scale-95 ${
        active
          ? "fx-glow-soft-green bg-neon/[0.10] ring-1 ring-neon/40"
          : "ring-1 ring-transparent hover:bg-white/[0.03]"
      }`}
    >
      <span className={active ? "text-neon" : "text-text-secondary"}>{icon}</span>
      <span
        className={`text-center text-[8.5px] font-semibold uppercase leading-[1.15] tracking-tight ${
          active ? "text-neon" : "text-text-muted"
        }`}
      >
        {title}
      </span>
    </button>
  );
}

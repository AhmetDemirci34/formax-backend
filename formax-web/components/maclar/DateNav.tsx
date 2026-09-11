"use client";

// FORMAX · Kompakt tarih navigasyonu — "‹ 30 Ağustos ›  Bugün".
// Eski yatay gün sekmeleri + lig chip karmaşasının yerine geçer.

interface Props {
  day: Date;
  onChange: (next: Date) => void;
}

function label(day: Date): string {
  const today = new Date();
  const yesterday = new Date(today);
  yesterday.setDate(today.getDate() - 1);
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);

  const dm = day.toLocaleDateString("tr-TR", { day: "numeric", month: "long" });
  if (day.toDateString() === today.toDateString()) return `Bugün · ${dm}`;
  if (day.toDateString() === tomorrow.toDateString()) return `Yarın · ${dm}`;
  if (day.toDateString() === yesterday.toDateString()) return `Dün · ${dm}`;
  const wd = day.toLocaleDateString("tr-TR", { weekday: "short" });
  return `${wd} · ${dm}`;
}

export function DateNav({ day, onChange }: Props) {
  const isToday = day.toDateString() === new Date().toDateString();

  const shift = (delta: number) => {
    const next = new Date(day);
    next.setDate(day.getDate() + delta);
    onChange(next);
  };

  return (
    <div className="flex items-center gap-2 px-[18px] pb-2.5">
      <div className="flex flex-1 items-center justify-between rounded-[12px] bg-bg-glass px-1 py-1">
        <NavArrow dir="prev" onClick={() => shift(-1)} />
        <span className="select-none truncate px-2 text-[13px] font-semibold text-text-primary">
          {label(day)}
        </span>
        <NavArrow dir="next" onClick={() => shift(1)} />
      </div>

      <button
        type="button"
        onClick={() => onChange(new Date())}
        disabled={isToday}
        className={`shrink-0 rounded-[12px] px-3 py-[9px] text-[12px] font-semibold transition-colors ${
          isToday
            ? "bg-bg-glass text-text-muted"
            : "bg-neon/10 text-neon hover:bg-neon/[0.18]"
        }`}
      >
        Bugün
      </button>
    </div>
  );
}

function NavArrow({ dir, onClick }: { dir: "prev" | "next"; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={dir === "prev" ? "Önceki gün" : "Sonraki gün"}
      className="flex h-8 w-8 items-center justify-center rounded-[10px] text-text-secondary transition-colors hover:bg-white/5 hover:text-text-primary"
    >
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path
          d={dir === "prev" ? "M15 6l-6 6 6 6" : "M9 6l6 6-6 6"}
          stroke="currentColor"
          strokeWidth="2.2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    </button>
  );
}

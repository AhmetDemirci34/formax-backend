"use client";

// FORMAX · SONUÇLAR tarih navigasyonu — "‹ Dün · 2 Eylül ›  Son sonuçlar".
//
// YAKLAŞAN'ın DateNav'ından AYRI bir bileşendir çünkü kuralları terstir: burada
// GELECEĞE gidilemez ve yalnız 7 gün geriye inilebilir. İki ekranı tek bileşende
// birleştirmek, o ekranın ileri gitme davranışını bu ekrana sızdırırdı.

import {
  dayLabel,
  isSelectableDay,
  istanbulDay,
  oldestSelectableDay,
  shiftDay,
} from "@/lib/matches/resultDays";

interface Props {
  day: string;
  onChange: (next: string) => void;
  /** Pencere içindeki en yakın sonuçlu gün; yoksa null. */
  nearestResultDay: string | null;
}

export function ResultDateNav({ day, onChange, nearestResultDay }: Props) {
  const today = istanbulDay();
  const prev = shiftDay(day, -1);
  const next = shiftDay(day, 1);

  const canGoPrev = isSelectableDay(prev, today);
  const canGoNext = isSelectableDay(next, today);

  // Yardımcı buton: bugünde değilsek "Bugün"; bugündeysek ve sonuçlar başka bir
  // gündeyse "Son sonuçlar". İkisi de anlamsızsa buton pasif kalır.
  const helper =
    day !== today
      ? { label: "Bugün", target: today }
      : nearestResultDay && nearestResultDay !== today
        ? { label: "Son sonuçlar", target: nearestResultDay }
        : null;

  return (
    <div className="flex items-center gap-2 px-[18px] pb-2.5">
      <div className="flex min-w-0 flex-1 items-center justify-between rounded-[12px] bg-bg-glass px-1 py-1">
        <NavArrow dir="prev" disabled={!canGoPrev} onClick={() => onChange(prev)} />
        <span className="min-w-0 select-none truncate px-2 text-[13px] font-semibold text-text-primary">
          {dayLabel(day, today)}
        </span>
        <NavArrow dir="next" disabled={!canGoNext} onClick={() => onChange(next)} />
      </div>

      <button
        type="button"
        onClick={() => helper && onChange(helper.target)}
        disabled={!helper}
        className={`shrink-0 whitespace-nowrap rounded-[12px] px-3 py-[9px] text-[12px] font-semibold transition-colors ${
          helper
            ? "bg-neon/10 text-neon hover:bg-neon/[0.18]"
            : "bg-bg-glass text-text-muted"
        }`}
      >
        {helper?.label ?? "Bugün"}
      </button>
    </div>
  );
}

/**
 * Sınıra gelindiğinde ok PASİF olur — basılabilir görünüp hiçbir şey yapmaz değil.
 * En eski gün: {oldestSelectableDay()}; en yeni gün: bugün.
 */
function NavArrow({
  dir,
  disabled,
  onClick,
}: {
  dir: "prev" | "next";
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={dir === "prev" ? "Önceki gün" : "Sonraki gün"}
      className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-[10px] transition-colors ${
        disabled
          ? "text-white/15"
          : "text-text-secondary hover:bg-white/5 hover:text-text-primary"
      }`}
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

export { oldestSelectableDay };

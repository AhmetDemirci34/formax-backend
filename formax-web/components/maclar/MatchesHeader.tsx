"use client";

// FORMAX · Maçlar Header (Component 01)
// Başlık + "AI'ya göre sıralı" bilgisi · Search · Ligler/Takımlar chip · Gün sekmeleri.
// Chip'ler şimdilik yalnızca UI (bottom sheet yok). Gün sekmeleri + arama kontrollü.

import { SearchIcon, TrophyIcon, ShirtIcon, SparklesIcon } from "./icons";
import type { DayMeta } from "./matchData";

interface Props {
  query: string;
  onQueryChange: (v: string) => void;
  tabs: DayMeta[];
  activeDay: string;
  onDayChange: (key: string) => void;
}

export function MatchesHeader({ query, onQueryChange, tabs, activeDay, onDayChange }: Props) {
  return (
    <div className="border-b border-white/[0.06] px-[18px] pb-3">
      {/* Başlık + sade sıralama bilgisi (aksiyon değil) */}
      <div className="flex items-baseline justify-between pt-3 pb-3">
        <h1 className="text-[22px] font-semibold tracking-[-0.3px] text-text-primary">Maçlar</h1>
        <span className="flex items-center gap-1.5 text-[12px] text-text-muted">
          <SparklesIcon size={13} className="text-neon/70" />
          AI&apos;ya göre sıralı
        </span>
      </div>

      {/* Search bar */}
      <div className="flex h-[46px] items-center gap-2.5 rounded-2xl border border-[#23272E] bg-bg-glass px-3.5">
        <SearchIcon size={18} className="text-text-muted" />
        <input
          value={query}
          onChange={(e) => onQueryChange(e.target.value)}
          placeholder="Maç, takım veya lig ara..."
          className="min-w-0 flex-1 bg-transparent text-[15px] text-text-primary placeholder:text-text-muted focus:outline-none"
        />
      </div>

      {/* Filter chips (şimdilik yalnızca UI) */}
      <div className="mt-3 grid grid-cols-2 gap-2.5">
        <button className="flex h-10 items-center justify-center gap-2 rounded-[14px] border border-[#23272E] bg-bg-card/60 text-[14px] font-medium text-text-primary/90">
          <TrophyIcon size={17} className="text-text-secondary" />
          Ligler
        </button>
        <button className="flex h-10 items-center justify-center gap-2 rounded-[14px] border border-[#23272E] bg-bg-card/60 text-[14px] font-medium text-text-primary/90">
          <ShirtIcon size={17} className="text-text-secondary" />
          Takımlar
        </button>
      </div>

      {/* Gün sekmeleri (yatay scroll) */}
      <div className="mt-3 flex gap-2 overflow-x-auto pb-0.5 [&::-webkit-scrollbar]:hidden [scrollbar-width:none]">
        {tabs.map((t) => {
          const active = t.key === activeDay;
          return (
            <button
              key={t.key}
              onClick={() => onDayChange(t.key)}
              className={`shrink-0 rounded-[14px] border px-3.5 py-[7px] text-center transition-colors ${
                active
                  ? "border-neon/60 bg-neon/[0.09]"
                  : "border-[#23272E] bg-bg-card/60"
              }`}
            >
              <div className={`text-[13px] font-medium ${active ? "text-neon" : "text-text-primary/85"}`}>{t.label}</div>
              <div className={`mt-0.5 text-[11px] ${active ? "text-neon/70" : "text-text-muted"}`}>{t.sub}</div>
            </button>
          );
        })}
      </div>
    </div>
  );
}

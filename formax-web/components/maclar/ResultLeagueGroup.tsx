"use client";

// FORMAX · Sonuç lig paneli — günün BİTMİŞ maçları lige göre gruplanır.
//
// YAKLAŞAN sekmesindeki LeagueGroupPanel ile AYNI görsel dile sahiptir (aynı kabuk,
// aynı başlık düzeni, aynı sayaç): iki sekme tek tasarım sistemine ait görünmelidir.
// Fark yalnız satırın kendisidir — burada skor ve maç özeti vardır.

import { ResultCard } from "./ResultCard";
import type { ResultLeagueGroup as ResultLeagueGroupModel } from "@/lib/matches/resultGrouping";

interface Props {
  group: ResultLeagueGroupModel;
  onOpen: (matchId: number) => void;
}

export function ResultLeagueGroupPanel({ group, onOpen }: Props) {
  return (
    <section className="w-full max-w-full overflow-hidden rounded-[14px] bg-bg-glass">
      {/* Lig başlığı: 🇹🇷 Türkiye · Süper Lig — sağda o ligdeki maç sayısı */}
      <header className="flex items-center gap-2 border-b border-white/[0.06] px-3 py-2">
        {group.flag ? (
          <span className="shrink-0 text-[13px] leading-none" aria-hidden="true">
            {group.flag}
          </span>
        ) : null}
        <h2 className="min-w-0 flex-1 truncate text-[12px] font-semibold text-text-secondary">
          {group.country ? (
            <>
              {group.country}
              <span className="mx-1 text-text-muted">·</span>
            </>
          ) : null}
          <span className="text-text-primary">{group.league}</span>
        </h2>
        <span className="shrink-0 text-[11px] tabular-nums text-text-muted">
          {group.results.length}
        </span>
      </header>

      <div className="divide-y divide-white/[0.04]">
        {group.results.map((r) => (
          <ResultCard key={r.matchId} result={r} onOpen={onOpen} />
        ))}
      </div>
    </section>
  );
}

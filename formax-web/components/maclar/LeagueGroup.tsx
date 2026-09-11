"use client";

// FORMAX · Lig paneli — günün BAŞLAMAMIŞ maçları lige göre gruplanır.
// Her lig TEK bir görsel grup; maçlar bu panelin içinde kompakt satırlardır.
// Canlı sayacı/rozeti KALDIRILDI (ürün kararı: FORMAX canlı maç göstermez).

import { MatchRow } from "./MatchRow";
import type { LeagueGroup as LeagueGroupModel } from "@/lib/matches/leagueGrouping";
import type { MatchListItemDto } from "@/lib/api/matchList";

interface Props {
  group: LeagueGroupModel;
  onOpen: (match: MatchListItemDto) => void;
}

export function LeagueGroupPanel({ group, onOpen }: Props) {
  return (
    <section className="overflow-hidden rounded-[14px] bg-bg-glass">
      {/* Lig başlığı: 🇹🇷 Türkiye · Süper Lig */}
      <header className="flex items-center gap-2 border-b border-white/[0.06] px-3 py-2">
        {group.flag ? (
          <span className="text-[13px] leading-none" aria-hidden="true">
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
          {group.matches.length}
        </span>
      </header>

      <div className="divide-y divide-white/[0.04]">
        {group.matches.map((m) => (
          <MatchRow key={m.matchId} match={m} onOpen={onOpen} />
        ))}
      </div>
    </section>
  );
}

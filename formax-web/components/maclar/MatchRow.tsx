"use client";

// FORMAX · Kompakt maç satırı (lig paneli içinde) — YALNIZ MAÇ ÖNCESİ.
//
// YERLEŞİM: [Takip ikonu] [Başlama saati] [Logo + Ev] [Logo + Deplasman] [AI İncele]
//
// KİLİTLİ ÜRÜN KARARI: canlı satır yoktur. Skor, dakika, CANLI rozeti, yeşil canlı zemin
// ve İY/2Y/MS alanları KALDIRILDI. Başlamamış maçta sahte 0-0 gösterilmez — hiçbir skor
// alanı render edilmez.
//
// ÜÇ AYRI TIKLAMA — birbirine karışmaz:
//   • Takip ikonu → yalnız takip durumunu değiştirir (olayı yutar, detayı AÇMAZ)
//   • Satırın gövdesi → Maç Detay ekranını açar
//   • AI İncele → Maç Detay ekranını açar

import { TeamCrest } from "@/components/ui/TeamCrest";
import { FollowStar } from "./FollowStar";
import type { MatchListItemDto } from "@/lib/api/matchList";

interface Props {
  match: MatchListItemDto;
  onOpen: (match: MatchListItemDto) => void;
}

function kickoffTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "--:--";
  return d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
}

export function MatchRow({ match, onOpen }: Props) {
  return (
    <div className="group flex w-full items-center gap-1.5 pl-1 pr-2.5 transition-colors hover:bg-white/[0.035]">
      {/* 1 · TAKİP — en solda, 40×40 dokunma alanı, detayı açmaz */}
      <FollowStar matchId={match.matchId} />

      {/* 2+3 · Satır gövdesi → Maç Detay */}
      <button
        type="button"
        onClick={() => onOpen(match)}
        aria-label={`${match.homeTeam} - ${match.awayTeam} maç detayı`}
        className="flex min-w-0 flex-1 items-center gap-2.5 py-[9px] text-left"
      >
        <div className="w-[42px] shrink-0 text-center">
          <span className="text-[13px] font-semibold tabular-nums text-text-secondary">
            {kickoffTime(match.startTime)}
          </span>
        </div>

        <div className="min-w-0 flex-1">
          <TeamLine name={match.homeTeam} logoUrl={match.homeTeamLogoUrl} />
          <div className="h-[3px]" />
          <TeamLine name={match.awayTeam} logoUrl={match.awayTeamLogoUrl} />
        </div>
      </button>

      {/* 4 · AI İncele → Maç Detay */}
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onOpen(match);
        }}
        className="shrink-0 rounded-[8px] border border-neon/25 bg-neon/[0.07] px-2 py-[4px] text-[10.5px] font-bold tracking-wide text-neon transition-colors hover:bg-neon/[0.14]"
      >
        AI İncele
      </button>
    </div>
  );
}

function TeamLine({ name, logoUrl }: { name: string; logoUrl?: string | null }) {
  return (
    <div className="flex items-center gap-2">
      <TeamCrest name={name} logoUrl={logoUrl} size={18} />
      <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium text-text-primary">
        {name}
      </span>
    </div>
  );
}

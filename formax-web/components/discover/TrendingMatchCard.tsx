"use client";

import Link from "next/link";
import { motion } from "framer-motion";
import type { MutableRefObject } from "react";
import type { RecommendationCardDto } from "@/types/api";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { awayName, homeName, leagueLabel, matchTime, radarLevel, shortTag } from "./cardSignals";

interface Props {
  card: RecommendationCardDto;
  dragGuard?: MutableRefObject<boolean>;
}

// Gündemdeki maçlar şeridindeki küçük kart — logo · isim · lig · saat · radar · kısa AI etiketi.
// Küçük ve sönük (Hero ile yarışmaz). Sürükleme sonrası tıklamayı dragGuard engeller.
export function TrendingMatchCard({ card, dragGuard }: Props) {
  const league = leagueLabel(card);
  const time = matchTime(card);
  const meta = [league, time?.text].filter(Boolean).join(" • ");

  return (
    <Link
      href={`/match/${card.matchId}`}
      draggable={false}
      onClick={(e) => {
        if (dragGuard?.current) e.preventDefault(); // sürüklemeyse navigasyon yok
      }}
      className="block w-[150px] shrink-0 snap-start"
    >
      <motion.div
        whileTap={{ scale: 0.97 }}
        className="rounded-2xl border border-white/[0.06] bg-white/[0.02] p-3"
      >
        <div className="flex items-center justify-center gap-1.5">
          <TeamCrest name={homeName(card)} logoUrl={card.homeTeam?.logoUrl} size={28} />
          <span className="text-[9px] font-medium text-text-muted/70">VS</span>
          <TeamCrest name={awayName(card)} logoUrl={card.awayTeam?.logoUrl} size={28} />
        </div>

        <p className="mt-2 line-clamp-1 text-center text-[11px] font-semibold text-white">
          {homeName(card)} – {awayName(card)}
        </p>

        {meta && (
          <p className="mt-1 line-clamp-1 text-center text-[9px] font-medium uppercase tracking-wide text-text-muted">
            {time?.live ? <span className="text-formax-red">Canlı</span> : meta}
            {time?.live && league ? ` · ${league}` : ""}
          </p>
        )}

        <div className="mt-2 flex items-center justify-center gap-1.5 border-t border-white/[0.05] pt-2 text-[9px] font-semibold uppercase tracking-wide">
          <span className="text-accent">Radar {radarLevel(card)}</span>
          <span className="text-text-muted/40">·</span>
          <span className="text-text-muted">{shortTag(card)}</span>
        </div>
      </motion.div>
    </Link>
  );
}

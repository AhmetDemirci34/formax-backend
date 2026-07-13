"use client";

import { useEffect, useRef } from "react";
import Link from "next/link";
import { motion } from "framer-motion";
import type { MutableRefObject } from "react";
import type { RecommendationCardDto } from "@/types/api";
import { useRecommendations } from "@/hooks/useRecommendations";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { ChevronRightIcon } from "./icons";
import { awayName, homeName, leagueLabel, matchTime, radarLevel, shortTag } from "./cardSignals";

const RADAR_COLOR: Record<string, string> = {
  YÜKSEK: "text-formax-amber",
  ORTA: "text-[#A855F7]",
  DÜŞÜK: "text-[#3B82F6]",
};

// Mouse (web) için pointer drag-to-scroll; dokunmada native snap/momentum.
function useDragScroll() {
  const ref = useRef<HTMLDivElement>(null);
  const draggedRef = useRef(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    let down = false, startX = 0, startLeft = 0;
    const onDown = (e: PointerEvent) => {
      if (e.pointerType !== "mouse") return;
      down = true; draggedRef.current = false; startX = e.pageX; startLeft = el.scrollLeft;
    };
    const onMove = (e: PointerEvent) => {
      if (!down) return;
      const dx = e.pageX - startX;
      if (Math.abs(dx) > 4) draggedRef.current = true;
      el.scrollLeft = startLeft - dx;
    };
    const onUp = () => { down = false; setTimeout(() => { draggedRef.current = false; }, 60); };
    el.addEventListener("pointerdown", onDown);
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    return () => {
      el.removeEventListener("pointerdown", onDown);
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
    };
  }, []);
  return { ref, draggedRef };
}

// "Öne çıkan maçlar" — Netflix/Spotify mantığında premium yatay carousel.
export function OtherMatches({ excludeMatchId }: { excludeMatchId?: number }) {
  const { data } = useRecommendations();
  const { ref, draggedRef } = useDragScroll();

  const cards = (data?.pages.flat() ?? [])
    .filter((c) => c.matchId !== excludeMatchId)
    .slice(0, 12);

  if (cards.length === 0) return null;

  return (
    <section>
      <div className="mb-2.5 flex items-center justify-between px-0.5">
        <h2 className="text-[11px] font-bold uppercase tracking-[0.15em] text-text-secondary">
          Öne çıkan maçlar
        </h2>
        <Link href="/tumu" className="inline-flex items-center gap-0.5 text-[11px] font-medium text-text-muted transition-colors hover:text-[#A855F7]">
          Tümünü Gör
          <ChevronRightIcon size={13} />
        </Link>
      </div>

      <div
        ref={ref}
        className="-mx-5 flex cursor-grab snap-x snap-mandatory select-none gap-3 overflow-x-auto px-5 pb-1 [&::-webkit-scrollbar]:hidden [&_img]:pointer-events-none [scrollbar-width:none]"
      >
        {cards.map((c) => (
          <Card key={c.matchId} card={c} dragGuard={draggedRef} />
        ))}
      </div>
    </section>
  );
}

function Card({ card, dragGuard }: { card: RecommendationCardDto; dragGuard: MutableRefObject<boolean> }) {
  const league = leagueLabel(card);
  const time = matchTime(card);
  const level = radarLevel(card);
  const meta = [league, time?.live ? "Canlı" : time?.text].filter(Boolean).join(" · ");

  return (
    <Link
      href={`/match/${card.matchId}`}
      draggable={false}
      onClick={(e) => { if (dragGuard.current) e.preventDefault(); }}
      className="block w-[210px] shrink-0 snap-start"
    >
      <motion.div
        whileTap={{ scale: 0.97 }}
        whileHover={{ y: -3 }}
        className="rounded-[20px] border border-white/[0.08] bg-white/[0.03] p-4 shadow-[0_12px_36px_-18px_rgba(0,0,0,0.7)]"
      >
        <div className="flex items-center justify-between">
          <span className={`text-[10px] font-bold uppercase tracking-wide ${RADAR_COLOR[level] ?? "text-[#3B82F6]"}`}>
            Radar {level}
          </span>
          <span className="rounded-full bg-[#A855F7]/12 px-2 py-0.5 text-[9px] font-semibold uppercase tracking-wide text-[#A855F7]">
            {shortTag(card)}
          </span>
        </div>

        <div className="mt-4 flex items-center justify-center gap-3">
          <TeamCrest name={homeName(card)} logoUrl={card.homeTeam?.logoUrl} size={50} />
          <span className="text-xs font-bold text-text-muted/70">VS</span>
          <TeamCrest name={awayName(card)} logoUrl={card.awayTeam?.logoUrl} size={50} />
        </div>

        <p className="mt-3.5 line-clamp-1 text-center text-[13px] font-bold text-white">
          {homeName(card)} – {awayName(card)}
        </p>
        {meta && (
          <p className="mt-1 line-clamp-1 text-center text-[10px] font-medium uppercase tracking-wide text-text-muted">
            {meta}
          </p>
        )}
      </motion.div>
    </Link>
  );
}

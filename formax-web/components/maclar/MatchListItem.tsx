"use client";

// FORMAX · Maç satırı (Match List item — LOCKED tasarım).
// Veri: GERÇEK backend (GET /api/matches → MatchListItemDto). Mock YOK.
// Görsel dil korunur; yalnız durum vurgusu eklendi:
//   • Canlı  → yeşil vurgu + zorunlu dakika (63')
//   • Bitti  → solumuş gri + "Bitti" etiketi
// Frontend AI/oran/olasılık ÜRETMEZ; backend göndermediği alan gösterilmez.

import { TeamCrest } from "@/components/ui/TeamCrest";
import { StarIcon, ChevronRightIcon } from "./icons";
import type { MatchListItemDto } from "@/lib/api/matchList";

interface Props {
  match: MatchListItemDto;
  onOpen: (match: MatchListItemDto) => void;
}

function kickoffLabel(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  const now = new Date();
  const sameDay = d.toDateString() === now.toDateString();
  const tomorrow = new Date(now);
  tomorrow.setDate(now.getDate() + 1);
  const isTomorrow = d.toDateString() === tomorrow.toDateString();
  const time = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
  if (sameDay) return time;
  if (isTomorrow) return `Yarın ${time}`;
  return `${d.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" })} ${time}`;
}

export function MatchListItem({ match, onOpen }: Props) {
  const isLive = match.status === "Live";
  const isFinished = match.status === "Finished";
  const score = match.score;

  return (
    <button
      type="button"
      onClick={() => onOpen(match)}
      className={`w-full rounded-[14px] px-3.5 py-2.5 text-left transition-colors ${
        isLive
          ? "border border-neon/30 bg-neon/[0.05] hover:bg-neon/[0.08]"
          : isFinished
            ? "bg-bg-glass/50 opacity-60 hover:bg-bg-hover/30"
            : "bg-bg-glass hover:bg-bg-hover/40"
      }`}
    >
      {/* Üst: lig + saat/durum · takip yıldızı */}
      <div className="mb-2 flex items-center justify-between">
        <div className="flex min-w-0 items-center gap-2 text-[12px] text-text-muted">
          <span className="truncate">{match.league || "—"}</span>
          <span className="shrink-0">·</span>
          {isLive ? (
            <span className="flex shrink-0 items-center gap-1 font-semibold text-neon">
              <span className="relative flex h-1.5 w-1.5">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-neon/70" />
                <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-neon" />
              </span>
              {match.minute != null ? `${match.minute}'` : "CANLI"}
            </span>
          ) : isFinished ? (
            <span className="shrink-0 font-medium text-text-muted">Bitti</span>
          ) : (
            <span className="shrink-0">{kickoffLabel(match.startTime)}</span>
          )}
        </div>
        <span style={{ color: "#5E6470" }}>
          <StarIcon size={18} filled={false} />
        </span>
      </div>

      {/* Orta: takımlar (sol) · skor (sağ) */}
      <div className="flex items-start justify-between gap-2.5">
        <div className="min-w-0 flex-1">
          <div className="mb-1.5 flex items-center gap-2">
            <TeamCrest name={match.homeTeam} size={21} />
            <span
              className={`truncate text-[17px] font-medium ${
                isFinished ? "text-text-secondary" : "text-text-primary"
              }`}
            >
              {match.homeTeam}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <TeamCrest name={match.awayTeam} size={21} />
            <span
              className={`truncate text-[17px] font-medium ${
                isFinished ? "text-text-secondary" : "text-text-primary"
              }`}
            >
              {match.awayTeam}
            </span>
          </div>
        </div>

        {/* Skor — yalnız backend verdiyse (canlı/biten). Uydurulmaz. */}
        {score ? (
          <div className="flex shrink-0 flex-col items-end justify-center gap-1.5">
            <span
              className={`text-[17px] font-semibold tabular-nums ${
                isLive ? "text-neon" : "text-text-secondary"
              }`}
            >
              {score.home}
            </span>
            <span
              className={`text-[17px] font-semibold tabular-nums ${
                isLive ? "text-neon" : "text-text-secondary"
              }`}
            >
              {score.away}
            </span>
          </div>
        ) : null}
      </div>

      {/* Alt: AI İncele */}
      <div className="mt-2.5 flex items-center justify-end border-t border-white/[0.05] pt-2">
        <span className="inline-flex items-center gap-0.5 rounded-full border border-neon/25 bg-neon/[0.04] px-2 py-[3px] text-[11px] font-medium text-neon/90">
          AI İncele
          <ChevronRightIcon size={12} />
        </span>
      </div>
    </button>
  );
}

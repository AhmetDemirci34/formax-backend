"use client";

// FORMAX · Maç satırı (Match List item — LOCKED tasarım).
// Hiyerarşi: Takımlar → AI Güveni → AI'ın Ana Tahmini → Oran → AI İncele.
// Kart değil, ince premium liste elemanı. Tüm satır tıklanabilir → AI Quick View.

import { TeamCrest } from "@/components/ui/TeamCrest";
import { StarIcon, ChevronRightIcon } from "./icons";
import { confidenceColor, kickoffLabel, type MaclarMatch } from "./matchData";

interface Props {
  match: MaclarMatch;
  onOpen: (match: MaclarMatch) => void;
}

export function MatchListItem({ match, onOpen }: Props) {
  const conf = confidenceColor(match.aiConfidence);
  const predConf = confidenceColor(match.mainPrediction.confidence);
  const odds = match.odds;

  return (
    <button
      type="button"
      onClick={() => onOpen(match)}
      className="w-full rounded-[14px] bg-bg-glass px-3.5 py-2.5 text-left transition-colors hover:bg-bg-hover/40"
    >
      {/* Üst: lig emblemi + saat · takip yıldızı */}
      <div className="mb-2 flex items-center justify-between">
        <div className="flex items-center gap-2 text-[12px] text-text-muted">
          <span
            className="flex h-[18px] w-[18px] items-center justify-center rounded-[5px] text-[8px] font-semibold text-white"
            style={{ background: match.league.color }}
          >
            {match.league.code}
          </span>
          <span>
            {match.league.name} · {kickoffLabel(match)}
          </span>
        </div>
        <span style={{ color: match.followed ? "#2EE66E" : "#5E6470" }}>
          <StarIcon size={18} filled={false} />
        </span>
      </div>

      {/* Orta: takımlar (sol, hero) · AI kümesi (sağ, isimlere yakın) */}
      <div className="flex items-start justify-between gap-2.5">
        <div className="min-w-0 flex-1">
          <div className="mb-1.5 flex items-center gap-2">
            <TeamCrest name={match.home} logoUrl={match.homeLogoUrl} size={21} />
            <span className="truncate text-[17px] font-medium text-text-primary">{match.home}</span>
          </div>
          <div className="flex items-center gap-2">
            <TeamCrest name={match.away} logoUrl={match.awayLogoUrl} size={21} />
            <span className="truncate text-[17px] font-medium text-text-primary">{match.away}</span>
          </div>
        </div>

        <div className="flex shrink-0 flex-col items-end gap-1.5">
          <span
            className="rounded-lg px-2.5 py-[3px] text-[12px] font-semibold"
            style={{ color: conf, background: "rgba(46,230,110,0.12)" }}
          >
            AI {match.aiConfidence}
          </span>
          {/* AI Tahmini — ölçeklenebilir blok (KG Var / Ev Sahibi / Alt / Üst … aynı yapı) */}
          <div className="text-right leading-[1.25]">
            <div className="text-[10px] tracking-wide text-text-muted">AI Tahmini</div>
            <div className="text-[13px] font-medium text-text-primary/95">{match.mainPrediction.label}</div>
            <div className="text-[11px]" style={{ color: predConf }}>AI %{match.mainPrediction.confidence}</div>
          </div>
        </div>
      </div>

      {/* Alt: oran önizleme (AI'ın önerdiği neon) · AI İncele pill */}
      <div className="mt-2.5 flex items-center justify-between border-t border-white/[0.05] pt-2">
        <div className="text-[12px] text-text-muted">
          <OddCell label="EV" value={odds.ev} on={odds.favored === "ev"} />
          <Dot />
          <OddCell label="BER" value={odds.draw} on={odds.favored === "draw"} />
          <Dot />
          <OddCell label="DEP" value={odds.dep} on={odds.favored === "dep"} />
        </div>
        <span className="inline-flex items-center gap-0.5 rounded-full border border-neon/25 bg-neon/[0.04] px-2 py-[3px] text-[11px] font-medium text-neon/90">
          AI İncele
          <ChevronRightIcon size={12} />
        </span>
      </div>
    </button>
  );
}

function OddCell({ label, value, on }: { label: string; value: number; on: boolean }) {
  return (
    <span style={on ? { color: "#2EE66E", fontWeight: 600 } : undefined}>
      {label} {value.toFixed(2)}
    </span>
  );
}

function Dot() {
  return <span className="mx-[7px] text-[#33383F]">·</span>;
}

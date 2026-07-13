"use client";

import { useEffect, useState } from "react";
import type { RecommendationCardDto, KeySignal } from "@/types/api";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { RadarBadge } from "@/components/ui/RadarBadge";
import { AIPanel } from "@/components/feed/AIPanel";

type Level = "YÜKSEK" | "ORTA" | "DÜŞÜK";

function levelFromScore(v: number): Level {
  if (v >= 66) return "YÜKSEK";
  if (v >= 40) return "ORTA";
  return "DÜŞÜK";
}

const TONE_COLOR: Record<string, string> = {
  purple: "#A855F7",
  orange: "#F5A623",
  green: "#34D27A",
  default: "#FFFFFF",
};
const SIGNAL_ICON: Record<string, string> = { fire: "🔥", ball: "⚽", home: "🏠" };

interface Props {
  card: RecommendationCardDto;
}

export function ActiveMatchCard({ card }: Props) {
  const home = card.homeTeam?.name || card.teamA || "—";
  const away = card.awayTeam?.name || card.teamB || "—";

  const hasRadar = !!card.radarLevel || card.radarScore > 0 || card.score > 0;
  const level: Level =
    (card.radarLevel as Level) ?? levelFromScore(card.radarScore > 0 ? card.radarScore : card.score);
  const radarReason = card.radarReason?.trim();

  const league = card.leagueName?.trim();
  const kickoff = card.kickoffTime ?? card.matchDate;

  const hasAI =
    !!(card.aiHeadline ?? card.storyHeadline)?.trim() ||
    !!(card.aiSummary ?? card.storyBody)?.trim();

  const signals = card.keySignals ?? [];

  const importance = Number(card.matchImportance ?? 0);

  const importanceLabel =
    importance >= 80
      ? "Çok Yüksek"
      : importance >= 60
       ? "Yüksek"
        : importance >= 40
        ? "Orta"
        : "Düşük";

  const showImportance = importance > 0;

  // WhyShown (recommendationReason) + Confidence — backend zaten üretiyor, burada yüzeye çıkar.
  const whyLabel = whyShownLabel(card.recommendationReason);
  const conf = confidenceMeta(card.confidenceLabel);

  return (
    <article className="relative overflow-hidden rounded-2xl border border-white/10 bg-[#0c1018]">
      <div
        aria-hidden
        className="absolute inset-x-0 top-0 h-40 pointer-events-none"
        style={{ background: "radial-gradient(80% 100% at 50% 0%, rgba(124,58,237,0.16) 0%, rgba(12,16,24,0) 70%)" }}
      />

      <div className="relative px-5 pt-4 pb-5">
        {/* ── ZONE 1 (üst) — MatchImportance | ZONE 2 (üst) — radar seviyesi ── */}
        <div className="flex items-start justify-between gap-2 min-h-[34px]">

          {showImportance ? <ImportanceChip label={importanceLabel} /> : <span />}

          {hasRadar && <RadarBadge level={level} variant="full" />}
        </div>

        {/* ── ZONE 1 (orta) — kimlik ── */}
        <div className="flex items-center justify-between gap-2 mt-5">
          <div className="flex-1 min-w-0 flex flex-col items-center gap-3">
            <TeamCrest name={home} logoUrl={card.homeTeam?.logoUrl} size={84} />
            <span className="text-white font-bold text-lg leading-tight text-center break-words w-full">{home}</span>
          </div>
          <span className="text-text-muted/60 font-semibold text-xl shrink-0 px-1">VS</span>
          <div className="flex-1 min-w-0 flex flex-col items-center gap-3">
            <TeamCrest name={away} logoUrl={card.awayTeam?.logoUrl} size={84} />
            <span className="text-white font-bold text-lg leading-tight text-center break-words w-full">{away}</span>
          </div>
        </div>

        {/* lig · saat (canlı countdown) — kimlik altı, ortalı */}
        <div className="flex justify-center mt-3">
          <MatchMeta league={league} kickoff={kickoff} />
        </div>

        {/* ── WhyShown + Confidence — neden gösterildi + güven seviyesi ── */}
        {(whyLabel || conf) && (
          <div className="flex items-center justify-center gap-2 mt-3 flex-wrap">
            {whyLabel && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/5 border border-white/10">
                <span className="text-[11px]">👁</span>
                <span className="text-[11px] font-semibold text-text-secondary">{whyLabel}</span>
              </span>
            )}
            {conf && (
              <span
                className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full border"
                style={{ borderColor: conf.color + "55", color: conf.color }}
              >
                <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: conf.color }} />
                <span className="text-[11px] font-semibold">{conf.text}</span>
              </span>
            )}
          </div>
        )}

        {/* ── ZONE 2 — radar açıklaması ── */}
        {radarReason && (
          <p className="text-[13px] text-text-secondary text-center leading-snug mt-4">{radarReason}</p>
        )}

        {/* ── ZONE 3 — AI commentary ── */}
        {hasAI && (
          <div className="mt-5 pt-4 border-t border-white/10">
            <AIPanel card={card} />
          </div>
        )}

        {/* ── ZONE 4 — top signals ── */}
        {signals.length > 0 && (
          <div className="mt-4 pt-4 border-t border-white/10 flex">
            {signals.slice(0, 3).map((s, i) => (
              <SignalCol key={i} signal={s} divider={i > 0} />
            ))}
          </div>
        )}
      </div>
    </article>
  );
}

// ── ZONE 1 üst — lig + canlı saat/countdown ──────────────────────────────────
function MatchMeta({ league, kickoff }: { league?: string; kickoff?: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  const timeNode = renderKickoff(kickoff, now);
  if (!league && !timeNode) return <span />;

  return (
    <div className="flex items-center gap-2 text-[13px] text-text-secondary pt-1.5">
      {league && <span className="font-medium">{league}</span>}
      {league && timeNode && <span className="text-white/25">•</span>}
      {timeNode}
    </div>
  );
}

function renderKickoff(iso: string | undefined, now: number): React.ReactNode {
  if (!iso) return null;
  const ts = new Date(iso).getTime();
  if (isNaN(ts)) return null;

  const diff = ts - now;

  if (diff <= 0) {
    return (
      <span className="inline-flex items-center gap-1.5 font-bold text-[#EF4444]">
        <span className="w-1.5 h-1.5 rounded-full bg-[#EF4444] animate-pulse" />
        CANLI
      </span>
    );
  }

  if (diff < 2 * 60 * 60 * 1000) {
    const s = Math.floor(diff / 1000);
    const hh = String(Math.floor(s / 3600)).padStart(2, "0");
    const mm = String(Math.floor((s % 3600) / 60)).padStart(2, "0");
    const ss = String(s % 60).padStart(2, "0");
    return <span className="font-semibold text-[#F5A623] tabular-nums">Başlamasına {hh}:{mm}:{ss}</span>;
  }

  return <span>{formatRelative(ts)}</span>;
}

function formatRelative(ts: number): string {
  const d = new Date(ts);
  const time = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
  const today = new Date();
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const sameDay = (a: Date, b: Date) =>
    a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  if (sameDay(d, today)) return `Bugün ${time}`;
  if (sameDay(d, tomorrow)) return `Yarın ${time}`;
  return `${d.toLocaleDateString("tr-TR", { day: "2-digit", month: "short" })} ${time}`;
}

// ── WhyShown — recommendationReason kodunu kullanıcı diline çevirir (backend kodu) ──
function whyShownLabel(code?: string): string | null {
  switch (code) {
    case "FOLLOWED_TEAM": return "Takip ettiğin takım";
    case "HIGH_INTEREST": return "Yüksek ilgi";
    case "TRENDING":      return "Yükselişte";
    case "MARKET_SIGNAL": return "Piyasa sinyali";
    case "GLOBAL_SIGNAL": return "Genel ilgi";
    default:              return null;
  }
}

// ── Confidence — confidenceLabel (HIGH/MEDIUM/LOW) → renk + Türkçe ──
function confidenceMeta(label?: string): { text: string; color: string } | null {
  switch (label) {
    case "HIGH":   return { text: "Yüksek güven", color: "#34D27A" };
    case "MEDIUM": return { text: "Orta güven",   color: "#F5A623" };
    case "LOW":    return { text: "Düşük güven",  color: "#9CA3AF" };
    default:       return null;
  }
}

// ── MatchImportance rozeti ───────────────────────────────────────────────────
function ImportanceChip({ label }: { label: string }) {
  
  const color = label === "Çok Yüksek" 
  ? "#F5A623" 
  : label === "Yüksek" 
  ? "#A855F7" 
  : "#9CA3AF";
  
  return (
    <span
      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full border"
      style={{ borderColor: color + "55", color }}
    >
      <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: color }} />
      <span className="text-[11px] font-bold tracking-wider uppercase">{label}</span>
    </span>
  );
}

// ── ZONE 4 — tek sinyal sütunu ───────────────────────────────────────────────
function SignalCol({ signal, divider }: { signal: KeySignal; divider: boolean }) {
  const color = TONE_COLOR[signal.tone ?? "default"] ?? TONE_COLOR.default;
  return (
    <div className={`flex-1 px-2 text-center ${divider ? "border-l border-white/10" : ""}`}>
      {signal.icon && <div className="text-lg leading-none mb-1.5">{SIGNAL_ICON[signal.icon] ?? signal.icon}</div>}
      <div className="text-[10px] font-bold tracking-wide text-white uppercase leading-tight">{signal.title}</div>
      {signal.value && <div className="text-lg font-extrabold mt-1" style={{ color }}>{signal.value}</div>}
      {signal.caption && <div className="text-[10px] text-text-muted mt-0.5 leading-tight">{signal.caption}</div>}
    </div>
  );
}

"use client";

import type { RecommendationCardDto } from "@/types/api";
import { useFollow } from "@/hooks/useFollow";
import { TagChip } from "@/components/ui/TagChip";

// ─────────────────────────────────────────────────────────────────────────────
// MatchCard — Swipe-first discovery card (FORMAX Swipe Card tasarımı)
// Tek dominant sinyal: RADAR HEAT (radarScore). Learning: "Senin İçin" rozeti.
// Gizli (radar'ı besler, kartta yok): momentum / spike / externalMomentum / globalTrend.
// Zone 0 sinyal barı · Zone 1 kimlik · Zone 2 bağlam · Zone 3 aksiyon.
// ─────────────────────────────────────────────────────────────────────────────

interface Props {
  card: RecommendationCardDto;
}

type HeatLevel = { label: string; tone: string; ring: string };

// radarScore (0–100) → görsel seviye. Sayı/yüzde gösterilmez (UI Constitution).
// radarScore yoksa recommendation score'a düşülür → dominant sinyal hep görünür.
function resolveHeat(card: RecommendationCardDto): HeatLevel {
  const v = card.radarScore && card.radarScore > 0 ? card.radarScore : (card.score ?? 0);
  if (v >= 66) return { label: "YÜKSEK", tone: "text-formax-amber", ring: "text-formax-amber" };
  if (v >= 40) return { label: "ORTA", tone: "text-accent", ring: "text-accent" };
  return { label: "DÜŞÜK", tone: "text-text-muted", ring: "text-text-muted" };
}

export function MatchCard({ card }: Props) {
  const homeTeamName = card.homeTeam?.name || card.teamA || "—";
  const awayTeamName = card.awayTeam?.name || card.teamB || "—";

  // Learning — "Senin İçin": yalnız gerçek ilgi (nötr 50'nin üstü) varsa.
  const personal =
    (card.teamInterestScore ?? 0) > 50 || (card.leagueInterestScore ?? 0) > 50;

  const heat = resolveHeat(card);
  const headline = card.storyHeadline?.trim();

  const { isFollowing, toggle, isPending } = useFollow(card.matchId);

  return (
    <article className="relative overflow-hidden rounded-xl border border-border-dim bg-bg-card">
      {/* Premium koyu zemin (stadyum hissi, dış asset yok) */}
      <div
        aria-hidden
        className="absolute inset-0 pointer-events-none"
        style={{
          background:
            "radial-gradient(120% 80% at 50% 0%, rgba(40,52,82,0.55) 0%, rgba(18,22,34,0) 60%), linear-gradient(180deg, rgba(20,26,40,0.6) 0%, rgba(13,16,26,0) 45%)",
        }}
      />

      <div className="relative px-4 pt-3 pb-3">
        {/* ── Zone 0 — TopSignalBar ─────────────────────────── */}
        <div className="flex items-start justify-between gap-2 min-h-[34px]">
          {personal ? <TagChip label="Senin İçin" variant="green" /> : <span />}
          <RadarHeat heat={heat} />
        </div>

        {/* ── Zone 1 — MatchIdentity ────────────────────────── */}
        <div className="flex items-center justify-between gap-3 mt-3 mb-1">
          <div className="flex-1 min-w-0 flex flex-col items-center gap-2">
            <TeamCrest name={homeTeamName} logoUrl={card.homeTeam?.logoUrl} />
            <span className="text-text-primary font-bold text-base leading-tight text-center break-words w-full">
              {homeTeamName}
            </span>
          </div>

          <span className="text-text-muted/50 text-sm font-semibold shrink-0 px-1">VS</span>

          <div className="flex-1 min-w-0 flex flex-col items-center gap-2">
            <TeamCrest name={awayTeamName} logoUrl={card.awayTeam?.logoUrl} />
            <span className="text-text-primary font-bold text-base leading-tight text-center break-words w-full">
              {awayTeamName}
            </span>
          </div>
        </div>

        {/* ── Zone 2 — Context (storyHeadline tek satır) ─────── */}
        {headline && (
          <div className="flex items-center gap-1.5 mt-2 pt-2 border-t border-border-dim">
            <span className="text-sm leading-none">🔥</span>
            <p className="text-xs font-bold text-text-primary leading-tight truncate">
              {headline}
            </p>
          </div>
        )}

        {/* ── Zone 3 — ActionRow (Follow, ikincil) ──────────── */}
        <div className="flex items-center justify-end mt-2">
          <button
            onClick={(e) => {
              e.stopPropagation();
              toggle();
            }}
            disabled={isPending}
            aria-label={isFollowing ? "Takibi kaldır" : "Takip et"}
            className={`flex items-center gap-1.5 px-2 py-1 rounded-lg transition-colors disabled:opacity-40 ${
              isFollowing ? "text-formax-green" : "text-text-muted hover:text-text-secondary"
            }`}
          >
            {isPending ? (
              <span className="w-3.5 h-3.5 block rounded-full border border-current border-t-transparent animate-spin" />
            ) : (
              <BookmarkIcon filled={isFollowing} />
            )}
            <span className="text-[11px] font-semibold tracking-wide">
              {isFollowing ? "Takip Ediliyor" : "Takip Et"}
            </span>
          </button>
        </div>
      </div>
    </article>
  );
}

// ── Radar Heat (dominant) ─────────────────────────────────────────────────────
function RadarHeat({ heat }: { heat: HeatLevel }) {
  return (
    <div className="flex items-center gap-2">
      <RadarRingIcon className={heat.ring} />
      <div className="flex flex-col leading-none">
        <span className={`text-xs font-bold tracking-wide ${heat.tone}`}>{heat.label}</span>
        <span className="text-[9px] font-semibold tracking-wider text-text-muted">RADAR</span>
      </div>
    </div>
  );
}

function RadarRingIcon({ className }: { className?: string }) {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" className={className}>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.4" opacity="0.35" />
      <circle cx="12" cy="12" r="5.5" stroke="currentColor" strokeWidth="1.4" opacity="0.6" />
      <circle cx="12" cy="12" r="2" fill="currentColor" />
    </svg>
  );
}

// ── Team Crest (logo veya monogram) ──────────────────────────────────────────
function TeamCrest({ name, logoUrl }: { name: string; logoUrl?: string | null }) {
  if (logoUrl) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={logoUrl}
        alt={name}
        className="w-14 h-14 rounded-full object-contain bg-bg-elevated border border-border"
      />
    );
  }
  const initials = name.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "").slice(0, 3).toUpperCase() || "—";
  return (
    <div className="w-14 h-14 rounded-full bg-bg-elevated border border-border flex items-center justify-center">
      <span className="text-text-secondary font-bold text-sm">{initials}</span>
    </div>
  );
}

function BookmarkIcon({ filled }: { filled: boolean }) {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill={filled ? "currentColor" : "none"}
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z" />
    </svg>
  );
}

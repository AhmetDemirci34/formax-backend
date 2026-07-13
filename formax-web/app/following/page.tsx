"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useFollowedMatches } from "@/hooks/useFollow";
import { useAuth } from "@/context/AuthContext";
import { formatMatchTime } from "@/lib/utils/formatMatchTime";
import type { FollowedMatchDto } from "@/types/api";

// ─────────────────────────────────────────────────────────────────────────────
// Status helpers
// ─────────────────────────────────────────────────────────────────────────────

const STATUS_LABEL: Record<string, string> = {
  Live:       "Canlı",
  Finished:   "Tamamlandı",
  Scheduled:  "Başlamadı",
  NotStarted: "Başlamadı",
  PreMatch:   "Başlamadı",
  Postponed:  "Ertelendi",
};

const STATUS_CLS: Record<string, string> = {
  Live:      "text-formax-red bg-formax-red/10",
  Finished:  "text-text-muted bg-bg-elevated",
  Scheduled: "text-formax-green bg-formax-green/10",
  Postponed: "text-formax-amber bg-formax-amber/10",
};

// ─────────────────────────────────────────────────────────────────────────────
// FollowedMatchRow
// ─────────────────────────────────────────────────────────────────────────────

function FollowedMatchRow({ match }: { match: FollowedMatchDto }) {
  const status = match.status ?? "Scheduled";
  const isLive = status === "Live";
  const isFinished = status === "Finished";

  const timeDisplay = !isLive && !isFinished && match.startTime
    ? formatMatchTime(match.startTime)
    : null;

  return (
    <Link
      href={`/match/${match.matchId}`}
      className="flex items-center gap-3 bg-bg-card border border-border rounded-xl px-4 py-3 hover:border-border-dim/70 transition-colors"
    >
      {/* Teams + league */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 text-sm font-bold text-text-primary leading-tight">
          <span className="truncate">{match.homeTeam}</span>
          <span className="text-text-muted/40 text-xs shrink-0">—</span>
          <span className="truncate text-right">{match.awayTeam}</span>
        </div>
        <div className="text-[10px] text-text-muted mt-0.5 truncate">{match.league}</div>
        {match.sapmaMetni && !match.sessizMi && (
          <div className="text-[10px] text-formax-amber mt-0.5 truncate">{match.sapmaMetni}</div>
        )}
      </div>

      {/* Right: score/time + status */}
      <div className="flex flex-col items-end gap-1 shrink-0">
        {isLive || isFinished ? (
          <div className="text-sm font-black text-text-primary tabular-nums">
            {match.score?.home ?? 0} – {match.score?.away ?? 0}
            {isLive && match.minute != null && (
              <span className="ml-1 text-xs text-formax-red">{match.minute}&apos;</span>
            )}
          </div>
        ) : (
          timeDisplay && (
            <div
              className={`text-xs font-bold ${
                timeDisplay.isImminent ? "text-formax-red" : "text-text-primary"
              }`}
            >
              {timeDisplay.primary}
            </div>
          )
        )}
        <span
          className={`text-[9px] font-semibold px-1.5 py-0.5 rounded ${
            STATUS_CLS[status] ?? STATUS_CLS.Scheduled
          } ${isLive ? "animate-pulse" : ""}`}
        >
          {STATUS_LABEL[status] ?? status}
        </span>
      </div>
    </Link>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Page
// ─────────────────────────────────────────────────────────────────────────────

export default function FollowingPage() {
  const router = useRouter();
  const { isLoggedIn, isHydrated } = useAuth();
  const { data: matches, isLoading } = useFollowedMatches();

  useEffect(() => {
    if (isHydrated && !isLoggedIn) {
      router.replace("/auth/login");
    }
  }, [isHydrated, isLoggedIn, router]);

  if (!isHydrated || !isLoggedIn) return null;

  // Group: live first, then scheduled by time, then finished
  const sorted = [...(matches ?? [])].sort((a, b) => {
    const order = { Live: 0, Scheduled: 1, Finished: 2, Postponed: 3 };
    const oa = order[a.status as keyof typeof order] ?? 1;
    const ob = order[b.status as keyof typeof order] ?? 1;
    if (oa !== ob) return oa - ob;
    return new Date(a.startTime).getTime() - new Date(b.startTime).getTime();
  });

  return (
    <div className="min-h-screen bg-bg-base pb-20">
      <header className="sticky top-0 z-10 bg-bg-base/90 backdrop-blur-sm border-b border-border-dim">
        <div className="max-w-md mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-accent font-black text-lg tracking-tight">FORMAX</span>
            <span className="text-text-muted text-xs">takipler</span>
          </div>
          {matches && matches.length > 0 && (
            <span className="text-xs text-text-muted">
              {matches.length} maç
            </span>
          )}
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-4 space-y-2">
        {/* Loading */}
        {isLoading && (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-16 bg-bg-card border border-border rounded-xl animate-pulse" />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!isLoading && sorted.length === 0 && (
          <div className="text-center py-16 space-y-3">
            <p className="text-4xl">⭐</p>
            <p className="text-sm text-text-secondary font-medium">Henüz takip ettiğin maç yok</p>
            <p className="text-xs text-text-muted">
              Ana sayfada maç kartlarındaki &ldquo;Takip&rdquo; butonuna bas
            </p>
            <Link
              href="/"
              className="inline-block text-accent text-sm font-semibold hover:underline mt-2"
            >
              Maçları Keşfet →
            </Link>
          </div>
        )}

        {/* Match list */}
        {sorted.map((match) => (
          <FollowedMatchRow key={match.matchId} match={match} />
        ))}
      </main>
    </div>
  );
}

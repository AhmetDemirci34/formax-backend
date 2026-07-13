"use client";

import { use, useEffect, useRef } from "react";
import Link from "next/link";
import { postSwipe } from "@/lib/api/swipe";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { MatchHeader } from "@/components/match/MatchHeader";
import { AiBlock } from "@/components/match/AiBlock";
import { InsightBlock, ProbabilitiesBlock, MarketIntelligenceBlock } from "@/components/match/InsightBlock";
import { TacticalMatchupBlock } from "@/components/match/TacticalMatchup";
import { FormSection } from "@/components/match/FormSection";
import { ComparisonBlock } from "@/components/match/ComparisonBlock";
import { RiskBlock } from "@/components/match/RiskBlock";
import { H2HSection } from "@/components/match/H2HSection";
import { LineupSection, PlayerStatusSection } from "@/components/match/LineupSection";
import { StandingsSection } from "@/components/match/StandingsSection";
import { LiveStatsPanel } from "@/components/live/LiveStatsPanel";
import { LiveTimeline } from "@/components/live/LiveTimeline";
import { MomentumChart } from "@/components/live/MomentumChart";
import { NabizFeed } from "@/components/match/NabizFeed";
import { SapmaBlock } from "@/components/match/SapmaBlock";
import { MatchVerdictBlock } from "@/components/match/MatchVerdictBlock";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function MatchDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const matchId = parseInt(id);
  const { data: match, isLoading, isError, refetch } = useMatchDetail(matchId);

  // Record how long user spent on this detail screen
  const openTimeRef = useRef(Date.now());
  useEffect(() => {
    openTimeRef.current = Date.now();
    return () => {
      const detailDurationMs = Date.now() - openTimeRef.current;
      postSwipe({
        matchId,
        action: "detail_return",
        detailDurationMs,
      }).catch(() => {});
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [matchId]);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-bg-base">
        <TopBar matchId={matchId} />
        <LoadingState label="Maç yükleniyor..." />
      </div>
    );
  }

  if (isError || !match) {
    return (
      <div className="min-h-screen bg-bg-base">
        <TopBar matchId={matchId} />
        <ErrorState
          message="Maç bilgileri şu an yüklenemiyor. Lütfen tekrar dene."
          onRetry={() => refetch()}
        />
      </div>
    );
  }

  const status = match.status ?? "Scheduled";
  const isLive = status === "Live";
  const isFinished = status === "Finished";

  return (
    <div className="min-h-screen bg-bg-base pb-12">
      <TopBar
        matchId={matchId}
        homeTeam={match.homeTeam?.name}
        awayTeam={match.awayTeam?.name}
      />

      <main className="max-w-md mx-auto px-4 py-4 space-y-3">
        {/* ── ALWAYS: Match header (scoreboard) ── */}
        <MatchHeader match={match} />

        {/* ── FINISHED: Verdict (sapma prediction vs result) ── */}
        {isFinished && !match.sapma?.sessizMi && match.live?.stats && (
          <MatchVerdictBlock
            sapma={match.sapma!}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
            homeScore={match.live.stats.homeScore}
            awayScore={match.live.stats.awayScore}
          />
        )}

        {/* ── LIVE: Live stats + momentum ── */}
        {isLive && match.live?.stats && (
          <>
            <LiveStatsPanel
              stats={match.live.stats}
              homeTeamName={match.homeTeam.name}
              awayTeamName={match.awayTeam.name}
            />
            <MomentumChart
              momentum={match.live.momentum ?? []}
              homeTeamName={match.homeTeam.name}
              awayTeamName={match.awayTeam.name}
            />
          </>
        )}

        {/* ── LIVE + FINISHED: Timeline ── */}
        {(isLive || isFinished) && (match.live?.timeline ?? []).length > 0 && (
          <LiveTimeline
            events={match.live!.timeline}
            title={isFinished ? "Maç Olayları" : "Canlı Olaylar"}
          />
        )}

        {/* ── FINISHED: Final stats panel ── */}
        {isFinished && match.live?.stats && (
          <LiveStatsPanel
            stats={match.live.stats}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
        )}

        {/* ══ STORY HERO ZONE — insight first, then AI, then signal ══ */}

        {/* ── Insight — the match story, promoted directly under the header ── */}
        <InsightBlock insight={match.insight} />

        {/* ── AI block — with post-match note when finished ── */}
        {match.ai && <AiBlock ai={match.ai} />}
        {isFinished && match.ai && match.ai.state !== "Silent" && (
          <p className="text-xs text-text-muted text-center -mt-2 px-2">
            Bu analiz maç öncesi üretilmiştir
          </p>
        )}

        {/* ── Sapma / Piyasa Sinyali — only pre-match ── */}
        {!isFinished && <SapmaBlock sapma={match.sapma} />}

        {/* ── Form & H2H — story support, moved up ── */}
        <FormSection
          homeTeamName={match.homeTeam.name}
          awayTeamName={match.awayTeam.name}
          homeLastMatches={match.homeTeamLastMatches ?? []}
          awayLastMatches={match.awayTeamLastMatches ?? []}
        />
        {/* ── Team comparison (computed form stats) ── */}
        <ComparisonBlock
          comparison={match.comparison}
          homeTeamName={match.homeTeam.name}
          awayTeamName={match.awayTeam.name}
        />
        <H2HSection
          h2h={match.h2h}
          homeTeamName={match.homeTeam.name}
          awayTeamName={match.awayTeam.name}
        />

        {/* ── TacticalMatchup (live & finished keep contextual value) ── */}
        {(isLive || isFinished) && (
          <TacticalMatchupBlock
            data={match.tacticalMatchup}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
        )}

        {/* ══ LOWER-VALUE / DETAIL ZONE — probabilities + market moved down ══ */}
        {/* Probabilities + Market intelligence — not shown finished (pre-match value only) */}
        {!isFinished && (
          <>
            {match.probabilities?.length > 0 && (
              <ProbabilitiesBlock items={match.probabilities} />
            )}
            <MarketIntelligenceBlock market={match.marketIntelligence} />
            {/* ── Risk analysis (computed from last-10 form) ── */}
            <RiskBlock
              risk={match.riskIntelligence}
              homeTeamName={match.homeTeam.name}
              awayTeamName={match.awayTeam.name}
            />
          </>
        )}

        {/* ── Lineup + injuries (pre-match / during match) ── */}
        {!isFinished && (
          <>
            <LineupSection
              lineup={match.lineup}
              homeTeamName={match.homeTeam.name}
              awayTeamName={match.awayTeam.name}
            />
            {(match.playerStatus?.injuries?.length > 0 ||
              match.playerStatus?.suspensions?.length > 0 ||
              match.playerStatus?.doubtful?.length > 0) && (
              <PlayerStatusSection playerStatus={match.playerStatus} />
            )}
          </>
        )}

        {/* ── Standings ── */}
        {match.standing && <StandingsSection standing={match.standing} />}

        {/* ── Nabız feed ── */}
        {match.nabizFeed?.items?.length > 0 && (
          <NabizFeed items={match.nabizFeed.items} />
        )}

        {/* ── Responsibility note (always last) ── */}
        {match.userProtection && (
          <div className="rounded-xl border border-border-dim bg-bg-card/50 p-3">
            <p className="text-xs text-text-muted text-center leading-relaxed">
              {match.userProtection.responsibilityNote}
            </p>
          </div>
        )}
      </main>
    </div>
  );
}

function TopBar({
  matchId,
  homeTeam,
  awayTeam,
}: {
  matchId: number;
  homeTeam?: string;
  awayTeam?: string;
}) {
  const title =
    homeTeam && awayTeam ? `${homeTeam} — ${awayTeam}` : `Maç #${matchId}`;

  return (
    <header className="sticky top-0 z-10 bg-bg-base/90 backdrop-blur-sm border-b border-border-dim">
      <div className="max-w-md mx-auto px-4 py-3 flex items-center gap-3 min-w-0">
        <Link
          href="/"
          className="text-text-muted hover:text-text-primary transition-colors text-sm shrink-0"
        >
          ← Geri
        </Link>
        <span className="text-text-secondary text-sm font-medium truncate">{title}</span>
      </div>
    </header>
  );
}

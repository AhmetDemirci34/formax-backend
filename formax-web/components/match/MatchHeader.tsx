import type { MatchDetailDto } from "@/types/api";
import { formatMatchTime } from "@/lib/utils/formatMatchTime";
import { MatchFollowButton } from "@/components/match/MatchFollowButton";
import { TeamCrest } from "@/components/ui/TeamCrest";

interface Props {
  match: MatchDetailDto;
}

// ─────────────────────────────────────────────────────────────────────────────
// Status badge config
// ─────────────────────────────────────────────────────────────────────────────

const STATUS_LABEL: Record<string, string> = {
  Live:       "Canlı",
  Finished:   "Tamamlandı",
  Scheduled:  "Başlamadı",
  NotStarted: "Başlamadı",
  PreMatch:   "Başlamadı",
  Postponed:  "Ertelendi",
};

const STATUS_COLOR: Record<string, string> = {
  Live:       "text-formax-red  bg-formax-red/10",
  Finished:   "text-text-muted  bg-bg-elevated",
  Scheduled:  "text-formax-green bg-formax-green/10",
  NotStarted: "text-formax-green bg-formax-green/10",
  PreMatch:   "text-formax-green bg-formax-green/10",
  Postponed:  "text-formax-amber bg-formax-amber/10",
};

// ─────────────────────────────────────────────────────────────────────────────
// Team name — uses shortName for long names to prevent wrapping
// ─────────────────────────────────────────────────────────────────────────────

function teamDisplay(name: string, shortName?: string): string {
  if (name.length > 14 && shortName) return shortName;
  return name;
}

// ─────────────────────────────────────────────────────────────────────────────
// MatchHeader V2
//
// Zone 0 · Context   — insight.headline prominently at top (if present)
// Zone 1 · League    — league · round + status badge
// Zone 2 · Scoreboard— teams + score/time + countdown
// Zone 3 · Venue     — stadyum + hakem (no emojis, labelled rows)
// ─────────────────────────────────────────────────────────────────────────────

export function MatchHeader({ match }: Props) {
  const status   = match.status ?? "Scheduled";
  const isLive   = status === "Live";
  const isFinished = status === "Finished";
  const isScheduled = status === "Scheduled";

  // Score (live or finished)
  const homeScore = match.live?.stats?.homeScore ?? 0;
  const awayScore = match.live?.stats?.awayScore ?? 0;
  const minute    = match.live?.stats?.minute;

  // Time display (scheduled only)
  const timeDisplay = isScheduled
    ? formatMatchTime(match.matchDate)
    : null;

  // Stakes context from insight headline
  const contextHeadline = match.insight?.headline;

  const homeDisplay = teamDisplay(match.homeTeam.name, match.homeTeam.shortName);
  const awayDisplay = teamDisplay(match.awayTeam.name, match.awayTeam.shortName);

  return (
    <div className="bg-bg-card rounded-xl border border-border overflow-hidden">

      {/* ── Zone 0: Context headline ───────────────────────────── */}
      {contextHeadline && (
        <div className="px-4 py-2.5 bg-accent/5 border-b border-accent/15">
          <p className="text-xs font-semibold text-text-secondary leading-snug">
            {contextHeadline}
          </p>
        </div>
      )}

      {/* ── Zone 1: League + Status + Follow ──────────────────── */}
      <div className="flex items-center justify-between px-4 pt-3 pb-0 gap-2">
        <span className="text-xs text-text-muted font-medium min-w-0 truncate">
          {match.league}
          {match.round && (
            <span className="ml-1 text-text-muted/60">· {match.round}</span>
          )}
        </span>
        <div className="flex items-center gap-2 shrink-0">
          <span
            className={`text-xs font-semibold px-2 py-0.5 rounded ${
              STATUS_COLOR[status] ?? STATUS_COLOR.Scheduled
            } ${isLive ? "animate-pulse" : ""}`}
          >
            {STATUS_LABEL[status] ?? status}
            {isLive && minute != null && (
              <span className="ml-1">{minute}&apos;</span>
            )}
          </span>
          <MatchFollowButton matchId={match.matchId} />
        </div>
      </div>

      {/* ── Zone 2: Scoreboard ─────────────────────────────────── */}
      <div className="flex items-center gap-2 px-4 pt-3 pb-3">

        {/* Home team */}
        <div className="flex-1 min-w-0 flex flex-col items-start gap-1.5">
          <TeamCrest name={match.homeTeam.name} logoUrl={match.homeTeam.logoUrl} size={40} />
          {match.homeTeam.rank && (
            <div className="text-[10px] text-text-muted">#{match.homeTeam.rank}</div>
          )}
          <div className="font-bold text-base text-text-primary leading-tight truncate w-full">
            {homeDisplay}
          </div>
        </div>

        {/* Centre: score OR time — fixed width so teams don't compress */}
        <div className="flex flex-col items-center shrink-0 w-[108px]">
          {isLive || isFinished ? (
            /* Score */
            <div className="flex items-center gap-2 text-2xl font-black text-text-primary">
              <span>{homeScore}</span>
              <span className="text-text-muted text-xl">–</span>
              <span>{awayScore}</span>
            </div>
          ) : (
            /* Scheduled: human-readable time */
            <>
              <div
                className={`text-lg font-bold leading-tight ${
                  timeDisplay?.isImminent ? "text-formax-red" : "text-text-primary"
                }`}
              >
                {timeDisplay?.primary ?? "—"}
              </div>
              {timeDisplay?.secondary && (
                <div className="text-[10px] text-accent font-semibold mt-0.5">
                  {timeDisplay.secondary}
                </div>
              )}
            </>
          )}
        </div>

        {/* Away team */}
        <div className="flex-1 min-w-0 flex flex-col items-end gap-1.5 text-right">
          <TeamCrest name={match.awayTeam.name} logoUrl={match.awayTeam.logoUrl} size={40} />
          {match.awayTeam.rank && (
            <div className="text-[10px] text-text-muted">#{match.awayTeam.rank}</div>
          )}
          <div className="font-bold text-base text-text-primary leading-tight truncate w-full">
            {awayDisplay}
          </div>
        </div>
      </div>

      {/* ── Zone 3: Venue + Referee ────────────────────────────── */}
      {(match.venue || match.referee || match.watchersCount > 0) && (
        <div className="px-4 pb-3 pt-0 border-t border-border-dim mt-0 space-y-1.5">
          <div className="h-0" />
          {match.venue && (
            <div className="flex items-baseline justify-between">
              <span className="text-[10px] font-medium text-text-muted min-w-[52px]">
                Stadyum
              </span>
              <span className="text-xs font-semibold text-text-secondary flex-1 text-right">
                {match.venue}
              </span>
              {match.watchersCount > 0 && (
                <span className="text-[10px] text-text-muted ml-3 shrink-0">
                  {match.watchersCount} izliyor
                </span>
              )}
            </div>
          )}
          {match.referee && (
            <div className="flex items-baseline justify-between">
              <span className="text-[10px] font-medium text-text-muted min-w-[52px]">
                Hakem
              </span>
              <span className="text-xs font-semibold text-text-secondary flex-1 text-right">
                {match.referee}
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

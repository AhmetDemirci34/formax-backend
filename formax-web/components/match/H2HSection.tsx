import type { H2HDto } from "@/types/api";

interface Props {
  h2h: H2HDto;
  homeTeamName: string;
  awayTeamName: string;
}

export function H2HSection({ h2h, homeTeamName, awayTeamName }: Props) {
  const totalMeetings = (h2h?.homeWins ?? 0) + (h2h?.draws ?? 0) + (h2h?.awayWins ?? 0);
  // No past meetings → render nothing (no value-less card).
  if (totalMeetings === 0 && (h2h?.matches?.length ?? 0) === 0) return null;

  const total = totalMeetings || 1;

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        H2H — Geçmiş Karşılaşmalar
      </h3>

      {/* Win bar */}
      <div className="flex gap-1 h-6 rounded-full overflow-hidden mb-2">
        {h2h.homeWins > 0 && (
          <div
            className="bg-accent flex items-center justify-center text-xs font-bold text-white"
            style={{ width: `${(h2h.homeWins / total) * 100}%` }}
          >
            {h2h.homeWins}
          </div>
        )}
        {h2h.draws > 0 && (
          <div
            className="bg-bg-elevated flex items-center justify-center text-xs font-bold text-text-secondary"
            style={{ width: `${(h2h.draws / total) * 100}%` }}
          >
            {h2h.draws}
          </div>
        )}
        {h2h.awayWins > 0 && (
          <div
            className="bg-text-muted/30 flex items-center justify-center text-xs font-bold text-text-secondary"
            style={{ width: `${(h2h.awayWins / total) * 100}%` }}
          >
            {h2h.awayWins}
          </div>
        )}
      </div>
      <div className="flex justify-between text-xs text-text-muted mb-3">
        <span>{homeTeamName}</span>
        <span>Berabere</span>
        <span>{awayTeamName}</span>
      </div>

      {/* Recent meetings */}
      {h2h.matches?.length > 0 && (
        <div className="space-y-1.5 mt-3 border-t border-border-dim pt-3">
          {h2h.matches.slice(0, 5).map((m, i) => (
            <div key={i} className="flex items-center justify-between text-xs text-text-secondary">
              <span className="truncate max-w-[35%]">{m.homeTeamName}</span>
              <span className="font-bold text-text-primary px-2 shrink-0">
                {m.homeScore} – {m.awayScore}
              </span>
              <span className="truncate max-w-[35%] text-right">{m.awayTeamName}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

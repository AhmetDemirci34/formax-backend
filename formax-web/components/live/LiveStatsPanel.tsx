import type { LiveStatsDto } from "@/types/api";

interface Props {
  stats: LiveStatsDto;
  homeTeamName: string;
  awayTeamName: string;
}

function StatRow({
  label,
  home,
  away,
}: {
  label: string;
  home: number;
  away: number;
}) {
  const total = home + away || 1;
  const homeW = Math.round((home / total) * 100);
  const awayW = 100 - homeW;

  return (
    <div className="space-y-1">
      <div className="flex justify-between text-xs">
        <span className="font-semibold text-text-primary">{home}</span>
        <span className="text-text-muted">{label}</span>
        <span className="font-semibold text-text-primary">{away}</span>
      </div>
      <div className="flex h-1 rounded-full overflow-hidden">
        <div className="bg-accent" style={{ width: `${homeW}%` }} />
        <div className="bg-text-muted/30 flex-1" />
      </div>
    </div>
  );
}

export function LiveStatsPanel({ stats, homeTeamName, awayTeamName }: Props) {
  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      {/* Header: teams + score */}
      <div className="flex items-center justify-between mb-4">
        <span className="text-xs font-semibold text-text-secondary truncate max-w-[35%]">
          {homeTeamName}
        </span>
        <div className="text-xl font-black text-text-primary tracking-tight">
          {stats.homeScore} – {stats.awayScore}
          {stats.minute != null && (
            <span className="text-xs font-normal text-formax-red ml-2">
              {stats.minute}&apos;
            </span>
          )}
        </div>
        <span className="text-xs font-semibold text-text-secondary truncate max-w-[35%] text-right">
          {awayTeamName}
        </span>
      </div>

      {/* Stats */}
      <div className="space-y-3">
        <StatRow label="Possession %" home={stats.possessionHome} away={stats.possessionAway} />
        <StatRow label="Şutlar" home={stats.shotsHome} away={stats.shotsAway} />
        <StatRow label="İsabetli Şut" home={stats.shotsOnTargetHome} away={stats.shotsOnTargetAway} />
        <StatRow label="Korner" home={stats.cornersHome} away={stats.cornersAway} />
        {(stats.xgHome != null && stats.xgHome > 0) ||
         (stats.xgAway != null && stats.xgAway > 0) ? (
          <StatRow
            label="xG"
            home={Math.round((stats.xgHome ?? 0) * 10) / 10}
            away={Math.round((stats.xgAway ?? 0) * 10) / 10}
          />
        ) : null}
        {(stats.dangerousAttacksHome > 0 || stats.dangerousAttacksAway > 0) && (
          <StatRow label="Tehlikeli Atak" home={stats.dangerousAttacksHome} away={stats.dangerousAttacksAway} />
        )}
      </div>

      {/* Cards */}
      {(stats.yellowHome + stats.yellowAway + stats.redHome + stats.redAway > 0) && (
        <div className="flex gap-3 mt-3 pt-3 border-t border-border-dim text-xs text-text-secondary">
          <span>🟨 {stats.yellowHome}–{stats.yellowAway}</span>
          <span>🟥 {stats.redHome}–{stats.redAway}</span>
          <span>⛔ {stats.foulsHome}–{stats.foulsAway} foul</span>
        </div>
      )}
    </div>
  );
}

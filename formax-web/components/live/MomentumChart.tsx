import type { MomentumSnapshotDto } from "@/types/api";

interface Props {
  momentum: MomentumSnapshotDto[];
  homeTeamName: string;
  awayTeamName: string;
}

export function MomentumChart({ momentum, homeTeamName, awayTeamName }: Props) {
  if (!momentum?.length) return null;

  const MAX_BARS = 45;
  const sliced = momentum.slice(-MAX_BARS);
  const maxPressure = Math.max(
    ...sliced.map((s) => Math.max(s.homePressure, s.awayPressure)),
    1
  );

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
          Momentum
        </h3>
        <div className="flex gap-3 text-xs text-text-muted">
          <span className="flex items-center gap-1">
            <span className="w-2 h-2 rounded-full bg-accent inline-block" />
            {homeTeamName.split(" ")[0]}
          </span>
          <span className="flex items-center gap-1">
            <span className="w-2 h-2 rounded-full bg-text-muted/50 inline-block" />
            {awayTeamName.split(" ")[0]}
          </span>
        </div>
      </div>

      {/* Bar chart — mirrored: home bars up, away bars down */}
      <div className="flex items-end gap-px h-16">
        {sliced.map((snap, i) => {
          const homeH = Math.round((snap.homePressure / maxPressure) * 32);
          const awayH = Math.round((snap.awayPressure / maxPressure) * 32);
          return (
            <div key={i} className="flex-1 flex flex-col items-center gap-px">
              <div
                className="w-full rounded-t bg-accent opacity-70 min-h-px"
                style={{ height: `${homeH}px` }}
              />
              <div
                className="w-full rounded-b bg-text-muted/30 min-h-px"
                style={{ height: `${awayH}px` }}
              />
            </div>
          );
        })}
      </div>
      <div className="flex justify-between mt-1 text-xs text-text-muted">
        <span>{sliced[0]?.minute ?? 0}&apos;</span>
        <span>{sliced[sliced.length - 1]?.minute ?? 0}&apos;</span>
      </div>
    </div>
  );
}

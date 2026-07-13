import type { TacticalMatchupDto } from "@/types/api";

interface Props {
  data: TacticalMatchupDto;
  homeTeamName: string;
  awayTeamName: string;
}

function TacticalRow({
  label,
  homeScore,
  awayScore,
}: {
  label: string;
  homeScore: number;
  awayScore: number;
}) {
  const total = homeScore + awayScore || 0.01;
  const homeW = (homeScore / 20) * 100; // max is 10/10 => 100%
  const awayW = (awayScore / 20) * 100;

  return (
    <div className="space-y-1">
      <div className="flex justify-between text-xs text-text-muted">
        <span className="font-medium text-text-secondary">{homeScore.toFixed(1)}</span>
        <span>{label}</span>
        <span className="font-medium text-text-secondary">{awayScore.toFixed(1)}</span>
      </div>
      <div className="flex h-1.5 gap-0.5">
        <div className="flex-1 flex justify-end">
          <div
            className="h-full rounded-l bg-accent transition-all"
            style={{ width: `${homeW}%` }}
          />
        </div>
        <div className="flex-1">
          <div
            className="h-full rounded-r bg-text-muted/40 transition-all"
            style={{ width: `${awayW}%` }}
          />
        </div>
      </div>
    </div>
  );
}

export function TacticalMatchupBlock({ data, homeTeamName, awayTeamName }: Props) {
  const dims = [
    data.attack,
    data.defense,
    data.transition,
    data.setPiece,
    data.form,
    data.discipline,
  ];

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Taktik Karşılaştırma
      </h3>
      <div className="flex justify-between text-xs font-semibold text-text-secondary mb-3">
        <span className="truncate max-w-[40%]">{homeTeamName}</span>
        <span className="truncate max-w-[40%] text-right">{awayTeamName}</span>
      </div>
      <div className="space-y-3">
        {dims.map((d) => (
          <TacticalRow
            key={d.label}
            label={d.label}
            homeScore={d.homeScore}
            awayScore={d.awayScore}
          />
        ))}
      </div>
    </div>
  );
}

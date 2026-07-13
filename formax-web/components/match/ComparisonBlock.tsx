import type { ComparisonDto } from "@/types/api";

interface Props {
  comparison: ComparisonDto;
  homeTeamName: string;
  awayTeamName: string;
}

type Metric = {
  label: string;
  home: number;
  away: number;
  /** true → higher value is the stronger side (drives the bar split + emphasis) */
  higherIsBetter: boolean;
  suffix?: string;
  decimals?: number;
};

function MetricRow({ m }: { m: Metric }) {
  const total = m.home + m.away;
  // Bar split is purely proportional to the two real values (no invented scale).
  const homePct = total > 0 ? (m.home / total) * 100 : 50;
  const fmt = (v: number) => (m.decimals != null ? v.toFixed(m.decimals) : `${v}`) + (m.suffix ?? "");

  const homeStronger = m.higherIsBetter ? m.home > m.away : m.home < m.away;
  const awayStronger = m.higherIsBetter ? m.away > m.home : m.away < m.home;

  return (
    <div className="space-y-1">
      <div className="flex justify-between items-baseline text-xs">
        <span className={`font-bold tabular-nums ${homeStronger ? "text-accent" : "text-text-secondary"}`}>
          {fmt(m.home)}
        </span>
        <span className="text-text-muted">{m.label}</span>
        <span className={`font-bold tabular-nums ${awayStronger ? "text-formax-amber" : "text-text-secondary"}`}>
          {fmt(m.away)}
        </span>
      </div>
      <div className="flex h-1.5 gap-0.5 rounded-full overflow-hidden bg-bg-elevated">
        <div className="bg-accent transition-all" style={{ width: `${homePct}%` }} />
        <div className="bg-formax-amber/70 transition-all" style={{ width: `${100 - homePct}%` }} />
      </div>
    </div>
  );
}

export function ComparisonBlock({ comparison, homeTeamName, awayTeamName }: Props) {
  const h = comparison?.home;
  const a = comparison?.away;
  // No computed form data for either side → render nothing (no value-less card).
  if (!h || !a) return null;
  const empty =
    h.avgGoalsFor === 0 && h.avgGoalsAgainst === 0 && h.formScore === 0 &&
    a.avgGoalsFor === 0 && a.avgGoalsAgainst === 0 && a.formScore === 0;
  if (empty) return null;

  const metrics: Metric[] = [
    { label: "Gol Ortalaması", home: h.avgGoalsFor, away: a.avgGoalsFor, higherIsBetter: true, decimals: 1 },
    { label: "Yenilen Gol", home: h.avgGoalsAgainst, away: a.avgGoalsAgainst, higherIsBetter: false, decimals: 1 },
    { label: "Gol Atma %", home: h.goalScoringRate, away: a.goalScoringRate, higherIsBetter: true, suffix: "%" },
    { label: "Clean Sheet %", home: h.cleanSheetRate, away: a.cleanSheetRate, higherIsBetter: true, suffix: "%" },
    { label: "Form Puanı", home: h.formScore, away: a.formScore, higherIsBetter: true },
  ];

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Takım Karşılaştırması
      </h3>
      <div className="flex justify-between text-xs font-medium text-text-secondary mb-3">
        <span className="text-accent truncate max-w-[45%]">{homeTeamName}</span>
        <span className="text-formax-amber truncate max-w-[45%] text-right">{awayTeamName}</span>
      </div>
      <div className="space-y-2.5">
        {metrics.map((m) => (
          <MetricRow key={m.label} m={m} />
        ))}
      </div>
    </div>
  );
}

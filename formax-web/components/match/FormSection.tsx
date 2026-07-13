import type { LastMatchDto } from "@/types/api";

const RESULT_STYLE: Record<string, string> = {
  W: "bg-formax-green text-white",
  D: "bg-formax-amber text-white",
  L: "bg-formax-red/80 text-white",
};

function FormBadge({ result }: { result: string }) {
  return (
    <span
      className={`w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold ${
        RESULT_STYLE[result] ?? "bg-bg-elevated text-text-muted"
      }`}
    >
      {result}
    </span>
  );
}

function TeamFormRow({
  teamName,
  matches,
}: {
  teamName: string;
  matches: LastMatchDto[];
}) {
  const recent = matches.slice(0, 5);
  return (
    <div className="space-y-1.5">
      <div className="text-xs font-medium text-text-secondary truncate">{teamName}</div>
      <div className="flex gap-1">
        {recent.map((m, i) => (
          <FormBadge key={i} result={m.result} />
        ))}
        {recent.length === 0 && (
          <span className="text-xs text-text-muted">Veri yok</span>
        )}
      </div>
    </div>
  );
}

interface Props {
  homeTeamName: string;
  awayTeamName: string;
  homeLastMatches: LastMatchDto[];
  awayLastMatches: LastMatchDto[];
}

export function FormSection({
  homeTeamName,
  awayTeamName,
  homeLastMatches,
  awayLastMatches,
}: Props) {
  // No form data for either side → render nothing (no value-less card).
  if ((homeLastMatches?.length ?? 0) === 0 && (awayLastMatches?.length ?? 0) === 0)
    return null;

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Son Form (5 maç)
      </h3>
      <div className="space-y-3">
        <TeamFormRow teamName={homeTeamName} matches={homeLastMatches} />
        <TeamFormRow teamName={awayTeamName} matches={awayLastMatches} />
      </div>
    </div>
  );
}

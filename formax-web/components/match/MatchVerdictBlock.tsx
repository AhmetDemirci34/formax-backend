import type { SapmaDto } from "@/types/api";

interface Props {
  sapma: SapmaDto;
  homeTeamName: string;
  awayTeamName: string;
  homeScore: number;
  awayScore: number;
}

type VerdictState = "hit" | "miss";

function resolveVerdict(
  gercekGucYonu: string,
  homeScore: number,
  awayScore: number
): VerdictState {
  if (gercekGucYonu === "Home") {
    return homeScore > awayScore ? "hit" : "miss";
  }
  if (gercekGucYonu === "Away") {
    return awayScore > homeScore ? "hit" : "miss";
  }
  // "Denge"
  return homeScore === awayScore ? "hit" : "miss";
}

function resolveStrength(gercekGucYonu: string, gucSkoru: number): number {
  if (gercekGucYonu === "Home") return gucSkoru;
  if (gercekGucYonu === "Away") return 100 - gucSkoru;
  return Math.abs(gucSkoru - 50) * 2;
}

function resolveStrengthLabel(gercekGucYonu: string): string {
  if (gercekGucYonu === "Home") return "Ev sahibi güç";
  if (gercekGucYonu === "Away") return "Deplasman güç";
  return "Fark";
}

function resolvePredictionLabel(
  gercekGucYonu: string,
  homeTeamName: string,
  awayTeamName: string
): string {
  if (gercekGucYonu === "Home") return homeTeamName;
  if (gercekGucYonu === "Away") return awayTeamName;
  return "Beraberlik";
}

function resolveActualLabel(
  homeScore: number,
  awayScore: number,
  homeTeamName: string,
  awayTeamName: string
): string {
  if (homeScore > awayScore) return homeTeamName;
  if (awayScore > homeScore) return awayTeamName;
  return "Beraberlik";
}

export function MatchVerdictBlock({
  sapma,
  homeTeamName,
  awayTeamName,
  homeScore,
  awayScore,
}: Props) {
  if (sapma.sessizMi) return null;

  const verdict = resolveVerdict(sapma.gercekGucYonu, homeScore, awayScore);
  const strength = resolveStrength(sapma.gercekGucYonu, sapma.gucSkoru);
  const strengthLabel = resolveStrengthLabel(sapma.gercekGucYonu);
  const predictionLabel = resolvePredictionLabel(
    sapma.gercekGucYonu,
    homeTeamName,
    awayTeamName
  );
  const actualLabel = resolveActualLabel(
    homeScore,
    awayScore,
    homeTeamName,
    awayTeamName
  );
  const hit = verdict === "hit";

  return (
    <div className="bg-bg-card rounded-xl border border-border overflow-hidden">
      <div className="px-4 pt-3 pb-2 border-b border-border-dim">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
          Maç Özeti
        </h3>
      </div>

      <div className="grid grid-cols-2 divide-x divide-border-dim">
        <div className="px-4 py-3 space-y-1">
          <p className="text-[10px] text-text-muted font-medium uppercase tracking-wider">
            Beklenti
          </p>
          <p className="text-sm font-semibold text-text-primary leading-tight">
            {predictionLabel}
          </p>
          <p className="text-xs text-text-muted">
            {strengthLabel} · %{strength}
          </p>
        </div>

        <div className="px-4 py-3 space-y-1">
          <p className="text-[10px] text-text-muted font-medium uppercase tracking-wider">
            Gerçekleşen
          </p>
          <p className="text-sm font-semibold text-text-primary leading-tight">
            {actualLabel}
          </p>
          <p className="text-lg font-black tabular-nums text-text-primary">
            {homeScore} – {awayScore}
          </p>
        </div>
      </div>

      <div className="px-4 pb-3 pt-1">
        <div
          className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold ${
            hit
              ? "bg-formax-green/10 text-formax-green border border-formax-green/20"
              : "bg-formax-red/10 text-formax-red border border-formax-red/20"
          }`}
        >
          <span className="text-sm">{hit ? "✓" : "✗"}</span>
          <span>{hit ? "Tahmin tuttu" : "Tahmin tutmadı"}</span>
        </div>
      </div>
    </div>
  );
}

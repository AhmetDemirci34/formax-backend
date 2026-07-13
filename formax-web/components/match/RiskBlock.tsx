import type { RiskIntelligenceDto } from "@/types/api";

interface Props {
  risk: RiskIntelligenceDto;
  homeTeamName: string;
  awayTeamName: string;
}

function RiskRow({
  teamName,
  label,
  detail,
  tone,
}: {
  teamName: string;
  label: string;
  detail: string;
  tone: "accent" | "amber";
}) {
  const accent = tone === "accent" ? "text-accent" : "text-formax-amber";
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-medium text-text-secondary truncate">{teamName}</span>
        <span className={`text-xs font-semibold ${accent} shrink-0`}>{label}</span>
      </div>
      {detail && <p className="text-[11px] text-text-muted leading-relaxed">{detail}</p>}
    </div>
  );
}

export function RiskBlock({ risk, homeTeamName, awayTeamName }: Props) {
  // No real risk labels → render nothing (no value-less card).
  if (!risk?.homeRiskLabel && !risk?.awayRiskLabel) return null;

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4 space-y-3">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
        Risk Analizi
      </h3>
      {risk.homeRiskLabel && (
        <RiskRow teamName={homeTeamName} label={risk.homeRiskLabel} detail={risk.homeRiskDetail} tone="accent" />
      )}
      {risk.awayRiskLabel && (
        <div className="border-t border-border-dim pt-3">
          <RiskRow teamName={awayTeamName} label={risk.awayRiskLabel} detail={risk.awayRiskDetail} tone="amber" />
        </div>
      )}
    </div>
  );
}

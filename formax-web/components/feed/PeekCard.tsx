import type { RecommendationCardDto } from "@/types/api";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { RadarBadge } from "@/components/ui/RadarBadge";

type Level = "YÜKSEK" | "ORTA" | "DÜŞÜK";

function levelFromScore(v: number): Level {
  if (v >= 66) return "YÜKSEK";
  if (v >= 40) return "ORTA";
  return "DÜŞÜK";
}

function formatKickoff(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return null;
  return d.toLocaleString("tr-TR", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" });
}

interface Props {
  card: RecommendationCardDto;
  side: "left" | "right";
}

// Sol/sağ kısmi kart (stack derinliği). Yalnız gerçek veri: radar · crest · isim · kickoff.
export function PeekCard({ card }: Props) {
  const team = card.homeTeam?.name || card.teamA || "—";
  const level: Level =
    (card.radarLevel as Level) ?? levelFromScore(card.radarScore > 0 ? card.radarScore : card.score);
  const kickoff = formatKickoff(card.kickoffTime ?? card.matchDate);

  return (
    <div className="h-full rounded-2xl border border-white/10 bg-[#0a0e16] px-3 pt-4 flex flex-col items-center">
      <div className="w-full flex justify-end">
        <RadarBadge level={level} variant="mini" />
      </div>

      <div className="mt-6 flex flex-col items-center gap-2">
        <TeamCrest name={team} logoUrl={card.homeTeam?.logoUrl} size={56} />
        <span className="text-white font-bold text-sm text-center break-words w-full">{team}</span>
      </div>

      {kickoff && (
        <span className="mt-3 text-[11px] text-text-muted">{kickoff}</span>
      )}
    </div>
  );
}

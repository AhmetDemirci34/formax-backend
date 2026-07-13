import type { RecommendationCardDto } from "@/types/api";
import { RadarBadge } from "@/components/ui/RadarBadge";
import { HeroTeams } from "./HeroTeams";
import { radarLevel } from "./cardSignals";

// Stack derinliği — bir sonraki/önceki Hero'nun küçük, sönük önizlemesi.
export function HeroPeek({ card }: { card: RecommendationCardDto }) {
  return (
    <div className="h-full overflow-hidden rounded-3xl border border-white/8 bg-bg-card/70 px-4 pt-5">
      <div className="flex justify-end">
        <RadarBadge level={radarLevel(card)} variant="mini" />
      </div>
      <div className="mt-6">
        <HeroTeams card={card} size={56} />
      </div>
    </div>
  );
}

import type { RecommendationCardDto } from "@/types/api";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { homeName, awayName } from "./cardSignals";

interface Props {
  card: RecommendationCardDto;
  /** Crest size in px — smaller for peek/trending contexts. */
  size?: number;
}

// Ortada büyük takım armaları + VS + isimler. Logo yoksa TeamCrest monogram döner.
export function HeroTeams({ card, size = 92 }: Props) {
  const home = homeName(card);
  const away = awayName(card);

  return (
    <div className="flex items-stretch justify-between gap-3">
      <TeamColumn name={home} logoUrl={card.homeTeam?.logoUrl} size={size} />

      <div className="flex shrink-0 flex-col items-center justify-center">
        <span className="text-lg font-semibold tracking-widest text-white/35">VS</span>
      </div>

      <TeamColumn name={away} logoUrl={card.awayTeam?.logoUrl} size={size} />
    </div>
  );
}

function TeamColumn({
  name,
  logoUrl,
  size,
}: {
  name: string;
  logoUrl?: string | null;
  size: number;
}) {
  return (
    <div className="flex flex-1 flex-col items-center gap-3 min-w-0">
      <TeamCrest name={name} logoUrl={logoUrl} size={size} />
      <span className="w-full text-center text-base font-bold leading-tight text-white break-words">
        {name}
      </span>
    </div>
  );
}

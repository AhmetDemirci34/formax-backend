import { TeamLogoPair } from "@/components/ui/TeamLogoPair";
import { MatchTime } from "@/components/ui/MatchTime";
import { StadiumBackground } from "./StadiumBackground";
import { HeroOverlay } from "./HeroOverlay";
import { PlayerSpotlight } from "./PlayerSpotlight";
import { AIBadge } from "./AIBadge";
import { NewsCounterBadge } from "./NewsCounterBadge";
import { LeagueBadge } from "./LeagueBadge";
import { ConfidenceRing } from "./ConfidenceRing";
import { HeroAICommentCard } from "./HeroAICommentCard";
import { SwipeHint } from "./SwipeHint";
import type { HeroMatchVM } from "./heroData";

/**
 * FORMAX · HeroCard (05) — referans Hero kompozisyonu.
 * Katmanlar: atmosfer + oyuncular (ana görsel) + kimlik. Merkez odak = AI Güven halkası
 * (VS yerine, iki armanın/oyuncunun tam ortasında). Altında tam-genişlik AI Yorumu kartı.
 * Metrik paneli ve CTA Hero'dan kaldırıldı. Veri tümüyle o maça ait (HeroMatchVM).
 */
export function HeroCard({ match }: { match: HeroMatchVM }) {
  return (
    <div className="relative overflow-hidden rounded-[var(--radius-section)] border border-white/[0.08] shadow-[var(--shadow-elevated)]">
      <StadiumBackground />
      <HeroOverlay />
      <PlayerSpotlight player={match.leftPlayer} side="left" />
      <PlayerSpotlight player={match.rightPlayer} side="right" />

      <div className="relative z-10 flex flex-col gap-5 p-5 pt-5">
        {/* Üst badge'ler — iki köşeye hizalı */}
        <div className="flex items-start justify-between gap-2">
          <AIBadge label="AI'nın Bugün İçin 1 Numaralı Maçı" />
          <NewsCounterBadge count={match.newsCount} />
        </div>

        {/* Merkez kimlik: Premier League → arma + (ortada AI Güven halkası) + arma → saat.
            Halka iki armanın/oyuncunun tam ortasında = görselin odak noktası. */}
        <div className="flex flex-col items-center gap-3 pt-2">
          <LeagueBadge league={match.league} />
          <TeamLogoPair
            home={match.home}
            away={match.away}
            size={46}
            showNames
            center={
              <ConfidenceRing
                value={match.confidence}
                label={match.confidenceLabel}
                size={104}
              />
            }
          />
          <MatchTime label={match.time} />
        </div>

        {/* AI Yorumu — oyuncuların altında, Hero genişliği boyunca tek premium glass kart */}
        <HeroAICommentCard text={match.aiComment} />

        {/* Swipe ipucu — kartın kaydırılabildiğini hissettirir (sayfa noktası değil) */}
        <SwipeHint />
      </div>
    </div>
  );
}

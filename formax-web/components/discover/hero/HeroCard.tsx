import type { MotionValue } from "framer-motion";
import type { RecommendationCardDto } from "@/types/api";
import { TeamLogoPair } from "@/components/ui/TeamLogoPair";
import { StadiumBackground } from "./StadiumBackground";
import { HeroOverlay } from "./HeroOverlay";
import { LeagueBadge } from "./LeagueBadge";
import { ConfidenceRing } from "./ConfidenceRing";
import { useMatchOutcomes } from "@/hooks/useMatchOutcomes";
import { outcomeExpectation } from "@/lib/outcomes/outcomeView";
import { HeroSignalRow } from "./HeroSignalRow";
import { HeroFollowButton } from "./HeroFollowButton";
import { CarouselIndicator } from "./CarouselIndicator";
import { MatchClock } from "./MatchClock";
import { homeName, awayName, leagueLabel } from "@/components/discover/cardSignals";

/**
 * FORMAX · HeroCard (Keşfet) — YORUMSUZ maç kartı.
 *
 * ÜRÜN KARARI (kilitli): Keşfet kartında HİÇBİR AI YORUMU/anlatısı görünmez.
 * Kaldırıldı: FORMAX AI Yorumu kutusu (HeroAICommentCard), teaser satırları
 * (radarSummary/radarHighlights → teaser.ts), "Tüm Analizi Gör" bağlantısı.
 * Backend bu alanları göndermeye devam edebilir; Keşfet UI RENDER ETMEZ.
 * Maç Detayı ekranındaki analiz yüzeyleri bu karardan ETKİLENMEZ.
 *
 * Kartta yalnız şunlar vardır (hepsi backend verisi, hiçbiri uydurma):
 *   sinyal rozeti · RADAR seviyesi · lig · başlama günü+saati · iki takım
 *   (gerçek logo + ad) · ortada sayısal AI Beklentisi · Takip Et · konum noktaları.
 *
 * CANLI KURALI: Keşfet yalnız BAŞLAMAMIŞ maç gösterir (süzgeç useRecommendations +
 * cardSignals.isDiscoverable). Bu yüzden kartta canlı rozeti, dakika ve skor YOKTUR.
 */
export function HeroCard({
  card,
  parallaxX,
  position,
}: {
  card: RecommendationCardDto;
  /** Arka plan parallax offset'i (HeroSection'dan gelir). */
  parallaxX?: MotionValue<number>;
  /** Feed içindeki konum — alt nokta göstergesi. */
  position?: { index: number; count: number };
}) {
  const league = leagueLabel(card);
  // AI BEKLENTİSİ — Olası Sonuçlar kartları ve Maç Detayı ile AYNI snapshot (aynı cache anahtarı; ikinci istek yok).
  // Tahmin Enabled değilse halka çizilmez (karar paketi güven skoru bu göstergede KULLANILMAZ).
  const { data: outcomes } = useMatchOutcomes(card.matchId);
  const expectation = outcomeExpectation(outcomes);
  const kickoff = card.kickoffTime ?? card.matchDate ?? null;

  return (
    <div className="relative overflow-hidden rounded-[var(--radius-section)] border border-white/[0.08] shadow-[var(--shadow-elevated)]">
      <StadiumBackground parallaxX={parallaxX} plain />
      <HeroOverlay />

      <div className="relative z-10 flex flex-col gap-3.5 p-4">
        {/* Üst — sol: sinyal rozeti · sağ: RADAR seviyesi */}
        <HeroSignalRow card={card} />

        {/* Meta — lig (varsa) + başlama günü/saati. Canlı/dakika/skor GÖSTERİLMEZ. */}
        <div className="flex min-h-[18px] items-center justify-between gap-2">
          {league ? <LeagueBadge league={league} /> : <span />}
          <MatchClock kickoff={kickoff} alwaysAbsolute />
        </div>

        {/* Merkez — iki takım (gerçek logo + ad) + ortada sayısal AI Beklentisi */}
        <div className="flex flex-col items-center gap-3 pt-0.5">
          <TeamLogoPair
            home={{ name: homeName(card), logoUrl: card.homeTeam?.logoUrl }}
            away={{ name: awayName(card), logoUrl: card.awayTeam?.logoUrl }}
            size={46}
            showNames
            center={expectation ? <ConfidenceRing value={expectation.value} size={104} /> : undefined}
          />
        </div>

        {/* Aksiyon — yalnız Takip Et (görünür buton). Kartın boş alanı tıklanabilir DEĞİL. */}
        <div className="flex justify-center">
          <HeroFollowButton matchId={card.matchId} />
        </div>

        {/* Alt — konum göstergesi (kaçıncı kart) */}
        {position && position.count > 1 ? (
          <CarouselIndicator count={position.count} activeIndex={position.index} />
        ) : null}
      </div>
    </div>
  );
}

import type { MotionValue } from "framer-motion";
import type { RecommendationCardDto, ConfidenceLabel } from "@/types/api";
import { TeamLogoPair } from "@/components/ui/TeamLogoPair";
import { StadiumBackground } from "./StadiumBackground";
import { HeroOverlay } from "./HeroOverlay";
import { LeagueBadge } from "./LeagueBadge";
import { ConfidenceRing } from "./ConfidenceRing";
import { HeroAICommentCard } from "./HeroAICommentCard";
import { HeroFollowButton } from "./HeroFollowButton";
import { SwipeHint } from "./SwipeHint";
import { MatchClock } from "./MatchClock";
import { homeName, awayName, leagueLabel } from "@/components/discover/cardSignals";
import { buildCardTeaserLines } from "@/components/discover/teaser";

/** confidenceLabel (backend enum) → TR etiket. */
const CONF_LABEL_TR: Record<ConfidenceLabel, string> = {
  HIGH: "YÜKSEK",
  MEDIUM: "ORTA",
  LOW: "DÜŞÜK",
};

/**
 * FORMAX · HeroCard (05) — sadeleştirilmiş, TAMAMEN gerçek veri (RecommendationCardDto).
 * Kaldırıldı: "1 Numaralı Maç" etiketi, oyuncu görselleri, yeşil/kırmızı renk katmanları.
 * Kalan: nötr stadyum + iki logo + isimler + lig + saat/geri sayım/canlı + AI Güven + AI yorumu.
 * Hiçbir alan uydurulmaz; yoksa (lig/yorum) ilgili parça gizlenir.
 */
export function HeroCard({
  card,
  onDetail,
  parallaxX,
}: {
  card: RecommendationCardDto;
  /** "Detaylı analizi gör" → Match Detail (AI bölümü açık). */
  onDetail?: () => void;
  /** Arka plan parallax offset'i (HeroSection'dan gelir). */
  parallaxX?: MotionValue<number>;
}) {
  const league = leagueLabel(card);
  // AI Güveni backend'den: AiTrustScore (0–100) öncelikli; yoksa geçici olarak confidenceScore.
  const conf = card.aiTrustScore ?? Math.round((card.confidenceScore ?? 0) * 100);
  const kickoff = card.kickoffTime ?? card.matchDate ?? null;
  // TEASER — KAYNAK: FEED YANITI (/api/home/recommendations → radarSummary +
  // radarHighlights). Kart, teaser için maç detayı ucunu ÇAĞIRMAZ.
  //
  // KİLİTLİ KARAR (16.08): Keşfet daha önce aktif kart adına /detail çağırıyordu;
  // o uç TEK istekte üç yüzeyi birden ürettiği için her kart görüntülemesi üç LLM
  // çağrısına mal oluyordu. Anlatı artık feed yanıtıyla gelir; maç detayı YALNIZ
  // kullanıcı gerçekten Maç Detay'ı açtığında çağrılır.
  //
  // Eski Decision/MatchReadingEngine teaser'ı da kaldırılmıştı (maç dışı takım adı,
  // tekrar eden cümle). Anlatı yoksa bölüm hiç render edilmez — uydurma metin YOK.
  const teaserLines = buildCardTeaserLines(card);

  return (
    <div className="relative overflow-hidden rounded-[var(--radius-section)] border border-white/[0.08] shadow-[var(--shadow-elevated)]">
      <StadiumBackground parallaxX={parallaxX} plain />
      <HeroOverlay />

      <div className="relative z-10 flex flex-col gap-5 p-5">
        {/* Üst — lig (varsa) + saat/geri sayım/canlı */}
        <div className="flex min-h-[20px] items-center justify-between gap-2">
          {league ? <LeagueBadge league={league} /> : <span />}
          <MatchClock kickoff={kickoff} isLive={card.isLive} liveMinute={card.liveMinute} />
        </div>

        {/* Merkez — iki logo + isimler + ortada AI Güven halkası */}
        <div className="flex flex-col items-center gap-3 pt-1">
          <TeamLogoPair
            home={{ name: homeName(card), logoUrl: card.homeTeam?.logoUrl }}
            away={{ name: awayName(card), logoUrl: card.awayTeam?.logoUrl }}
            size={46}
            showNames
            center={
              <ConfidenceRing
                value={conf}
                label={CONF_LABEL_TR[card.confidenceLabel] ?? "—"}
                size={104}
              />
            }
          />
        </div>

        {/* Canlı skor — yalnız canlı maçta, MatchLiveStats'tan gelen gerçek skor (yoksa gizli, uydurma yok). */}
        {card.isLive && card.homeScore != null && card.awayScore != null ? (
          <div className="-mt-1 flex items-center justify-center gap-3">
            <span className="text-[26px] font-black leading-none tabular-nums text-text-primary">
              {card.homeScore}
            </span>
            <span className="text-[18px] font-bold text-text-muted">-</span>
            <span className="text-[26px] font-black leading-none tabular-nums text-text-primary">
              {card.awayScore}
            </span>
          </div>
        ) : null}

        {/* AI Teaser — yalnız backend gerçek içgörü verdiyse (maks. 3 satır) */}
        {teaserLines.length > 0 ? (
          <HeroAICommentCard lines={teaserLines} onDetail={onDetail} />
        ) : null}

        {/* Kart footer aksiyonu — AI yorumundan BAĞIMSIZ, her maçta sağ altta.
            Takip bir maç kartı aksiyonudur; AI anlatısının varlığı görünürlüğünü
            etkilemez (bkz. HeroFollowButton). */}
        <div className="-mb-1 mt-auto flex justify-end">
          <HeroFollowButton matchId={card.matchId} />
        </div>

        <SwipeHint />
      </div>
    </div>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { PrimaryCtaButton } from "@/components/ui/PrimaryCtaButton";
import { LoadingCard } from "@/components/ui/LoadingState";
import { FlameIcon } from "@/components/discover/icons";
import { useRecommendations } from "@/hooks/useRecommendations";
import { trackInterest } from "@/lib/api/interests";
import { ComboLegItem } from "./ComboLegItem";
import { homeName, awayName, matchTime } from "@/components/discover/cardSignals";
import { selectComboLegs, comboTotalOdd } from "@/lib/combo/selectComboLegs";

/**
 * FORMAX · Günün AI Kombini — Keşfet kartı (saf View).
 * Kaynak: /api/home/recommendations. Backend'in gönderdiği sıradan İLK N maç
 * kombin ayağı olur (frontend sıralama/AI hesabı YAPMAZ). Her ayak TIKLANABİLİR
 * → Match Detail. "Kombini İncele" → yeni AI Kombin Analizi ekranı (/ai-combo).
 */
export function AIComboSection() {
  const router = useRouter();
  const { data, isLoading } = useRecommendations();

  // TEK SEÇİM NOKTASI — "Kombini İncele" ekranı (/ai-combo) da AYNI fonksiyonu çağırır,
  // böylece iki ekranın MatchId'leri birebir aynıdır. Market/olasılık/oran backend'in
  // Decision paketinden gelir (card.topPrediction); frontend hiçbirini hesaplamaz.
  const legs = selectComboLegs(data?.pages.flat());
  const totalOdd = comboTotalOdd(legs);

  if (isLoading) {
    return (
      <GlassCard sectionGlow className="p-4">
        <SectionHeader
          icon={<FlameIcon size={16} />}
          accent="purple"
          title="Günün AI Kombini"
          subtitle="FORMAX AI'nın bugünkü en güçlü kombini"
        />
        <div className="mt-2">
          <LoadingCard />
        </div>
      </GlassCard>
    );
  }

  if (legs.length < 2) return null;

  return (
    <GlassCard sectionGlow className="p-4">
      <SectionHeader
        icon={<FlameIcon size={16} />}
        accent="amber"
        title="Günün AI Kombini"
        subtitle="FORMAX AI'nın bugünkü en güçlü kombini"
        right={
          <span className="flex items-center gap-2 whitespace-nowrap text-[9px] font-bold uppercase tracking-wide text-text-muted">
            <span>{legs.length} Maç</span>
            {/* Toplam oran — yalnız TÜM ayakların gerçek oranı varsa hesaplanır. */}
            {totalOdd != null ? (
              <span className="text-neon">Toplam Oran {totalOdd.toFixed(2)}</span>
            ) : null}
          </span>
        }
      />

      {/* Dikey liste — her maç tek satır (Sprint #2 · madde 6). */}
      <div className="mt-1 flex flex-col gap-2">
        {legs.map((card) => (
          <ComboLegItem
            key={card.matchId}
            matchId={card.matchId}
            time={matchTime(card)?.text ?? ""}
            home={{ name: homeName(card), logoUrl: card.homeTeam?.logoUrl }}
            away={{ name: awayName(card), logoUrl: card.awayTeam?.logoUrl }}
            // AI sonucu + olasılık + GERÇEK oran: hepsi backend Decision paketinden.
            market={card.topPrediction?.market ?? null}
            probability={card.topPrediction?.probability ?? null}
            odd={card.topPrediction?.odd ?? null}
            // Kombin ayağı açılışı = ilgi sinyali (gerçek /api/interests/track).
            onOpen={() => trackInterest("click", card.matchId)}
          />
        ))}
      </div>

      <PrimaryCtaButton
        label="Kombini İncele"
        variant="ghost"
        className="mt-3.5"
        onClick={() => router.push("/ai-combo")}
      />
    </GlassCard>
  );
}

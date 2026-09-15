"use client";

import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { PrimaryCtaButton } from "@/components/ui/PrimaryCtaButton";
import { SparklesIcon, InfoIcon } from "@/components/discover/icons";
import { useMatchOutcomes } from "@/hooks/useMatchOutcomes";
import { predictionRepository } from "@/lib/predictions";
import { outcomeViewState } from "@/lib/outcomes/outcomeView";
import { OutcomeCards } from "@/components/outcomes/OutcomeCards";
import { homeName, awayName, leagueLabel } from "@/components/discover/cardSignals";
import type { RecommendationCardDto } from "@/types/api";
import type { OutcomeCandidateDto } from "@/types/outcomes";

interface Props {
  /** Aktif hero maçı (gerçek feed'den). */
  card: RecommendationCardDto;
  /** "Maçı Keşfet" → Match Detail. */
  onExplore?: () => void;
}

/**
 * FORMAX · AI Olası Sonuçlar (Keşfet).
 *
 * TEK KAYNAK (15.09.2026): arka planda üretilen olasılık snapshot'ı (GET /api/matches/{id}/outcomes) — Maç Detayı
 * AYNI snapshot'ı okur. Eski yol (ham yüzdeye göre ilk 3) çifte şansı hep öne çıkardığı için KALDIRILDI; frontend
 * sıralama/hesap yapmaz, oran göstermez.
 */
export function AIPredictionsSection({ card, onExplore }: Props) {
  const queryClient = useQueryClient();
  const { data, isLoading } = useMatchOutcomes(card.matchId);
  const view = outcomeViewState(data);
  const [selected, setSelected] = useState<string | null>(null);

  // Kullanıcının bu maç için kayıtlı seçimi (Tahminlerim ile senkron).
  useEffect(() => {
    let alive = true;
    predictionRepository.getAll().then((all) => {
      if (!alive) return;
      setSelected(all.find((p) => p.matchId === card.matchId)?.market ?? null);
    });
    return () => {
      alive = false;
    };
  }, [card.matchId]);

  // Seçim → Tahminlerim. Maç başına TEK kayıt; farklı market seçilirse güncellenir. Oran kaydedilmez.
  const choose = async (c: OutcomeCandidateDto) => {
    const result = await predictionRepository.setForMatch({
      matchId: card.matchId,
      market: c.market,
      selection: c.market,
      odds: null,
      homeTeam: { name: homeName(card), logoUrl: card.homeTeam?.logoUrl ?? null },
      awayTeam: { name: awayName(card), logoUrl: card.awayTeam?.logoUrl ?? null },
      league: leagueLabel(card) ?? undefined,
    });
    setSelected(result === "removed" ? null : c.market);
    queryClient.invalidateQueries({ queryKey: ["predictions"] });
  };

  return (
    <GlassCard sectionGlow className="p-5">
      <SectionHeader
        icon={<SparklesIcon size={16} />}
        accent="purple"
        title="AI Olası Sonuçlar"
        titleAfter={<InfoIcon size={13} className="text-text-muted" />}
      />

      {isLoading ? (
        <div className="mt-1 flex flex-col gap-2">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-[64px] animate-pulse rounded-xl bg-white/[0.04]" />
          ))}
        </div>
      ) : view.kind === "ready" ? (
        <div className="mt-1">
          <OutcomeCards
            snapshot={view.snapshot}
            selectedMarkets={selected ? new Set([selected]) : undefined}
            onSelect={(c) => void choose(c)}
          />
        </div>
      ) : (
        <p className="mt-3 text-[12px] leading-relaxed text-text-muted">{view.text}</p>
      )}

      <PrimaryCtaButton label="Maçı Keşfet" className="mt-5" onClick={onExplore} />
    </GlassCard>
  );
}

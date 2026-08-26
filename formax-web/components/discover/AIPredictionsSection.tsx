"use client";

import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { PrimaryCtaButton } from "@/components/ui/PrimaryCtaButton";
import { SparklesIcon, InfoIcon } from "@/components/discover/icons";
import { useMatchDecision } from "@/hooks/useMatchDecision";
import { predictionRepository } from "@/lib/predictions";
import { homeName, awayName, leagueLabel } from "@/components/discover/cardSignals";
import type { RecommendationCardDto, OddsMovement } from "@/types/api";

interface Props {
  /** Aktif hero maçı (gerçek feed'den). */
  card: RecommendationCardDto;
  /** "Maçı Keşfet" → Match Detail. */
  onExplore?: () => void;
}

/** Tek kutu görünüm modeli — tüm alanlar backend'den; frontend HESAP YAPMAZ. */
interface BoxVM {
  market: string;
  probability: number;
  currentOdd?: number;
  previousOdd?: number | null;
  movement?: OddsMovement;
}

/**
 * FORMAX · AI Olası Sonuçlar — yatay 3 seçilebilir premium kutu.
 *
 * Kaynak önceliği (frontend hesap/sıralama YAPMAZ, madde 10/11):
 *  1) Backend `card.predictions` (AiPredictionDto): market + probability + oran + hareket → FAZ 3/7 tam.
 *  2) Yoksa geçici olarak Match Detail `probabilities` (market + probability). Oran/hareket
 *     bu kaynakta olmadığından gösterilmez — uydurulmaz.
 * Backend sıralı gönderir; frontend yalnız ilk 3'ünü render eder ve seçimi (UI state) tutar.
 */
export function AIPredictionsSection({ card, onExplore }: Props) {
  const queryClient = useQueryClient();

  // TEK KAYNAK: Decision paketi (Maç Detay / AI İncele ile aynı cache).
  // Backend 16 market üretir; UI yalnız EN YÜKSEK 3'ünü gösterir.
  // Değerler backend'indir — frontend olasılık HESAPLAMAZ, oran ÜRETMEZ.
  const { data: decision, isLoading } = useMatchDecision(card.matchId);

  // GÜVEN KAPISI: backend'in kendi güven endeksi 0 ise motorun elinde girdi yoktur ve
  // olasılıklar her maçta aynı varsayılana düşer. Backend "bu sayıya güvenmiyorum" diyorsa
  // UI onu AI tahmini gibi göstermez. Bu bir hesap değil, backend beyanına uyum.
  const trusted = (decision?.confidence?.score ?? 0) > 0;

  const boxes: BoxVM[] = trusted
    ? [...(decision?.probabilities ?? [])]
        .sort((a, b) => b.probability - a.probability)
        .slice(0, 3)
        .map((p) => ({
          market: p.market,
          probability: p.probability,
          // Oran backend'den gelir; gelmezse kutuda oran satırı çıkmaz.
          currentOdd: p.odd ?? undefined,
          previousOdd: p.previousOdd ?? null,
        }))
    : [];

  const loading = isLoading;
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

  // Seçim → Tahminlerim. Maç başına TEK kayıt; farklı market seçilirse güncellenir.
  const choose = async (b: BoxVM) => {
    const result = await predictionRepository.setForMatch({
      matchId: card.matchId,
      market: b.market,
      selection: b.market,
      odds: b.currentOdd ?? null,
      homeTeam: { name: homeName(card), logoUrl: card.homeTeam?.logoUrl ?? null },
      awayTeam: { name: awayName(card), logoUrl: card.awayTeam?.logoUrl ?? null },
      league: leagueLabel(card) ?? undefined,
    });
    setSelected(result === "removed" ? null : b.market);
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

      {loading ? (
        <div className="mt-1 flex gap-2">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-[86px] flex-1 animate-pulse rounded-2xl bg-white/[0.04]" />
          ))}
        </div>
      ) : boxes.length === 0 ? (
        <p className="mt-3 text-[12px] leading-relaxed text-text-muted">
          Bu maç için AI olası sonuç verisi henüz yok.
        </p>
      ) : (
        <div className="mt-1 flex gap-2">
          {boxes.map((b) => (
            <PredictionBox
              key={b.market}
              box={b}
              active={selected === b.market}
              onSelect={() => void choose(b)}
            />
          ))}
        </div>
      )}

      <PrimaryCtaButton label="Maçı Keşfet" className="mt-5" onClick={onExplore} />
    </GlassCard>
  );
}

/** Movement rengi/ok'u — backend değerinden (frontend hesaplamaz). */
function movementView(movement?: OddsMovement) {
  if (movement === "Down") return { arrow: "↓", cls: "text-signal-red" };
  if (movement === "Up") return { arrow: "↑", cls: "text-neon" };
  return null;
}

function PredictionBox({
  box,
  active,
  onSelect,
}: {
  box: BoxVM;
  active: boolean;
  onSelect: () => void;
}) {
  const mv = movementView(box.movement);
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onSelect}
      className={`flex min-w-0 flex-1 flex-col items-center justify-start gap-1 rounded-2xl border px-2.5 py-3 text-center transition-all duration-200 ${
        active
          ? "scale-[1.03] border-neon bg-neon/[0.10] fx-glow-green"
          : "border-white/[0.07] bg-gradient-to-b from-white/[0.06] to-white/[0.015] ring-1 ring-inset ring-white/[0.03] active:scale-[0.98]"
      }`}
    >
      <span className={`text-[11px] font-bold leading-tight ${active ? "text-neon" : "text-text-primary"}`}>
        {box.market}
      </span>
      <span className={`text-[16px] font-black leading-none tabular-nums ${active ? "text-neon" : "text-text-primary"}`}>
        %{Math.round(box.probability)}
      </span>

      {/* Oran — yalnız backend gönderdiyse (frontend hesaplamaz) */}
      {box.currentOdd != null ? (
        <span className={`mt-0.5 text-[15px] font-extrabold leading-none tabular-nums ${active ? "text-white" : "text-neon"}`}>
          {box.currentOdd.toFixed(2)}
        </span>
      ) : null}

      {box.previousOdd != null || mv ? (
        <span className="flex items-center gap-1 leading-none">
          {box.previousOdd != null ? (
            <span className="text-[10px] font-semibold tabular-nums text-text-muted line-through opacity-40">
              {box.previousOdd.toFixed(2)}
            </span>
          ) : null}
          {mv ? <span className={`text-[11px] font-bold ${mv.cls}`}>{mv.arrow}</span> : null}
        </span>
      ) : null}
    </button>
  );
}

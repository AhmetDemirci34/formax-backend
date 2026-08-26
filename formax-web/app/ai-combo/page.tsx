"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useQueries, useQueryClient } from "@tanstack/react-query";
import { archivoNarrow } from "@/components/match-center/fonts";
import { useRecommendations } from "@/hooks/useRecommendations";
import { getMatchDetail } from "@/lib/api/matches";
import { homeName, awayName, matchTime, stripEmoji } from "@/components/discover/cardSignals";
import { selectComboLegs, comboTotalOdd } from "@/lib/combo/selectComboLegs";
import { predictionRepository, type PredictionInput } from "@/lib/predictions";
import { LoadingState } from "@/components/ui/LoadingState";
import { EmptyState } from "@/components/ui/EmptyState";
import { AIComboHero } from "@/components/ai-combo/AIComboHero";
import { AIComboMatchCard, type AIComboMatchVM } from "@/components/ai-combo/AIComboMatchCard";
import { AIComboBottomBar } from "@/components/ai-combo/AIComboBottomBar";
import { ShareSheet, type ShareLine } from "@/components/ai-combo/ShareSheet";

/**
 * FORMAX · AI Kombin Analizi ekranı (GOALAI Match Center kardeşi, scoped kimlik).
 *
 * AYAKLAR KEŞFET KARTIYLA AYNI: her iki ekran da `selectComboLegs()` çağırır ve aynı
 * `/api/home/recommendations` cache'ini okur → gösterilen MatchId'ler BİREBİR aynıdır.
 * Market/olasılık/oran backend'in Decision paketinden (card.topPrediction) gelir; bu ekran
 * kendi AI seçimini YAPMAZ. Aktif swipe kartı, hero maçı veya ikinci bir feed sorgusu
 * KULLANILMAZ. `/detail` yalnız "Neden Seçildi" metni (insight) için okunur.
 */
export default function AIComboAnalysisPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data, isLoading } = useRecommendations();
  const [saved, setSaved] = useState(false);
  const [shareOpen, setShareOpen] = useState(false);

  const legs = useMemo(() => selectComboLegs(data?.pages.flat()), [data]);

  // Ayak başına yalnız GEREKÇE metni — /api/matches/{id}/detail (insight.summary).
  // Market/olasılık/oran BURADAN OKUNMAZ; kombin kartıyla farklı değer çıkmasın diye
  // tek kaynak card.topPrediction'dır.
  const details = useQueries({
    queries: legs.map((l) => ({
      queryKey: ["match", l.matchId],
      queryFn: () => getMatchDetail(l.matchId),
      staleTime: 30_000,
    })),
  });

  const cards: AIComboMatchVM[] = legs.map((card, i) => {
    const d = details[i];
    return {
      matchId: card.matchId,
      order: i + 1,
      time: matchTime(card)?.text ?? "",
      league: d?.data?.league?.trim() || null,
      home: { name: homeName(card), logoUrl: card.homeTeam?.logoUrl },
      away: { name: awayName(card), logoUrl: card.awayTeam?.logoUrl },
      selectionLabel: card.topPrediction?.market ?? null,
      selectionProbability: card.topPrediction?.probability ?? null,
      selectionOdd: card.topPrediction?.odd ?? null,
      // "NEDEN ÖNE ÇIKIYOR?" — kaynak: insight.summary (mevcut davranış korundu).
      //
      // aiNarrative.whyThisMatch bu amaç için daha uygun görünüyor ("kullanıcı bu maça neden
      // baksın") ve Gemma çalıştığında somut cümle veriyor. ÖLÇÜLDÜ (17.08) ve KULLANILMADI:
      // maç 71515 ve 101525'te isAiGenerated=true olmasına RAĞMEN whyThisMatch deterministik
      // fallback cümlesini ("X tarafı bir adım önde görünse de dengenin belirleyici olması
      // bekleniyor") döndürüyor — yani bayrak alan bazında güvenilir değil ve bu kalıp
      // ürün dilinde yasaklı. Bayrağa güvenip geçiş yapmak kartlara o cümleyi taşıyordu.
      // Backend tarafındaki bu tutarsızlık RAPORLANDI; düzelene kadar somut ve güvenli olan
      // insight.summary kullanılır. Frontend hiçbir metin ÜRETMEZ.
      reasoning: d?.data?.insight?.summary?.trim() || null,
      badge: stripEmoji(card.storyHeadline)?.trim() || null,
      loading: d?.isLoading ?? false,
    };
  });

  function handleAdd() {
    const items: PredictionInput[] = legs
      .map((l, i) => {
        const pick = l.topPrediction;
        if (!pick) return null;
        return {
          matchId: l.matchId,
          market: pick.market,
          odds: pick.odd ?? null,
          homeTeam: { name: homeName(l), logoUrl: l.homeTeam?.logoUrl },
          awayTeam: { name: awayName(l), logoUrl: l.awayTeam?.logoUrl },
          league: details[i]?.data?.league,
        } as PredictionInput;
      })
      .filter((x): x is PredictionInput => x !== null);

    if (items.length === 0) return;
    void predictionRepository.add(items).then(() => {
      queryClient.invalidateQueries({ queryKey: ["predictions"] });
      setSaved(true);
    });
  }

  // Paylaşım önizlemesi EKRANDAKİ kartların ta kendisinden kurulur — ikinci bir veri
  // kaynağı, yeniden hesap veya biçim dışı dönüşüm yok.
  const shareLines: ShareLine[] = cards.map((vm) => ({
    matchId: vm.matchId,
    time: vm.time,
    home: vm.home.name,
    away: vm.away.name,
    market: vm.selectionLabel,
    probability: vm.selectionProbability,
    odd: vm.selectionOdd,
  }));

  // Toplam oran MEVCUT ürün mantığından gelir (comboTotalOdd — ayak oranlarının çarpımı,
  // ayaklardan biri oransızsa null). Burada yeni bahis matematiği KURULMAZ.
  const totalOdd = comboTotalOdd(legs);

  const shell = `relative flex h-[100dvh] flex-col overflow-hidden bg-bg-deep text-white ${archivoNarrow.variable} ${archivoNarrow.className}`;

  return (
    <div className={shell}>
      {/* Sticky Header */}
      <header className="z-50 flex h-16 shrink-0 items-center justify-between border-b border-white/10 bg-bg-deep/90 px-3 backdrop-blur-xl">
        <button
          type="button"
          onClick={() => router.back()}
          aria-label="Geri"
          className="flex h-10 w-10 items-center justify-center rounded-full text-white/90 transition-colors hover:bg-white/5 active:scale-95"
        >
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M15 18l-6-6 6-6" />
          </svg>
        </button>
        <h1 className="text-lg font-bold uppercase tracking-[0.16em] text-white">Bugünün Seçkisi</h1>
        <button
          type="button"
          onClick={() => setShareOpen(true)}
          aria-label="Paylaş"
          className="flex h-10 w-10 items-center justify-center rounded-full text-neon transition-colors hover:bg-white/5 active:scale-95"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" />
            <path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4" />
          </svg>
        </button>
      </header>

      {/* Scroll içerik */}
      <div className="min-h-0 flex-1 overflow-y-auto px-4 pt-4 pb-[calc(76px+max(var(--safe-bottom,16px),16px))] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {isLoading ? (
          <LoadingState />
        ) : legs.length < 2 ? (
          <EmptyState title="Kombin hazır değil" message="Bugün için yeterli maç bulunamadı." />
        ) : (
          <div className="space-y-4">
            <AIComboHero matchCount={legs.length} />
            <div className="space-y-4">
              {cards.map((vm) => (
                <AIComboMatchCard key={vm.matchId} vm={vm} onDetail={(id) => router.push(`/match/${id}`)} />
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Sabit alt aksiyon barı — "Tahminlerime Ekle" ve "Paylaş" KORUNUR (ürün işlevi). */}
      {legs.length >= 2 && (
        <AIComboBottomBar saved={saved} onAdd={handleAdd} onShare={() => setShareOpen(true)} />
      )}

      <ShareSheet
        open={shareOpen}
        onClose={() => setShareOpen(false)}
        lines={shareLines}
        totalOdd={totalOdd}
      />
    </div>
  );
}

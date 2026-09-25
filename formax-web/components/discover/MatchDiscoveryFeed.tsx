"use client";

import { useRouter } from "next/navigation";
import { HeroSection } from "@/components/discover/hero/HeroSection";
import { HeroEmptyState } from "@/components/discover/hero/HeroEmptyState";
import { AIPredictionsSection } from "@/components/discover/AIPredictionsSection";
import { useFeedQueue } from "@/hooks/useFeedQueue";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";

/**
 * FORMAX · MatchDiscoveryFeed (05) — GERÇEK Discovery Feed (saf View).
 *
 * Kaynak: useFeedQueue → /api/home/recommendations. SIRALAMA backend'e aittir; frontend
 * sıralama/AI hesabı YAPMAZ, backend sırasını render eder. Swipe/oklar backend sırasında
 * ilerler.
 *
 * KİLİTLİ KARAR: Keşfet YALNIZ henüz başlamamış (NotStarted/Scheduled) maçları gösterir.
 * Süzgeç okuma yolunda tek yerdedir (hooks/useRecommendations). Canlı/başlamış/bitmiş maç
 * bu ekrana hiç ulaşmaz; kartta canlı rozeti, dakika ve skor YOKTUR.
 *
 * KİLİTLİ KARAR: Keşfet'te AI YORUMU/anlatısı GÖSTERİLMEZ. Kart üstündeki rozet satırı
 * (DiscoverBadgeRow) karta taşındı; AI yorum kutusu (HeroAICommentCard) ve teaser
 * kaldırıldı. Yalnız sayısal göstergeler kalır (AI Beklentisi, RADAR, olasılıklar).
 */
export function MatchDiscoveryFeed() {
  const router = useRouter();
  const {
    activeCard,
    currentIndex,
    total,
    isLoading,
    isError,
    isEmpty,
    refetch,
    advance,
    recordDetailOpen,
  } = useFeedQueue();

  if (isLoading) return <LoadingState label="Maçlar yükleniyor..." />;
  if (isError)
    return (
      <ErrorState message="Maçlar şu an yüklenemiyor. Lütfen tekrar dene." onRetry={() => refetch()} />
    );
  if (isEmpty || !activeCard) return <HeroEmptyState />;

  // Maç Detayı YALNIZ görünür butondan açılır ("Maçı Keşfet"). Kart bağlantı değildir.
  const openDetail = () => {
    recordDetailOpen();
    router.push(`/match/${activeCard.matchId}`);
  };

  return (
    <div className="flex flex-col gap-2">
      <HeroSection
        card={activeCard}
        onSwipe={(dir) => advance(dir)}
        position={{ index: currentIndex, count: total }}
      />
      <AIPredictionsSection card={activeCard} onExplore={openDetail} />
    </div>
  );
}

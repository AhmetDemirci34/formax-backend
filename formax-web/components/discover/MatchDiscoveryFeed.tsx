"use client";

import { useRouter } from "next/navigation";
import { HeroSection } from "@/components/discover/hero/HeroSection";
import { HeroEmptyState } from "@/components/discover/hero/HeroEmptyState";
import { AIPredictionsSection } from "@/components/discover/AIPredictionsSection";
import { DiscoverBadgeRow } from "@/components/discover/DiscoverBadgeRow";
import { useFeedQueue } from "@/hooks/useFeedQueue";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";

/**
 * FORMAX · MatchDiscoveryFeed (05) — GERÇEK Discovery Feed (saf View).
 *
 * Kaynak: useFeedQueue → /api/home/recommendations. SIRALAMA backend'e aittir; frontend
 * sıralama/AI hesabı YAPMAZ, backend sırasını render eder (madde 1/10). Swipe backend
 * sırasında ilerler (madde 9).
 *
 * MVP KİLİTLİ KARAR: Discover YALNIZ henüz başlamamış (NotStarted) maçları gösterir ve bunun
 * tek yetkili filtresi BACKEND'dedir. Frontend hiçbir durum filtresi UYGULAMAZ ve maç durumu
 * ÜRETMEZ — backend'den gelen listeyi olduğu gibi render eder. Boş durumda HeroEmptyState.
 */
export function MatchDiscoveryFeed() {
  const router = useRouter();
  const { activeCard, isLoading, isError, isEmpty, refetch, advance, recordDetailOpen } =
    useFeedQueue();

  if (isLoading) return <LoadingState label="Maçlar yükleniyor..." />;
  if (isError)
    return (
      <ErrorState message="Maçlar şu an yüklenemiyor. Lütfen tekrar dene." onRetry={() => refetch()} />
    );
  if (isEmpty || !activeCard) return <HeroEmptyState />;

  const openDetail = (aiFocus = false) => {
    recordDetailOpen();
    router.push(`/match/${activeCard.matchId}${aiFocus ? "?section=ai" : ""}`);
  };

  return (
    <div className="flex flex-col gap-2">
      {/* #8 "Neden bu maçı görüyorum" — mevcut backend alanlarından (recommendationReason) rozet. */}
      <DiscoverBadgeRow card={activeCard} />
      <HeroSection card={activeCard} onSwipe={(dir) => advance(dir)} onOpen={() => openDetail(true)} />
      <AIPredictionsSection card={activeCard} onExplore={() => openDetail(false)} />
    </div>
  );
}

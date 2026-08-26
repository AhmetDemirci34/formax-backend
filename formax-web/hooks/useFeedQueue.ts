"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useRecommendations } from "./useRecommendations";
import { postSwipe } from "@/lib/api/swipe";
import { trackInterest } from "@/lib/api/interests";
import { homeName } from "@/components/discover/cardSignals";
import type { RecommendationCardDto } from "@/types/api";
import type { SwipeDirection } from "@/types/api";

const PREFETCH_THRESHOLD = 3;

/**
 * DISCOVER HERO / SWIPE UFKU — ürün kararı: Discover "şimdi/çok yakında keşfetmeye değer ne
 * var?" sorusudur; haftalık takvim "Maçlar" ekranının işidir. Bugün + 4 takvim günü.
 *
 * Bu bir frontend filtresi DEĞİLDİR: değer backend'e parametre olarak gider, aday havuzu
 * BACKEND'de daraltılır. Burada tarih karşılaştırması, gizleme veya sıralama yapılmaz.
 * Yalnız bu kuyruk ufuklu çağırır — Sana Özel, Günün AI Kombini, /tumu ve Trending
 * ufuksuz çağırdıkları için geniş evrenlerini korur.
 */
const HERO_HORIZON_DAYS = 4;

export function useFeedQueue(filter?: (c: RecommendationCardDto) => boolean) {
  const { data, isLoading, isError, refetch, fetchNextPage, hasNextPage } =
    useRecommendations(HERO_HORIZON_DAYS);

  const all: RecommendationCardDto[] = data?.pages.flat() ?? [];
  const cards: RecommendationCardDto[] = filter ? all.filter(filter) : all;

  const [currentIndex, setCurrentIndex] = useState(0);
  // 0 ile başlar, kartlar gelince (aşağıdaki effect) Date.now() ile ayarlanır — render saf kalır.
  const cardStartTimeRef = useRef<number>(0);
  const pausedAtRef = useRef<number | null>(null);
  const accumulatedPauseRef = useRef<number>(0);

  // Reset index only when the first page arrives (new session)
  const hasCards = cards.length > 0;
  const hasResetRef = useRef(false);
  useEffect(() => {
    if (hasCards && !hasResetRef.current) {
      hasResetRef.current = true;
      cardStartTimeRef.current = Date.now();
    }
  }, [hasCards]);

  // Pause view-duration timer when tab is hidden
  useEffect(() => {
    function onVisibilityChange() {
      if (document.hidden) {
        pausedAtRef.current = Date.now();
      } else if (pausedAtRef.current != null) {
        accumulatedPauseRef.current += Date.now() - pausedAtRef.current;
        pausedAtRef.current = null;
      }
    }
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => document.removeEventListener("visibilitychange", onVisibilityChange);
  }, []);

  // Prefetch next page when queue is running low
  useEffect(() => {
    if (hasNextPage && cards.length - currentIndex <= PREFETCH_THRESHOLD) {
      fetchNextPage();
    }
  }, [currentIndex, cards.length, hasNextPage, fetchNextPage]);

  // Son karttan sonra feed ASLA kilitlenmesin: kartlar tükendiyse (gösterilecek kart var,
  // yeni sayfa yok) ~3 sn "Gösterilecek maç yok" gösterilir, sonra İLK karta dönülür.
  // Tek seferlik: reset sonrası tükenmiş değil → timer temizlenir (infinite loop / duplicate yok).
  useEffect(() => {
    const exhausted = currentIndex >= cards.length;
    if (!exhausted || cards.length === 0 || hasNextPage) return;
    const t = setTimeout(() => setCurrentIndex(0), 3000);
    return () => clearTimeout(t);
  }, [currentIndex, cards.length, hasNextPage]);

  // Hero görüntülenme sinyali: aktif kart Hero'da göründüğünde bir kez "view" event'i
  // (mevcut Interest sistemi MatchView'ı destekler; yeni tip üretilmez). Kart başına tek
  // kez — trackInterest fire-and-forget + anonim guard'lıdır.
  const viewedRef = useRef<Set<number>>(new Set());
  const activeMatchId = cards[currentIndex]?.matchId;
  useEffect(() => {
    if (!activeMatchId || viewedRef.current.has(activeMatchId)) return;
    viewedRef.current.add(activeMatchId);
    trackInterest("view", activeMatchId);
  }, [activeMatchId]);

  const advance = useCallback(
    (direction?: SwipeDirection) => {
      const card = cards[currentIndex];
      if (!card) return;

      const now = Date.now();
      const viewDurationMs =
        now - cardStartTimeRef.current - accumulatedPauseRef.current;

      postSwipe({
        matchId: card.matchId,
        action: direction === "left" ? "skip" : "like",
        swipeDirection: direction,
        viewDurationMs: Math.max(0, viewDurationMs),
        confidenceLabel: card.confidenceLabel,
        isTrending: card.momentumScore > 60,
        oddsDrop: card.spikeScore > 0 ? 1 : 0,
        // Backend UserAction.Team tek alanlıdır; maçın temsili takımı olarak ev sahibi
        // gönderilir (UserTrendService team-boost'unu besler). İki-takım kapsamı ayrıca
        // trackInterest → UserInterestScores yolundan (home+away) yazılır.
        team: homeName(card),
      }).catch(() => {/* fire & forget */});

      // R.14.8 — yalnız pozitif sinyal: swipe-right = ilgi (click). Skip track edilmez.
      if (direction === "right") {
        trackInterest("click", card.matchId);
      }

      setCurrentIndex((i) => i + 1);
      cardStartTimeRef.current = Date.now();
      accumulatedPauseRef.current = 0;
      pausedAtRef.current = null;
    },
    [cards, currentIndex],
  );

  const recordDetailOpen = useCallback(() => {
    const card = cards[currentIndex];
    if (!card) return;
    postSwipe({
      matchId: card.matchId,
      action: "detail",
      viewDurationMs: Math.max(0, Date.now() - cardStartTimeRef.current - accumulatedPauseRef.current),
      team: homeName(card),
    }).catch(() => {});

    // R.14.8 — detay açma = güçlü ilgi (open).
    trackInterest("open", card.matchId);
  }, [cards, currentIndex]);

  const activeCard = cards[currentIndex] ?? null;
  const nextCard = cards[currentIndex + 1] ?? null;
  const leftPeek = cards[currentIndex - 1] ?? null;
  const rightPeek = cards[currentIndex + 1] ?? null;
  const isExhausted = currentIndex >= cards.length;

  return {
    activeCard,
    nextCard,
    leftPeek,
    rightPeek,
    isLoading,
    isError,
    isEmpty: !isError && !isLoading && isExhausted && !hasNextPage,
    refetch,
    advance,
    recordDetailOpen,
  };
}

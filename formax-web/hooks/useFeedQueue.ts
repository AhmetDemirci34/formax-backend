"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useRecommendations } from "./useRecommendations";
import { postSwipe } from "@/lib/api/swipe";
import { trackInterest } from "@/lib/api/interests";
import type { RecommendationCardDto } from "@/types/api";
import type { SwipeDirection } from "@/types/api";

const PREFETCH_THRESHOLD = 3;

export function useFeedQueue() {
  const { data, isLoading, isError, refetch, fetchNextPage, hasNextPage } =
    useRecommendations();

  const cards: RecommendationCardDto[] = data?.pages.flat() ?? [];

  const [currentIndex, setCurrentIndex] = useState(0);
  const cardStartTimeRef = useRef<number>(Date.now());
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

"use client";

import { useEffect, useRef, useState } from "react";
import { useRecommendations } from "@/hooks/useRecommendations";
import { TrendingMatchCard } from "./TrendingMatchCard";

// Mouse (web) için pointer drag-to-scroll. Dokunmada native scroll + CSS snap/momentum kullanılır.
// draggedRef: sürükleme sonrası yanlış navigasyonu engellemek için (kart onClick guard).
function useDragScroll() {
  const ref = useRef<HTMLDivElement>(null);
  const draggedRef = useRef(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    let down = false;
    let startX = 0;
    let startLeft = 0;

    const onDown = (e: PointerEvent) => {
      if (e.pointerType !== "mouse") return; // dokunma native scroll'a bırakılır
      down = true;
      draggedRef.current = false;
      startX = e.pageX;
      startLeft = el.scrollLeft;
      el.classList.add("cursor-grabbing");
    };
    const onMove = (e: PointerEvent) => {
      if (!down) return;
      const dx = e.pageX - startX;
      if (Math.abs(dx) > 4) draggedRef.current = true;
      el.scrollLeft = startLeft - dx;
    };
    const onUp = () => {
      down = false;

      el.classList.remove("cursor-grabbing");

      // click event'i pointerup'tan hemen sonra → guard'ı kısa süre tut.
      setTimeout(() => {
        draggedRef.current = false;
      }, 60);
    };

    el.addEventListener("pointerdown", onDown);
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    return () => {
      el.removeEventListener("pointerdown", onDown);
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
    };
  }, []);

  return { ref, draggedRef };
}

// "Gündemdeki Maçlar" — yatay swipe carousel (Netflix/Spotify): snap + momentum + mouse drag.
// Hero ile yarışmaz; destek bölümü. Mevcut öneri listesinden (yeni veri yok).
export function TrendingMatches({ excludeMatchId }: { excludeMatchId?: number }) {
  const { data } = useRecommendations();
  const { ref, draggedRef } = useDragScroll();
                           
  const cards = (data?.pages.flat() ?? [])
    .filter((c) => c.matchId !== excludeMatchId)
    .slice(0, 12);

  if (cards.length === 0) return null;

  return (
    <section>
      <h2 className="mb-2.5 px-0.5 text-[11px] font-semibold uppercase tracking-[0.15em] text-text-muted">
        Gündemdeki Maçlar
      </h2>
      <div
      ref={ref}
      className="
        -mx-5
        flex
        gap-3
        overflow-x-auto

        px-5
        pb-2

        cursor-grab
        active:cursor-grabbing

        snap-x
        snap-mandatory

        scroll-smooth

        overscroll-x-contain
        touch-pan-x

        select-none

        scroll-px-5

        [scrollbar-width:none]
        [&::-webkit-scrollbar]:hidden
        [&_img]:pointer-events-none
      "
      >
      
      
      
        {cards.map((c) => (
          <TrendingMatchCard key={c.matchId} card={c} dragGuard={draggedRef} />
        ))}
      </div>
    </section>
  );
}

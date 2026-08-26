"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { LoadingCard } from "@/components/ui/LoadingState";
import { HeartIcon, InfoIcon, ArrowRightIcon, ChevronRightIcon } from "@/components/discover/icons";
import { FeaturedMatchCard } from "./FeaturedMatchCard";
import { useRecommendations } from "@/hooks/useRecommendations";
import { trackInterest } from "@/lib/api/interests";
import { homeName, awayName, matchTime, isDiscoverable } from "@/components/discover/cardSignals";

/**
 * FORMAX · Sana Özel Maçlar — GERÇEK kişisel öneri feed'inden (useRecommendations).
 * Her kart TIKLANABİLİR (gerçek MatchId → Match Detail). "Tüm önerileri gör" → Maçlar.
 * Yatay scroll + kenar okları korunur. Mock/fake yok; veri yoksa bölüm gizlenir (uydurmaz).
 */
export function FeaturedMatchesSection({ compact = false }: { compact?: boolean } = {}) {
  const { data, isLoading } = useRecommendations();
  // Sıralama backend'e ait; yalnız GEÇİCİ güvenlik katmanı (bitmiş maç Discovery'de görünmesin).
  const cards = (data?.pages.flat() ?? []).filter(isDiscoverable);

  const scrollRef = useRef<HTMLDivElement>(null);
  const [canLeft, setCanLeft] = useState(false);
  const [canRight, setCanRight] = useState(false);

  const update = () => {
    const el = scrollRef.current;
    if (!el) return;
    setCanLeft(el.scrollLeft > 4);
    setCanRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 4);
  };

  useEffect(() => {
    update();
  }, [cards.length]);

  const scrollByDir = (dir: 1 | -1) => {
    scrollRef.current?.scrollBy({ left: dir * 168, behavior: "smooth" });
  };

  if (isLoading) {
    return (
      <GlassCard sectionGlow className="p-4">
        <SectionHeader
          icon={<HeartIcon size={16} />}
          accent="purple"
          title="Sana Özel Maçlar"
          subtitle="İlgi alanlarına göre senin için seçtik"
        />
        <div className="mt-2">
          <LoadingCard />
        </div>
      </GlassCard>
    );
  }

  if (cards.length === 0) return null;

  return (
    <GlassCard sectionGlow className={compact ? "p-2.5" : "p-4"}>
      <SectionHeader
        icon={<HeartIcon size={16} />}
        accent="purple"
        title="Sana Özel Maçlar"
        subtitle="İlgi alanlarına göre senin için seçtik"
        right={
          <span className="inline-flex items-center gap-1 whitespace-nowrap text-[11px] font-medium text-text-muted">
            Neden bu maçlar?
            <InfoIcon size={13} />
          </span>
        }
      />

      <div className="relative -mx-4 mt-1">
        <div
          ref={scrollRef}
          onScroll={update}
          className="flex items-stretch gap-2 overflow-x-auto px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
        >
          {cards.map((card) => (
            <FeaturedMatchCard
              key={card.matchId}
              matchId={card.matchId}
              time={matchTime(card)?.text ?? ""}
              home={{ name: homeName(card), logoUrl: card.homeTeam?.logoUrl }}
              away={{ name: awayName(card), logoUrl: card.awayTeam?.logoUrl }}
              // Backend TopPrediction gönderirse gösterilir; yoksa alt satır render edilmez.
              // (Backend, güven endeksi 0 olan maçta bu alanı hiç göndermez.)
              topPrediction={card.topPrediction ?? null}
              // Kartın alt satırı = AYNI marketin GERÇEK oranı (backend TopPrediction.Odd).
              // AI güven yüzdesi (aiTrustScore) buraya BASILMAZ: kartta zaten olasılık
              // yüzdesi var, ikinci bir yüzde oran sanılıyordu. Oran yoksa satır çıkmaz.
              odd={card.topPrediction?.odd ?? null}
              compact={compact}
              // Öne çıkan maç açılışı = ilgi sinyali (gerçek /api/interests/track).
              onOpen={() => trackInterest("click", card.matchId)}
            />
          ))}
        </div>

        {canLeft ? (
          <button
            type="button"
            aria-label="Öncekiler"
            onClick={() => scrollByDir(-1)}
            className="absolute left-1 top-1/2 grid h-8 w-8 -translate-y-1/2 place-items-center rounded-full border border-white/10 bg-bg-deep/80 text-text-primary shadow-[0_4px_16px_rgba(0,0,0,0.5)] backdrop-blur-sm transition hover:bg-bg-deep active:scale-95"
          >
            <span className="rotate-180">
              <ChevronRightIcon size={16} />
            </span>
          </button>
        ) : null}

        {canRight ? (
          <button
            type="button"
            aria-label="Sonrakiler"
            onClick={() => scrollByDir(1)}
            className="absolute right-1 top-1/2 grid h-8 w-8 -translate-y-1/2 place-items-center rounded-full border border-white/10 bg-bg-deep/80 text-text-primary shadow-[0_4px_16px_rgba(0,0,0,0.5)] backdrop-blur-sm transition hover:bg-bg-deep active:scale-95"
          >
            <ChevronRightIcon size={16} />
          </button>
        ) : null}
      </div>

      <Link
        href="/maclar"
        className="relative mt-3.5 flex w-full items-center justify-center rounded-2xl border border-white/[0.06] bg-white/[0.02] py-3 transition-colors hover:bg-white/[0.04]"
      >
        <span className="text-[13px] font-semibold text-text-secondary">Tüm önerileri gör</span>
        <span className="absolute right-4 text-text-secondary">
          <ArrowRightIcon size={15} />
        </span>
      </Link>
    </GlassCard>
  );
}

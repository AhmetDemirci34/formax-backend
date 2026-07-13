"use client";

import { useEffect, useRef, useState } from "react";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { HeartIcon, InfoIcon, ArrowRightIcon, ChevronRightIcon } from "@/components/discover/icons";
import { FeaturedMatchCard } from "./FeaturedMatchCard";
import { FEATURED_DEMO, type FeaturedMatchVM } from "./featuredData";

/**
 * FORMAX · Sana Özel Maçlar (10, new)
 * Referans bloğu: kalp başlığı + "Neden bu maçlar?" → yatay öne-çıkan maç kartları
 * → "Tüm önerileri gör" CTA. Kartlar sabit genişlik + yatay scroll (swipe korunur).
 * Scroll pozisyonuna göre kenar okları: sağda maç varsa sağ ok, sola varsa sol ok.
 * Oklar tıklanınca da kaydırır; swipe/dokunmatik davranışı değişmez.
 */
export function FeaturedMatchesSection({
  matches = FEATURED_DEMO,
}: {
  matches?: FeaturedMatchVM[];
}) {
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
  }, [matches]);

  const scrollByDir = (dir: 1 | -1) => {
    scrollRef.current?.scrollBy({ left: dir * 168, behavior: "smooth" });
  };

  return (
    <GlassCard sectionGlow className="p-4">
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
          {matches.map((m) => (
            <FeaturedMatchCard key={m.id} {...m} />
          ))}
        </div>

        {/* Sol ok — yalnızca solda görülmemiş maç varsa */}
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

        {/* Sağ ok — yalnızca sağda görülmemiş maç varsa */}
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

      <button
        type="button"
        className="relative mt-3.5 flex w-full items-center justify-center rounded-2xl border border-white/[0.06] bg-white/[0.02] py-3 transition-colors hover:bg-white/[0.04]"
      >
        <span className="text-[13px] font-semibold text-text-secondary">Tüm önerileri gör</span>
        <span className="absolute right-4 text-text-secondary">
          <ArrowRightIcon size={15} />
        </span>
      </button>
    </GlassCard>
  );
}

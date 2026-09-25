interface Props {
  count: number;
  activeIndex: number;
  /** Ekranda gösterilecek en fazla nokta sayısı (kaydırmalı pencere). */
  max?: number;
}

/**
 * FORMAX · CarouselIndicator (05)
 * Kart altındaki konum göstergesi (aktif = neon, uzun). Feed uzun olabildiği için
 * noktalar aktif kartın etrafında kayan bir pencerede gösterilir.
 */
export function CarouselIndicator({ count, activeIndex, max = 5 }: Props) {
  const window = Math.min(max, count);
  // Aktif kart pencerenin ortasında kalsın; başta/sonda pencere kenara yaslanır.
  const start = Math.max(0, Math.min(activeIndex - Math.floor(window / 2), count - window));

  return (
    <div className="flex items-center justify-center gap-1.5" aria-hidden>
      {Array.from({ length: window }).map((_, i) => {
        const idx = start + i;
        const active = idx === activeIndex;
        return (
          <span
            key={idx}
            className={`h-1.5 rounded-full transition-all ${
              active ? "w-5 bg-neon" : "w-1.5 bg-white/20"
            }`}
          />
        );
      })}
    </div>
  );
}

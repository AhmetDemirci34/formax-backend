interface Props {
  count: number;
  activeIndex: number;
}

/**
 * FORMAX · CarouselIndicator (05)
 * PNG hero altındaki nokta göstergesi (aktif = neon, uzun).
 */
export function CarouselIndicator({ count, activeIndex }: Props) {
  return (
    <div className="flex items-center justify-center gap-1.5">
      {Array.from({ length: count }).map((_, i) => {
        const active = i === activeIndex;
        return (
          <span
            key={i}
            className={`h-1.5 rounded-full transition-all ${
              active ? "w-5 bg-neon" : "w-1.5 bg-white/20"
            }`}
          />
        );
      })}
    </div>
  );
}

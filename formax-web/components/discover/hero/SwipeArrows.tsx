"use client";

import { motion, useTransform, type MotionValue } from "framer-motion";

interface Props {
  /** Kartın yatay sürüklenme değeri — oklar sürükleme yönünde belirginleşir. */
  x: MotionValue<number>;
  /** Sol ok — bir önceki/sonraki karta geçiş (kart değiştirir, BAŞKA hiçbir şey yapmaz). */
  onLeft: () => void;
  /** Sağ ok — kart değiştirir. */
  onRight: () => void;
}

/**
 * FORMAX · SwipeArrows — kartın SOLUNDA ve SAĞINDA duran KART GEÇİŞ okları.
 *
 * Artık dekor değil GERÇEK BUTONDUR: tıklama kartı değiştirir (swipe ile aynı akış).
 * Maç Detayı AÇMAZ, takip etmez — tek işi kart geçişidir. Kartın üzerine binmez,
 * kenarda durur; kartın boş alanı tıklanabilir değildir.
 */
export function SwipeArrows({ x, onLeft, onRight }: Props) {
  const leftOpacity = useTransform(x, [-120, -20, 0], [0.95, 0.55, 0.35]);
  const rightOpacity = useTransform(x, [0, 20, 120], [0.35, 0.55, 0.95]);

  return (
    <>
      <Arrow side="left" opacity={leftOpacity} onClick={onLeft} />
      <Arrow side="right" opacity={rightOpacity} onClick={onRight} />
    </>
  );
}

function Arrow({
  side,
  opacity,
  onClick,
}: {
  side: "left" | "right";
  opacity: MotionValue<number>;
  onClick: () => void;
}) {
  const isLeft = side === "left";
  return (
    <motion.button
      type="button"
      aria-label={isLeft ? "Önceki kart" : "Sonraki kart"}
      onClick={onClick}
      style={{ opacity }}
      whileTap={{ scale: 0.88 }}
      className={`absolute top-1/2 z-20 grid h-10 w-8 -translate-y-1/2 place-items-center text-text-secondary transition-colors hover:text-text-primary ${
        isLeft ? "left-0" : "right-0"
      }`}
    >
      <svg width="14" height="22" viewBox="0 0 14 22" fill="none" aria-hidden>
        <path
          d={isLeft ? "M10 3 L3 11 L10 19" : "M4 3 L11 11 L4 19"}
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    </motion.button>
  );
}

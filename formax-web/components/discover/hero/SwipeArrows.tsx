"use client";

import { motion, useTransform, type MotionValue } from "framer-motion";

interface Props {
  /** Kartın yatay sürüklenme değeri — oklar sürükleme yönünde belirginleşir. */
  x: MotionValue<number>;
}

/**
 * FORMAX · SwipeArrows — kartın SOLUNDA ve SAĞINDA duran kaydırma yönü göstergesi.
 *
 * Amaç tek: "bu kart sağa/sola kaydırılabilir" mesajını vermek. Yeni tasarım dili
 * kurulmaz — mevcut renk değişkenleri (text-muted / text-secondary) ve mevcut radius
 * kullanılır. Kart üzerine binmez, kenarda durur ve tıklamayı engellemez.
 *
 * Hareket: sürekli dikkat dağıtan animasyon YOK. Yalnız çok yavaş, düşük genlikli bir
 * nefes; kullanıcı kartı sürüklerken o yöndeki ok belirginleşir (yönü doğrular).
 */
export function SwipeArrows({ x }: Props) {
  const leftOpacity = useTransform(x, [-120, -20, 0], [0.85, 0.4, 0.22]);
  const rightOpacity = useTransform(x, [0, 20, 120], [0.22, 0.4, 0.85]);

  return (
    <>
      <Arrow side="left" opacity={leftOpacity} />
      <Arrow side="right" opacity={rightOpacity} />
    </>
  );
}

function Arrow({ side, opacity }: { side: "left" | "right"; opacity: MotionValue<number> }) {
  const isLeft = side === "left";
  return (
    <motion.span
      aria-hidden
      style={{ opacity }}
      animate={{ x: isLeft ? [0, -2.5, 0] : [0, 2.5, 0] }}
      transition={{ duration: 2.4, repeat: Infinity, ease: "easeInOut" }}
      className={`pointer-events-none absolute top-1/2 z-10 -translate-y-1/2 text-text-secondary ${
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
    </motion.span>
  );
}

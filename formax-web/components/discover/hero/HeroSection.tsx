"use client";

import { useRef, useState } from "react";
import { motion, useMotionValue, useTransform, animate, type PanInfo } from "framer-motion";
import { HeroCard } from "./HeroCard";
import { MATCHES, type HeroMatchVM } from "./heroData";

const wrap = (i: number, len: number) => ((i % len) + len) % len;

interface Props {
  /** Feed maçları — her kart ayrı maç (backend: HeroSelectionEngine). */
  matches?: HeroMatchVM[];
  /** Aktif maç değişince (AI Olası Sonuçlar gibi bağlı bölümleri senkronlar). */
  onIndexChange?: (index: number) => void;
}

/**
 * FORMAX · HeroSection (05) — AI Match Discovery Feed.
 * Ana kart parmağa bağlı swipe edilir (spring, hafif rotate/scale). Threshold geçilince
 * eski kart o yöne fırlar, yeni kart karşı taraftan spring ile gelir. İçerik tümüyle o
 * maça ait HeroMatchVM'den beslenir; eski maçtan hiçbir veri kalmaz.
 */
export function HeroSection({ matches = MATCHES, onIndexChange }: Props) {
  const [index, setIndex] = useState(0);
  const x = useMotionValue(0);
  const rotate = useTransform(x, [-240, 240], [-7, 7]);
  const scale = useTransform(x, [-240, 0, 240], [0.94, 1, 0.94]);
  const animating = useRef(false);
  const canSwipe = matches.length > 1;

  const commit = (i: number) => {
    setIndex(i);
    onIndexChange?.(i);
  };

  // flyDir: kartın uçtuğu yön (-1 sol, +1 sağ). step: index değişimi (+1 sonraki, -1 önceki).
  const go = (flyDir: 1 | -1, step: 1 | -1) => {
    if (animating.current || !canSwipe) {
      animate(x, 0, { type: "spring", stiffness: 400, damping: 34 });
      return;
    }
    animating.current = true;
    const w = 400;
    animate(x, flyDir * w, {
      type: "spring",
      stiffness: 260,
      damping: 30,
      onComplete: () => {
        commit(wrap(index + step, matches.length));
        x.set(-flyDir * w);
        animate(x, 0, {
          type: "spring",
          stiffness: 260,
          damping: 28,
          onComplete: () => {
            animating.current = false;
          },
        });
      },
    });
  };

  const onDragEnd = (_e: unknown, info: PanInfo) => {
    const threshold = 80;
    if (info.offset.x <= -threshold || info.velocity.x < -600) go(-1, 1); // sola → sonraki
    else if (info.offset.x >= threshold || info.velocity.x > 600) go(1, -1); // sağa → önceki
    else animate(x, 0, { type: "spring", stiffness: 400, damping: 34 });
  };

  return (
    <section className="flex flex-col">
      <motion.div
        drag={canSwipe ? "x" : false}
        dragConstraints={{ left: 0, right: 0 }}
        dragElastic={0.7}
        onDragEnd={onDragEnd}
        style={{ x, rotate, scale }}
        className="touch-pan-y select-none"
      >
        <HeroCard match={matches[index]} />
      </motion.div>
    </section>
  );
}

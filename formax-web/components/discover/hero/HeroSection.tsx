"use client";

import { useEffect, useRef } from "react";
import { motion, useMotionValue, useTransform, animate, type PanInfo } from "framer-motion";
import { HeroCard } from "./HeroCard";
import { SwipeArrows } from "./SwipeArrows";
import type { RecommendationCardDto, SwipeDirection } from "@/types/api";

interface Props {
  /** Aktif maç (gerçek feed'den — useFeedQueue). */
  card: RecommendationCardDto;
  /** Swipe tamamlanınca feed'i ilerletir (dedup: aynı maç tekrar gelmez). */
  onSwipe: (dir: SwipeDirection) => void;
  /** "Detaylı analizi gör" → Match Detail. */
  onOpen?: () => void;
}

/**
 * FORMAX · HeroSection (05) — gerçek AI Discovery Feed kartı.
 * Kart parmağa bağlı swipe edilir (premium spring + hafif rotate/scale + gölge + parallax).
 * Threshold geçilince kart o yöne TAM çıkar, feed ilerler (sola=skip, sağa=like), yeni
 * kart karşı taraftan girer. Kaynak tamamen backend (RecommendationCardDto); mock yok.
 */
export function HeroSection({ card, onSwipe, onOpen }: Props) {
  const x = useMotionValue(0);
  const rotate = useTransform(x, [-240, 240], [-3, 3]);
  const scale = useTransform(x, [-240, 0, 240], [0.98, 1, 0.98]);
  const boxShadow = useTransform(
    x,
    [-160, 0, 160],
    ["0 30px 70px rgba(0,0,0,.55)", "0 0px 0px rgba(0,0,0,0)", "0 30px 70px rgba(0,0,0,.55)"]
  );
  const bgParallax = useTransform(x, [-140, 0, 140], [12, 0, -12], { clamp: true });
  const animating = useRef(false);

  // GESTURE LIFECYCLE FIX: Yeni kart geldiğinde (matchId değişince) drag'i GARANTİ
  // yeniden aktive et. Son kart atılınca HeroCard boş duruma geçerken slide-in
  // animate(x,0) iptal olur → onComplete çalışmaz → animating.current true takılı kalır
  // ve reuse edilen instance'ta yeni kartın swipe'ını bloke eder. Kart değişiminde
  // sıfırlamak, animasyonun nasıl bittiğinden bağımsız olarak drag'i her kartta açar.
  // (Fly-out sırasında matchId aynı kaldığından mevcut slide-in animasyonu bozulmaz;
  //  x'e DOKUNULMAZ — sadece gesture guard sıfırlanır.)
  useEffect(() => {
    animating.current = false;
  }, [card.matchId]);

  const haptic = () => {
    if (
      typeof window !== "undefined" &&
      window.matchMedia?.("(pointer: coarse)").matches &&
      typeof navigator !== "undefined" &&
      "vibrate" in navigator
    ) {
      navigator.vibrate(8);
    }
  };

  const go = (flyDir: 1 | -1, dir: SwipeDirection) => {
    if (animating.current) return;
    animating.current = true;
    const w = 400;
    animate(x, flyDir * w, {
      type: "spring",
      stiffness: 500,
      damping: 36,
      onComplete: () => {
        haptic();
        onSwipe(dir); // feed ilerler → yeni activeCard prop olarak gelir
        x.set(-flyDir * w);
        animate(x, 0, {
          type: "spring",
          stiffness: 500,
          damping: 35,
          onComplete: () => {
            animating.current = false;
          },
        });
      },
    });
  };

  const onDragEnd = (_e: unknown, info: PanInfo) => {
    const threshold = 80;
    if (info.offset.x <= -threshold || info.velocity.x < -600) go(-1, "left"); // sola → skip
    else if (info.offset.x >= threshold || info.velocity.x > 600) go(1, "right"); // sağa → like
    else animate(x, 0, { type: "spring", stiffness: 400, damping: 34 });
  };

  return (
    // relative + yatay iç boşluk: kaydırma okları kartın SOLUNDA/SAĞINDA durur,
    // kartın üzerine binmez. Kart genişliği ve iç yerleşimi değişmez.
    <section className="relative flex flex-col px-4">
      <SwipeArrows x={x} />
      <motion.div
        drag="x"
        dragConstraints={{ left: 0, right: 0 }}
        dragElastic={0.7}
        onDragEnd={onDragEnd}
        style={{ x, rotate, scale, boxShadow }}
        className="touch-pan-y select-none rounded-[var(--radius-section)]"
      >
        <HeroCard card={card} parallaxX={bgParallax} onDetail={onOpen} />
      </motion.div>
    </section>
  );
}

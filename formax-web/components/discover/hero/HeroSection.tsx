"use client";

import { useEffect, useRef } from "react";
import { motion, useMotionValue, useTransform, animate, type PanInfo } from "framer-motion";
import { HeroCard } from "./HeroCard";
import { SwipeArrows } from "./SwipeArrows";
import type { RecommendationCardDto, SwipeDirection } from "@/types/api";

interface Props {
  /** Aktif maç (gerçek feed'den — useFeedQueue). */
  card: RecommendationCardDto;
  /** Swipe/ok tamamlanınca feed'i ilerletir (dedup: aynı maç tekrar gelmez). */
  onSwipe: (dir: SwipeDirection) => void;
  /** Feed içindeki konum — kart altındaki nokta göstergesi. */
  position?: { index: number; count: number };
}

/**
 * FORMAX · HeroSection (05) — gerçek AI Discovery Feed kartı.
 * Kart parmağa bağlı swipe edilir (premium spring + hafif rotate/scale + gölge + parallax).
 * Threshold geçilince kart o yöne TAM çıkar, feed ilerler (sola=skip, sağa=like), yeni
 * kart karşı taraftan girer. Kaynak tamamen backend (RecommendationCardDto); mock yok.
 *
 * TIKLAMA KURALI (ürün kararı): kartın kendisi bağlantı DEĞİLDİR; boş alan tıklanmaz.
 * Yalnız görünür butonlar iş yapar — kenardaki oklar SADECE kart değiştirir, karttaki
 * "Takip Et" SADECE takip eder. Maç Detayı ayrı görünür butondan açılır (aşağıdaki
 * "Maçı Keşfet"), karttan değil.
 */
export function HeroSection({ card, onSwipe, position }: Props) {
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

  // Kart geçişi (ok tıklaması veya swipe) — TEK akış.
  //
  // DAYANIKLILIK: geçişin tamamlanması animasyonun bitişine bağlı OLAMAZ. Sekme arka
  // plana alınırsa tarayıcı requestAnimationFrame'i durdurur; spring hiç bitmez, onComplete
  // çalışmaz ve kart ekran dışında ASILI KALIR (feed de ilerlemez). Bu yüzden ilerleme
  // "animasyon bitti VEYA emniyet süresi doldu" ile tetiklenir; hangisi önce olursa.
  const go = (flyDir: 1 | -1, dir: SwipeDirection) => {
    if (animating.current) return;
    animating.current = true;
    const w = 400;
    let settled = false;
    let flyOut: { stop: () => void } | null = null;

    const settle = () => {
      if (settled) return;
      settled = true;
      clearTimeout(safety);
      // Emniyet süresiyle geldiysek uçuş animasyonu hâlâ "çalışıyor" olabilir; durdurulmazsa
      // sekme yeniden görünür olunca kartı tekrar ekran dışına taşır.
      flyOut?.stop();
      haptic();
      onSwipe(dir); // feed ilerler → yeni activeCard prop olarak gelir

      // Sekme görünür değilse giriş animasyonu da çalışamaz → kartı doğrudan yerine koy.
      if (typeof document !== "undefined" && document.hidden) {
        x.set(0);
        animating.current = false;
        return;
      }

      x.set(-flyDir * w);
      animate(x, 0, {
        type: "spring",
        stiffness: 500,
        damping: 35,
        onComplete: () => {
          animating.current = false;
        },
      });
    };

    const safety = setTimeout(settle, 900); // rAF durursa geçiş yine tamamlanır
    flyOut = animate(x, flyDir * w, {
      type: "spring",
      stiffness: 500,
      damping: 36,
      onComplete: settle,
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
      {/* Oklar = kart geçişi (swipe ile aynı akış, aynı backend sinyali). */}
      <SwipeArrows x={x} onLeft={() => go(-1, "left")} onRight={() => go(1, "right")} />
      <motion.div
        drag="x"
        dragConstraints={{ left: 0, right: 0 }}
        dragElastic={0.7}
        onDragEnd={onDragEnd}
        style={{ x, rotate, scale, boxShadow }}
        className="touch-pan-y select-none rounded-[var(--radius-section)]"
      >
        <HeroCard card={card} parallaxX={bgParallax} position={position} />
      </motion.div>
    </section>
  );
}

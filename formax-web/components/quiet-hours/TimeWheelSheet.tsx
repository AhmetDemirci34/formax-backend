"use client";

import { useEffect, useRef, useState } from "react";
import { AnimatePresence, motion, type PanInfo } from "framer-motion";
import { haptic } from "@/lib/utils/haptics";

const ITEM_H = 40; // her satır yüksekliği (px)
const VISIBLE = 5; // görünen satır sayısı (tek olmalı — orta satır seçili)
const PAD = ((VISIBLE - 1) / 2) * ITEM_H; // ilk/son öğe merkeze gelebilsin diye dolgu

const pad2 = (n: number) => String(n).padStart(2, "0");

interface TimeWheelSheetProps {
  open: boolean;
  /** Sheet başlığı — ilgili satırın adı (Başlangıç / Bitiş Saati). */
  title: string;
  /** Geçerli saat "HH:mm". */
  value: string;
  onClose: () => void;
  /** Seçilen yeni saat "HH:mm" — sheet kapanırken çağrılır (otomatik kaydeder). */
  onConfirm: (next: string) => void;
}

/**
 * TimeWheelSheet — Apple tarzı Bottom Sheet içinde iOS wheel time picker.
 *
 * İki tekerlek (saat 00–23, dakika 00–59) scroll-snap ile döner; ortadaki
 * vurgulu bant seçili değeri gösterir. Kullanıcı seçimi yapıp sheet'i
 * kapattığında (Bitti / overlay / aşağı sürükleme) değer otomatik kaydedilir.
 * Kaydet butonu yoktur; yeni bir sayfaya gidilmez.
 */
export function TimeWheelSheet({ open, title, value, onClose, onConfirm }: TimeWheelSheetProps) {
  return (
    <AnimatePresence>
      {open ? (
        // key={value}: her açılışta iç durum geçerli saatten yeniden başlar.
        <SheetBody key={value} title={title} value={value} onClose={onClose} onConfirm={onConfirm} />
      ) : null}
    </AnimatePresence>
  );
}

function SheetBody({
  title,
  value,
  onClose,
  onConfirm,
}: Omit<TimeWheelSheetProps, "open">) {
  const [h, setH] = useState(() => clampInt(value.split(":")[0], 23));
  const [m, setM] = useState(() => clampInt(value.split(":")[1], 59));

  const commit = () => {
    haptic();
    onConfirm(`${pad2(h)}:${pad2(m)}`);
    onClose();
  };

  const onDragEnd = (_e: unknown, info: PanInfo) => {
    if (info.offset.y > 90 || info.velocity.y > 600) commit();
  };

  return (
    <div className="fixed inset-0 z-[85]">
      <motion.button
        type="button"
        aria-label="Kapat"
        onClick={commit}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.22, ease: "easeInOut" }}
        className="absolute inset-0 bg-black/60 backdrop-blur-[2px]"
      />

      <motion.div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        drag="y"
        dragConstraints={{ top: 0, bottom: 0 }}
        dragElastic={{ top: 0, bottom: 0.4 }}
        onDragEnd={onDragEnd}
        initial={{ y: "100%" }}
        animate={{ y: 0 }}
        exit={{ y: "100%" }}
        transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
        className="absolute inset-x-0 bottom-0 rounded-t-[26px] bg-profile-container pb-[max(var(--safe-bottom),16px)]"
      >
        <div className="flex justify-center pt-3">
          <span className="h-1 w-10 rounded-full bg-white/20" />
        </div>

        <div className="flex items-center justify-between px-5 pb-1 pt-3">
          <h2 className="text-[15px] font-bold text-white">{title}</h2>
          <button
            type="button"
            onClick={commit}
            className="text-[15px] font-bold text-profile-accent transition-opacity active:opacity-70"
          >
            Bitti
          </button>
        </div>

        {/* Wheel alanı — iki tekerlek + orta ayırıcı, üzerinde vurgu bandı */}
        <div className="relative px-5 pt-2">
          <div className="mx-auto flex max-w-[240px] items-stretch justify-center">
            <Wheel max={23} value={h} onChange={setH} ariaLabel="Saat" />
            <span
              className="flex items-center justify-center text-[22px] font-semibold text-white"
              style={{ height: VISIBLE * ITEM_H }}
              aria-hidden
            >
              :
            </span>
            <Wheel max={59} value={m} onChange={setM} ariaLabel="Dakika" />
          </div>

          {/* Orta seçim bandı — iki ince çizgi arasında, wheel'lerin üstünde */}
          <div
            className="pointer-events-none absolute inset-x-5 top-2 -z-0"
            style={{ height: VISIBLE * ITEM_H }}
          >
            <div
              className="absolute inset-x-0 border-y border-white/10 bg-white/[0.03]"
              style={{ height: ITEM_H, top: (VISIBLE * ITEM_H - ITEM_H) / 2 }}
            />
          </div>
        </div>
      </motion.div>
    </div>
  );
}

/** Tek tekerlek — 0..max arası değerler, scroll-snap ile döner. */
function Wheel({
  max,
  value,
  onChange,
  ariaLabel,
}: {
  max: number;
  value: number;
  onChange: (v: number) => void;
  ariaLabel: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const settleTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const items = Array.from({ length: max + 1 }, (_, i) => i);

  // Açılışta geçerli değeri ortaya hizala (DOM yazımı — setState değil).
  useEffect(() => {
    if (ref.current) ref.current.scrollTop = value * ITEM_H;
    // Yalnızca mount'ta çalışır; değer değişimi kullanıcı kaydırmasından gelir.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleScroll = () => {
    if (settleTimer.current) clearTimeout(settleTimer.current);
    settleTimer.current = setTimeout(() => {
      const el = ref.current;
      if (!el) return;
      const idx = Math.max(0, Math.min(max, Math.round(el.scrollTop / ITEM_H)));
      if (idx !== value) {
        haptic(6);
        onChange(idx);
      }
    }, 90);
  };

  return (
    <div className="relative flex-1" style={{ height: VISIBLE * ITEM_H }}>
      <div
        ref={ref}
        onScroll={handleScroll}
        aria-label={ariaLabel}
        className="h-full snap-y snap-mandatory overflow-y-scroll [-ms-overflow-style:none] [scrollbar-width:none] [mask-image:linear-gradient(180deg,transparent,#000_35%,#000_65%,transparent)] [&::-webkit-scrollbar]:hidden"
        style={{ scrollPaddingTop: PAD }}
      >
        <div style={{ height: PAD }} aria-hidden />
        {items.map((it) => (
          <div
            key={it}
            className={`flex snap-center items-center justify-center tabular-nums transition-colors ${
              it === value ? "text-[22px] font-semibold text-white" : "text-[20px] text-profile-muted"
            }`}
            style={{ height: ITEM_H }}
          >
            {pad2(it)}
          </div>
        ))}
        <div style={{ height: PAD }} aria-hidden />
      </div>
    </div>
  );
}

function clampInt(raw: string | undefined, max: number): number {
  const n = parseInt(raw ?? "0", 10);
  if (Number.isNaN(n)) return 0;
  return Math.max(0, Math.min(max, n));
}

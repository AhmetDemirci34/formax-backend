"use client";

import type { ReactNode } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { useChrome } from "@/context/ChromeContext";
import { MenuIcon, GlobeIcon } from "./icons";

/**
 * FORMAX · Header (03) — yeni düzen.
 * Sol: ☰ (Side Drawer) · Orta: marka (ekranın tam ortasında) · Sağ: 🌍 (Dil).
 * 3 kolonlu grid, orta kolon ortalanır → FORMAX her zaman ekran ortasında.
 * Menü ve dil aksiyonları ChromeContext'e bağlıdır (tek kaynak). Bildirim ikonu kaldırıldı.
 */
export function DiscoverHeader() {
  const { openDrawer, openLang } = useChrome();

  return (
    <header className="grid h-[var(--header-height)] grid-cols-[1fr_auto_1fr] items-center border-b border-white/5 bg-bg-deep/70 px-2">
      {/* Sol — menü (Side Drawer) */}
      <div className="flex justify-start">
        <IconButton label="Menü" onClick={openDrawer}>
          <MenuIcon size={22} />
        </IconButton>
      </div>

      {/* Orta — marka, ekranın tam ortasında */}
      <BrandLogo />

      {/* Sağ — yalnızca dil */}
      <div className="flex items-center justify-end">
        <IconButton label="Dil" onClick={openLang}>
          <GlobeIcon size={21} />
        </IconButton>
      </div>
    </header>
  );
}

function BrandLogo() {
  const reduce = useReducedMotion();
  return (
    <motion.div
      className="text-center leading-none"
      // Apple seviyesinde, çok hafif "nefes" — abartısız (scale 1.00↔1.015, ~4.2s).
      animate={reduce ? undefined : { scale: [1, 1.015, 1] }}
      transition={reduce ? undefined : { duration: 4.2, ease: "easeInOut", repeat: Infinity }}
      style={{ transformOrigin: "center" }}
    >
      <span className="text-[25px] font-black tracking-[-0.01em] text-text-primary">
        FORMA<span className="text-neon">X</span>
      </span>
      <p className="mt-1 text-[10px] font-semibold uppercase tracking-[0.13em] text-text-secondary">
        Futbolu Anlayan Zekâ
      </p>
    </motion.div>
  );
}

function IconButton({
  children,
  label,
  onClick,
}: {
  children: ReactNode;
  label: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className="grid h-11 w-11 place-items-center rounded-full text-text-secondary transition-colors hover:bg-white/5 hover:text-text-primary active:scale-95"
    >
      {children}
    </button>
  );
}

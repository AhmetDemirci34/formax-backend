"use client";

import { PencilIcon } from "./icons";
import { haptic } from "@/lib/utils/haptics";

interface ProfileHeaderProps {
  /** Scroll y > 10 olduğunda alt kenarda 1px sınır çizgisi belirir. */
  scrolled: boolean;
  onEdit: () => void;
}

/**
 * StickyHeader — Stitch görseli: solda lime "FORMAX" logosu, ortada "PROFİL",
 * sağda "Düzenle" (kalem) aksiyonu.
 *
 * Blur reçetesi `.profile-header-blur` (globals.css): blur(20px) + %80 opacity.
 * Üst çentik boşluğu `--safe-top` ile bırakılır.
 */
export function ProfileHeader({ scrolled, onEdit }: ProfileHeaderProps) {
  return (
    <header
      className={`
        profile-header-blur sticky top-0 z-40 pt-[var(--safe-top)]
        border-b transition-colors duration-200
        ${scrolled ? "border-profile-border" : "border-transparent"}
      `}
    >
      <div className="relative flex h-14 items-center px-4">
        {/* FORMAX — Stitch'e göre küçültüldü (13 → 11px). Sol boşluk 16px:
            hero ve section kartlarının kenar hizasıyla aynı.
            `translate-y-[2px]`: 11px ile 16px metin optik olarak ortalanınca
            taban çizgileri 2px kayıyor; bu düzeltme ikisini aynı hizaya getirir. */}
        <span className="translate-y-[2px] text-[11px] font-bold uppercase italic leading-none tracking-[0.5px] text-profile-accent">
          FORMAX
        </span>

        {/* Metin doğrudan büyük harf yazılır: `text-transform: uppercase` bazı
            motorlarda Türkçe i→İ eşlemesini yapmaz. */}
        <h1 className="pointer-events-none absolute inset-x-0 text-center text-[16px] font-bold uppercase leading-none tracking-[1px] text-white">
          PROFİL
        </h1>

        {/* 44px dokunma alanı korunurken 20px ikon ekran kenarından 16px'te
            kalsın diye kutu 12px dışarı çekilir. */}
        <button
          type="button"
          onClick={() => {
            haptic();
            onEdit();
          }}
          aria-label="Profili düzenle"
          className="-mr-3 ml-auto flex h-11 w-11 items-center justify-center rounded-full text-white transition-opacity duration-150 active:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60"
        >
          <PencilIcon size={20} />
        </button>
      </div>
    </header>
  );
}

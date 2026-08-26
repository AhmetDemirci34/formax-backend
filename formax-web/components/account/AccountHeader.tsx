"use client";

import { BackIcon } from "./icons";
import { haptic } from "@/lib/utils/haptics";

interface AccountHeaderProps {
  /** Scroll y > 10 olduğunda alt kenarda 1px sınır çizgisi belirir. */
  scrolled: boolean;
  onBack: () => void;
}

/**
 * Hesabım — Sticky Header (Stitch görseli): SOLDA geri butonu, ORTADA "HESABIM".
 * Sağda aksiyon yoktur; geri butonuyla simetri için sağda aynı genişlikte
 * boşluk bırakılır ki başlık gerçekten ortalansın.
 *
 * Blur reçetesi Profil Merkezi ile aynı (`.profile-header-blur`).
 */
export function AccountHeader({ scrolled, onBack }: AccountHeaderProps) {
  return (
    <header
      className={`
        profile-header-blur sticky top-0 z-40 pt-[var(--safe-top)]
        border-b transition-colors duration-200
        ${scrolled ? "border-profile-border" : "border-transparent"}
      `}
    >
      <div className="relative flex h-14 items-center px-4">
        <button
          type="button"
          onClick={() => {
            haptic();
            onBack();
          }}
          aria-label="Geri"
          className="-ml-3 flex h-11 w-11 items-center justify-center rounded-full text-white transition-opacity duration-150 active:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60"
        >
          <BackIcon size={22} />
        </button>

        {/* Metin doğrudan büyük harf: CSS uppercase bazı motorlarda Türkçe
            ı→I eşlemesini yapmaz. */}
        <h1 className="pointer-events-none absolute inset-x-0 text-center text-[17px] font-semibold uppercase leading-none tracking-[0.5px] text-white">
          HESABIM
        </h1>
      </div>
    </header>
  );
}

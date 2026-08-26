"use client";

import { haptic } from "@/lib/utils/haptics";

/**
 * LogoutCard — "Çıkış Yap", section listelerinden AYRI, en alttaki kendi kartı
 * (Stitch görseli): ekran kenarından 16px içeride, köşeleri yuvarlatılmış,
 * metin ORTALANMIŞ ve sistem hata renginde (destructive). İkon yoktur.
 */
export function LogoutCard({ onClick }: { onClick: () => void }) {
  return (
    <div className="mx-4">
      <button
        type="button"
        onClick={() => {
          haptic();
          onClick();
        }}
        className="flex h-14 w-full items-center justify-center rounded-2xl bg-profile-container text-[15px] font-semibold text-formax-red transition-colors duration-150 ease-in hover:bg-white/[0.06] active:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60"
      >
        Çıkış Yap
      </button>
    </div>
  );
}

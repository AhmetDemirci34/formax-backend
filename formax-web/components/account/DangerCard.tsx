"use client";

import { TrashIcon } from "./icons";
import { haptic } from "@/lib/utils/haptics";

/**
 * DangerCard — "Hesabı Sil" (Stitch görseli).
 *
 * Section kartlarından ayrı, kendi kartı: solda kırmızı metin, sağda çöp kutusu
 * ikonu. Kırmızı tonlu zemin + kırmızı kenarlık ile "danger zone" ayrışır.
 * Renk: #FF453A (System Red, Handoff §10).
 */
export function DangerCard({ onClick }: { onClick: () => void }) {
  return (
    <div className="mx-4">
      <button
        type="button"
        onClick={() => {
          haptic();
          onClick();
        }}
        className="flex h-[66px] w-full items-center justify-between rounded-xl border border-profile-danger/25 bg-profile-danger/[0.07] px-4 text-profile-danger transition-colors duration-150 ease-in hover:bg-profile-danger/[0.12] active:bg-profile-danger/[0.16] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-danger/60"
      >
        <span className="text-[16px] font-medium leading-none">Hesabı Sil</span>
        <TrashIcon size={20} />
      </button>
    </div>
  );
}

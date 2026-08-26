"use client";

import { ChevronRightIcon } from "@/components/discover/icons";

/**
 * SubHeader — Management görünümü başlığı (geri + başlık + açıklama).
 */
export function SubHeader({ onBack }: { onBack: () => void }) {
  return (
    <div className="flex items-center gap-3 px-4 pb-2 pt-3">
      <button
        type="button"
        onClick={onBack}
        aria-label="Geri"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-text-primary transition-colors hover:bg-white/5 active:scale-95"
      >
        {/* sola bakan chevron */}
        <ChevronRightIcon size={22} className="rotate-180" />
      </button>
      <div className="leading-tight">
        <h1 className="text-[20px] font-bold uppercase tracking-tight text-text-primary">
          Takip Ettiklerim
        </h1>
        <p className="text-[11px] font-medium text-text-muted">Takip ettiğin tüm içerikleri yönet</p>
      </div>
    </div>
  );
}

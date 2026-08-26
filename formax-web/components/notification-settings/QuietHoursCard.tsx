"use client";

import { ChevronRightIcon } from "@/components/discover/icons";
import { MoonIcon } from "./icons";
import { haptic } from "@/lib/utils/haptics";

interface QuietHoursCardProps {
  /**
   * Saat aralığı (ör. "22:00 - 08:00). Backend bu alanı döndürmeye başlayınca
   * verilir; TANIMSIZ olduğunda hiçbir şey gösterilmez — yer tutucu YOKTUR,
   * satır yalnızca başlık + açıklama + chevron olarak görünür.
   */
  range?: string;
  onClick: () => void;
}

/**
 * QuietHoursCard — "Sessiz Saatler" satırı (Stitch görseli).
 *
 * Bu satır bir TOGGLE DEĞİLDİR: saat aralığı seçimi gerektirdiği için chevron
 * ile alt ekrana yönlendirir. Yükseklik 80px, solda ay ikonu, sağda lime
 * aralık metni + chevron.
 */
export function QuietHoursCard({ range, onClick }: QuietHoursCardProps) {
  return (
    <div className="mx-4 overflow-hidden rounded-xl bg-profile-container">
      <button
        type="button"
        onClick={() => {
          haptic();
          onClick();
        }}
        className="flex h-20 w-full items-center gap-3 px-4 text-left transition-colors duration-150 ease-in hover:bg-white/5 active:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-profile-accent/60"
      >
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-profile-surface text-profile-muted">
          <MoonIcon size={18} />
        </span>

        <span className="flex min-w-0 flex-1 flex-col gap-1.5">
          <span className="truncate text-[16px] font-medium leading-none text-white">
            Sessiz Saatler
          </span>
          <span className="text-[12px] leading-[1.35] text-profile-muted">
            Belirlediğin saatler arasında bildirim gönderilmez.
          </span>
        </span>

        {range ? (
          <span className="shrink-0 text-[15px] font-semibold leading-none text-profile-accent tabular-nums">
            {range}
          </span>
        ) : null}
        <ChevronRightIcon size={20} className="shrink-0 text-profile-chevron" />
      </button>
    </div>
  );
}

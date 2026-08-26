"use client";

import type { ReactNode } from "react";
import { ChevronRightIcon } from "@/components/discover/icons";
import { haptic } from "@/lib/utils/haptics";

export interface InfoRowProps {
  /**
   * `field`  → üstte küçük gri etiket, altta belirgin beyaz değer
   *            (KİŞİSEL BİLGİLER satırları: Ad Soyad, Kullanıcı Adı, E-posta).
   * `action` → üstte belirgin beyaz başlık, altta küçük gri açıklama
   *            (GÜVENLİK ve ÜYELİK satırları).
   */
  variant?: "field" | "action";
  /** `field`'da etiket, `action`'da başlık. */
  label: string;
  /** `field`'da değer, `action`'da açıklama. */
  value: string;
  /** Chevron'dan önce gelen ek içerik (ör. lime "YÜKSELT" metni). */
  trailing?: ReactNode;
  /**
   * Salt-okunur satır: chevron gösterilmez, dokunmaya kapalıdır ve düşük
   * opaklıkta görünür (E-posta).
   */
  readOnly?: boolean;
  onClick?: () => void;
}

/**
 * InfoRow — Hesabım ekranının liste satırı (Stitch görseli).
 *
 * Stitch'te bu satırlarda SOL İKON YOKTUR (Handoff §5 ikon listeler; görsel
 * öncelikli olduğu için ikonlar eklenmedi). Yükseklik 66px, yatay padding 16px,
 * chevron 20px #4D4D4D, press state Apple-style `bg-white/5`.
 */
export function InfoRow({
  variant = "field",
  label,
  value,
  trailing,
  readOnly = false,
  onClick,
}: InfoRowProps) {
  const top =
    variant === "field"
      ? "text-[12px] font-normal text-profile-muted"
      : "text-[15px] font-semibold text-white";
  const bottom =
    variant === "field"
      ? "text-[15px] font-semibold text-white"
      : "text-[12px] font-normal text-profile-muted";

  const content = (
    <>
      <span className="flex min-w-0 flex-1 flex-col gap-1.5">
        <span className={`truncate leading-none ${top}`}>{label}</span>
        <span className={`truncate leading-none ${bottom}`}>{value}</span>
      </span>

      {trailing}

      {readOnly ? null : <ChevronRightIcon size={20} className="shrink-0 text-profile-chevron" />}
    </>
  );

  if (readOnly) {
    return (
      <div className="flex h-[66px] w-full items-center gap-3 px-5 opacity-60">{content}</div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => {
        haptic();
        onClick?.();
      }}
      className="flex h-[66px] w-full items-center gap-3 px-5 text-left transition-colors duration-150 ease-in hover:bg-white/5 active:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-profile-accent/60"
    >
      {content}
    </button>
  );
}

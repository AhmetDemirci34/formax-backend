"use client";

import type { ReactNode } from "react";
import { ChevronRightIcon } from "@/components/discover/icons";
import { haptic } from "@/lib/utils/haptics";

export interface NavListItemProps {
  /** Sol ikon. */
  icon: ReactNode;
  /** Satır başlığı. */
  title: string;
  /** Başlığın altındaki açıklama. */
  subtitle?: string;
  /**
   * Vurgulu satır (Stitch'te "Premium"): zemin koyu yeşile döner; ikon, başlık
   * ve chevron lime olur, başlık bold ağırlığa geçer.
   */
  isHighlighted?: boolean;
  onClick: () => void;
}

/**
 * Navigation List Item — Profil Merkezi'nin temel satırı (Stitch görseli).
 *
 * Ölçüler: yükseklik 78px · yatay padding 16px · ikon 20px · ikon→metin 16px
 * chevron 18px #4D4D4D · press: arka planın %10 aydınlanması (150ms).
 * Köşe yuvarlaklığı satırda değil, `SectionGroup` konteynerindedir.
 */
export function NavListItem({
  icon,
  title,
  subtitle,
  isHighlighted = false,
  onClick,
}: NavListItemProps) {
  return (
    <button
      type="button"
      onClick={() => {
        haptic();
        onClick();
      }}
      className={`
        flex h-[78px] w-full items-center gap-4 px-4 text-left
        transition-colors duration-150 ease-in
        hover:bg-white/[0.06] active:bg-white/10
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-profile-accent/60
        ${isHighlighted ? "bg-[var(--profile-premium-bg)]" : ""}
      `}
    >
      {/* Sabit 20px kutu: ikon glifinin genişliği ne olursa olsun (dolu yıldız
          dahil) tüm satırlarda başlık aynı x'ten başlar, chevron aynı hizada. */}
      <span
        className={`flex h-5 w-5 shrink-0 items-center justify-center ${
          isHighlighted ? "text-profile-accent" : "text-white"
        }`}
        aria-hidden
      >
        {icon}
      </span>

      <span className="flex min-w-0 flex-1 flex-col justify-center gap-1">
        <span
          className={`truncate text-[15px] leading-none ${
            isHighlighted ? "font-bold text-profile-accent" : "font-semibold text-white"
          }`}
        >
          {title}
        </span>
        {subtitle ? (
          <span className="truncate text-[12px] font-normal leading-none text-profile-muted">
            {subtitle}
          </span>
        ) : null}
      </span>

      <ChevronRightIcon
        size={18}
        className={`shrink-0 ${isHighlighted ? "text-profile-accent" : "text-profile-chevron"}`}
      />
    </button>
  );
}

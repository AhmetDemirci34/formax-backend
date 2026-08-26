"use client";

import type { ReactNode } from "react";
import { NotificationToggle } from "./NotificationToggle";

export interface NotificationItemProps {
  /** Dairesel önden gelen görsel: takım baş harfleri ya da kategori ikonu. */
  leading: ReactNode;
  /** Satır başlığı — maç/takım/lig adı ya da FORMAX içerik adı. */
  title: string;
  /** Kategori etiketi: MAÇ · TAKIM · LİG · FORMAX. */
  category: string;
  /** Kategori etiketinin önündeki küçük glif. */
  categoryIcon: ReactNode;
  checked: boolean;
  onChange: (next: boolean) => void;
}

/**
 * NotificationItem — "Takip Ettiklerim" listesinin satırı (Stitch görseli).
 *
 * Yükseklik 72px · yatay padding 16px · dairesel görsel 32px ·
 * görsel→metin boşluğu 12px · sağda Apple tarzı toggle.
 * Satırın kendisi tıklanabilir değildir; yalnızca toggle etkileşimlidir.
 */
export function NotificationItem({
  leading,
  title,
  category,
  categoryIcon,
  checked,
  onChange,
}: NotificationItemProps) {
  return (
    <div className="flex h-[72px] w-full items-center gap-3 px-4">
      <span className="flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-profile-surface">
        {leading}
      </span>

      <span className="flex min-w-0 flex-1 flex-col gap-1.5">
        <span className="truncate text-[16px] font-medium leading-none text-white">{title}</span>
        <span className="flex items-center gap-1 text-profile-muted">
          <span className="shrink-0">{categoryIcon}</span>
          <span className="truncate text-[10px] font-medium uppercase leading-none tracking-[0.3px]">
            {category}
          </span>
        </span>
      </span>

      <NotificationToggle checked={checked} onChange={onChange} label={title} />
    </div>
  );
}

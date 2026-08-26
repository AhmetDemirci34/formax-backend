"use client";

import { useState } from "react";
import { ChevronRightIcon } from "@/components/discover/icons";
import { ClockIcon } from "@/components/notification-settings/icons";
import { TimeWheelSheet } from "./TimeWheelSheet";
import { haptic } from "@/lib/utils/haptics";

interface TimeRowProps {
  title: string;
  /** Geçerli saat "HH:mm". */
  value: string;
  /** Yeni saat seçildiğinde çağrılır (otomatik kaydeder). */
  onChange: (next: string) => void;
}

/**
 * TimeRow — "Başlangıç Saati" / "Bitiş Saati" satırı (Stitch görseli).
 *
 * Yükseklik 64px · saat ikonu solda · başlık · lime saat değeri + chevron.
 * Satıra dokununca Apple tarzı Bottom Sheet içinde iOS wheel picker açılır
 * (TimeWheelSheet); bu ekran içinde yeni bir sayfaya gidilmez. Seçim yapılıp
 * sheet kapandığında değer otomatik kaydedilir.
 */
export function TimeRow({ title, value, onChange }: TimeRowProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => {
          haptic();
          setOpen(true);
        }}
        className="flex h-16 w-full items-center gap-3 px-4 text-left transition-colors duration-150 ease-in hover:bg-white/5 active:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-profile-accent/60"
      >
        <span className="flex h-6 w-6 shrink-0 items-center justify-center text-profile-muted">
          <ClockIcon size={20} />
        </span>
        <span className="flex-1 text-[16px] font-medium leading-none text-white">{title}</span>
        <span className="text-[16px] font-medium leading-none text-profile-accent tabular-nums">
          {value}
        </span>
        <ChevronRightIcon size={18} className="text-profile-chevron" />
      </button>

      <TimeWheelSheet
        open={open}
        title={title}
        value={value}
        onClose={() => setOpen(false)}
        onConfirm={onChange}
      />
    </>
  );
}

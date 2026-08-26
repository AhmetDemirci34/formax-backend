"use client";

import { haptic } from "@/lib/utils/haptics";

interface NotificationToggleProps {
  checked: boolean;
  onChange: (next: boolean) => void;
  /** Erişilebilirlik için satır başlığı. */
  label: string;
}

/**
 * NotificationToggle — Apple tarzı anahtar (Handoff §7).
 * 51×31 gövde · 27px beyaz thumb · aktif #CCFF00 · pasif #38393D ·
 * thumb kayması 200ms ease-in-out. Değişim anında üst katman kaydeder
 * (Kaydet butonu yoktur).
 */
export function NotificationToggle({ checked, onChange, label }: NotificationToggleProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => {
        haptic();
        onChange(!checked);
      }}
      className={`relative h-[31px] w-[51px] shrink-0 rounded-full transition-colors duration-200 ease-in-out focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60 focus-visible:ring-offset-2 focus-visible:ring-offset-profile-container ${
        checked ? "bg-profile-accent" : "bg-profile-border"
      }`}
    >
      <span
        className="absolute top-[2px] h-[27px] w-[27px] rounded-full bg-white transition-transform duration-200 ease-in-out"
        style={{ left: 2, transform: `translateX(${checked ? 20 : 0}px)` }}
      />
    </button>
  );
}

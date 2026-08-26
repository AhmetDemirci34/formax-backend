"use client";

import { BackIcon } from "@/components/account/icons";
import { haptic } from "@/lib/utils/haptics";

interface QuietHoursHeaderProps {
  /** Scroll y > 10 olduğunda alt kenarda 1px sınır çizgisi belirir. */
  scrolled: boolean;
  onBack: () => void;
}

/**
 * Sessiz Saatler — Sticky Header (Stitch görseli): SOLDA lime geri butonu,
 * ORTADA "Sessiz Saatler", altında ekranın amacını anlatan açıklama metni.
 * Kardeş ekranlarla (SCREEN_04) aynı yapı ve blur reçetesi.
 */
export function QuietHoursHeader({ scrolled, onBack }: QuietHoursHeaderProps) {
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
          className="-ml-3 flex h-11 w-11 items-center justify-center rounded-full text-profile-accent transition-opacity duration-150 active:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60"
        >
          <BackIcon size={22} />
        </button>

        <h1 className="pointer-events-none absolute inset-x-0 text-center text-[17px] font-bold leading-none text-white">
          Sessiz Saatler
        </h1>
      </div>

      <p className="px-10 pb-3 text-center text-[12px] leading-[1.45] text-profile-muted">
        Belirlediğin saatler arasında bildirimler gönderilmez.
      </p>
    </header>
  );
}

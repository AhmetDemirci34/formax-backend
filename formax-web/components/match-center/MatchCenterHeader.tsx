"use client";

import Link from "next/link";
import { BackIcon, BellIcon } from "./icons";

/**
 * Maç Detay Merkezi · TopAppBar (Fixed, 64px — Teknik Doküman §12).
 * Geri butonu davranışı sayfaya bırakılır (`onBack`): dashboard değilse
 * dashboard'a döner, dashboard'daysa route geri gider.
 */
export function MatchCenterHeader({
  onBack,
  title = "Maç Detayı",
}: {
  onBack: () => void;
  /**
   * Ekranın ÜRÜN ADI. Bitmiş maçta ekran "Maç Detayı" değil "MAÇ ÖZETİ"dir;
   * başlık da bunu söylemelidir, yoksa kullanıcı eksik bir detay ekranına
   * baktığını sanır.
   */
  title?: string;
}) {
  return (
    <header className="z-50 flex h-16 shrink-0 items-center justify-between border-b border-white/10 bg-goalai-surface/90 px-3 backdrop-blur-xl">
      <button
        type="button"
        onClick={onBack}
        aria-label="Geri"
        className="flex h-10 w-10 items-center justify-center rounded-full text-white/90 transition-colors hover:bg-white/5 active:scale-95"
      >
        <BackIcon size={22} />
      </button>

      <h1 className="truncate px-2 text-lg font-bold uppercase tracking-[0.16em] text-white">
        {title}
      </h1>

      <Link
        href="/notifications"
        aria-label="Bildirimler"
        className="flex h-10 w-10 items-center justify-center rounded-full text-goalai-accent transition-colors hover:bg-white/5 active:scale-95"
      >
        <BellIcon size={22} />
      </Link>
    </header>
  );
}

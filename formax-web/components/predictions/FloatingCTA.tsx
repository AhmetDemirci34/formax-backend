"use client";

import { useRouter } from "next/navigation";
import { motion } from "framer-motion";

/**
 * FloatingCTA — "Yeni Tahmin Oluştur". Sayfa içeriğinin üzerinde, Bottom
 * Navigation'ın hemen üstünde yüzer (fixed). Basınca yeni tahminlerin
 * oluşturulduğu Keşfet ekranına yönlendirir (kombin sheet orada).
 */
export function FloatingCTA() {
  const router = useRouter();
  return (
    <div className="pointer-events-none fixed inset-x-0 bottom-0 z-40">
      <div className="mx-auto max-w-[var(--app-max-width)] px-4 pb-[calc(var(--bottom-nav-height)+8px)]">
        <motion.button
          type="button"
          whileTap={{ scale: 0.96 }}
          transition={{ duration: 0.12 }}
          onClick={() => router.push("/")}
          className="goalai-cta pointer-events-auto flex h-14 w-full items-center justify-center gap-2 rounded-2xl text-[15px] font-bold uppercase tracking-wide text-[#0a0e16]"
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.6} strokeLinecap="round" aria-hidden="true">
            <path d="M12 5v14M5 12h14" />
          </svg>
          Yeni Tahmin Oluştur
        </motion.button>
      </div>
    </div>
  );
}

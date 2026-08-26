"use client";

import type { ReactNode } from "react";
import { motion } from "framer-motion";
import { CloseIcon } from "../icons";

/**
 * ViewShell — modül panellerinin ortak kabuğu (Teknik Doküman §5).
 * Tam genişlik panel; aşağıdan yukarı slide-up + fade-in (300ms, ease-out);
 * exit y:20 opacity:0. Sağ üstte 'X' → dashboard'a döner.
 */
export function ViewShell({
  title,
  onClose,
  children,
  scroll = true,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  scroll?: boolean;
}) {
  return (
    <motion.section
      initial={{ y: 20, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      exit={{ y: 20, opacity: 0 }}
      transition={{ duration: 0.3, ease: "easeOut" }}
      className="flex h-full w-full flex-col"
    >
      <div className="mb-3 flex shrink-0 items-center justify-between">
        <h2 className="text-lg font-bold uppercase tracking-wide text-white">{title}</h2>
        <button
          type="button"
          onClick={onClose}
          aria-label="Kapat"
          className="flex h-8 w-8 items-center justify-center rounded-full border border-goalai-border text-white/70 transition-colors hover:bg-white/10 hover:text-white active:scale-95"
        >
          <CloseIcon size={18} />
        </button>
      </div>

      <div className={scroll ? "min-h-0 flex-1 overflow-y-auto pr-0.5" : "min-h-0 flex-1 overflow-hidden"}>
        {children}
      </div>
    </motion.section>
  );
}

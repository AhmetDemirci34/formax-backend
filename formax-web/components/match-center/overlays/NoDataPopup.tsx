"use client";

import { motion } from "framer-motion";

/**
 * NoDataPopup — merkezlenmiş, blur arka planlı uyarı penceresi
 * (Teknik Doküman §8 / §6: Scale-up + Blur-in). "Tamam" veya dışına
 * tıklama ile kapanır (onClose opsiyonel; inline kullanımda verilmez).
 */
export function NoDataPopup({
  title,
  message,
  onClose,
}: {
  title?: string;
  message: string;
  onClose?: () => void;
}) {
  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.25 }}
      className="absolute inset-0 z-40 flex items-center justify-center bg-black/50 p-6 backdrop-blur-sm"
      onClick={onClose}
    >
      <motion.div
        initial={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        exit={{ scale: 0.9, opacity: 0 }}
        transition={{ duration: 0.25, ease: "easeOut" }}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-xs rounded-2xl border border-goalai-border bg-goalai-surface-bright p-5 text-center"
      >
        {title && (
          <p className="mb-1.5 text-sm font-bold uppercase tracking-wide text-goalai-accent">
            {title}
          </p>
        )}
        <p className="text-[14px] leading-relaxed text-white/85">{message}</p>
        {onClose && (
          <button
            type="button"
            onClick={onClose}
            className="mt-4 h-10 w-full rounded-xl bg-goalai-accent text-sm font-bold text-goalai-surface transition-transform active:scale-95"
          >
            Tamam
          </button>
        )}
      </motion.div>
    </motion.div>
  );
}

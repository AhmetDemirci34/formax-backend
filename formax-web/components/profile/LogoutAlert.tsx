"use client";

import { AnimatePresence, motion } from "framer-motion";

interface LogoutAlertProps {
  open: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

/**
 * LogoutAlert — "Çıkış Yap" onay uyarısı (Navigation Flow E: Profil → Çıkış Yap
 * → Onay Alert → Login Screen).
 *
 * Telefon çerçevesi içinde (absolute) yaşar; LanguageSheet ile aynı desen.
 * Onay butonu destructive (sistem hata rengi).
 */
export function LogoutAlert({ open, onCancel, onConfirm }: LogoutAlertProps) {
  return (
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-[90] flex items-center justify-center px-10">
          <motion.button
            type="button"
            aria-label="Kapat"
            onClick={onCancel}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2, ease: "easeInOut" }}
            className="absolute inset-0 bg-black/60 backdrop-blur-[2px]"
          />

          <motion.div
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="logout-alert-title"
            initial={{ opacity: 0, scale: 0.92 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.96 }}
            transition={{ duration: 0.2, ease: [0.4, 0, 0.2, 1] }}
            className="relative w-full max-w-[280px] overflow-hidden rounded-2xl bg-profile-container"
          >
            <div className="px-5 pb-4 pt-5 text-center">
              <h2 id="logout-alert-title" className="text-[16px] font-semibold text-white">
                Çıkış Yap
              </h2>
              <p className="mt-1.5 text-[13px] leading-snug text-profile-muted">
                Oturumunuz kapatılacak. Devam etmek istiyor musunuz?
              </p>
            </div>

            <div className="h-px bg-[var(--profile-divider)]" />

            <div className="flex">
              <button
                type="button"
                onClick={onCancel}
                className="h-12 flex-1 text-[15px] font-medium text-white transition-colors duration-150 ease-in active:bg-white/10"
              >
                Vazgeç
              </button>
              <span className="w-px self-stretch bg-[var(--profile-divider)]" aria-hidden />
              <button
                type="button"
                onClick={onConfirm}
                className="h-12 flex-1 text-[15px] font-semibold text-formax-red transition-colors duration-150 ease-in active:bg-white/10"
              >
                Çıkış Yap
              </button>
            </div>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

"use client";

import { AnimatePresence, motion, type PanInfo } from "framer-motion";
import { useChrome, type LanguageCode } from "@/context/ChromeContext";
import { CheckIcon, GlobeIcon } from "@/components/discover/icons";

export interface LanguageOption {
  code: LanguageCode;
  label: string;
  native: string;
}

/** Dil listesi — tek kaynak. Seçim şimdilik local state (ChromeContext). */
export const LANGUAGES: LanguageOption[] = [
  { code: "tr", label: "Türkçe", native: "Türkçe" },
  { code: "en", label: "English", native: "English" },
  { code: "de", label: "Deutsch", native: "Deutsch" },
  { code: "es", label: "Español", native: "Español" },
  { code: "fr", label: "Français", native: "Français" },
  { code: "it", label: "Italiano", native: "Italiano" },
  { code: "pt", label: "Português", native: "Português" },
  { code: "ar", label: "العربية", native: "العربية" },
];

/**
 * FORMAX · LanguageSheet
 * 🌍 ile açılan alttan Bottom Sheet. easeInOut, aşağı sürükleyerek / overlay ile kapanır.
 * Telefon çerçevesi içinde (absolute) yaşar — global BottomNav'ı da örter.
 */
export function LanguageSheet() {
  const { langOpen, closeLang, language, setLanguage } = useChrome();

  const onDragEnd = (_e: unknown, info: PanInfo) => {
    if (info.offset.y > 90 || info.velocity.y > 600) closeLang();
  };

  const select = (code: LanguageCode) => {
    setLanguage(code);
    // Seçimin görülmesi için kısa gecikmeyle kapat (premium his).
    setTimeout(closeLang, 180);
  };

  return (
    <AnimatePresence>
      {langOpen && (
        <div className="absolute inset-0 z-[80]">
          {/* Overlay — dokununca kapanır */}
          <motion.button
            type="button"
            aria-label="Kapat"
            onClick={closeLang}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.25, ease: "easeInOut" }}
            className="absolute inset-0 bg-black/60 backdrop-blur-[2px]"
          />

          {/* Sheet */}
          <motion.div
            role="dialog"
            aria-label="Dil seçimi"
            drag="y"
            dragConstraints={{ top: 0, bottom: 0 }}
            dragElastic={{ top: 0, bottom: 0.4 }}
            onDragEnd={onDragEnd}
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            exit={{ y: "100%" }}
            transition={{ duration: 0.32, ease: [0.4, 0, 0.2, 1] }}
            className="absolute inset-x-0 bottom-0 rounded-t-[26px] border-t border-white/10 bg-bg-glass pb-[max(var(--safe-bottom),20px)] shadow-[0_-24px_60px_rgba(0,0,0,.6)]"
          >
            {/* Sürükleme tutamağı */}
            <div className="flex justify-center pt-3">
              <span className="h-1 w-10 rounded-full bg-white/20" />
            </div>

            <div className="flex items-center gap-2 px-5 pb-2 pt-4">
              <GlobeIcon size={18} className="text-neon" />
              <h2 className="text-[15px] font-bold text-text-primary">Dil Seçimi</h2>
            </div>

            <ul className="max-h-[52vh] overflow-y-auto px-3 pb-2">
              {LANGUAGES.map((lang) => {
                const active = lang.code === language;
                return (
                  <li key={lang.code}>
                    <button
                      type="button"
                      onClick={() => select(lang.code)}
                      className={`flex w-full items-center justify-between rounded-2xl px-4 py-3.5 text-left transition-colors active:scale-[0.99] ${
                        active
                          ? "bg-neon/[0.08] text-text-primary"
                          : "text-text-secondary hover:bg-white/5"
                      }`}
                    >
                      <span className="text-[15px] font-medium">{lang.native}</span>
                      {active ? (
                        <span className="text-neon">
                          <CheckIcon size={18} />
                        </span>
                      ) : null}
                    </button>
                  </li>
                );
              })}
            </ul>
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

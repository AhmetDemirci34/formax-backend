"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";

/**
 * FORMAX · ChromeContext
 * App "chrome" (kabuk) durumu — Side Drawer ve Language Selector için tek kaynak.
 * Header (menü / 🌍) tetikler, LayoutWrapper içindeki AppChrome tüketir.
 * Seçilen dil şimdilik yalnızca local state'te tutulur (UI hazır, backend yok).
 */

export type LanguageCode =
  | "tr"
  | "en"
  | "de"
  | "es"
  | "fr"
  | "it"
  | "pt"
  | "ar";

interface ChromeState {
  drawerOpen: boolean;
  openDrawer: () => void;
  closeDrawer: () => void;

  langOpen: boolean;
  openLang: () => void;
  closeLang: () => void;

  language: LanguageCode;
  setLanguage: (code: LanguageCode) => void;
}

const ChromeContext = createContext<ChromeState | null>(null);

export function ChromeProvider({ children }: { children: ReactNode }) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [langOpen, setLangOpen] = useState(false);
  const [language, setLanguage] = useState<LanguageCode>("tr");

  const openDrawer = useCallback(() => setDrawerOpen(true), []);
  const closeDrawer = useCallback(() => setDrawerOpen(false), []);
  const openLang = useCallback(() => setLangOpen(true), []);
  const closeLang = useCallback(() => setLangOpen(false), []);

  const value = useMemo<ChromeState>(
    () => ({
      drawerOpen,
      openDrawer,
      closeDrawer,
      langOpen,
      openLang,
      closeLang,
      language,
      setLanguage,
    }),
    [drawerOpen, langOpen, language, openDrawer, closeDrawer, openLang, closeLang]
  );

  return <ChromeContext.Provider value={value}>{children}</ChromeContext.Provider>;
}

export function useChrome(): ChromeState {
  const ctx = useContext(ChromeContext);
  if (!ctx) {
    throw new Error("useChrome must be used within a ChromeProvider");
  }
  return ctx;
}

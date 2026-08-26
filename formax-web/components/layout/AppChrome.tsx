"use client";

import type { ReactNode } from "react";
import { motion } from "framer-motion";
import { BottomNav } from "@/components/ui/BottomNav";
import { SideDrawer } from "./SideDrawer";
import { LanguageSheet } from "./LanguageSheet";
import { useChrome } from "@/context/ChromeContext";

/**
 * FORMAX · AppChrome
 * Telefon çerçevesi içindeki "ekran" katmanı. Drawer açıkken ekran (içerik + BottomNav)
 * tek bir kart gibi sağa kayar, hafif küçülür, radius + gölge + karartma alır.
 *
 * Ekran, kendi içinde kaydırılan tam-yükseklik (100dvh) bir konteynerdir; böylece
 * `fixed` BottomNav dönüşümden etkilenmeden kartla birlikte hareket eder (60 FPS,
 * yalnızca transform anime edilir). Drawer ve LanguageSheet dönüşümün dışında kalır.
 */
export function AppChrome({ children }: { children: ReactNode }) {
  const { drawerOpen } = useChrome();

  return (
    <>
      <motion.div
        className="relative h-[100dvh] w-full overflow-hidden bg-[#070911] will-change-transform"
        style={{ transformOrigin: "center center" }}
        initial={false}
        animate={
          drawerOpen
            ? { scale: 0.86, x: "60%", borderRadius: 28 }
            : { scale: 1, x: "0%", borderRadius: 0 }
        }
        transition={{ duration: 0.42, ease: [0.4, 0, 0.2, 1] }}
      >
        {/* Kaydırılabilir içerik — YALNIZCA bu alan scroll olur */}
        <div className="h-full overflow-y-auto overflow-x-hidden overscroll-contain">
          {children}
        </div>

        {/* Bottom navigation — scroll alanının DIŞINDA, transform konteynerinin dibinde
            viewport'a sabit. İçerik kaydıkça yerinde kalır; drawer açılınca kartla
            birlikte kayar. Safe-area padding'i BottomNav'ın kendi stilinde korunur. */}
        <BottomNav />
      </motion.div>

      {/* Karartma — ekran kart görünürken içeriği hafif karartır (tap ile kapanır) */}
      <SideDrawer />
      <LanguageSheet />
    </>
  );
}

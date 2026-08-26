import { Hanken_Grotesk } from "next/font/google";

/**
 * Profil Merkezi (SCREEN_15) tipografisi — "Hanken Grotesk"
 * (UI Spec §5 / §13: "Hanken Grotesk font ailesinin tüm ağırlıkları").
 *
 * Match Center'daki `archivoNarrow` ile aynı desen: font YALNIZCA bu ekranın
 * kök elemanına scoped uygulanır (className), uygulamanın geri kalanının
 * sistem fontu değişmez.
 */
/* Hanken Grotesk bir "variable font"tur — `weight` verilmediğinde Next tüm
   ağırlık eksenini (100–900) tek dosyada yükler; Regular/Medium/Semibold/Bold
   ağırlıklarının hepsi kullanılabilir olur (Next 16 font dokümanı önerisi). */
export const hankenGrotesk = Hanken_Grotesk({
  subsets: ["latin", "latin-ext"], // latin-ext: Türkçe ı/ğ/ş/İ karakterleri
  display: "swap",
  variable: "--font-profile",
});

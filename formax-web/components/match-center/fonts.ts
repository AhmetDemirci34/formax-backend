import { Archivo_Narrow } from "next/font/google";

/**
 * Maç Detay Merkezi (Velocity Pitch) tipografisi — "Archivo Narrow"
 * (Teknik Doküman §2 / §5). Yalnızca Match Center ağacına scoped uygulanır
 * (className kökte), uygulamanın geri kalanının sistem fontu değişmez.
 */
export const archivoNarrow = Archivo_Narrow({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
  variable: "--font-goalai",
});

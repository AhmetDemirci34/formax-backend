import type { RecommendationCardDto } from "@/types/api";
import { isDiscoverable } from "@/components/discover/cardSignals";

/** Keşfet "Günün AI Kombini" ayak adedi. */
export const COMBO_SIZE = 4;

/**
 * FORMAX · Günün AI Kombini — ayakların TEK seçim noktası.
 *
 * Keşfet kartı (AIComboSection) ve "Kombini İncele" ekranı (/ai-combo) bu fonksiyonu
 * çağırır. İki ekran ayrı ayrı liste kesmediği sürece gösterdikleri MatchId'ler BİREBİR
 * aynı olur. (Kök neden buydu: Keşfet kartı `topPrediction != null` süzüyordu, /ai-combo
 * ise süzmeden ilk 4'ü alıyordu → iki ekran farklı maç gösteriyordu.)
 *
 * Seçim FRONTEND'DE YAPILMAZ, yalnız süzülür:
 *  • Sıra backend'indir (DiscoveryScore) — yeniden sıralama YOK.
 *  • `topPrediction` backend'in Decision paketinden gelir; güven endeksi 0 olan maçta
 *    backend bu alanı hiç göndermez, dolayısıyla o maç kombine giremez.
 *  • Gerçek oranı olmayan ayak alınmaz: kombin satırı oransız kalmasın ve toplam oran
 *    eksik ayakla hesaplanmasın diye. Oran UYDURULMAZ.
 */
export function selectComboLegs(
  cards: RecommendationCardDto[] | undefined
): RecommendationCardDto[] {
  return (cards ?? [])
    .filter(isDiscoverable)
    .filter((c) => c.topPrediction != null && c.topPrediction.odd != null)
    .slice(0, COMBO_SIZE);
}

/**
 * Toplam oran — YALNIZ ekranda gösterilen gerçek oranların çarpımı.
 * Backend toplam oran alanı döndürmüyor; ayaklardan biri oransızsa null döner
 * (sahte/fallback oranla asla tamamlanmaz).
 */
export function comboTotalOdd(legs: RecommendationCardDto[]): number | null {
  if (legs.length === 0) return null;
  const odds = legs.map((l) => l.topPrediction?.odd ?? null);
  if (odds.some((o) => o == null)) return null;
  return (odds as number[]).reduce((acc, o) => acc * o, 1);
}

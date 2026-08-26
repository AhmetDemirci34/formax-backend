import type { Prediction, PredictionInput } from "./types";

/**
 * PredictionDataSource — tahmin verisinin geldiği kaynağın sözleşmesi.
 * Implementasyonlar: LocalPredictionDataSource (bugün, localStorage),
 * ileride ApiPredictionDataSource (backend). UI bu arayüzü bile görmez;
 * yalnızca PredictionRepository üzerinden erişilir.
 */
export interface PredictionDataSource {
  getAll(): Promise<Prediction[]>;
  add(items: PredictionInput[]): Promise<number>;

  /**
   * Bir MAÇ için kullanıcının seçimini yazar — maç başına TEK kayıt.
   * Aynı maça yeni bir tahmin seçilirse mevcut kayıt GÜNCELLENİR (duplicate yok).
   * Aynı market tekrar seçilirse kayıt kaldırılır (seçim iptali).
   */
  setForMatch(input: PredictionInput): Promise<"added" | "updated" | "removed">;
}

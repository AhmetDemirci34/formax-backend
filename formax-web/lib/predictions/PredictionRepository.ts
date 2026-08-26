import type { PredictionDataSource } from "./PredictionDataSource";
import type { Prediction, PredictionInput } from "./types";

/**
 * PredictionRepository — UI/hook ile veri kaynağı arasındaki TEK köprü.
 * Hangi DataSource'un kullanıldığını gizler. Backend hazır olduğunda YALNIZCA
 * enjekte edilen dataSource değişir (bkz. index.ts); hook ve componentler değişmez.
 */
export class PredictionRepository {
  constructor(private readonly dataSource: PredictionDataSource) {}

  getAll(): Promise<Prediction[]> {
    return this.dataSource.getAll();
  }

  add(items: PredictionInput[]): Promise<number> {
    return this.dataSource.add(items);
  }

  /** Maç başına tek seçim — yeni seçim mevcut kaydı günceller (duplicate oluşmaz). */
  setForMatch(input: PredictionInput): Promise<"added" | "updated" | "removed"> {
    return this.dataSource.setForMatch(input);
  }
}

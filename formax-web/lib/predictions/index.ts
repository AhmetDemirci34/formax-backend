import { PredictionRepository } from "./PredictionRepository";
import { LocalPredictionDataSource } from "./LocalPredictionDataSource";

export type {
  Prediction,
  PredictionInput,
  PredictionTeam,
  PredictionStatus,
} from "./types";
export type { PredictionDataSource } from "./PredictionDataSource";
export { PredictionRepository } from "./PredictionRepository";
export { LocalPredictionDataSource } from "./LocalPredictionDataSource";

/**
 * Uygulama genelinde kullanılan tahmin veri sağlayıcısı — TEK DEĞİŞİM NOKTASI.
 *
 *   BUGÜN:  LocalPredictionDataSource (localStorage)
 *   YARIN:  new PredictionRepository(new ApiPredictionDataSource(apiClient))
 *
 * Backend hazır olduğunda yalnızca aşağıdaki satır değişir; useUserPredictions
 * hook'u, ComboSheet ve tüm Tahminlerim UI'ı olduğu gibi kalır.
 */
export const predictionRepository = new PredictionRepository(new LocalPredictionDataSource());

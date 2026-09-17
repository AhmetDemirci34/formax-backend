import type { MatchDetailDto } from "@/types/api";

/**
 * Maç Detay Merkezi — statik dashboard yapılandırması + gerçek DTO'dan türetilen
 * analiz metni. Backend Mapping tek gerçek endpoint olan
 * `GET /api/matches/{id}/detail` (MatchDetailDto) ile beslenir; karşılama ve
 * aksiyon menüsü statik tasarım içeriğidir (Teknik Doküman §9 "Statik").
 */

/** SPA görünüm state'i (Teknik Doküman §3). */
export type ActiveView =
  | "dashboard"
  | "analysis"
  | "lineup"
  | "stats"
  | "news"
  | "live";

export type ActionKey = "analysis" | "stats" | "lineup" | "live" | "news";

export interface MatchAction {
  key: ActionKey;
  label: string;
  /** Hedef görünüm. */
  view: ActiveView;
  slot: "primary" | "grid";
}

/** Asistan karşılama metni — statik (Stitch ile birebir). */
export const AI_GREETING =
  "Hoş geldiniz, bu maç ile ilgili size nasıl yardımcı olabilirim?";

/** Dashboard aksiyonları — Stitch etiketleriyle birebir. */
export const MATCH_ACTIONS: MatchAction[] = [
  { key: "analysis", label: "AI Maç Analizi", view: "analysis", slot: "primary" },
  { key: "stats", label: "Form Durumları", view: "stats", slot: "grid" },
  { key: "lineup", label: "Kadro Bilgisi", view: "lineup", slot: "grid" },
  // "Canlı Takip" KALDIRILDI (kilitli ürün kararı): FORMAX canlı maç göstermez.
  // Detay ekranı yalnız maç öncesi analiz sunar; canlı skor/dakika/olay yoktur.
  { key: "news", label: "Son Dakika", view: "news", slot: "grid" },
  // "Önemli anlar" video aksiyonu KALDIRILDI (13.09.2026, kilitli ürün kararı): yaklaşan,
  // canlı ya da bitmiş hiçbir maç ekranında gösterilmez. Bitmiş maçta yalnız doğrulanmış
  // resmî MAÇ ÖZETİ oynatıcısı kalır (FinishedMatchSummary).
];

/**
 * KALDIRILDI — buildAnalysisText().
 * AI metni artık YALNIZCA Decision paketinden gelir:
 *   GET /api/matches/{id}/decision → decision.reading (bkz. DecisionBlocks).
 * Bu ekran /detail'i AI için referans almaz; frontend metin birleştirmez.
 */

/**
 * KALDIRILDI — hasLiveData().
 * Maçın canlı olup olmadığına artık BACKEND karar verir:
 *   GET /api/matches/{id}/livefeed → state (NotStarted | Live | Finished).
 * Frontend maç durumunu /detail'den TÜRETMEZ.
 */

export type ConfidenceLevel = "YÜKSEK" | "ORTA" | "DÜŞÜK";

export interface AiConfidence {
  score: number; // 0–100
  level: ConfidenceLevel;
}

function normalizeLevel(raw: string): ConfidenceLevel {
  const v = raw?.toLocaleUpperCase("tr-TR");
  if (v === "YÜKSEK") return "YÜKSEK";
  if (v === "ORTA") return "ORTA";
  return "DÜŞÜK";
}

/**
 * Karar paketi endeksi — DOĞRUDAN backend'in ürettiği değerdir (kullanıcıya "AI Beklentisi" olarak snapshot yüzdesi gösterilir).
 * Kaynak: Decision paketi `confidence { score, level, basis }`.
 *
 * Frontend hesaplama YAPMAZ. Eskiden senaryo olasılığından türetip, o da yoksa
 * `ai.state`'e göre 65/40/25 uyduruyordu — ikisi de kaldırıldı. Backend değer
 * vermezse gösterge hiç render edilmez (null döner, placeholder yok).
 */
export function readAiConfidence(
  confidence?: { score: number; level: string } | null
): AiConfidence | null {
  if (!confidence || typeof confidence.score !== "number") return null;
  return { score: Math.round(confidence.score), level: normalizeLevel(confidence.level) };
}

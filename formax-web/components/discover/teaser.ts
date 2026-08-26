import type { RadarNarrativeDto, RecommendationCardDto } from "@/types/api";

/**
 * FORMAX — Keşfet teaser SEÇİCİSİ.
 *
 * TEK KAYNAK: Match Intelligence anlatısı (Gemma) → /api/matches/{id}/detail
 * → aiNarrative. Backend Keşfet yüzeyi için AYRI bir metin üretir
 * (radarSummary + highlights); bu, Maç Detayı ve AI İncele metinlerinin
 * kopyası değildir.
 *
 * Bu dosya CÜMLE ÜRETMEZ: backend'in yazdığı satırlardan en fazla MAX_LINES
 * tanesini SEÇER. Sabit metin, klişe, şablon veya fallback cümle YOKTUR —
 * backend boşsa boş dizi döner ve kart AI bölümünü hiç göstermez.
 *
 * KİLİTLİ KARAR (16.08): eski Decision/MatchReadingEngine teaser'ı
 * (decision.reading → whyWatch/mainStory/turningPoint/hidden/context)
 * KALDIRILDI. O katman maçta yer almayan takımdan söz eden, tekrar eden ve
 * bozuk ekli cümleler üretiyordu; kullanıcıya artık gösterilmez. /decision
 * endpoint'i ve backend kodu yerinde durur, yalnız SAYISAL karar verisi
 * (market/olasılık/güven) için kullanılır.
 *
 * Tüm analiz verilmez: teaser kullanıcıyı Maç Detay'a göndermek içindir.
 */

const MAX_LINES = 3;

const ok = (s?: string | null): s is string => typeof s === "string" && s.trim().length > 0;

export function buildNarrativeTeaserLines(narrative?: RadarNarrativeDto | null): string[] {
  if (!narrative) return [];

  const candidates: (string | undefined | null)[] = [
    narrative.radarSummary,
    ...(narrative.highlights ?? []),
  ];

  return pickLines(candidates);
}

/**
 * KEŞFET KARTININ TEASER'I — kaynak FEED YANITIDIR (/api/home/recommendations).
 *
 * Kart, teaser göstermek için maç detayı ucunu ÇAĞIRMAZ: o uç tek istekte üç
 * yüzeyi birden ürettiği için kart görüntülemek gereksiz LLM üretimine mal
 * oluyordu. Backend anlatıyı feed yanıtında taşır; yoksa alanlar boş gelir ve
 * kart AI yorumu bölümünü hiç göstermez (uydurma metin yok).
 */
export function buildCardTeaserLines(card?: RecommendationCardDto | null): string[] {
  if (!card) return [];

  return pickLines([card.radarSummary, ...(card.radarHighlights ?? [])]);
}

/** Ortak seçici: dolu satırları sırayla alır, tekrarı eler, MAX_LINES'ta durur. */
function pickLines(candidates: (string | undefined | null)[]): string[] {
  const lines: string[] = [];
  const seen = new Set<string>();

  for (const c of candidates) {
    if (!ok(c)) continue;
    const line = c.trim();
    const key = line.toLocaleLowerCase("tr-TR");
    if (seen.has(key)) continue; // aynı cümle iki alandan gelebilir
    seen.add(key);
    lines.push(line);
    if (lines.length === MAX_LINES) break;
  }

  return lines;
}

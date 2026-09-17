import type { OutcomeSnapshotDto } from "@/types/outcomes";

/** Kart etiketi — "Güven" değil "Beklenti". */
export const OUTCOME_EXPECTATION_LABEL = "Beklenti";
export const OUTCOME_PENDING_TEXT = "Bu maç için AI olası sonuçları arka planda hazırlanıyor.";
/** Limited / Disabled tahmin — backend yüzde göndermez; ekran yalnız bu metni gösterir. */
export const OUTCOME_INSUFFICIENT_TEXT = "Bu maç için güvenilir AI beklentisi oluşturacak yeterli doğrulanmış veri bulunmuyor.";
/** Kullanıcıya görünen sayısal gösterge etiketi (Keşfet halkası + Detay göstergesi). */
export const AI_EXPECTATION_LABEL = "AI Beklentisi";
export const OUTCOME_DISCLAIMER = "Olasılıklar kesin sonuç değildir; maçın nasıl geçebileceğine dair model beklentisidir.";

/**
 * Snapshot durumunu ekran durumuna çevirir. HESAP YOK: kart listesi, sırası ve yüzdeleri backend'indir;
 * burada yalnız hangi bileşenin çizileceği seçilir.
 */
export function outcomeViewState(s: OutcomeSnapshotDto | undefined | null):
  | { kind: "ready"; snapshot: OutcomeSnapshotDto }
  | { kind: "insufficient"; text: string }
  | { kind: "pending"; text: string } {
  if (!s || s.status === "Pending") return { kind: "pending", text: OUTCOME_PENDING_TEXT };
  if (s.status !== "Available" || s.mainCards.length === 0 || (s.predictionEligibility != null && s.predictionEligibility !== "Enabled"))
    return { kind: "insufficient", text: s.notice ?? OUTCOME_INSUFFICIENT_TEXT };
  return { kind: "ready", snapshot: s };
}

/**
 * AI BEKLENTİSİ — güncel snapshot'ın "Maç Sonucu" ana kartının backend yüzdesi (hesap yok). Snapshot hazır değilse ya da
 * tahmin Enabled değilse null → gösterge HİÇ çizilmez (uydurma sayı yok).
 */
export function outcomeExpectation(s: OutcomeSnapshotDto | undefined | null): { value: number; side: string } | null {
  const view = outcomeViewState(s);
  if (view.kind !== "ready") return null;
  const card = view.snapshot.mainCards.find((c) => c.family === "MatchResult");
  if (!card) return null;
  const side = card.marketKey === "MS1" ? "Ev Sahibi" : card.marketKey === "MS2" ? "Deplasman" : card.marketKey === "MSX" ? "Beraberlik" : card.market;
  return { value: card.probability, side };
}

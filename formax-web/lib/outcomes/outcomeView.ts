import type { OutcomeSnapshotDto } from "@/types/outcomes";

/** Kart etiketi — "Güven" değil "Beklenti". */
export const OUTCOME_EXPECTATION_LABEL = "Beklenti";
export const OUTCOME_PENDING_TEXT = "Bu maç için AI olası sonuçları arka planda hazırlanıyor.";
export const OUTCOME_INSUFFICIENT_TEXT = "Bu maç için olası sonuç üretecek yeterli doğrulanmış veri bulunamadı.";
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
  if (s.status !== "Available" || s.mainCards.length === 0)
    return { kind: "insufficient", text: s.notice ?? OUTCOME_INSUFFICIENT_TEXT };
  return { kind: "ready", snapshot: s };
}

// FORMAX · Maç olayı etiketi — kullanıcıya görünen tek metin.
//
// KAYNAK backend'dir (MatchEventLabels, deterministik sözlük; LLM YOK). Ekran eşlemeyi
// YENİDEN YAZMAZ: yalnız backend'in `label` alanını gösterir. Alan gelmediyse (eski yanıt)
// ham sağlayıcı terimi ("Substitution 1", "Normal Goal") ASLA gösterilmez — olay türünün
// güvenli genel adı kullanılır. Ham değer DTO'nun `detail` alanında teşhis için kalır.

import type { MatchEventDto } from "@/types/api";

const SAFE_BY_TYPE: Record<string, string> = {
  goal: "Gol",
  card: "Kart",
  subst: "Oyuncu Değişikliği",
  var: "VAR İncelemesi",
};

export const UNKNOWN_EVENT_LABEL = "Maç Olayı";

export function eventLabel(e: Pick<MatchEventDto, "label" | "eventType">): string {
  const fromBackend = e.label?.trim();
  if (fromBackend) return fromBackend;
  return SAFE_BY_TYPE[(e.eventType ?? "").trim().toLowerCase()] ?? UNKNOWN_EVENT_LABEL;
}

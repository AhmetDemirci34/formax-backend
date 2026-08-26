// ─────────────────────────────────────────────────────────────────────────────
// Maçlar ekranı — GÜN SEKMESİ yardımcıları.
//
// MOCK VERİ KALDIRILDI: eski SAMPLE_MATCHES (uydurma maçlar, sahte AI güveni,
// sahte ana tahmin, sahte EV/BER/DEP oranları) ve MaclarMatch/MaclarLeague
// modelleri silindi. Maçlar ekranı artık yalnızca GERÇEK backend verisiyle
// çalışır: GET /api/matches → MatchListItemDto (bkz. lib/api/matchList.ts).
//
// Bu dosyada yalnız tarih/gün etiketi üreten saf yardımcılar kalır — veri değil,
// biçimlendirme.
// ─────────────────────────────────────────────────────────────────────────────

const WEEKDAYS = ["Pazar", "Pzt", "Salı", "Çarş", "Perş", "Cuma", "Cmt"];
const MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

export interface DayMeta {
  key: string;   // "featured" | offset olarak "0".."3"
  label: string; // "Öne Çıkanlar" | "Bugün" | "Pazar" ...
  sub: string;   // "4 gün" | "05 Tem"
}

export function dayMeta(offset: number): DayMeta {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  const label = offset === 0 ? "Bugün" : WEEKDAYS[d.getDay()];
  const sub = `${String(d.getDate()).padStart(2, "0")} ${MONTHS[d.getMonth()]}`;
  return { key: String(offset), label, sub };
}

export function dayTabs(): DayMeta[] {
  return [
    { key: "featured", label: "Öne Çıkanlar", sub: "4 gün" },
    ...[0, 1, 2, 3].map(dayMeta),
  ];
}

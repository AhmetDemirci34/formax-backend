// ─────────────────────────────────────────────────────────────────────────────
// Maçlar ekranı — görünüm modeli + ÖRNEK (PLACEHOLDER) veri.
//
// ⚠️ ÖNEMLİ (veri bütünlüğü):
//   • teams / league / time / aiConfidence → gerçek feed'de VAR (RecommendationCardDto:
//     homeTeam, awayTeam, leagueName, matchDate, confidenceScore) → gerçek entegrasyonda
//     useRecommendations'tan beslenmelidir.
//   • mainPrediction (ör. "2.5 Gol Üst") ve odds (EV/BER/DEP) feed payload'unda YOKTUR.
//     cardSignals.ts kuralı: market olasılıkları Match Detail'e aittir, UYDURULMAZ.
//     Bu alanlar aşağıda yalnızca yeni ekranı görsel olarak çalıştırmak için örnek
//     placeholder'dır; canlıda Match Detail servisinden gelmelidir.
//
// Bu dosya gerçek feed pipeline'ına (cardSignals) BAĞLANMAZ — fake veriyi gerçek
// intelligence gibi göstermez; yalnızca /maclar ekranının iskeletini besler.
// ─────────────────────────────────────────────────────────────────────────────

export interface MaclarLeague {
  code: string;   // kısa lig kodu (emblem içinde) — ör. "SL"
  name: string;   // kısa ad — ör. "Süper Lig"
  color: string;  // emblem zemin rengi
}

export interface MaclarMatch {
  id: number;
  league: MaclarLeague;
  dayOffset: number; // bugünden gün farkı (0 = bugün) → gün sekmesi filtresi + tarih
  time: string;      // "21:45"
  home: string;
  away: string;
  homeLogoUrl?: string | null;
  awayLogoUrl?: string | null;
  followed: boolean;
  /** Maçın genel AI güveni (0–100) — gerçek: confidenceScore * 100. */
  aiConfidence: number;
  /** 🎯 AI'ın ana tahmini — PLACEHOLDER (feed'de yok, Match Detail'e ait). */
  mainPrediction: { label: string; confidence: number };
  /** 💬 AI Yorumu — en fazla 3 kısa paragraf (her biri 1–2 satır); son paragraf ana tahminle biter.
   *  PLACEHOLDER: canlıda RecommendationCardDto.aiComment/storyBody string'inden paragraflara bölünerek gelmeli. */
  aiComment: string[];
  /** Oran önizleme — PLACEHOLDER (feed'de yok). favored: neon accent verilen sonuç. */
  odds: { ev: number; draw: number; dep: number; favored: "ev" | "draw" | "dep" };
}

const LEAGUES = {
  SL: { code: "SL", name: "Süper Lig", color: "#E30A17" },
  LL: { code: "LL", name: "La Liga", color: "#EB5B2A" },
  PL: { code: "PL", name: "Premier Lig", color: "#37003C" },
} satisfies Record<string, MaclarLeague>;

// Örnek maçlar — AI güvenine göre yüksek → düşük sıralı (ürün ilkesi).
export const SAMPLE_MATCHES: MaclarMatch[] = [
  {
    id: 1, league: LEAGUES.SL, dayOffset: 0, time: "21:45",
    home: "Galatasaray", away: "Fenerbahçe", followed: false,
    aiConfidence: 84,
    mainPrediction: { label: "2.5 Gol Üst", confidence: 74 },
    aiComment: [
      "Galatasaray son haftalarda yüksek hücum temposuyla oynuyor, Fenerbahçe ise deplasmanda açık bir oyun tercih ediyor.",
      "İki takımın da savunmada zaman zaman boşluk vermesi, karşılıklı gol olasılığını artırıyor.",
      "Bu nedenle AI, 2.5 Gol Üst senaryosunu en güçlü seçenek olarak değerlendiriyor.",
    ],
    odds: { ev: 2.08, draw: 3.4, dep: 2.95, favored: "ev" },
  },
  {
    id: 2, league: LEAGUES.SL, dayOffset: 0, time: "19:00",
    home: "Beşiktaş", away: "Trabzonspor", followed: true,
    aiConfidence: 78,
    mainPrediction: { label: "KG Var", confidence: 71 },
    aiComment: [
      "Beşiktaş ve Trabzonspor bu dönemde gol yollarında etkili bir görüntü veriyor.",
      "Her iki takımın da savunmada açık vermesi, iki tarafın da fileleri bulmasını olası kılıyor.",
      "Bu nedenle AI, KG Var senaryosunu en güçlü seçenek olarak öne çıkarıyor.",
    ],
    odds: { ev: 1.95, draw: 3.55, dep: 3.2, favored: "ev" },
  },
  {
    id: 3, league: LEAGUES.LL, dayOffset: 1, time: "22:00",
    home: "Real Madrid", away: "Barcelona", followed: false,
    aiConfidence: 71,
    mainPrediction: { label: "2.5 Gol Alt", confidence: 63 },
    aiComment: [
      "El Clásico'nun yüksek önemi, iki takımı da temkinli bir başlangıca itiyor.",
      "Hata yapma korkusu oyunu orta alanda kilitleyebilir ve gol sayısını düşürebilir.",
      "Bu nedenle AI, 2.5 Gol Alt senaryosunu en güçlü seçenek olarak değerlendiriyor.",
    ],
    odds: { ev: 2.45, draw: 3.3, dep: 2.6, favored: "dep" },
  },
  {
    id: 4, league: LEAGUES.PL, dayOffset: 1, time: "18:30",
    home: "Man City", away: "Arsenal", followed: false,
    aiConfidence: 66,
    mainPrediction: { label: "Ev Sahibi", confidence: 69 },
    aiComment: [
      "Manchester City kendi sahasında baskın oyununu sürdürüyor ve genel formda önde.",
      "Arsenal'in deplasmanda daha temkinli bir plan izlemesi bekleniyor.",
      "Bu nedenle AI, Ev Sahibi senaryosunu en güçlü seçenek olarak görüyor.",
    ],
    odds: { ev: 1.75, draw: 3.8, dep: 4.1, favored: "ev" },
  },
];

// ── AI güven seviyesi → renk (locked tasarım skalası; tek-bakış güç okuması) ──
// %80+ neon yeşil · %60–79 turkuaz · %40–59 amber · %40 altı gri (muted).
export function confidenceColor(v: number): string {
  if (v >= 80) return "#2EE66E"; // --neon
  if (v >= 60) return "#2FD8C0"; // turkuaz (tek-bakış ayrımı için)
  if (v >= 40) return "#F5A623"; // --signal-amber
  return "#8b91a8"; // --text-secondary
}

// ── Gün sekmesi / tarih yardımcıları (bugüne göre dinamik) ──
const WEEKDAYS = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
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

export function kickoffLabel(match: MaclarMatch): string {
  return `${dayMeta(match.dayOffset).label} ${match.time}`;
}

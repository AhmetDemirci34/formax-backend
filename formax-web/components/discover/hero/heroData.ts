// FORMAX · Hero Discovery Feed (05) — view-model tipleri + mock maç verisi.
// İş mantığı / API yok; her maç kendi tüm verisini taşır (swipe'ta tümü değişir).

import type { PredictionTone } from "@/components/discover/PredictionCard";

export type HeroMetricTone = "green" | "purple" | "amber" | "red" | "blue";
export type HeroMetricIcon = "news" | "goal" | "fans" | "social" | "deviation";

export interface HeroMetricVM {
  label: string;
  value: string;
  tone: HeroMetricTone;
  icon: HeroMetricIcon;
}

export interface HeroPlayerVM {
  firstName: string;
  lastName: string;
  form: number;
  /** Forma rengi tonu (placeholder figür + parıltı). */
  tone: "blue" | "red";
  /** HeroSelectionEngine → HomeHero/AwayHero fotoğrafı. Boşsa placeholder figür. */
  photoUrl?: string | null;
}

export interface HeroTeamVM {
  name: string;
  short: string;
  logoUrl?: string | null;
}

/** AI Olası Sonuçlar satırı — maça özel (PredictionCard ile birebir). */
export interface HeroPredictionVM {
  code: string;
  description: string;
  percent: string;
  odds: string;
  tone: PredictionTone;
  data: number[];
}

export interface HeroMatchVM {
  id: string;
  league: string;
  home: HeroTeamVM;
  away: HeroTeamVM;
  time: string;
  confidence: number;
  confidenceLabel: string;
  newsCount: number;
  leftPlayer: HeroPlayerVM;
  rightPlayer: HeroPlayerVM;
  aiComment: string;
  /** Bu maça ait AI Olası Sonuçlar. */
  predictions: HeroPredictionVM[];
  // Hero'da artık kullanılmıyor (eski dev başlık/metrik paneli) — opsiyonel bırakıldı.
  titleTop?: string;
  titleBottom?: string;
  metrics?: HeroMetricVM[];
}

// ── Mock feed (3 maç) — swipe ile hepsi değişir. Backend gelince yalnızca kaynak değişir. ──
export const MATCHES: HeroMatchVM[] = [
  {
    id: "mci-liv",
    league: "Premier League",
    home: { name: "Man City", short: "MCI" },
    away: { name: "Liverpool", short: "LIV" },
    time: "Bugün 20:30",
    confidence: 92,
    confidenceLabel: "ÇOK YÜKSEK",
    newsCount: 624,
    leftPlayer: { firstName: "ERLING", lastName: "HAALAND", form: 9.8, tone: "blue" },
    rightPlayer: { firstName: "MOHAMED", lastName: "SALAH", form: 9.4, tone: "red" },
    aiComment:
      "Hücum gücü yüksek iki takımın karşılaşmasında gol potansiyeli oldukça yüksek. " +
      "Son analizlere göre tempo ortalamanın üstünde olacak. Maçın kırılma anı orta sahada yaşanacak.",
    // AI'nın bu maça özel en güçlü 3 senaryosu (karışık kategoriler).
    predictions: [
      { code: "MS 1", description: "Man City Kazanır", percent: "%78", odds: "1.56", tone: "green", data: [40, 44, 42, 50, 55, 60, 66] },
      { code: "2.5 Üst", description: "Toplam Gol 2.5 Üst", percent: "%84", odds: "1.62", tone: "green", data: [52, 56, 58, 64, 70, 74, 80] },
      { code: "KG Var", description: "Karşılıklı Gol Var", percent: "%71", odds: "1.80", tone: "green", data: [46, 50, 52, 58, 62, 66, 70] },
    ],
  },
  {
    id: "rma-fcb",
    league: "La Liga",
    home: { name: "Real Madrid", short: "RMA" },
    away: { name: "Barcelona", short: "FCB" },
    time: "Bugün 22:00",
    confidence: 84,
    confidenceLabel: "YÜKSEK",
    newsCount: 512,
    leftPlayer: { firstName: "JUDE", lastName: "BELLINGHAM", form: 9.5, tone: "blue" },
    rightPlayer: { firstName: "ROBERT", lastName: "LEWANDOWSKI", form: 9.1, tone: "red" },
    aiComment:
      "El Clásico'da iki takım da yüksek pres uygulayacak. Real Madrid ev sahibi avantajıyla öne çıkıyor; " +
      "geçiş hücumları ve duran toplar maçın kaderini belirleyebilir.",
    // Farklı kategorilerden — ilk yarı + çifte şans + toplam gol.
    predictions: [
      { code: "1X", description: "Real Madrid Kaybetmez", percent: "%74", odds: "1.36", tone: "green", data: [50, 54, 56, 62, 66, 70, 74] },
      { code: "3.5 Alt", description: "Toplam Gol 3.5 Alt", percent: "%66", odds: "1.60", tone: "green", data: [44, 48, 50, 56, 60, 63, 66] },
      { code: "İY X", description: "İlk Yarı Beraberlik", percent: "%44", odds: "2.05", tone: "purple", data: [30, 32, 29, 33, 31, 30, 28] },
    ],
  },
  {
    id: "bay-bvb",
    league: "Bundesliga",
    home: { name: "Bayern Münih", short: "BAY" },
    away: { name: "Dortmund", short: "BVB" },
    time: "Bugün 19:30",
    confidence: 76,
    confidenceLabel: "YÜKSEK",
    newsCount: 388,
    leftPlayer: { firstName: "HARRY", lastName: "KANE", form: 9.6, tone: "red" },
    rightPlayer: { firstName: "JULIAN", lastName: "BRANDT", form: 8.7, tone: "blue" },
    aiComment:
      "Der Klassiker'de Bayern'in hücum organizasyonu belirleyici. Dortmund kontratakta tehlikeli; " +
      "ilk yarı temposu yüksek, ikinci yarıda gol beklentisi artıyor.",
    // Farklı kategorilerden — maç sonucu + ilk gol + gol aralığı.
    predictions: [
      { code: "MS 1", description: "Bayern Kazanır", percent: "%64", odds: "1.72", tone: "green", data: [44, 46, 48, 52, 56, 60, 64] },
      { code: "İlk Gol", description: "İlk Golü Bayern", percent: "%69", odds: "1.50", tone: "green", data: [48, 50, 54, 58, 62, 66, 69] },
      { code: "4+ Gol", description: "Toplam 4+ Gol", percent: "%38", odds: "2.55", tone: "purple", data: [26, 28, 30, 33, 35, 37, 38] },
    ],
  },
];

/** Geriye uyum: tek maç isteyen yerler için ilk maç. */
export const HERO_DEMO: HeroMatchVM = MATCHES[0];

// Ton → Tailwind text sınıf eşlemesi (tek kaynak; magic değer yok).
export const TONE_TEXT: Record<HeroMetricTone, string> = {
  green: "text-neon",
  purple: "text-signal-purple",
  amber: "text-signal-amber",
  red: "text-signal-red",
  blue: "text-signal-blue",
};

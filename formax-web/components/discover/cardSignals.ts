// ─────────────────────────────────────────────────────────────────────────────
// Discover — card → view-model derivations (single source of truth).
// Every value maps to a REAL RecommendationCardDto field; absent/zero data yields
// null/empty so the consuming component hides itself. No data is invented.
// Emoji that arrive inside backend strings are stripped for a premium, SVG-only UI.
// ─────────────────────────────────────────────────────────────────────────────
import type { RecommendationCardDto } from "@/types/api";
import type { IconKey } from "./icons";

// Banding thresholds (no magic numbers in components).
export const RADAR_HIGH = 66;
export const RADAR_MID = 40;
export const IMPORTANCE_VERY_HIGH = 80;
export const IMPORTANCE_HIGH = 60;
export const IMPORTANCE_MID = 40;

export type RadarLevel = "YÜKSEK" | "ORTA" | "DÜŞÜK";
export type Level = 1 | 2 | 3;

export interface UserBadgeVM {
  iconKey: IconKey;
  label: string;
}

export interface QuickSignalVM {
  key: string;
  iconKey: IconKey;
  label: string;
  value: string; // gerçek yüzde, ör. "%79"
  source: string; // verinin geldiği Intelligence katmanı (ör. "Global Trend")
}

// ── Emoji / leading-symbol stripper (presentation only — backend data untouched) ──
export function stripEmoji(input?: string): string {
  if (!input) return "";
  return input
    .replace(/\p{Extended_Pictographic}/gu, "")
    .replace(/[️‍]/g, "")
    .replace(/^[\s·:\-–—]+/, "")
    .replace(/\s{2,}/g, " ")
    .trim();
}

// ── Team display name ────────────────────────────────────────────────────────
export function homeName(card: RecommendationCardDto): string {
  return card.homeTeam?.name || card.teamA || "—";
}
export function awayName(card: RecommendationCardDto): string {
  return card.awayTeam?.name || card.teamB || "—";
}

// ── User badge — WHY this match is surfaced to YOU (recommendationReason) ─────
export function userBadge(card: RecommendationCardDto): UserBadgeVM | null {
  if (card.recommendationReason === "FOLLOWED_TEAM") return { iconKey: "heart", label: "Favorin" };
  if (card.recommendationReason === "TRENDING" || card.trend?.isTrending)
    return { iconKey: "flame", label: "Bugün Trend" };
  if (card.recommendationReason === "HIGH_INTEREST") return { iconKey: "star", label: "Senin İçin" };
  if (card.recommendationReason === "MARKET_SIGNAL") return { iconKey: "activity", label: "Piyasa Hareketi" };
  if (card.recommendationReason === "GLOBAL_SIGNAL") return { iconKey: "users", label: "Genel İlgi" };
  return null;
}

// ── Hero / AI tema ayrımı — Hero tek headline verdict, AI ise Hero temasını DIŞLAYIP
// kalan açıları sentezler. Böylece Hero ile AI birbirini tekrarlamaz.
type IntelTheme = "importance" | "derby" | "rank" | "buzz" | "form" | "tempo" | "radar";

export function intelTheme(card: RecommendationCardDto): IntelTheme {
  const titles = (card.keySignals ?? []).map((s) => stripEmoji(s?.title));
  const importance = Number(card.matchImportance ?? 0);
  const text = `${card.storyHeadline ?? ""} ${card.storyBody ?? ""}`;
  const goalTag = (card.tags ?? []).map(stripEmoji).find((t) => /gol|skor/i.test(t));

  if (importance >= IMPORTANCE_HIGH) return "importance";
  if (titles.some((t) => /derbi/i.test(t))) return "derby";
  if (titles.some((t) => /(zirve|lider|rekabet)/i.test(t))) return "rank";
  if ((card.globalTrendScore ?? 0) > 0.6 || BUZZ.test(text)) return "buzz";
  if (titles.some((t) => /form/i.test(t))) return "form";
  if (goalTag && /yüksek/i.test(goalTag)) return "tempo";
  return "radar";
}

// KALDIRILDI — HERO_SENTENCE / heroLine().
// Frontend'de yazılmış 7 sabit cümle vardı ("Form grafikleri bu maçı öne çıkarıyor",
// "FORMAX radarında bugün öne çıkan bir eşleşme" …) ve "her zaman dolu" olduğu için
// gerçek analiz olmayan maçlarda bile klişe metin gösteriyordu.
// Keşfet teaser'ı artık YALNIZCA Decision paketinden seçilir: components/discover/teaser.ts
// (buildTeaserLines → decision.reading). Frontend cümle üretmez.

// ── Radar — discovery signal strength (radarScore, else recommendation score) ──
function radarValue(card: RecommendationCardDto): number {
  return card.radarScore && card.radarScore > 0 ? card.radarScore : (card.score ?? 0);
}
export function radarLevel(card: RecommendationCardDto): RadarLevel {
  const v = radarValue(card);
  if (v >= RADAR_HIGH) return "YÜKSEK";
  if (v >= RADAR_MID) return "ORTA";
  return "DÜŞÜK";
}
export function radarLevelNum(card: RecommendationCardDto): Level {
  const v = radarValue(card);
  if (v >= RADAR_HIGH) return 3;
  if (v >= RADAR_MID) return 2;
  return 1;
}

// ── Reason chips — CONCRETE reasons to WATCH (the primary reason already lives in the
// hero whyLine + badge). Backend-weighted key signals only (Derbi, Zirve, Form…).
// Negatives ("Zayıf Form" gibi) bir maçı önermenin gerekçesi değildir → elenir.
const NEGATIVE_SIGNAL = /^(zayıf|düşük|kötü)/i;
export function reasonChips(card: RecommendationCardDto): string[] {
  const titles = (card.keySignals ?? [])
    .map((s) => stripEmoji(s?.title))
    .filter(Boolean);

  const positive = titles.filter((t) => !NEGATIVE_SIGNAL.test(t));
  // Pozitif sinyal varsa negatifleri gösterme; hiç yoksa elimizdekini göster.
  const chosen = positive.length > 0 ? positive : titles;
  return [...new Set(chosen)].slice(0, 3);
}

// ── Quick signals — REAL, quantitative, dynamic. Single accent + level bars. ──
// Hiçbir değer sabit/kelime değil: her biri kart-kart değişen GERÇEK Intelligence yüzdesi.
// NOT: 2.5 Üst / KG Var gibi market olasılıkları feed payload'unda YOK (onlar Match Detail'de) →
// uydurulmaz; bunun yerine feed'in gerçek Intelligence skorları yüzdeye çevrilir.
const pct = (x: number): string => `%${Math.round(x)}`;

export function quickSignals(card: RecommendationCardDto): QuickSignalVM[] {
  const out: QuickSignalVM[] = [];

  // Maç Önemi — gerçek matchImportance (0–100). 0 ise gizlenir.
  const importance = Number(card.matchImportance ?? 0); // DTO types this as string; coerce, don't mutate.
  if (importance > 0) {
    out.push({ key: "importance", iconKey: "trophy", label: "Maç Önemi", value: pct(importance), source: "FORMAX Radar" });
  }

  // Küresel İlgi — gerçek globalTrendScore (0–1).
  out.push({ key: "global", iconKey: "users", label: "Küresel İlgi", value: pct((card.globalTrendScore ?? 0) * 100), source: "Global Trend" });

  // Öneri güven skoru — gerçek confidenceScore (0–1). Kullanıcı etiketi: AI Beklentisi.
  out.push({ key: "confidence", iconKey: "shield", label: "AI Beklentisi", value: pct((card.confidenceScore ?? 0) * 100), source: "AI Intelligence" });

  // İlgi İvmesi — gerçek momentumScore (0–1). Yalnız boş slot varsa (cap 3).
  if (out.length < 3) {
    out.push({ key: "momentum", iconKey: "activity", label: "İlgi İvmesi", value: pct((card.momentumScore ?? 0) * 100), source: "Momentum" });
  }

  return out.slice(0, 3);
}

// ── Competition label — storyHeadline GERÇEKTEN bir lig/turnuva adıysa döner. ──
// storyHeadline lig ("Irish Premier Division") olabildiği gibi buzz ("Bu Hafta…") veya
// form-anlatısı ("Form farkı belirgin…") da olabilir → yalnız lig anahtar kelimeleri kabul edilir.
const BUZZ = /(konuşul|gündem|hafta)/i;
const LEAGUE_KW =
  /(lig|league|division|cup|kupa|primera|segunda|serie|bundesliga|eredivisie|ligue|championship|premier|liga|conference|s[uü]per|[şs]ampiyon|world cup|copa|higher|first|second|third|fourth)/i;
export function leagueLabel(card: RecommendationCardDto): string | null {
  return card.leagueName?.trim() || null;
}

// KALDIRILDI — aiIntelligence() ve AI_DISCLAIMER.
// Frontend'de yazılmış cümle bankasıydı ("Derbi atmosferi maçın belirleyici yönü olarak
// değerlendiriliyor.", "Akışta yüksek tempolu, gollü bir maç beklentisi öne çıkıyor." …).
// Keşfet teaser'ı artık YALNIZCA Decision paketinden seçilir: components/discover/teaser.ts

// ── Maç zamanı — gerçek MatchDate'ten. Gelecek: Bugün/Yarın/tarih · Başladı: Canlı · Geçmiş: Bitti.
// NOT: feed'de canlı DAKİKA yok (yalnız MatchDate izinli) → "Canlı" dakikasız; yapı dakikaya hazır.
const LIVE_WINDOW_MIN = 135;
export interface MatchTimeVM {
  text: string;
  live: boolean;
}
export function matchTime(card: RecommendationCardDto): MatchTimeVM | null {
  const iso = card.matchDate;
  if (!iso) return null;
  const ts = new Date(iso).getTime();
  if (isNaN(ts)) return null;

  const now = Date.now();
  const diffMin = (ts - now) / 60000;

  if (diffMin > 0) {
    const d = new Date(ts);
    const hm = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(today.getDate() + 1);
    const sameDay = (a: Date, b: Date) =>
      a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    if (sameDay(d, today)) return { text: `Bugün ${hm}`, live: false };
    if (sameDay(d, tomorrow)) return { text: `Yarın ${hm}`, live: false };
    return { text: `${d.toLocaleDateString("tr-TR", { day: "2-digit", month: "short" })} ${hm}`, live: false };
  }

  if (-diffMin <= LIVE_WINDOW_MIN) return { text: "Canlı", live: true };
  return { text: "Bitti", live: false };
}

// ── Keşfet kapsamı: YALNIZ BAŞLAMAMIŞ MAÇ (kilitli ürün kararı) ───────────────
// Keşfet ekranında canlı, devre arası, uzatma, penaltı, bitmiş, ertelenmiş veya
// iptal edilmiş maç GÖRÜNMEZ. Kural sade ve iki koşulludur:
//   1) başlama zamanı gelecekte (kickoffUtc > nowUtc), ve
//   2) backend durumu "başlamadı" ailesinde (Scheduled / NotStarted / Upcoming …),
//      ayrıca backend isLive dememiş olmalı.
// Frontend maç DURUMU ÜRETMEZ: yalnız backend'in gönderdiği status/isLive/kickoff
// alanlarını okur. Durum alanı hiç gelmezse tek ölçüt gerçek kickoff zamanıdır.
const NOT_STARTED_STATUSES = new Set([
  "notstarted",
  "ns",
  "scheduled",
  "upcoming",
  "pending",
  "tbd",
  "timetobedefined",
]);

/** Backend kickoff'u (kickoffTime öncelikli, yoksa matchDate) → ms; yoksa NaN. */
function kickoffMsOfCard(card: RecommendationCardDto): number {
  const iso = card.kickoffTime ?? card.matchDate;
  return iso ? new Date(iso).getTime() : NaN;
}

/**
 * Keşfet/öneri yüzeylerinin TEK kapsam kapısı: maç henüz başlamamış mı?
 * (Eski adı isDiscoverable — davranışı daraltıldı: canlı maç artık geçmez.)
 */
export function isDiscoverable(card: RecommendationCardDto): boolean {
  if (card.isLive === true) return false;

  const ts = kickoffMsOfCard(card);
  if (!Number.isFinite(ts) || ts <= Date.now()) return false;

  const s = card.status?.toString().toLowerCase().replace(/\s+|_|-/g, "");
  if (s && !NOT_STARTED_STATUSES.has(s)) return false;

  return true;
}

// ── Kısa AI etiketi — trending kartı için tek kelime, intel temasından (gerçek veriye bağlı).
const SHORT_TAG: Record<IntelTheme, string> = {
  importance: "Önemli",
  derby: "Derbi",
  rank: "Zirve",
  buzz: "Gündem",
  form: "Form",
  tempo: "Tempo",
  radar: "Radar",
};
export function shortTag(card: RecommendationCardDto): string {
  return SHORT_TAG[intelTheme(card)];
}

// ── Referans tasarımı için gerçek feed yüzdeleri (hiçbiri uydurma) ──────────────
export const interestPct = (c: RecommendationCardDto): number => Math.round((c.globalTrendScore ?? 0) * 100);
export const momentumPct = (c: RecommendationCardDto): number => Math.round((c.momentumScore ?? 0) * 100);
export const importancePct = (c: RecommendationCardDto): number => Math.round(Number(c.matchImportance ?? 0));
export const isTopInterest = (c: RecommendationCardDto): boolean => (c.globalTrendScore ?? 0) > 0.6;

// ── "Neden bu maç?" ikonlu chip listesi — gerçek keySignals + reason + gol tag'i ──
export interface ReasonChipVM {
  label: string;
  iconKey: IconKey;
}
function chipIcon(label: string): IconKey {
  if (/derbi/i.test(label)) return "users";
  if (/(zirve|lider|rekabet)/i.test(label)) return "trophy";
  if (/form/i.test(label)) return "trendingUp";
  if (/(gol|skor)/i.test(label)) return "goal";
  if (/ilgi/i.test(label)) return "star";
  return "target";
}
const REASON_CHIP: Record<string, string> = {
  FOLLOWED_TEAM: "Favori Takım",
  HIGH_INTEREST: "Yüksek İlgi",
  TRENDING: "Yükselişte",
  MARKET_SIGNAL: "Piyasa Sinyali",
  GLOBAL_SIGNAL: "Genel İlgi",
};
export function reasonChipList(card: RecommendationCardDto): ReasonChipVM[] {
  const labels: string[] = [];
  const titles = (card.keySignals ?? []).map((s) => stripEmoji(s?.title)).filter(Boolean);
  // Negatifler ("Zayıf Form" gibi) bir maçı önermenin gerekçesi değil → hiç gösterilmez.
  titles.filter((t) => !/^(zayıf|düşük|kötü)/i.test(t)).forEach((t) => labels.push(t));

  const goalTag = (card.tags ?? []).map(stripEmoji).find((t) => /gol|skor/i.test(t));
  if (goalTag && /yüksek/i.test(goalTag)) labels.push("Gol Potansiyeli");

  const r = REASON_CHIP[card.recommendationReason];
  if (r) labels.push(r);

  return [...new Set(labels)].slice(0, 5).map((l) => ({ label: l, iconKey: chipIcon(l) }));
}

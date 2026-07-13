// ─────────────────────────────────────────────────────────────────────────────
// Discover — detay-farkındalıklı zengin içerik (REAL Intelligence).
// Aktif maçın gerçek Match Detail'i (lastMatches, comparison, risk, probabilities,
// sapma) + feed (globalTrend, momentum, importance) birleştirilir. Hiçbir yüzde
// uydurma değildir; gerçek kaynağı olmayan metrik üretilmez (gizlenir).
// ─────────────────────────────────────────────────────────────────────────────
import type { MatchDetailDto, RecommendationCardDto } from "@/types/api";
import type { ReasonChipVM } from "./cardSignals";
import type { SignalVM } from "./QuickSignalCard";
import { awayName, homeName, importancePct, interestPct, intelTheme, stripEmoji } from "./cardSignals";

const tr1 = (x: number) => x.toFixed(1).replace(".", ",");

function wld(list: { result: string }[]) {
  return {
    w: list.filter((m) => m.result === "W").length,
    d: list.filter((m) => m.result === "D").length,
    l: list.filter((m) => m.result === "L").length,
    n: list.length,
  };
}

// ── "Neden bu maç?" — spesifik, tek başına anlamlı gerçek faktörler ───────────
export function reasonFacts(card: RecommendationCardDto, detail?: MatchDetailDto): ReasonChipVM[] {
  const out: ReasonChipVM[] = [];
  const home = homeName(card);
  const away = awayName(card);

  if (detail) {
    const hl = wld(detail.homeTeamLastMatches ?? []);
    const al = wld(detail.awayTeamLastMatches ?? []);

    if (hl.n >= 2 && hl.w >= 2 && hl.w >= hl.n - 1)
      out.push({ label: `${home} son ${hl.n} maçın ${hl.w}'ini kazandı`, iconKey: "trophy" });
    else if (hl.n >= 2 && hl.l >= hl.n - 1)
      out.push({ label: `${home} son ${hl.n} maçta ${hl.l} yenilgi aldı`, iconKey: "trendingUp" });

    if (al.n >= 2 && al.w >= 2 && al.w >= al.n - 1)
      out.push({ label: `${away} son ${al.n} maçın ${al.w}'ini kazandı`, iconKey: "trophy" });
    else if (al.n >= 2 && al.l >= al.n - 1)
      out.push({ label: `${away} son ${al.n} maçta ${al.l} yenilgi aldı`, iconKey: "trendingUp" });

    const c = detail.comparison;
    if (c?.away?.goalScoringRate >= 70)
      out.push({ label: `${away} son maçların %${c.away.goalScoringRate}'inde gol attı`, iconKey: "goal" });
    if (c?.home?.cleanSheetRate >= 60)
      out.push({ label: `${home} son maçların %${c.home.cleanSheetRate}'inde gol yemedi`, iconKey: "shield" });
  }

  // Feed sinyalleri
  if ((card.globalTrendScore ?? 0) > 0.8) out.push({ label: "Dünya gündeminde öne çıkıyor", iconKey: "star" });
  if ((card.crossUserScore ?? 0) > 0.7) out.push({ label: "Toplulukta yüksek ilgi", iconKey: "users" });
  const goalTag = (card.tags ?? []).map(stripEmoji).find((t) => /gol|skor/i.test(t));
  if (goalTag && /yüksek/i.test(goalTag)) out.push({ label: "Gol beklentisi yüksek", iconKey: "goal" });

  // Tekille + kıs
  const seen = new Set<string>();
  return out.filter((c) => !seen.has(c.label) && seen.add(c.label)).slice(0, 5);
}

// ── FORMAX AI YORUM — STORYTELLING (analist/gazeteci dili). İstatistik DEĞİL.
// Her cümle gerçek bir sinyale dayanır; ama "veriyi okumaz", anlamlandırır.
// Sahte kaynak atfı YOK (News/Social verisi backend'de yoksa "Avrupa basını" denmez).
export function aiSummaryLines(card: RecommendationCardDto, detail?: MatchDetailDto): string[] {
  const exclude = intelTheme(card);
  const goalTag = (card.tags ?? []).map(stripEmoji).find((t) => /gol|skor/i.test(t));
  const highGoal = !!goalTag && /yüksek/i.test(goalTag);
  const lowGoal = !!goalTag && /(düşük|az)/i.test(goalTag);

  const cand: { theme: string; line: string }[] = [];

  // 1) Hikâye cümlesi — favori + ilginin nerede toplandığı (referans sesi).
  const c = detail?.comparison;
  if (c?.home && c?.away) {
    const stronger = c.away.formScore > c.home.formScore ? awayName(card) : homeName(card);
    if (highGoal)
      cand.push({ theme: "story", line: `${stronger} favori görünse de ilgi yoğunluğu gol senaryolarında toplanıyor.` });
    else if (lowGoal)
      cand.push({ theme: "story", line: `${stronger} öne çıksa da beklenti kontrollü bir oyun yönünde yoğunlaşıyor.` });
    else
      cand.push({ theme: "story", line: `${stronger} formuyla öne çıkıyor; maçın temposu belirleyici olacak.` });
  }

  // 2) Gündem
  if ((card.globalTrendScore ?? 0) > 0.8)
    cand.push({ theme: "buzz", line: "Bugün dünya genelinde en çok ilgi gören karşılaşmalardan biri." });

  // 3) Topluluk
  if ((card.crossUserScore ?? 0) > 0.7)
    cand.push({ theme: "community", line: "Topluluk ilgisi son dönemde belirgin biçimde arttı." });

  // 4) Form farkı
  if (c?.home && c?.away && Math.abs(c.away.formScore - c.home.formScore) >= 3)
    cand.push({ theme: "form", line: "İki takımın form farkı bu karşılaşmayı gündeme taşıyor." });

  // 5) Tempo beklentisi
  if (highGoal) cand.push({ theme: "tempo", line: "Beklenti yüksek tempolu, gollü bir oyun yönünde yoğunlaşıyor." });
  else if (lowGoal) cand.push({ theme: "tempo", line: "Topluluk beklentisi kontrollü, düşük tempolu bir oyun yönünde." });

  // 6) Savunma farkı (risk)
  if (c?.home && c?.away) {
    const leaky = c.home.avgGoalsAgainst >= c.away.avgGoalsAgainst ? c.home : c.away;
    if (leaky.avgGoalsAgainst >= 2)
      cand.push({ theme: "risk", line: "Savunma performansları arasındaki fark maçın kaderini belirleyebilir." });
  }

  let lines = cand.filter((x) => x.theme !== exclude).map((x) => x.line);
  if (lines.length === 0) {
    const fb = stripEmoji(card.storyBody) || stripEmoji(card.storyHeadline);
    if (fb) lines = [fb];
  }

  // Kapanış — narrative'i GERÇEK model çıktısına bağlayan ürün-dili cümlesi.
  // En yüksek olasılıklı market'i (2.5 Üst / KG Var …) gerçek probabilities'ten alır.
  const probs = detail?.probabilities ?? [];
  if (probs.length) {
    const top = [...probs].sort((a, b) => b.probability - a.probability)[0];
    if (top) {
      lines = lines.slice(0, 2);
      lines.push(`FORMAX Intelligence modeli bu maçta ${top.market} senaryosunu öne çıkarıyor.`);
    }
  }
  return lines.slice(0, 4);
}

// ── Hızlı Sinyaller — DİNAMİK Intelligence havuzu. Tüm GERÇEK sinyaller toplanır,
// en güçlü/anlamlı 4'ü öncelik sırasına göre otomatik seçilir. Her kart kaynak-katman
// etiketli. Sahte metrik (1X2 / xG / Hava / Hakem) feed/detayda gerçek üretilmediği
// için EKLENMEZ. Sabit kart yok — kart kümesi maça göre değişir.
export function quickSignalsRich(card: RecommendationCardDto, detail?: MatchDetailDto): SignalVM[] {
  const pool: { p: number; s: SignalVM }[] = [];
  const probs = detail?.probabilities ?? [];
  const c = detail?.comparison;

  const over = probs.find((x) => /2[.,]5/.test(x.market));
  if (over) pool.push({ p: 95, s: { key: "over25", iconKey: "goal", label: "2.5 Üst", value: `%${over.probability}`, caption: "Global Intelligence", color: "purple" } });

  const kg = probs.find((x) => /kg/i.test(x.market));
  if (kg) pool.push({ p: 90, s: { key: "kg", iconKey: "goal", label: "KG Var", value: `%${kg.probability}`, caption: "AI Consensus", color: "amber" } });

  if (c?.home && c?.away) {
    const atk = Math.max(c.home.goalScoringRate, c.away.goalScoringRate);
    if (atk >= 60) pool.push({ p: 80, s: { key: "atk", iconKey: "trendingUp", label: "Hücum Formu", value: `%${atk}`, caption: "Form Intelligence", color: "green" } });
    const def = Math.max(c.home.cleanSheetRate, c.away.cleanSheetRate);
    if (def >= 50) pool.push({ p: 76, s: { key: "def", iconKey: "shield", label: "Savunma Gücü", value: `%${def}`, caption: "Form Intelligence", color: "amber" } });
  }

  pool.push({ p: 70, s: { key: "interest", iconKey: "trendingUp", label: "İlgi Artışı", value: `%${interestPct(card)}`, caption: "Son 24 Saat", color: "green" } });

  if (importancePct(card) > 0)
    pool.push({ p: 66, s: { key: "importance", iconKey: "target", label: "Maç Önemi", value: `%${importancePct(card)}`, caption: "Radar Engine", color: "blue" } });

  const sapma = detail?.sapma;
  if (sapma?.sapmaBolgesi && !/denge/i.test(sapma.sapmaBolgesi)) {
    const z = sapma.sapmaBolgesi;
    const short = /yüksek/i.test(z) ? "Yüksek" : /yanılma/i.test(z) ? "Riskli" : "Dikkat";
    pool.push({ p: 62, s: { key: "sapma", iconKey: "pulse", label: "Piyasa Sapması", value: short, caption: "Model–kullanıcı farkı", color: "blue" } });
  }

  if ((card.crossUserScore ?? 0) > 0.7)
    pool.push({ p: 55, s: { key: "community", iconKey: "users", label: "Topluluk Beklentisi", value: `%${Math.round((card.crossUserScore ?? 0) * 100)}`, caption: "Social Intelligence", color: "purple" } });

  return pool.sort((a, b) => b.p - a.p).slice(0, 4).map((x) => x.s);
}

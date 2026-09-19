import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import fs from "node:fs";
import path from "node:path";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { OutcomeSnapshotDto } from "@/types/outcomes";
import {
  AI_EXPECTATION_LABEL,
  OUTCOME_INSUFFICIENT_TEXT,
  outcomeExpectation,
  outcomeViewState,
} from "@/lib/outcomes/outcomeView";
import { OUTCOMES_REFRESH_MS, matchOutcomesKey } from "@/hooks/useMatchOutcomes";
import { ConfidenceRing } from "@/components/discover/hero/ConfidenceRing";
import { ConfidenceGauge } from "@/components/match-center/ConfidenceGauge";
import { MatchCenterHero } from "@/components/match-center/MatchCenterHero";
import { OutcomeCards } from "@/components/outcomes/OutcomeCards";
import type { MatchDetailDto } from "@/types/api";

// FORMAX · TAHMİN UYGUNLUĞU (17.09.2026) — AI BEKLENTİSİ dili, Limited/Disabled boş durumu, Keşfet ↔ Detay aynı snapshot,
// manuel yenilemesiz güncelleme ve sayfa açılışında keşif/dış istek olmaması.

const root = path.resolve(__dirname, "..");
const read = (f: string) => fs.readFileSync(path.join(root, f), "utf8");

const card = (family: string, market: string, marketKey: string | null, probability: number) => ({
  family, familyTitle: family, market, marketKey, probability, rawProbability: probability / 100, calibratedProbability: probability / 100,
  baselineProbability: 0.4, informationLift: 0.05, evidenceCoverage: 1, sampleQuality: "Rich", uncertainty: 0, selectionScore: 0.1,
  reasonCodes: [], reason: null, limitation: null,
});

const snap = (p: Partial<OutcomeSnapshotDto> = {}): OutcomeSnapshotDto => ({
  snapshotId: "snp-1", matchId: 42, modelVersion: "formax-outcome-3.0", status: "Available", predictionEligibility: "Enabled",
  evidenceCoverage: 1, sampleQuality: "Rich", homeSampleSize: 20, awaySampleSize: 20, reasonCodes: [], topScores: [],
  mainCards: [card("MatchResult", "Deplasman Kazanır", "MS2", 47), card("TotalGoals", "2.5 Alt", "ALT_2_5", 56), card("BothTeamsScore", "Karşılıklı Gol Var", "KG_VAR", 53)],
  families: [],
  ...p,
});

function walk(dir: string, out: string[] = []): string[] {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (/\.(tsx?|json)$/.test(e.name)) out.push(p);
  }
  return out;
}

describe("AI BEKLENTİSİ dili", () => {
  it("Keşfet halkası ve Detay göstergesi 'AI Beklentisi' yazar", () => {
    const ring = renderToStaticMarkup(<ConfidenceRing value={47} label="" size={104} />);
    expect(ring).toContain("AI Beklentisi");
    expect(ring).not.toMatch(/AI Güven/i);
    const gauge = renderToStaticMarkup(<ConfidenceGauge score={47} level="Deplasman" />);
    expect(gauge).toContain("AI Beklentisi");
    expect(AI_EXPECTATION_LABEL).toBe("AI Beklentisi");
  });

  it("kullanıcı ekranı kaynaklarında (app, components, lib, hooks, types) eski 'AI GÜVENİ' metni yok", () => {
    const files = ["app", "components", "lib", "hooks", "types"].flatMap((d) => walk(path.join(root, d)));
    // Kullanıcıya görünen metin: string literal ya da JSX metni (yorum satırları iç teknik nottur).
    const visible = /["'`>]\s*AI\s*G[üÜ]VEN/i;
    const offenders = files.filter((f) => visible.test(fs.readFileSync(f, "utf8")));
    expect(offenders).toEqual([]);
  });
});

describe("Limited / Disabled tahmin", () => {
  it("NotEligible snapshot yüzdesiz dürüst metin gösterir; Available ama Enabled olmayan da gösterilmez", () => {
    expect(outcomeViewState(snap({ status: "NotEligible", predictionEligibility: "Limited", mainCards: [], notice: OUTCOME_INSUFFICIENT_TEXT })))
      .toEqual({ kind: "insufficient", text: "Bu maç için güvenilir AI beklentisi oluşturacak yeterli doğrulanmış veri bulunmuyor." });
    expect(outcomeViewState(snap({ predictionEligibility: "Disabled", notice: null })).kind).toBe("insufficient");
    expect(outcomeExpectation(snap({ predictionEligibility: "Limited" }))).toBeNull();
    expect(outcomeExpectation(snap())).toEqual({ value: 47, side: "Deplasman" });
  });

  it("ana kartlar farklı ailelerden gelir; backend sözleşmesi aynen çizilir", () => {
    const s = snap();
    expect(new Set(s.mainCards.map((c) => c.family)).size).toBe(s.mainCards.length);
  });

  it("kart sayısı 0/1/2/3 olabilir; ekran eksik yuvayı DOLDURMAZ, yer tutucu çizmez", () => {
    const one = snap({ mainCards: [card("MatchResult", "Ev Sahibi Kazanır", "MS1", 52)], publishedCardCount: 1, overallStatus: "Partial" });
    const two = snap({
      mainCards: [card("MatchResult", "Ev Sahibi Kazanır", "MS1", 52), card("TotalGoals", "1.5 Üst", "UST_1_5", 78)],
      publishedCardCount: 2, overallStatus: "Partial",
    });
    for (const [s, n] of [[one, 1], [two, 2], [snap(), 3]] as const) {
      const view = outcomeViewState(s);
      expect(view.kind).toBe("ready");
      const html = renderToStaticMarkup(<OutcomeCards snapshot={s} />);
      expect((html.match(/Beklenti/g) ?? []).length).toBe(n);      // kart başına tek etiket → kart sayısı kadar
      expect(html).not.toMatch(/placeholder|--%|%NaN/i);
    }
    // 0 kart: yüzde yok, dürüst metin.
    expect(outcomeViewState(snap({ mainCards: [], publishedCardCount: 0, overallStatus: "NotEligible", status: "NotEligible", predictionEligibility: "Limited" })).kind)
      .toBe("insufficient");
  });

  it("Partial durumda kullanıcıya teknik/korkutucu sistem dili gösterilmez", () => {
    const s = snap({
      mainCards: [card("MatchResult", "Ev Sahibi Kazanır", "MS1", 52)], publishedCardCount: 1, overallStatus: "Partial",
      markets: [
        { family: "MatchResult1X2", title: "Maç Sonucu", status: "Eligible", reasonCodes: [], published: true, sampleSize: 500 },
        { family: "BothTeamsToScore", title: "İki Takımın Gol Durumu", status: "WorseThanBaseline", reasonCodes: ["WORSE_THAN_LEAGUE_AVERAGE"], published: false, sampleSize: 500 },
      ],
    });
    const html = renderToStaticMarkup(<OutcomeCards snapshot={s} />);
    for (const forbidden of ["WORSE_THAN_LEAGUE_AVERAGE", "Partial", "NotEligible", "CalibrationFailed", "InsufficientSample", "market-eligibility"]) {
      expect(html).not.toContain(forbidden);
    }
    expect(html).not.toMatch(/AI GÜVENİ/i);
  });

  it("frontend eligibility HESAPLAMAZ: karar alanları yalnız okunur", () => {
    const view = read("lib/outcomes/outcomeView.ts");
    const cards = read("components/outcomes/OutcomeCards.tsx");
    for (const src of [view, cards]) {
      // Eşik/karşılaştırma ile market açma-kapama ya da yüzde aritmetiği yok.
      expect(src).not.toMatch(/baselineProbability\s*[<>]/);
      expect(src).not.toMatch(/informationLift\s*[<>]/);
      expect(src).not.toMatch(/selectionScore\s*[<>]/);
      expect(src).not.toMatch(/\.sort\(/);
      expect(src).not.toMatch(/probability\s*[+\-*/]\s*\d/);
    }
  });
});

describe("Keşfet ↔ Detay aynı snapshot, manuel yenilemesiz", () => {
  it("Keşfet halkası, Keşfet kartları, Detay göstergesi ve Detay kartları AYNI hook ve cache anahtarını kullanır", () => {
    for (const f of [
      "components/discover/hero/HeroCard.tsx",
      "components/discover/AIPredictionsSection.tsx",
      "components/match-center/MatchCenterHero.tsx",
      "components/match-center/views/AIAnalysisView.tsx",
    ]) {
      const src = read(f);
      expect(src, f).toContain("useMatchOutcomes(");
      expect(src, f).not.toMatch(/aiTrustScore|useMatchDecision\(/);
    }
    expect(matchOutcomesKey(42)).toEqual(["outcomes", 42]);
  });

  it("yeni snapshot dakikada bir salt-DB okumasıyla gelir; arka plandaki sekmede yoklama yok, tek uç", () => {
    expect(OUTCOMES_REFRESH_MS).toBe(60_000);
    const hook = read("hooks/useMatchOutcomes.ts");
    expect(hook).toContain("refetchInterval");
    expect(hook).toContain("refetchIntervalInBackground: false");
    expect(read("lib/api/outcomes.ts")).toMatch(/\/api\/matches\/\$\{matchId\}\/outcomes/);
  });

  it("Detay göstergesi snapshot'ı okur: yeni snapshot cache'e gelince sayı değişir, Limited olunca gösterge kaybolur", () => {
    const match = { matchId: 42, status: "NotStarted", matchDate: "2099-01-01T18:00:00.000Z", league: "Serie A",
      homeTeam: { name: "Venezia", logoUrl: null }, awayTeam: { name: "Fiorentina", logoUrl: null } } as unknown as MatchDetailDto;
    const render = (s: OutcomeSnapshotDto) => {
      const qc = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });
      qc.setQueryData(matchOutcomesKey(42), s);
      return renderToStaticMarkup(<QueryClientProvider client={qc}><MatchCenterHero match={match} /></QueryClientProvider>);
    };
    const first = render(snap());
    expect(first).toContain("AI Beklentisi");
    expect(first).toContain(">47<");
    const updated = render(snap({ snapshotId: "snp-2", mainCards: [card("MatchResult", "Ev Sahibi Kazanır", "MS1", 51), ...snap().mainCards.slice(1)] }));
    expect(updated).toContain(">51<");
    expect(render(snap({ status: "NotEligible", predictionEligibility: "Limited", mainCards: [] }))).not.toContain("AI Beklentisi");
  });
});

describe("sayfa açılışı", () => {
  it("maç sayfası ve Keşfet açılışı tahmin üretmez, keşif/yeniden hesap/dış kaynak isteği atmaz (yalnız okuma uçları)", () => {
    const files = [
      "app/match/[id]/page.tsx",
      "components/match-center/MatchCenterHero.tsx",
      "components/match-center/views/AIAnalysisView.tsx",
      "components/discover/AIPredictionsSection.tsx",
      "components/discover/hero/HeroCard.tsx",
      "hooks/useMatchOutcomes.ts",
      "lib/api/outcomes.ts",
    ].map((f) => read(f));
    for (const src of files) {
      expect(src).not.toMatch(/snapshots\/run|queue\/run|outcome-model\/train|video-discovery|api\/[^"`]*discover|catalog\/discover|youtube|oembed/i);
    }
  });
});

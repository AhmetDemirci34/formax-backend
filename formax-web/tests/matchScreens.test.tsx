import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import fs from "node:fs";
import path from "node:path";
import { ActionGrid } from "@/components/match-center/dashboard/ActionGrid";
import { MATCH_ACTIONS } from "@/components/match-center/aiContext";
import type { LineupPlayerDto, MatchDetailDto } from "@/types/api";
import type { OutcomeSnapshotDto } from "@/types/outcomes";
import { ANALYSIS_INSUFFICIENT_TEXT, ANALYSIS_PENDING_TEXT } from "@/lib/matches/postMatchTexts";
import { OutcomeCards } from "@/components/outcomes/OutcomeCards";
import { outcomeViewState, OUTCOME_INSUFFICIENT_TEXT, OUTCOME_PENDING_TEXT } from "@/lib/outcomes/outcomeView";
import { formatLastCheck, lineupWaitingText, LINEUP_TEXT_NOT_FOUND } from "@/lib/lineup/lineupStatus";
import { eventLabel, UNKNOWN_EVENT_LABEL } from "@/lib/matches/eventLabels";
import { FinishedMatchSummary } from "@/components/match-center/views/FinishedMatchSummary";
import { LineupPanel, hasPitchPositions } from "@/components/match-center/lineup/LineupPanel";
import { notificationHref, notificationTypeLabel } from "@/lib/api/notifications";
import {
  AnalysisSections,
  ScenarioReasonLines,
  ANALYSIS_PREPARING_TEXT,
} from "@/components/match-center/analysis/AnalysisSections";
import { seasonRecordText } from "@/components/match-center/views/FormStatusView";
import { TeamCrest, crestInitials } from "@/components/ui/TeamCrest";
import { groupByDateAndLeague } from "@/components/maclar/SearchResults";
import type { MatchResultItemDto } from "@/lib/api/matchResults";

// ── Yardımcı fixture'lar — gerçek /detail yanıtının biçimi ──────────────────

function player(n: number, name: string, grid: string | null = null): LineupPlayerDto {
  return { shirtNumber: n, playerName: name, position: "M", grid } as LineupPlayerDto;
}

function finishedMatch(p: Partial<MatchDetailDto> = {}): MatchDetailDto {
  return {
    matchId: 97933,
    status: "Finished",
    matchDate: "2026-09-06T13:30:00.000Z",
    league: "Eredivisie",
    homeTeam: { name: "Telstar", logoUrl: null },
    awayTeam: { name: "Cambuur", logoUrl: null },
    scoreBreakdown: { fullTime: { home: 2, away: 2 }, halfTime: { home: 0, away: 0 }, secondHalf: { home: 2, away: 2 } },
    events: [],
    statistics: null,
    lineup: { lineupsAnnounced: false, homeStartingXI: [], homeBench: [], awayStartingXI: [], awayBench: [] },
    ...p,
  } as unknown as MatchDetailDto;
}

// ── 3. KADRO SON KONTROL ZAMANI ────────────────────────────────────────────

describe("kadro paneli", () => {
  it("son kontrol zamanı SS:DD olarak gösterilir", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={finishedMatch({
          lineup: {
            lineupsAnnounced: false,
            pollingWindowOpen: true,
            kickoffPassed: false,
            lastCheckedUtc: "2026-09-07T17:46:05Z",
            homeStartingXI: [],
            homeBench: [],
            awayStartingXI: [],
            awayBench: [],
          },
        })}
      />
    );
    expect(html).toMatch(/Son kontrol: \d{2}:\d{2}/);
    expect(formatLastCheck("2026-09-07T17:46:05Z", "Europe/Istanbul")).toBe("Son kontrol: 20:46");
    expect(formatLastCheck(null)).toBeNull();
  });

  it("'1 saat önce açıklanacak' metni hiçbir durumda üretilmez", () => {
    for (const l of [{}, { pollingWindowOpen: true }, { kickoffPassed: true }]) {
      expect(lineupWaitingText(l)).not.toMatch(/1 saat/);
    }
    expect(lineupWaitingText({ kickoffPassed: true })).toBe(LINEUP_TEXT_NOT_FOUND);
  });

  // ── 4. DOĞRULANMIŞ LINEUP İLK 11 VE YEDEKLER ─────────────────────────────
  it("doğrulanmış kadroda ilk 11, yedekler ve formasyon gösterilir", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={finishedMatch({
          lineup: {
            lineupsAnnounced: true,
            homeFormation: "4-3-3",
            awayFormation: "4-2-3-1",
            lastCheckedUtc: "2026-09-07T17:16:04Z",
            homeStartingXI: [player(1, "Maduka Okoye", "1:1"), player(10, "Florian Thauvin", "4:2")],
            awayStartingXI: [player(1, "Ivan Provedel", "1:1"), player(9, "Taty Castellanos", "5:1")],
            homeBench: [player(23, "Edoardo Piana")],
            awayBench: [player(94, "Christos Mandas")],
          },
        })}
      />
    );
    expect(html).toContain('data-lineup-state="verified"');
    expect(html).toContain("4-3-3");
    expect(html).toContain("4-2-3-1");
    expect(html).toContain("F. Thauvin"); // sahada kısa ad
    expect(html).toContain("T. Castellanos");
    expect(html).toContain("Yedekler");
    expect(html).toContain("Edoardo Piana");
    expect(html).toContain("Christos Mandas");
    expect(html).toMatch(/Son kontrol: \d{2}:\d{2}/);
  });
});

// ── 7. TAKIM LOGOSU YÜKLENMEZSE GERÇEK FALLBACK ────────────────────────────

describe("takım arması", () => {
  it("logo yoksa monogram görünür (boş beyaz daire değil)", () => {
    const html = renderToStaticMarkup(<TeamCrest name="Fenerbahçe" logoUrl={null} size={44} />);
    expect(html).toContain('data-crest="broken"');
    expect(html).toContain(crestInitials("Fenerbahçe"));
    expect(html).not.toContain("bg-white/95");
  });

  it("logo yüklenirken monogram altta görünür, görsel görünmez", () => {
    const html = renderToStaticMarkup(<TeamCrest name="Lyon" logoUrl="https://media.api-sports.io/football/teams/80.png" size={44} />);
    expect(html).toContain('data-crest="loading"');
    expect(html).toContain("LYO");
    expect(html).toContain("opacity:0");
    expect(html).not.toContain("bg-white/95");
  });
});

// ── 8. BACKEND SIRASI FRONTEND TARAFINDAN BOZULMAZ ──────────────────────────

describe("backend sırası", () => {
  it("arama sonuçlarının sırası korunur (en yeni maç en üstte kalır)", () => {
    const item = (id: number, date: string, leagueId = 203): MatchResultItemDto =>
      ({ matchId: id, leagueId, leagueName: "Süper Lig", matchDateUtc: date } as MatchResultItemDto);
    const groups = groupByDateAndLeague([
      item(3, "2026-09-06T17:00:00Z"),
      item(2, "2026-08-30T17:00:00Z"),
      item(1, "2026-07-20T17:00:00Z"),
    ]);
    expect(groups.map((g) => g.date)).toEqual(["2026-09-06", "2026-08-30", "2026-07-20"]);
  });

});

// ── 9. OLAY TERİMLERİ ──────────────────────────────────────────────────────

describe("olay etiketi", () => {
  it("backend etiketi gösterilir; yoksa ham sağlayıcı kodu ASLA gösterilmez", () => {
    expect(eventLabel({ label: "Oyuncu Değişikliği", eventType: "subst" })).toBe("Oyuncu Değişikliği");
    expect(eventLabel({ eventType: "subst" })).toBe("Oyuncu Değişikliği");
    expect(eventLabel({ eventType: "WeirdProviderCode" })).toBe(UNKNOWN_EVENT_LABEL);
  });

  it("bitmiş maç ekranında 'Substitution 1' ya da 'Normal Goal' görünmez", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          events: [
            { minute: 42, eventType: "subst", detail: "Substitution 1", label: "Oyuncu Değişikliği", kind: "Substitution", playerIn: "S. Bouhoudane", playerOut: "I. Baouf", team: "Cambuur" },
            { minute: 53, eventType: "Goal", detail: "Normal Goal", label: "Gol", kind: "Goal", player: "R. El Arguioui", team: "Cambuur" },
          ],
        })}
      />
    );
    expect(html).not.toContain("Substitution 1");
    expect(html).not.toContain("Normal Goal");
    expect(html).toContain("Oyuncu Değişikliği");
    expect(html).toContain("Giren: S. Bouhoudane");
    expect(html).toContain("Çıkan: I. Baouf");
    expect(html).not.toContain("Asist: S. Bouhoudane");
  });
});

// ── RESMÎ KADRO: KONUMSUZ KAYNAK + BİLDİRİM ROTASI ──────────────────────────

describe("resmî kadro görünümü", () => {
  const eleven = (prefix: string, grid: boolean) =>
    Array.from({ length: 11 }, (_, i) => player(i + 1, `${prefix} ${i + 1}`, grid ? `${i === 0 ? 1 : 2}:${i + 1}` : null));

  it("kaynak saha konumu vermediyse saha çizilmez, ilk 11 liste olarak ve kaynak etiketiyle gösterilir", () => {
    const html = renderToStaticMarkup(
      <LineupPanel
        match={finishedMatch({
          homeTeam: { name: "Beşiktaş" } as never,
          awayTeam: { name: "Erzurumspor FK" } as never,
          lineup: {
            lineupsAnnounced: true,
            homeStartingXI: eleven("BJK", false),
            awayStartingXI: eleven("ERZ", false),
            homeBench: [],
            awayBench: [],
            source: "Türkiye Futbol Federasyonu",
            homeCoach: "VINCENZO ITALIANO",
            awayCoach: "SERKAN ÖZBALTA",
          } as never,
        })}
      />
    );
    expect(html).toContain('data-lineup-view="list"');
    expect(html).not.toContain("Saha konumu bildirilmeyen");
    expect(html).toContain("BJK 11");
    expect(html).toContain("Resmî kaynak: Türkiye Futbol Federasyonu");
    expect(html).toContain("VINCENZO ITALIANO");
  });

  it("saha yalnız bütün ilk 11 oyuncularında kaynak konumu varsa çizilir", () => {
    expect(hasPitchPositions(eleven("A", true), eleven("B", true))).toBe(true);
    const mixed = [...eleven("A", true)];
    mixed[3] = player(4, "A 4", null);
    expect(hasPitchPositions(mixed, eleven("B", true))).toBe(false);
    expect(hasPitchPositions([], [])).toBe(false);
  });

  it("bildirim doğru maç detayına gider; dış/protokol-göreli rota kabul edilmez", () => {
    expect(notificationHref({ route: "/match/15383", matchId: 15383 })).toBe("/match/15383");
    expect(notificationHref({ route: null, matchId: 99 })).toBe("/match/99");
    expect(notificationHref({ route: "https://evil.example", matchId: 7 })).toBe("/match/7");
    expect(notificationHref({ route: "//evil.example", matchId: 7 })).toBe("/match/7");
    expect(notificationTypeLabel("MATCH_LINEUP_AVAILABLE")).toBe("Kadro");
    expect(notificationTypeLabel("MATCH_CRITICAL_UPDATE")).toBe("Kritik gelişme");
    expect(notificationTypeLabel(null)).toBeNull();
  });
});

// ── KANITA DAYALI AI ANALİZİ ─────────────────────────────────────────────────

describe("AI maç analizi", () => {
  const ready = {
    status: "Ready",
    whyWatch: ["Barcelona bu sezon oynadığı 4 lig maçının hiçbirini kaybetmedi: 4 galibiyet."],
    keyBattle: ["Barcelona iç sahada oynadığı 2 lig maçında 7 gol attı; Racing Santander deplasmanda oynadığı 2 lig maçında 4 gol yedi."],
    lineupImpact: [],
    uncertainty: "Örneklem sınırlı: Barcelona için 4, Racing Santander için 5 tamamlanmış lig maçı var.",
    scenarios: [{ market: "Ev Sahibi Kazanır", support: "Barcelona iç sahada oynadığı 2 lig maçında 2 galibiyet aldı.", risk: null }],
  };

  it("hazır analiz bölümleri gösterilir; boş bölüm (Kadro Etkisi) hiç render edilmez", () => {
    const html = renderToStaticMarkup(<AnalysisSections analysis={ready} />);
    expect(html).toContain("Bu Maçı Neden İzlemeli?");
    expect(html).toContain("Maçın Kilidi");
    expect(html).toContain("Belirsizlik");
    expect(html).not.toContain("Kadro Etkisi");
    expect(html).toContain("Racing Santander deplasmanda");
  });

  it("analiz hazır değilse uydurma metin yerine 'Analiz hazırlanıyor' yazılır", () => {
    expect(renderToStaticMarkup(<AnalysisSections analysis={null} />)).toContain(ANALYSIS_PREPARING_TEXT);
    expect(renderToStaticMarkup(<AnalysisSections analysis={{ ...ready, status: "Preparing" }} />)).toContain(ANALYSIS_PREPARING_TEXT);
  });

  it("senaryo gerekçesi yalnız aynı market için ve etiketli gösterilir", () => {
    const html = renderToStaticMarkup(<ScenarioReasonLines analysis={ready} market="Ev Sahibi Kazanır" />);
    expect(html).toContain("Destekleyen veri");
    expect(html).not.toContain("Zayıflatan risk");
    expect(renderToStaticMarkup(<ScenarioReasonLines analysis={ready} market="2.5 Üst" />)).toBe("");
  });

  it("ham teknik form satırı (O/G/B/M/AG/YG/AV) üretilmez", () => {
    const text = seasonRecordText({ played: 4, won: 2, drawn: 1, lost: 1, goalsFor: 6, goalsAgainst: 3 });
    expect(text).toBe("4 maçta 2 galibiyet, 1 beraberlik, 1 mağlubiyet · 6 gol attı, 3 gol yedi");
    expect(text).not.toMatch(/\b(O|G|B|M) \d|\bAG\b|\bYG\b|\bAV\b/);
    const html = renderToStaticMarkup(<AnalysisSections analysis={ready} />);
    expect(html).not.toMatch(/\bAG\b|\bYG\b|\bAV\b|O \d · G \d/);
  });
});

// ── "ÖNEMLİ ANLARI İZLE" AKSİYONU KALDIRILDI ─────────────────────────────────

describe("önemli anlar aksiyonu", () => {
  const forbidden = /önemli anları izle/i;

  it("maç ekranı aksiyon menüsünde buton yok", () => {
    const html = renderToStaticMarkup(<ActionGrid onSelect={() => {}} />);
    expect(html).not.toMatch(forbidden);
    expect(MATCH_ACTIONS.map((a) => a.label).join(" ")).not.toMatch(forbidden);
    expect(html).toContain("AI Maç Analizi");
  });

  it("hiçbir ekran kaynağında buton metni geçmez (app + components)", () => {
    const hits: string[] = [];
    const walk = (dir: string) => {
      for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, entry.name);
        if (entry.isDirectory()) walk(p);
        else if (/\.(tsx?|jsx?)$/.test(entry.name) && forbidden.test(fs.readFileSync(p, "utf8"))) hits.push(p);
      }
    };
    walk(path.resolve(__dirname, "../app"));
    walk(path.resolve(__dirname, "../components"));
    expect(hits).toEqual([]);
  });

});

// ── BİTMİŞ MAÇ: GOLLER, VİDEO DURUMU VE MAÇ SONRASI ANALİZ (13.09.2026) ─────

describe("bitmiş maç: maç sonrası analiz", () => {
  it("maç sonrası analiz yalnız backend'in DB metnini gösterir; yoksa bölüm hiç çizilmez", () => {
    const sentences = [
      "Nottingham Forest, Aston Villa deplasmanında 1-2 kazandı; ilk yarı 0-0 tamamlanmıştı.",
      "Goller: 90+4' Igor Jesus (Nottingham Forest).",
    ];
    const withText = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({ postMatchSummary: { sentences, generator: "Deterministic", generatedAtUtc: "2026-09-13T22:00:00Z" } })}
      />
    );
    expect(withText).toContain("Maç Sonrası Analiz");
    expect(withText).toContain("Aston Villa deplasmanında 1-2 kazandı");

    const without = renderToStaticMarkup(<FinishedMatchSummary match={finishedMatch({ postMatchSummary: null })} />);
    expect(without).not.toContain("Maç Sonrası Analiz");
  });

  it("oyuncu değişikliğinde giren/çıkan backend alanlarından ters çevrilmeden basılır", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          events: [
            { minute: 44, eventType: "subst", detail: "Substitution", label: "Oyuncu Değişikliği", kind: "Substitution", playerIn: "Þórir Helgason", playerOut: "Gianluca Busio", team: "Venezia" },
          ],
        })}
      />
    );
    expect(html).toContain("Giren: Þórir Helgason");
    expect(html).toContain("Çıkan: Gianluca Busio");
    expect(html).not.toContain("Giren: Gianluca Busio");
  });
});

// ── 15.09.2026: ANALİZ DURUMU, OYNATICI HATASI BİLDİRİMİ, SONUÇ AKIŞI ──────────────────────────

describe("maç sonu analizi: backend durumu", () => {
  it("yetersiz veri → dürüst cümle; analiz uydurulmaz", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary match={finishedMatch({ postMatchSummary: { sentences: [], generator: "InsufficientData", generatedAtUtc: "2026-09-15T00:00:00Z", status: "InsufficientData" } })} />
    );
    expect(html).toContain("Bu maç için ayrıntılı analiz oluşturacak yeterli doğrulanmış veri bulunamadı.");
    expect(ANALYSIS_INSUFFICIENT_TEXT).toBe("Bu maç için ayrıntılı analiz oluşturacak yeterli doğrulanmış veri bulunamadı.");
  });

  it("hazırlanıyor ve mevcut analiz ayrı gösterilir", () => {
    const pending = renderToStaticMarkup(
      <FinishedMatchSummary match={finishedMatch({ postMatchSummary: { sentences: [], generator: "None", generatedAtUtc: "2026-09-15T00:00:00Z", status: "Pending" } })} />
    );
    expect(pending).toContain(ANALYSIS_PENDING_TEXT);
    const ok = renderToStaticMarkup(
      <FinishedMatchSummary match={finishedMatch({ postMatchSummary: { sentences: ["Telstar ile Cambuur 2-2 berabere kaldı.", "Goller: …"], generator: "Deterministic", generatedAtUtc: "2026-09-15T00:00:00Z", status: "Available" } })} />
    );
    expect(ok).toContain('data-testid="post-match-analysis"');
    expect(ok).not.toContain(ANALYSIS_INSUFFICIENT_TEXT);
  });
});

describe("sonuç akışı: manuel yenilemesiz", () => {
  it("Maçlar listesi arka planda değilken periyodik tazelenir (biten maç YAKLAŞAN'dan düşer)", () => {
    const root = path.resolve(__dirname, "..");
    const hook = fs.readFileSync(path.join(root, "hooks/useMatchList.ts"), "utf8");
    expect(hook).toContain("refetchInterval: 5 * 60_000");
    expect(hook).toContain("refetchIntervalInBackground: false");
  });
});

// ── 15.09.2026 (2): VİDEO TAMAMEN KALDIRILDI ────────────────────────────────────

describe("video özelliği kapalı", () => {
  const root = path.resolve(__dirname, "..");
  const walk = (dir: string, out: string[] = []) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(p, out);
      else if (/\.(tsx?|jsx?)$/.test(entry.name)) out.push(p);
    }
    return out;
  };

  it("bitmiş maç ekranında MAÇ ÖZETİ / GOLLER video paneli, oynatıcı ve arama cümlesi yok", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary match={finishedMatch({ events: [{ minute: 12, eventType: "Goal", label: "Gol", kind: "Goal", player: "X", team: "Telstar" }] } as never)} />
    );
    expect(html).not.toContain(">Maç Özeti</h3>");
    expect(html).not.toContain(">Goller</h3>");
    expect(html).not.toContain("<iframe");
    expect(html).not.toMatch(/Resmî maç özeti kontrol ediliyor|Maç özetini izle|Video var|Önemli Anları İzle/i);
  });

  it("uygulama kaynaklarında video oynatıcı, YouTube adresi ve video durum metinleri yok", () => {
    const files = [...walk(path.join(root, "app")), ...walk(path.join(root, "components")), ...walk(path.join(root, "lib")), ...walk(path.join(root, "hooks"))];
    const hits = files.filter((f) =>
      /youtube-nocookie|VideoPlayerCard|videoSearch|hasPlayableOfficialVideo|Video var|Maç özetini izle|NotAvailableYet|SourceBlocked|FullHighlightsAvailable|GoalClipsAvailable/.test(fs.readFileSync(f, "utf8"))
    );
    expect(hits.map((h) => path.relative(root, h))).toEqual([]);
    expect(fs.existsSync(path.join(root, "lib/video"))).toBe(false);
  });

  it("istatistikte kaynakta yayımlanmayan alan '0' diye basılmaz, adıyla söylenir; gerçek 0 satırda kalır", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          statistics: {
            rows: [{ key: "yellow", label: "Sarı kart", home: 0, away: 2, isPercentage: false }],
            notPublished: ["Kırmızı kart", "Başarılı pas %"],
            sourceName: "LALIGA",
          },
        })}
      />
    );
    expect(html).toContain("Kaynakta yayımlanmadı: Kırmızı kart, Başarılı pas %");
    expect(html).toContain("Resmî kaynak: LALIGA");
    expect(html).toMatch(/>0<\/span>/);
  });
});

// ── AI OLASI SONUÇLAR: SNAPSHOT, ÜÇ AİLE, ÇİFTE ŞANS YOK ─────────────────────────

const snapshot = (p: Partial<OutcomeSnapshotDto> = {}): OutcomeSnapshotDto => {
  const card = (family: string, familyTitle: string, market: string, marketKey: string | null, probability: number) => ({
    family, familyTitle, market, marketKey, probability, rawProbability: probability / 100, calibratedProbability: probability / 100,
    baselineProbability: 0.4, informationLift: 0.05, evidenceCoverage: 1, sampleQuality: "Rich", uncertainty: 0, selectionScore: 0.1,
    reasonCodes: ["RESULT_HOME_STRONGER"], reason: market + " gerekçesi", limitation: null,
  });
  return {
    snapshotId: "snp-abc", matchId: 1, modelVersion: "formax-outcome-2.0", status: "Available", evidenceCoverage: 1, sampleQuality: "Rich",
    homeSampleSize: 20, awaySampleSize: 20, reasonCodes: [], topScores: [{ home: 1, away: 0, probability: 12 }],
    mainCards: [
      card("MatchResult", "Maç Sonucu", "Ev Sahibi Kazanır", "MS1", 52),
      card("TotalGoals", "Gol Beklentisi", "2.5 Alt", "ALT_2_5", 58),
      card("BothTeamsScore", "İki Takımın Gol Durumu", "Karşılıklı Gol Yok", "KG_YOK", 55),
    ],
    families: [
      { family: "MatchResult", title: "Maç Sonucu", items: [card("MatchResult", "Maç Sonucu", "Ev Sahibi Kazanır", "MS1", 52), card("MatchResult", "Maç Sonucu", "Beraberlik", "MSX", 27), card("MatchResult", "Maç Sonucu", "Deplasman Kazanır", "MS2", 21)] },
      { family: "Other", title: "Diğer", items: [card("Other", "Diğer", "Çifte Şans (1X)", "CS_1X", 79)] },
    ],
    ...p,
  } as OutcomeSnapshotDto;
};

describe("AI olası sonuçlar", () => {
  it("üç ana kart backend sırasıyla ve 'Beklenti' etiketiyle çizilir; çifte şans ana kartta yok, oran yok", () => {
    const html = renderToStaticMarkup(<OutcomeCards snapshot={snapshot()} />);
    const order = ["Maç Sonucu", "Gol Beklentisi", "İki Takımın Gol Durumu"].map((t) => html.indexOf(t));
    expect(order.every((i, k) => i >= 0 && (k === 0 || i > order[k - 1]))).toBe(true);
    expect(html).toContain("Beklenti");
    expect(html).not.toContain("Güven");
    expect(html).not.toContain("Çifte Şans");
    expect(html).toContain('data-snapshot-id="snp-abc"');
    expect(html).toContain("Tüm Olasılıkları Gör");
    expect(html).toContain("2.5 Alt gerekçesi");
  });

  it("yetersiz veri ve bekleyen snapshot dürüst metin gösterir, sahte kart yok", () => {
    expect(outcomeViewState(snapshot({ status: "InsufficientData", mainCards: [], notice: null }))).toEqual({ kind: "insufficient", text: OUTCOME_INSUFFICIENT_TEXT });
    expect(outcomeViewState(undefined)).toEqual({ kind: "pending", text: OUTCOME_PENDING_TEXT });
    expect(outcomeViewState(snapshot()).kind).toBe("ready");
  });

  it("Keşfet ve Detay aynı uç ve aynı cache anahtarını kullanır; frontend sıralama/hesap yapmaz", () => {
    const root = path.resolve(__dirname, "..");
    const discover = fs.readFileSync(path.join(root, "components/discover/AIPredictionsSection.tsx"), "utf8");
    const detail = fs.readFileSync(path.join(root, "components/match-center/views/AIAnalysisView.tsx"), "utf8");
    const cards = fs.readFileSync(path.join(root, "components/outcomes/OutcomeCards.tsx"), "utf8");
    for (const src of [discover, detail]) {
      expect(src).toContain("useMatchOutcomes(");
      expect(src).not.toContain("useMatchDecision(");
      expect(src).not.toMatch(/\.sort\(|probability\s*>=\s*30|currentOdd|toFixed\(2\)/);
    }
    expect(cards).not.toMatch(/\.sort\(|\.filter\(|Math\./);
    expect(fs.readFileSync(path.join(root, "lib/api/outcomes.ts"), "utf8")).toContain("/outcomes");
  });
});

import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import fs from "node:fs";
import path from "node:path";
import { ActionGrid } from "@/components/match-center/dashboard/ActionGrid";
import { MATCH_ACTIONS } from "@/components/match-center/aiContext";
import type { LineupPlayerDto, MatchDetailDto, MatchVideoDto } from "@/types/api";
import {
  VIDEO_CHECKING_TEXT,
  VIDEO_NOT_FOUND_TEXT,
  arrangeVideos,
  videoEmptyStateText,
} from "@/lib/video/videoSearch";
import { nextPlaybackState, parsePlayerMessage, playbackErrorText } from "@/lib/video/youtubePlayback";
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
import { VideoPlayerCard } from "@/components/match-center/views/VideoPlayerCard";
import { TeamCrest, crestInitials } from "@/components/ui/TeamCrest";
import { groupByDateAndLeague } from "@/components/maclar/SearchResults";
import type { MatchResultItemDto } from "@/lib/api/matchResults";

// ── Yardımcı fixture'lar — gerçek /detail yanıtının biçimi ──────────────────

function video(p: Partial<MatchVideoDto>): MatchVideoDto {
  return {
    title: "ÖZET",
    publisher: "TRT SPOR",
    sourcePageUrl: "https://www.youtube.com/watch?v=x",
    embedUrl: "https://www.youtube-nocookie.com/embed/x",
    thumbnailUrl: null,
    videoType: "MatchHighlights",
    publishedAtUtc: null,
    durationSeconds: 494,
    canPlayInApp: true,
    availableCountries: [],
    isRegionRestricted: false,
    ...p,
  } as MatchVideoDto;
}

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
    videos: [],
    statistics: null,
    lineup: { lineupsAnnounced: false, homeStartingXI: [], homeBench: [], awayStartingXI: [], awayBench: [] },
    ...p,
  } as unknown as MatchDetailDto;
}

// ── 1. VİDEO DURUMU LEDGER SONUCUNA GÖRE ─────────────────────────────────────

describe("video arama durumu", () => {
  it("yalnız defter NotFound derse 'bulunamadı' der", () => {
    expect(videoEmptyStateText({ status: "NotFound", attemptsMade: 4, maxAttempts: 4 })).toBe(VIDEO_NOT_FOUND_TEXT);
    expect(videoEmptyStateText({ status: "Checking", attemptsMade: 2, maxAttempts: 4 })).toBe(VIDEO_CHECKING_TEXT);
    // Defter bilgisi hiç gelmediyse de "bulunamadı" DENMEZ.
    expect(videoEmptyStateText(undefined)).toBe(VIDEO_CHECKING_TEXT);
  });

  // ── 2. TAKVİM GEÇMİŞ AMA DENEME YOK → "bulunamadı" GÖSTERİLMEZ ──────────────
  it("maç günler önce bitmiş ama defterde deneme yoksa ekranda 'bulunamadı' yok", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          matchDate: "2026-08-01T18:00:00.000Z", // saat "çoktan bitti" der
          videoSearch: { status: "Checking", attemptsMade: 0, maxAttempts: 4, lastAttemptUtc: null },
        })}
      />
    );
    expect(html).toContain(VIDEO_CHECKING_TEXT);
    expect(html).not.toContain(VIDEO_NOT_FOUND_TEXT);
  });

  it("dört deneme tamamlanıp defter NotFound derse ekranda 'bulunamadı'", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({ videoSearch: { status: "NotFound", attemptsMade: 4, maxAttempts: 4 } })}
      />
    );
    expect(html).toContain(VIDEO_NOT_FOUND_TEXT);
    expect(html).not.toContain(VIDEO_CHECKING_TEXT);
  });

  it("oynatılabilir video varsa player çizilir, boş durum metni yok", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          videos: [video({ title: "Fenerbahçe - Lyon (Özet)" })],
          videoSearch: { status: "Found", attemptsMade: 1, maxAttempts: 4 },
        })}
      />
    );
    expect(html).toContain("Fenerbahçe - Lyon (Özet) — oynat");
    expect(html).not.toContain(VIDEO_CHECKING_TEXT);
    expect(html).not.toContain(VIDEO_NOT_FOUND_TEXT);
  });
});

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

// ── 6. BÖLGE NEDENİYLE OYNATILAMAYAN VİDEO AKTİF PLAYER DEĞİL ───────────────

describe("gerçek oynatılabilirlik", () => {
  it("oynatıcı hata bildirirse durum 'error' olur ve bir daha 'playing'e dönmez", () => {
    const [err] = parsePlayerMessage(JSON.stringify({ event: "onError", info: 150 }));
    expect(err).toEqual({ kind: "error", code: 150 });
    const s = nextPlaybackState("loading", err);
    expect(s).toBe("error");
    expect(nextPlaybackState(s, { kind: "state", state: 1 })).toBe("error");
  });

  it("iframe yüklenmesi 'oynuyor' sayılmaz; yalnız playerState=1 ya da ilerleyen zaman", () => {
    expect(nextPlaybackState("loading", { kind: "ready" })).toBe("loading");
    expect(nextPlaybackState("loading", { kind: "state", state: -1 })).toBe("loading");
    const [info] = parsePlayerMessage({ event: "infoDelivery", info: { playerState: 1 } });
    expect(nextPlaybackState("loading", info)).toBe("playing");
  });

  it("bölge kısıtlı videonun hatası 'bulunduğunuz bölgede oynatılamıyor' olarak söylenir", () => {
    expect(playbackErrorText(150, { isRegionRestricted: true, availableCountries: ["TR"] })).toBe(
      "Bu resmî video bulunduğunuz bölgede oynatılamıyor (yalnız TR)."
    );
  });

  it("uygulama içinde oynatılamayan video için oynatıcı hiç kurulmaz", () => {
    const html = renderToStaticMarkup(<VideoPlayerCard video={video({ canPlayInApp: false, embedUrl: null })} />);
    expect(html).not.toContain("<iframe");
    expect(html).toContain("Uygulama içinde oynatılamıyor.");
    expect(html).toContain("Resmî kaynakta izle");
  });

  it("aynı video maç özeti ve önemli anlarda iki kez gösterilmez", () => {
    const main = video({ title: "Özet" });
    const goal = video({ title: "Gol", videoType: "Goal" });
    const { main: m, moments } = arrangeVideos([main, goal]);
    expect(m).toBe(main);
    expect(moments).toEqual([goal]);
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

  it("video sırası backend'in verdiği gibi kalır", () => {
    const g2 = video({ title: "Gol 2", videoType: "Goal" });
    const g1 = video({ title: "Gol 1", videoType: "Goal" });
    expect(arrangeVideos([g2, g1]).moments.map((v) => v.title)).toEqual(["Gol 2", "Gol 1"]);
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

  it("bitmiş maçta doğrulanmış resmî MAÇ ÖZETİ oynatıcısı korunur", () => {
    const html = renderToStaticMarkup(
      <FinishedMatchSummary
        match={finishedMatch({
          videos: [video({ title: "Telstar - Cambuur (Özet)", videoType: "MatchHighlights" })],
        })}
      />
    );
    expect(html).toMatch(/Maç Özeti/i);
    expect(html).toContain("Telstar - Cambuur (Özet)");
    expect(html).not.toMatch(forbidden);
  });
});

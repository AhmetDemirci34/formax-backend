import { describe, expect, it, vi } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import fs from "node:fs";
import path from "node:path";
import { QueryClient, QueryClientProvider, QueryObserver } from "@tanstack/react-query";
import type { MatchDetailDto } from "@/types/api";

// next/font derleme zamanı dönüşümüdür; testte sınıf adı yeterli.
vi.mock("@/components/match-center/fonts", () => ({
  archivoNarrow: { className: "font-goalai", variable: "--font-goalai" },
}));

import { MatchCenterScreen, MATCH_DETAIL_ERROR_TEXT, type MatchCenterState } from "@/components/match-center/MatchCenterScreen";
import {
  awaitingResultRefreshInterval,
  AWAITING_RESULT_REFRESH_MS,
  matchDetailKey,
  matchDetailQueryOptions,
  matchDetailRetry,
} from "@/hooks/useMatchDetail";
import { matchOutcomesKey } from "@/hooks/useMatchOutcomes";

const noop = () => {};

function upcoming(p: Partial<MatchDetailDto> = {}): MatchDetailDto {
  return {
    matchId: 107680,
    status: "NotStarted",
    matchDate: new Date(Date.now() + 3 * 60 * 60_000).toISOString(),
    league: "UEFA Europa League",
    matchTypeLabel: "Lig Aşaması",
    homeTeam: { name: "Celtic", logoUrl: null },
    awayTeam: { name: "Ferencvarosi TC", logoUrl: null },
    lineup: { lineupsAnnounced: false, homeStartingXI: [], homeBench: [], awayStartingXI: [], awayBench: [] },
    ...p,
  } as unknown as MatchDetailDto;
}

/** Sorgular hiç çözülmez: sunucu çiziminde istek atılmaz, "geç veri" durumunun kendisidir. */
function render(state: MatchCenterState, client = new QueryClient()) {
  return renderToStaticMarkup(
    <QueryClientProvider client={client}>
      <MatchCenterScreen
        state={state}
        activeView="dashboard"
        onSelect={noop}
        onCloseView={noop}
        onBack={noop}
        onRouteBack={noop}
        onRetry={noop}
        onGoMatches={noop}
      />
    </QueryClientProvider>
  );
}

describe("maç detayı · ilk açılış", () => {
  it("1 · yüklenirken boş siyah alan yerine Hero + aksiyon ölçülerinde iskelet görünür", () => {
    const html = render({ kind: "loading" });
    expect(html).toContain('data-testid="match-center-skeleton"');
    expect(html).toContain('aria-busy="true"');
    // Header ve alt menü iskelette de yerinde: ekran çerçevesi hemen kurulur.
    expect(html).toContain("Maç Detayı");
    expect(html).toContain("Keşfet");
    // Hero kartı + asistan kartı + 1 ana + 3 ızgara aksiyonu yer tutucusu.
    expect(html).toContain("rounded-[26px]");
    expect((html.match(/h-24 animate-pulse/g) ?? []).length).toBe(3);
    expect(html).toContain("h-14 w-full animate-pulse");
    // Tek başına dönen simge (eski LoadingState) değil.
    expect(html).not.toContain("animate-spin");
  });

  it("2 · detail cevabı gelince Header, Hero ve ana aksiyonlar aynı çizimde görünür (opacity:0 yok)", () => {
    const html = render({ kind: "ready", match: upcoming() });
    expect(html).toContain("Maç Detayı");
    expect(html).toContain("Celtic");
    expect(html).toContain("Ferencvarosi TC");
    expect(html).toContain("GOALAI Asistan");
    for (const label of ["AI Maç Analizi", "Form Durumları", "Kadro Bilgisi", "Son Dakika"]) {
      expect(html).toContain(label);
    }
    expect(html).not.toContain('data-testid="match-center-skeleton"');
    // KÖK NEDEN: içerik JS animasyon karesine bağlı gizlenmez.
    expect(html).not.toMatch(/opacity:\s*0[;"]/);
  });

  it("2b · kaynak: dashboard ilk çizimde giriş animasyonu beklemez", () => {
    const src = fs.readFileSync(path.join(__dirname, "..", "components", "match-center", "MatchCenterScreen.tsx"), "utf8");
    expect(src).toMatch(/<AnimatePresence mode="wait" initial=\{false\}>/);
  });

  it("3 · hata durumu sonsuz yükleme değildir: anlaşılır mesaj + Tekrar Dene", () => {
    const html = render({ kind: "error" });
    expect(html).toContain(MATCH_DETAIL_ERROR_TEXT);
    expect(html).toContain("Tekrar Dene");
    expect(html).not.toContain('data-testid="match-center-skeleton"');
    expect(html).not.toContain("hazırlanıyor");

    // Yeniden deneme sınırlı: ağ/5xx en fazla 1 kez, 4xx hiç.
    expect(matchDetailRetry(0, { response: { status: 500 } })).toBe(true);
    expect(matchDetailRetry(0, new Error("Network Error"))).toBe(true);
    expect(matchDetailRetry(1, new Error("timeout"))).toBe(false);
    expect(matchDetailRetry(0, { response: { status: 404 } })).toBe(false);
  });

  it("3b · hata sonrası sorgu 'error' durumuna geçer, 'pending'de kalmaz", async () => {
    const client = new QueryClient();
    const fetcher = vi.fn(async () => {
      throw Object.assign(new Error("404"), { response: { status: 404 } });
    });
    await expect(client.fetchQuery(matchDetailQueryOptions(5, true, fetcher))).rejects.toThrow();
    expect(client.getQueryState(matchDetailKey(5))?.status).toBe("error");
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it("4 · aynı mount'ta (StrictMode çift mount + Hero) tek detail isteği gider", async () => {
    const client = new QueryClient();
    let calls = 0;
    const fetcher = async (id: number) => {
      calls++;
      await new Promise((r) => setTimeout(r, 20));
      return upcoming({ matchId: id });
    };
    const observers = [0, 1, 2].map(() => new QueryObserver(client, matchDetailQueryOptions(107680, true, fetcher)));
    const unsubscribes = observers.map((o) => o.subscribe(noop));
    await vi.waitFor(() => expect(observers[0].getCurrentResult().status).toBe("success"));
    unsubscribes.forEach((u) => u());
    expect(calls).toBe(1);

    // Ekran ağacında detail'i yalnız sayfa çeker; görünüm bileşenleri ikinci kez istemez.
    const root = path.join(__dirname, "..", "components", "match-center");
    const offenders: string[] = [];
    const walk = (dir: string) => {
      for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, e.name);
        if (e.isDirectory()) walk(p);
        else if (/\.(tsx?|jsx?)$/.test(e.name) && /useMatchDetail\(|getMatchDetail\(/.test(fs.readFileSync(p, "utf8"))) offenders.push(p);
      }
    };
    walk(root);
    expect(offenders).toEqual([]);
  });

  it("5 · bağımsız geç veri (AI BEKLENTİSİ snapshot'ı, haber) ana ekranı bekletmez", () => {
    const client = new QueryClient();
    const html = render({ kind: "ready", match: upcoming() }, client);
    // Snapshot sorgusu henüz sonuçlanmadı…
    expect(client.getQueryState(matchOutcomesKey(107680))?.status ?? "pending").toBe("pending");
    // …yine de Hero ve aksiyonlar çizildi.
    expect(html).toContain("Celtic");
    expect(html).toContain("AI Maç Analizi");
    expect(html).not.toContain('data-testid="match-center-skeleton"');
  });
});

describe("maç detayı · sonucu beklenen açık ekran", () => {
  const now = Date.parse("2026-09-17T19:00:00Z");

  it("başlama saati geçmiş, bitmemiş maçta DB kontrollü tazelenir; bitince/ertelenince/pencere dışında durur", () => {
    const started = { status: "NotStarted", matchDate: "2026-09-17T16:45:00Z" };
    expect(awaitingResultRefreshInterval(started, now)).toBe(AWAITING_RESULT_REFRESH_MS);
    expect(AWAITING_RESULT_REFRESH_MS).toBeLessThanOrEqual(30_000);
    expect(awaitingResultRefreshInterval({ ...started, status: "Finished" }, now)).toBe(false);
    expect(awaitingResultRefreshInterval({ ...started, status: "Postponed" }, now)).toBe(false);
    expect(awaitingResultRefreshInterval({ status: "NotStarted", matchDate: "2026-09-17T21:00:00Z" }, now)).toBe(false);
    expect(awaitingResultRefreshInterval({ status: "NotStarted", matchDate: "2026-09-17T08:00:00Z" }, now)).toBe(false);
    expect(awaitingResultRefreshInterval(undefined, now)).toBe(false);
  });
});

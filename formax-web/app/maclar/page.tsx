"use client";

// FORMAX · Maçlar (Match List) — /maclar
//
// EKRANIN İKİ YOLU VAR:
//   YAKLAŞAN (varsayılan) — bugün ve sonraki dört Türkiye takvim günü, YALNIZ
//     başlamamış maçlar. Kilitli ürün kararı korunur: canlı maç, canlı dakika,
//     canlı skor ve durum filtresi YOKTUR; hiçbir sağlayıcıya canlı istek çıkmaz.
//   SONUÇLAR — kilitli 11 organizasyonun BİTMİŞ maçları, bugün + önceki 7 gün.
//     Bu sekme, bitmiş maç özetine normal UI akışından ulaşmanın yoludur; kullanıcı
//     artık /match/{id} adresini elle yazmak zorunda değildir.
//
// İki sekme de SALT DB okur — sekme değiştirmek api-football kotası harcamaz.
// Veri: gerçek backend. Frontend maç, skor veya AI verisi ÜRETMEZ.

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { DateNav } from "@/components/maclar/DateNav";
import { LeagueGroupPanel } from "@/components/maclar/LeagueGroup";
import { MatchesTabs, type MatchesTab } from "@/components/maclar/MatchesTabs";
import { ResultDateNav } from "@/components/maclar/ResultDateNav";
import { ResultsView } from "@/components/maclar/ResultsView";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { useMatchList } from "@/hooks/useMatchList";
import { useResultDaySelection } from "@/hooks/useResultDaySelection";
import { buildLeagueGroups } from "@/lib/matches/leagueGrouping";

export default function MaclarPage() {
  const router = useRouter();
  const [tab, setTab] = useState<MatchesTab>("upcoming");
  const [day, setDay] = useState<Date>(() => new Date());

  // YAKLAŞAN listesi sekme değişse de sorgulanmaya devam eder (önbellekte durur),
  // böylece geri dönüşte yeniden yüklenmez ve sayfa zıplamaz.
  const { data = [], isLoading, isError, refetch } = useMatchList();

  // SONUÇLAR gün seçimi — tarih seçici başlıkta, liste <main> içinde; ikisi de
  // aynı kaynaktan beslenir.
  const results = useResultDaySelection(tab === "results");

  const groups = useMemo(() => buildLeagueGroups(data, day), [data, day]);
  const totalMatches = groups.reduce((sum, g) => sum + g.matches.length, 0);

  const openMatch = (matchId: number) => router.push(`/match/${matchId}`);
  const upcoming = tab === "upcoming";

  return (
    <div className="flex min-h-[100dvh] flex-col overflow-x-hidden bg-bg-deep">
      <header className="sticky top-0 z-20 bg-bg-deep/95 pt-[var(--safe-top)] backdrop-blur-md">
        <div className="flex items-baseline justify-between gap-2 px-[18px] pb-3 pt-3">
          <h1 className="text-[22px] font-bold tracking-tight text-text-primary">Maçlar</h1>
          {upcoming && totalMatches > 0 ? (
            <span className="shrink-0 whitespace-nowrap text-[11.5px] tabular-nums text-text-muted">
              {groups.length} lig · {totalMatches} maç
            </span>
          ) : null}
        </div>

        <MatchesTabs value={tab} onChange={setTab} />

        {/* Her sekmenin tarih kuralı FARKLIDIR: YAKLAŞAN ileri gider, SONUÇLAR gitmez.
            İkisi de aynı yerde (sticky başlıkta) durur ki sekme değişince kontroller
            yer değiştirmesin ve liste zıplamasın. */}
        {upcoming ? (
          <DateNav day={day} onChange={setDay} />
        ) : results.day !== null ? (
          <ResultDateNav
            day={results.day}
            onChange={results.setDay}
            nearestResultDay={results.nearestResultDay}
          />
        ) : null}
      </header>

      {upcoming ? (
        <main className="flex flex-col gap-2.5 px-3.5 pb-[calc(var(--bottom-nav-height)+16px)] pt-2">
          {isLoading ? (
            <LoadingState label="Maçlar yükleniyor..." />
          ) : isError ? (
            <ErrorState
              message="Maçlar şu an yüklenemiyor. Lütfen tekrar dene."
              onRetry={() => refetch()}
            />
          ) : groups.length === 0 ? (
            <p className="pt-16 text-center text-[13px] text-text-muted">
              Bu tarihte başlamamış maç bulunamadı.
            </p>
          ) : (
            groups.map((g) => (
              <LeagueGroupPanel
                key={g.key}
                group={g}
                onOpen={(match) => openMatch(match.matchId)}
              />
            ))
          )}
        </main>
      ) : (
        <ResultsView
          day={results.day}
          windowHasAnyResult={results.windowHasAnyResult}
          onOpen={openMatch}
        />
      )}
    </div>
  );
}

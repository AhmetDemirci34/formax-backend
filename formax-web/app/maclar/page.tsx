"use client";

// FORMAX · Maçlar (Match List) ekranı — /maclar route.
// Veri: TAMAMEN GERÇEK backend (GET /api/matches). Mock/placeholder KALDIRILDI.
// Canlı maçlar yeşil vurgulu + zorunlu dakika; biten maçlar solumuş gri + "Bitti".
// Filtreler (lig / takım) gerçek listeden türetilir; frontend veri üretmez.

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { MatchesHeader } from "@/components/maclar/MatchesHeader";
import { MatchListItem } from "@/components/maclar/MatchListItem";
import { RobotIcon } from "@/components/maclar/icons";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { useMatchList } from "@/hooks/useMatchList";
import { dayTabs } from "@/components/maclar/matchData";

export default function MaclarPage() {
  const router = useRouter();
  const tabs = useMemo(() => dayTabs(), []);
  const [query, setQuery] = useState("");
  const [activeDay, setActiveDay] = useState("featured");
  const [league, setLeague] = useState<string>("all");

  const { data = [], isLoading, isError, refetch } = useMatchList();

  // Lig filtresi seçenekleri — GERÇEK listeden türetilir (sabit lig listesi yok).
  const leagues = useMemo(() => {
    const set = new Set<string>();
    for (const m of data) if (m.league) set.add(m.league);
    return [...set].sort((a, b) => a.localeCompare(b, "tr"));
  }, [data]);

  const list = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("tr");
    const now = Date.now();

    return data.filter((m) => {
      // Lig filtresi
      if (league !== "all" && m.league !== league) return false;

      // Takım / lig arama (takım filtresi)
      if (q && !`${m.homeTeam} ${m.awayTeam} ${m.league}`.toLocaleLowerCase("tr").includes(q))
        return false;

      // Gün sekmesi — gerçek kickoff tarihine göre
      if (activeDay !== "featured") {
        const offset = Number(activeDay);
        const d = new Date(m.startTime);
        const dayDiff = Math.floor(
          (new Date(d.toDateString()).getTime() - new Date(new Date(now).toDateString()).getTime()) /
            86_400_000
        );
        if (dayDiff !== offset) return false;
      }

      return true;
    });
  }, [data, query, activeDay, league]);

  const liveCount = list.filter((m) => m.status === "Live").length;

  return (
    <div className="flex min-h-[100dvh] flex-col bg-bg-deep">
      {/* Sticky header */}
      <header className="sticky top-0 z-20 bg-bg-deep/95 pt-[var(--safe-top)] backdrop-blur-md">
        <MatchesHeader
          query={query}
          onQueryChange={setQuery}
          tabs={tabs}
          activeDay={activeDay}
          onDayChange={setActiveDay}
        />

        {/* Lig filtresi — seçenekler gerçek veriden gelir */}
        {leagues.length > 0 ? (
          <div className="flex gap-1.5 overflow-x-auto px-[18px] pb-2 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
            <FilterPill label="Tüm Ligler" on={league === "all"} onClick={() => setLeague("all")} />
            {leagues.map((l) => (
              <FilterPill key={l} label={l} on={league === l} onClick={() => setLeague(l)} />
            ))}
          </div>
        ) : null}
      </header>

      {/* Canlı AI bilgi satırı */}
      <div className="flex items-center gap-2 px-[18px] pb-1 pt-2.5 text-[12px] text-text-muted">
        <span className="text-[#5B8C74]">
          <RobotIcon size={15} />
        </span>
        <span>
          {liveCount > 0 ? `${liveCount} maç canlı` : "AI analizleri canlı olarak güncelleniyor"}
        </span>
      </div>

      {/* Liste */}
      <main className="flex flex-col gap-[7px] px-3.5 pb-[calc(var(--bottom-nav-height)+16px)] pt-2">
        {isLoading ? (
          <LoadingState label="Maçlar yükleniyor..." />
        ) : isError ? (
          <ErrorState message="Maçlar şu an yüklenemiyor. Lütfen tekrar dene." onRetry={() => refetch()} />
        ) : list.length === 0 ? (
          <p className="pt-16 text-center text-[13px] text-text-muted">Bu filtreye uygun maç yok.</p>
        ) : (
          list.map((m) => (
            <MatchListItem
              key={m.matchId}
              match={m}
              onOpen={(match) => router.push(`/match/${match.matchId}/ai`)}
            />
          ))
        )}
      </main>
    </div>
  );
}

function FilterPill({ label, on, onClick }: { label: string; on: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`shrink-0 whitespace-nowrap rounded-full border px-2.5 py-[3px] text-[11px] font-medium transition-colors ${
        on
          ? "border-neon/40 bg-neon/10 text-neon"
          : "border-white/10 text-text-secondary hover:text-text-primary"
      }`}
    >
      {label}
    </button>
  );
}

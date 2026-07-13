"use client";

// FORMAX · Maçlar (Match List) ekranı — /maclar route.
// BottomNav'daki "Maçlar" sekmesi buraya bağlı (daha önce 404 dönüyordu).
// Header + Match List + AI Quick View (placeholder) birleşimi. Global BottomNav +
// telefon çerçevesi app/layout.tsx'ten gelir → burada tekrarlanmaz.
//
// Ürün ilkesi: liste DAİMA AI güvenine göre yüksek → düşük sıralı.
// Veri: şimdilik örnek (matchData.ts placeholder); gerçek entegrasyonda useRecommendations.

import { useMemo, useState } from "react";
import { MatchesHeader } from "@/components/maclar/MatchesHeader";
import { MatchListItem } from "@/components/maclar/MatchListItem";
import { AIQuickViewSheet } from "@/components/maclar/AIQuickViewSheet";
import { RobotIcon } from "@/components/maclar/icons";
import { SAMPLE_MATCHES, dayTabs, type MaclarMatch } from "@/components/maclar/matchData";

export default function MaclarPage() {
  const tabs = useMemo(() => dayTabs(), []);
  const [query, setQuery] = useState("");
  const [activeDay, setActiveDay] = useState("featured");
  const [openMatch, setOpenMatch] = useState<MaclarMatch | null>(null);

  const list = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("tr");
    return SAMPLE_MATCHES.filter((m) => {
      if (activeDay !== "featured" && m.dayOffset !== Number(activeDay)) return false;
      if (q && !`${m.home} ${m.away} ${m.league.name}`.toLocaleLowerCase("tr").includes(q)) return false;
      return true;
    }).sort((a, b) => b.aiConfidence - a.aiConfidence); // AI güveni: yüksek → düşük
  }, [query, activeDay]);

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
      </header>

      {/* Canlı AI bilgi satırı — yalnızca bilgilendirme, sayı yok (backend gerçek sayaç üretene dek).
          Sayaçlar üretildiğinde bu satır tekrar zenginleştirilecek → project_recommendation_count_gap. */}
      <div className="flex items-center gap-2 px-[18px] pb-1 pt-2.5 text-[12px] text-text-muted">
        <span className="text-[#5B8C74]"><RobotIcon size={15} /></span>
        <span>AI analizleri canlı olarak güncelleniyor</span>
      </div>

      {/* Liste */}
      <main className="flex flex-col gap-[7px] px-3.5 pb-[calc(var(--bottom-nav-height)+16px)] pt-2">
        {list.length === 0 ? (
          <p className="pt-16 text-center text-[13px] text-text-muted">Bu filtreye uygun maç yok.</p>
        ) : (
          list.map((m) => <MatchListItem key={m.id} match={m} onOpen={setOpenMatch} />)
        )}
      </main>

      <AIQuickViewSheet match={openMatch} onClose={() => setOpenMatch(null)} />
    </div>
  );
}

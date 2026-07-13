"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { RecommendationCardDto } from "@/types/api";
import { useRecommendations } from "@/hooks/useRecommendations";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { LoadingCard } from "@/components/ui/LoadingState";
import { awayName, homeName, leagueLabel, matchTime, radarLevel } from "@/components/discover/cardSignals";

type Filter = "Tümü" | "Bugün" | "Yarın" | "Bu Hafta" | "Canlı" | "Tamamlanan";
const FILTERS: Filter[] = ["Tümü", "Bugün", "Yarın", "Bu Hafta", "Canlı", "Tamamlanan"];

const RADAR_COLOR: Record<string, string> = {
  YÜKSEK: "text-formax-amber",
  ORTA: "text-[#A855F7]",
  DÜŞÜK: "text-[#3B82F6]",
};

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}
function matchesFilter(card: RecommendationCardDto, f: Filter): boolean {
  if (f === "Tümü") return true;
  if (!card.matchDate) return false;
  const d = new Date(card.matchDate);
  const ts = d.getTime();
  const now = Date.now();
  const diffMin = (ts - now) / 60000;
  const today = new Date();
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);

  if (f === "Canlı") return diffMin <= 0 && -diffMin <= 135;
  if (f === "Tamamlanan") return diffMin < -135;
  if (f === "Bugün") return sameDay(d, today);
  if (f === "Yarın") return sameDay(d, tomorrow);
  if (f === "Bu Hafta") return diffMin > 0 && diffMin < 7 * 1440;
  return true;
}

// Tümünü Gör — bugünün keşif listesi. Radar skoruna göre sıralı, filtre + arama.
export default function AllMatchesPage() {
  const router = useRouter();
  const { data, isLoading } = useRecommendations();
  const [filter, setFilter] = useState<Filter>("Tümü");
  const [q, setQ] = useState("");

  const list = useMemo(() => {
    const all = data?.pages.flat() ?? [];
    const ql = q.trim().toLocaleLowerCase("tr");
    return all
      .filter((c) => matchesFilter(c, filter))
      .filter((c) =>
        !ql || `${homeName(c)} ${awayName(c)} ${leagueLabel(c) ?? ""}`.toLocaleLowerCase("tr").includes(ql),
      )
      .sort((a, b) => (b.radarScore || b.score || 0) - (a.radarScore || a.score || 0));
  }, [data, filter, q]);

  return (
    <div className="flex min-h-screen flex-col bg-bg-base pb-10">
      <header className="sticky top-0 z-10 border-b border-white/[0.06] bg-bg-base/95 px-4 py-3 backdrop-blur">
        <div className="flex items-center gap-3">
          <button onClick={() => router.back()} aria-label="Geri" className="text-text-secondary">‹ Geri</button>
          <h1 className="text-sm font-bold text-white">Tüm Maçlar · Radar Sıralı</h1>
        </div>
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Takım veya lig ara…"
          className="mt-3 w-full rounded-xl border border-white/10 bg-white/[0.04] px-3 py-2 text-sm text-white placeholder:text-text-muted focus:border-[#A855F7]/50 focus:outline-none"
        />
        <div className="mt-2.5 flex gap-2 overflow-x-auto pb-0.5 [&::-webkit-scrollbar]:hidden [scrollbar-width:none]">
          {FILTERS.map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`shrink-0 rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors ${
                filter === f
                  ? "border-[#A855F7]/50 bg-[#A855F7]/15 text-[#A855F7]"
                  : "border-white/10 bg-white/[0.03] text-text-secondary"
              }`}
            >
              {f}
            </button>
          ))}
        </div>
      </header>

      <main className="px-4 pt-3">
        {isLoading && <LoadingCard />}
        {!isLoading && list.length === 0 && (
          <p className="pt-16 text-center text-sm text-text-muted">Bu filtreye uygun maç yok.</p>
        )}
        <div className="overflow-hidden rounded-2xl border border-white/[0.07] bg-white/[0.02]">
          {list.map((c, i) => {
            const time = matchTime(c);
            const league = leagueLabel(c);
            const level = radarLevel(c);
            return (
              <Link
                key={c.matchId}
                href={`/match/${c.matchId}`}
                className={`flex items-center gap-2.5 px-3 py-2.5 ${i === list.length - 1 ? "" : "border-b border-white/[0.05]"}`}
              >
                <span className="w-5 shrink-0 text-center text-[11px] font-bold text-text-muted">{i + 1}</span>
                <TeamCrest name={homeName(c)} logoUrl={c.homeTeam?.logoUrl} size={26} />
                <span className="min-w-0 flex-1 truncate text-[12px] font-semibold text-white">{homeName(c)}</span>
                <div className="shrink-0 px-1 text-center leading-tight">
                  <div className="text-[9px] font-medium text-text-secondary">{time?.live ? "Canlı" : time?.text ?? "—"}</div>
                  {league && <div className="max-w-[92px] truncate text-[8px] text-text-muted">{league}</div>}
                </div>
                <span className="min-w-0 flex-1 truncate text-right text-[12px] font-semibold text-white">{awayName(c)}</span>
                <TeamCrest name={awayName(c)} logoUrl={c.awayTeam?.logoUrl} size={26} />
                <span className={`w-10 shrink-0 text-right text-[10px] font-bold ${RADAR_COLOR[level] ?? "text-[#3B82F6]"}`}>{level}</span>
              </Link>
            );
          })}
        </div>
      </main>
    </div>
  );
}

"use client";

// FORMAX · Takip sayfasındaki "Takip ettiğin maçlar" bölümü.
//
// Maçlar ekranındaki takip ikonuyla AYNI durumu okur (useFollow → tek sistem).
// Her satır: logolar · ev/deplasman · lig · tarih+saat · takipten çıkarma · Maç Detayı.
//
// KİLİTLİ ÜRÜN KARARI: aktif Takip listesi YALNIZ başlamamış maçları gösterir.
// Canlı dakika, canlı skor, İY/2Y/MS ve "Bitti" satırı KALDIRILDI. Takip kaydı
// silinmez (arşiv korunur); başlamış maç yalnız bu aktif listede görünmez —
// süzgeç tek merkezde: hooks/useFollowedMatchDetails.ts → lib/matches/upcomingOnly.ts

import { useRouter } from "next/navigation";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { useFollow } from "@/hooks/useFollow";
import { useFollowedMatchDetails } from "@/hooks/useFollowedMatchDetails";
import type { FollowedMatchDto } from "@/types/api";

function whenLabel(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const time = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
  const today = new Date();
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  if (d.toDateString() === today.toDateString()) return `Bugün ${time}`;
  if (d.toDateString() === tomorrow.toDateString()) return `Yarın ${time}`;
  return `${d.toLocaleDateString("tr-TR", { day: "2-digit", month: "short" })} ${time}`;
}

export function FollowedMatchesSection() {
  const { matches } = useFollowedMatchDetails();
  if (matches.length === 0) return null;

  return (
    <section className="px-4 pb-1 pt-3">
      <div className="mb-2 flex items-baseline justify-between">
        <h2 className="text-[13px] font-semibold text-text-primary">Takip ettiğin maçlar</h2>
        <span className="text-[11px] tabular-nums text-text-muted">{matches.length}</span>
      </div>

      <div className="flex flex-col gap-2">
        {matches.map((m) => (
          <FollowedMatchRow key={m.matchId} match={m} />
        ))}
      </div>
    </section>
  );
}

function FollowedMatchRow({ match }: { match: FollowedMatchDto }) {
  const router = useRouter();
  const { toggle, isPending } = useFollow(match.matchId);

  return (
    <div className="flex items-center gap-2.5 rounded-[13px] bg-bg-glass px-2.5 py-2.5">
      <button
        type="button"
        onClick={() => router.push(`/match/${match.matchId}`)}
        className="flex min-w-0 flex-1 items-center gap-2.5 text-left"
        aria-label={`${match.homeTeam} - ${match.awayTeam} maç detayı`}
      >
        <div className="min-w-0 flex-1">
          <div className="mb-1 flex items-center gap-2 text-[11px] text-text-muted">
            <span className="truncate">{match.league || "—"}</span>
            <span>·</span>
            <span className="shrink-0">{whenLabel(match.startTime)}</span>
          </div>

          <TeamLine name={match.homeTeam} logoUrl={match.homeTeamLogoUrl} />
          <div className="h-[3px]" />
          <TeamLine name={match.awayTeam} logoUrl={match.awayTeamLogoUrl} />
        </div>
      </button>

      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          e.preventDefault();
          toggle();
        }}
        disabled={isPending}
        aria-label="Maçı takipten çıkar"
        title="Maçı takipten çıkar"
        className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] text-neon transition-colors hover:bg-neon/10 disabled:opacity-50"
      >
        <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
          <path
            d="M12 3.5l2.6 5.28 5.83.85-4.22 4.11.996 5.8L12 16.82l-5.21 2.74.996-5.8L3.57 9.63l5.83-.85L12 3.5z"
            fill="currentColor"
            stroke="currentColor"
            strokeWidth="1.7"
            strokeLinejoin="round"
          />
        </svg>
      </button>
    </div>
  );
}

function TeamLine({ name, logoUrl }: { name: string; logoUrl?: string | null }) {
  return (
    <div className="flex items-center gap-2">
      <TeamCrest name={name} logoUrl={logoUrl} size={20} />
      <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium text-text-primary">
        {name}
      </span>
    </div>
  );
}

"use client";

import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { TeamCrest } from "@/components/ui/TeamCrest";
import type { UiPrediction } from "@/types/predictions";
import { PredictionStatusBadge } from "./PredictionStatusBadge";

/**
 * PredictionCard — tek tahmin kartı (kompakt, ~100px). Basınca ilgili maçın
 * detayına gider (Match Detail). Veri gerçek /detail zenginleştirmesinden gelir.
 * Not: Backend'de gerçek "oran" alanı olmadığından sağdaki değer AI olasılık %'sidir.
 */
/**
 * Skor gösterimi. Alan yoksa "—" döner — eksik veride 0-0 UYDURULMAZ.
 * Hesap yapılmaz: değerler backend'den olduğu gibi gelir.
 */
function fmtScore(s?: { home: number; away: number } | null): string {
  if (!s) return "—";
  return `${s.home}-${s.away}`;
}

export function PredictionCard({ p }: { p: UiPrediction }) {
  const router = useRouter();
  const live = p.status === "live";
  const finished = p.status === "finished";

  return (
    <motion.button
      type="button"
      whileTap={{ scale: 0.98 }}
      transition={{ duration: 0.15 }}
      onClick={() => router.push(`/match/${p.matchId}`)}
      className="flex h-[100px] w-full items-stretch gap-3 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-3 py-2.5 text-left transition-opacity active:opacity-70"
    >
      {/* Sol: durum / tarih-saat */}
      <div className="flex w-[52px] shrink-0 flex-col justify-center gap-0.5 border-r border-white/[0.06] pr-2">
        {finished ? (
          /* BİTMİŞ MAÇ: İY / 2Y / MS. Eksik alan "—" gösterilir, 0-0 UYDURULMAZ.
             İkinci yarı backend'de hesaplanır (MS − İY); burada çıkarma yapılmaz. */
          <>
            <span className="text-[9px] font-bold uppercase text-text-muted">Bitti</span>
            <div className="flex flex-col gap-[1px] leading-none">
              <span className="whitespace-nowrap text-[10px] tabular-nums text-text-muted">
                İY {fmtScore(p.scoreBreakdown?.halfTime)}
              </span>
              <span className="whitespace-nowrap text-[10px] tabular-nums text-text-muted">
                2Y {fmtScore(p.scoreBreakdown?.secondHalf)}
              </span>
              <span className="whitespace-nowrap text-[13px] font-bold tabular-nums text-text-primary">
                MS {fmtScore(p.scoreBreakdown?.fullTime)}
              </span>
            </div>
          </>
        ) : live ? (
          <>
            <span className="text-[9px] font-bold uppercase text-goalai-accent">Canlı</span>
            <span className="text-[16px] font-bold leading-none text-text-primary">{p.score ?? "–"}</span>
          </>
        ) : (
          <>
            <span className="text-[9px] font-bold uppercase text-goalai-accent">{kickoffDay(p.kickoff)}</span>
            <span className="text-[15px] font-bold leading-none text-text-primary">{kickoffTime(p.kickoff)}</span>
          </>
        )}
      </div>

      {/* Orta: takımlar */}
      <div className="flex min-w-0 flex-1 flex-col justify-center gap-1.5">
        {p.league ? (
          <span className="truncate text-[9px] font-medium uppercase tracking-wide text-text-muted">{p.league}</span>
        ) : null}
        <TeamRow name={p.home.name} logoUrl={p.home.logoUrl} />
        <TeamRow name={p.away.name} logoUrl={p.away.logoUrl} />
      </div>

      {/* Sağ: market + AI % + durum */}
      <div className="flex shrink-0 flex-col items-end justify-center gap-1">
        <span className="rounded-md bg-white/[0.06] px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-text-secondary">
          {p.market}
        </span>
        {p.odds != null ? (
          <span className="text-[18px] font-bold leading-none text-goalai-accent">{p.odds.toFixed(2)}</span>
        ) : (
          // Backend gerçek oran gönderince otomatik olarak yukarıdaki değere geçer.
          <span className="text-[10px] font-semibold uppercase tracking-wide text-text-secondary">
            Hazırlanıyor
          </span>
        )}
        {live ? (
          <span className="flex items-center gap-1 text-[10.5px] font-semibold text-text-secondary">
            <PlayGlyph />
            {p.minute != null ? `${p.minute}'` : "Canlı"}
          </span>
        ) : finished ? (
          <PredictionStatusBadge status="finished" />
        ) : (
          <span className="flex items-center gap-1 text-[10.5px] font-semibold text-text-secondary">
            <BellGlyph />
            Bekliyor
          </span>
        )}
      </div>
    </motion.button>
  );
}

function TeamRow({ name, logoUrl }: { name: string; logoUrl?: string | null }) {
  return (
    <span className="flex items-center gap-2">
      <TeamCrest name={name} logoUrl={logoUrl} size={20} />
      <span className="truncate text-[14px] font-medium text-text-primary">{name}</span>
    </span>
  );
}

function kickoffDay(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const now = new Date();
  const sameDay = d.toDateString() === now.toDateString();
  if (sameDay) return "Bugün";
  const months = ["OCA", "ŞUB", "MAR", "NİS", "MAY", "HAZ", "TEM", "AĞU", "EYL", "EKİ", "KAS", "ARA"];
  return `${d.getDate()} ${months[d.getMonth()]}`;
}
function kickoffTime(iso: string | null): string {
  if (!iso) return "--:--";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "--:--";
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function PlayGlyph() {
  return (
    <svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M8 5l11 7-11 7z" />
    </svg>
  );
}
function BellGlyph() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.7 21a2 2 0 0 1-3.4 0" />
    </svg>
  );
}

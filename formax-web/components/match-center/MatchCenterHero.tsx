"use client";

import { useEffect, useState } from "react";
import type { MatchDetailDto, TeamSummaryDto } from "@/types/api";
import { computeMatchClock, isFinishedStatus, kickoffMsOf } from "@/lib/matchClock";
import { readAiConfidence } from "./aiContext";
import { ConfidenceGauge } from "./ConfidenceGauge";
import { useMatchDecision } from "@/hooks/useMatchDecision";

/**
 * Maç Detay Merkezi · Hero Card (referans yerleşim — Teknik Doküman §14).
 * Kompakt kart: üstte lig rozeti + kickoff saati/geri sayımı, ortada dairesel AI Güven
 * göstergesi (tam merkez), iki yanda simetrik takım blokları. Arka planda mevcut
 * stadyum asset'i (`/images/hero/stadium-bg.webp`) düşük opaklıkta.
 * Yalnızca Hero; diğer componentler değişmedi. Veri: gerçek `/detail`.
 */
export function MatchCenterHero({ match }: { match: MatchDetailDto }) {
  // AI Güveni backend'den gelir (aynı Decision cache'i — ikinci istek atılmaz).
  const { data: decision } = useMatchDecision(match.matchId);
  const confidence = readAiConfidence(decision?.confidence);
  const countdown = useKickoffCountdown(match);
  const leagueInitial = (match.league || "•").trim().charAt(0).toLocaleUpperCase("tr-TR");

  return (
    <div className="z-40 shrink-0 px-4 pb-1 pt-1.5">
      <div className="relative overflow-hidden rounded-[26px] border border-white/5 bg-goalai-surface px-4 py-2.5 shadow-[0_16px_40px_rgba(0,0,0,0.45)]">
        {/* stadyum atmosferi — mevcut asset, düşük opaklık, altta */}
        <div aria-hidden className="pointer-events-none absolute inset-0">
          <div
            className="absolute inset-0 bg-cover bg-bottom opacity-25"
            style={{ backgroundImage: "url(/images/hero/stadium-bg.webp)" }}
          />
          <div
            className="absolute inset-0"
            style={{ background: "linear-gradient(180deg, rgba(19,19,19,.92) 0%, rgba(19,19,19,.55) 46%, rgba(19,19,19,.8) 100%)" }}
          />
          <div
            className="absolute inset-x-0 bottom-0 h-14"
            style={{ background: "radial-gradient(72% 100% at 50% 130%, rgba(204,255,0,.14), transparent 60%)" }}
          />
        </div>

        {/* üst satır: lig · kickoff saati / geri sayım (MVP: CANLI yok) */}
        <div className="relative flex items-center justify-between gap-3">
          <span className="flex min-w-0 items-center gap-2">
            <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white/90 text-[10px] font-bold text-[#131313]">
              {leagueInitial}
            </span>
            <span className="flex min-w-0 flex-col">
              <span className="min-w-0 truncate text-xs font-semibold uppercase tracking-wide text-white/60">
                {match.league || "—"}
              </span>
              {/*
                MAÇ TÜRÜ — backend'in sağlayıcı tur adından türettiği etiket
                ("Eleme Turu", "Son 16 Turu", "Lig Maçı · 1. Hafta", "Final").
                Backend null gönderirse satır HİÇ çizilmez; frontend maç türünü
                TAHMİN ETMEZ ve "önemli maç" gibi yorum ÜRETMEZ.
              */}
              {match.matchTypeLabel && (
                <span className="min-w-0 truncate text-[10px] font-medium tracking-wide text-white/40">
                  {match.matchTypeLabel}
                </span>
              )}
            </span>
          </span>
          {/* Geri sayımda vurgulu aksan; planlanan saat/BİTTİ daha sakin. CANLI rozeti YOK. */}
          <span
            className={`shrink-0 font-mono text-sm font-bold tabular-nums tracking-wide ${
              countdown.countdown ? "text-goalai-accent" : "text-white/70"
            }`}
          >
            {countdown.label}
          </span>
        </div>

        {/* orta: takım · gösterge · takım — logo optik merkezi ile gauge merkezi hizalı */}
        <div className="relative mt-1.5 flex items-start justify-between gap-2">
          <TeamBlock team={match.homeTeam} />
          {/* Backend güven vermezse gösterge hiç çizilmez — uydurma skor yok. */}
          {confidence && <ConfidenceGauge score={confidence.score} level={confidence.level} />}
          <TeamBlock team={match.awayTeam} />
        </div>
      </div>
    </div>
  );
}

function TeamBlock({ team }: { team: TeamSummaryDto }) {
  const initials =
    team.name.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "").slice(0, 3).toUpperCase() || "—";

  return (
    <div className="flex w-[84px] shrink-0 flex-col items-center gap-1">
      {/* logo kutusu — yüksekliği gauge ile eşit → optik merkez hizası */}
      <div className="flex h-[80px] items-center justify-center">
        <div className="flex h-[63px] w-[63px] items-center justify-center overflow-hidden rounded-2xl bg-white/95 shadow-[0_4px_16px_rgba(0,0,0,0.45)]">
          {team.logoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={team.logoUrl} alt={team.name} width={63} height={63} className="h-full w-full object-contain" />
          ) : (
            <span className="text-base font-black text-[#0a0e16]">{initials}</span>
          )}
        </div>
      </div>
      <span className="line-clamp-2 min-h-[2em] w-full text-center text-[13px] font-bold uppercase leading-[1] tracking-normal text-white">
        {team.name}
      </span>
    </div>
  );
}

/**
 * Kickoff saati / geri sayımı — hesap ORTAK modülden gelir (`lib/matchClock.ts`),
 * Keşfet'teki `MatchClock` ile BİREBİR aynı fonksiyon. Aynı maç iki ekranda aynı
 * değeri gösterir.
 *
 * KİLİTLİ: burada saatten CANLI ÜRETİLMEZ. Önceki sürüm `kickoff - now <= 0` olduğunda
 * "CANLI" yazıyordu; backend kickoff'u UTC eki olmadan gönderdiği için bu, gelecekteki
 * maçı da canlı gösteriyordu (ölçüldü: Fenerbahçe–Lyon 22:00 maçı, 19:06'da CANLI).
 * MVP'de canlı maç özelliği kapalıdır (LiveMatchData:Enabled=false) → CANLI rozeti yok.
 * "BİTTİ" yalnız backend'in status alanından okunur, zamandan türetilmez.
 */
function useKickoffCountdown(match: MatchDetailDto): { label: string; countdown: boolean } {
  const [now, setNow] = useState(0);

  useEffect(() => {
    const update = () => setNow(Date.now());
    const t0 = setTimeout(update, 0);
    const iv = setInterval(update, 1000);
    return () => {
      clearTimeout(t0);
      clearInterval(iv);
    };
  }, []);

  if (isFinishedStatus(match.status)) return { label: "BİTTİ", countdown: false };
  if (now === 0) return { label: "--:--", countdown: false };

  const kickoffMs = kickoffMsOf(match.matchDate);
  if (!Number.isFinite(kickoffMs)) return { label: "--:--", countdown: false };

  const state = computeMatchClock(kickoffMs, now);
  return { label: state.label, countdown: state.mode === "countdown" };
}

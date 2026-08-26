"use client";

import Link from "next/link";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { stripEmoji } from "@/components/discover/cardSignals";

interface Props {
  matchId: number;
  time: string;
  home: { name: string; logoUrl?: string | null };
  away: { name: string; logoUrl?: string | null };
  /** Backend'in seçtiği AI sonucu (market + olasılık). Yoksa alan gösterilmez. */
  market?: string | null;
  probability?: number | null;
  /** Gerçek market oranı (backend Odds). Yoksa gösterilmez — uydurulmaz. */
  odd?: number | null;
  /** Ayak açılışında ilgi sinyali (trackInterest click) — parent (client) sağlar. */
  onOpen?: () => void;
}

/**
 * FORMAX · ComboLegItem — kombindeki tek maç ayağı, TEK SATIR kompakt liste öğesi.
 *
 * Yatay/makara yapıdan dikey listeye geçildi (Sprint #2 · madde 6). Görsel dil
 * (renk, tipografi, kenarlık, radius) korunur; yalnız yerleşim satır oldu.
 * Tüm değerler backend'den gelir — frontend olasılık/oran ÜRETMEZ.
 */
export function ComboLegItem({
  matchId,
  time,
  home,
  away,
  market,
  probability,
  odd,
  onOpen,
}: Props) {
  return (
    <Link
      href={`/match/${matchId}`}
      onClick={onOpen}
      className="flex items-center gap-2.5 rounded-2xl border border-white/[0.06] bg-white/[0.03] px-3 py-2.5 transition-colors hover:bg-white/[0.06] active:scale-[0.99]"
    >
      {/* Sol — takımlar (küçük logo + isim) ve maç zamanı */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <TeamCrest name={home.name} logoUrl={home.logoUrl} size={18} />
          <span className="min-w-0 truncate text-[11.5px] font-bold text-text-primary">
            {home.name}
          </span>
          <span className="shrink-0 text-[9px] font-semibold text-text-muted">-</span>
          <TeamCrest name={away.name} logoUrl={away.logoUrl} size={18} />
          <span className="min-w-0 truncate text-[11.5px] font-bold text-text-primary">
            {away.name}
          </span>
        </div>
        {time ? (
          <span className="mt-0.5 block text-[9.5px] font-medium text-text-muted">{time}</span>
        ) : null}
      </div>

      {/* Sağ — AI sonucu · güven · gerçek oran */}
      <div className="flex shrink-0 items-center gap-2.5">
        {market ? (
          <div className="flex flex-col items-end leading-tight">
            <span className="text-[9.5px] font-bold text-text-secondary">{stripEmoji(market)}</span>
            {probability != null ? (
              <span className="text-[12.5px] font-extrabold tabular-nums text-neon">
                %{Math.round(probability)}
              </span>
            ) : null}
          </div>
        ) : null}

        {odd != null ? (
          <span className="text-[13px] font-extrabold tabular-nums text-text-primary">
            {odd.toFixed(2)}
          </span>
        ) : null}
      </div>
    </Link>
  );
}

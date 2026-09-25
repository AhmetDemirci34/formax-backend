import type { RecommendationCardDto } from "@/types/api";
import { userBadge, radarLevel } from "@/components/discover/cardSignals";
import { ICONS } from "@/components/discover/icons";

/**
 * FORMAX · HeroSignalRow — Keşfet kartının üst satırı.
 *
 * Sol: sinyal rozeti (ör. PİYASA HAREKETİ) — kaynak backend `recommendationReason`.
 * Sağ: RADAR seviyesi — kaynak backend `radarScore`/`score` (cardSignals.radarLevel).
 *
 * İkisi de SAYISAL/KATEGORİK sinyaldir; yorum, açıklama veya anlatı DEĞİLDİR.
 * Veri yoksa ilgili taraf hiç render edilmez (uydurma yok).
 *
 * Daha önce kartın DIŞINDA duran DiscoverBadgeRow'un yerini alır: referans yerleşimde
 * bu iki sinyal kartın kendi üst satırındadır.
 */

const RADAR_TONE: Record<string, string> = {
  YÜKSEK: "var(--signal-amber)",
  ORTA: "var(--signal-purple)",
  DÜŞÜK: "var(--text-secondary)",
};

export function HeroSignalRow({ card }: { card: RecommendationCardDto }) {
  const badge = userBadge(card);
  const level = radarLevel(card);
  const tone = RADAR_TONE[level] ?? "var(--signal-purple)";
  const BadgeIcon = badge ? ICONS[badge.iconKey] : null;

  return (
    <div className="flex items-start justify-between gap-2">
      {badge && BadgeIcon ? (
        <span className="inline-flex items-center gap-1.5 rounded-full border border-signal-purple/40 bg-signal-purple/10 px-2.5 py-1">
          <BadgeIcon size={12} className="text-signal-purple" />
          <span className="text-[9.5px] font-bold uppercase tracking-[0.09em] text-signal-purple">
            {badge.label}
          </span>
        </span>
      ) : (
        <span />
      )}

      <span className="flex flex-col items-end leading-none">
        <span className="text-[9px] font-bold uppercase tracking-[0.16em] text-text-secondary">
          Radar
        </span>
        <span className="mt-1 text-[12px] font-black uppercase tracking-[0.06em]" style={{ color: tone }}>
          {level}
        </span>
      </span>
    </div>
  );
}

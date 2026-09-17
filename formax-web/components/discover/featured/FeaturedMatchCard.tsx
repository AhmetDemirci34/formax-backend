import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { TeamLogoPair } from "@/components/ui/TeamLogoPair";

interface Props {
  matchId: number;
  time: string;
  home: { name: string; logoUrl?: string | null };
  away: { name: string; logoUrl?: string | null };
  /**
   * Backend AI Olası Sonuç (TopPredictionMarket + TopPredictionProbability).
   * Backend göndermezse alt alan GÖSTERİLMEZ — frontend ilgi skoru/olasılık HESAPLAMAZ.
   */
  topPrediction?: { market: string; probability: number } | null;
  /**
   * Bu marketin GERÇEK oranı — backend TopPrediction.Odd (MatchMarketOdds).
   * Yoksa gösterilmez (uydurulmaz, olasılıktan türetilmez).
   */
  odd?: number | null;
  /** Bir tık daha küçük yerleşim (Keşfet üstünde). Renk/typography değişmez. */
  compact?: boolean;
  /** Kart açılışında ilgi sinyali (trackInterest click) — parent (client) sağlar. */
  onOpen?: () => void;
}

/**
 * FORMAX · FeaturedMatchCard — "Sana Özel" kartı (TIKLANABİLİR → Match Detail).
 * Saf View: eski "İlgi Skoru %" (frontend hesabı) ve regex-etiket seçimi kaldırıldı.
 * Alt alanda yalnız backend'in gönderdiği AI Olası Sonuç gösterilir; yoksa render edilmez.
 */
export function FeaturedMatchCard({
  matchId,
  time,
  home,
  away,
  topPrediction,
  odd,
  compact = false,
  onOpen,
}: Props) {
  return (
    <Link
      href={`/match/${matchId}`}
      onClick={onOpen}
      className={`flex shrink-0 flex-col items-center rounded-2xl border border-white/[0.06] bg-white/[0.03] text-center transition-colors hover:bg-white/[0.06] active:scale-[0.98] ${
        compact ? "w-[110px] gap-1 p-1.5" : "w-[150px] gap-2.5 p-3"
      }`}
    >
      <div className="self-start">
        <Badge label="Öne Çıkan" tone="neon" />
      </div>

      {time ? <span className="text-[10px] font-medium text-text-muted">{time}</span> : null}

      <TeamLogoPair home={home} away={away} size={compact ? 19 : 28} showVs />

      <div className="flex w-full items-start justify-center gap-2 leading-tight">
        <span className="flex-1 truncate text-[11px] font-bold text-text-primary">{home.name}</span>
        <span className="flex-1 truncate text-[11px] font-bold text-text-primary">{away.name}</span>
      </div>

      {/* Market · olasılık · oran — üçü de BACKEND değeri, aynı sütunda alt alta:
            Çifte Şans (1X)
            %85          ← TopPrediction.Probability (olasılık)
            1.20         ← TopPrediction.Odd (GERÇEK bookmaker oranı)
          Oran yoksa yalnız o satır çıkmaz. Frontend oran HESAPLAMAZ, % → oran
          çevirmez, 1/probability yapmaz; karar paketi skoru buraya BASILMAZ. */}
      {topPrediction ? (
        <div className="mt-auto flex w-full flex-col items-center gap-0.5 border-t border-white/[0.06] pt-2">
          <span className="w-full break-words text-center text-[10px] font-semibold leading-tight text-text-secondary">
            {topPrediction.market}
          </span>
          <span className="text-[12px] font-extrabold leading-none tabular-nums text-neon">
            %{topPrediction.probability}
          </span>
          {odd != null ? (
            <span className="text-[12px] font-extrabold leading-none tabular-nums text-text-primary">
              {odd.toFixed(2)}
            </span>
          ) : null}
        </div>
      ) : null}
    </Link>
  );
}

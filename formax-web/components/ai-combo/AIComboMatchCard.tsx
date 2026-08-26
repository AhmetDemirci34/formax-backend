"use client";

import { TeamCrest } from "@/components/ui/TeamCrest";

/**
 * FORMAX · Bugünün Seçkisi — tek maç kartı (KOMPAKT).
 *
 * Bu ekran bir maç detayı değil, hızlı taranan bir seçki listesidir: kart yüksekliği
 * bilinçli olarak düşürüldü (büyük logo bloğu → tek satır takım şeridi, dikey boşluklar
 * daraltıldı, gerekçe 2 satıra kırpıldı). Uzun anlatı maç detayında kalır.
 *
 * SADECE gerçek backend verisi render edilir; olmayan alan GİZLENİR (mock yok).
 * Kaynak: /api/home/recommendations (sıra, takımlar, topPrediction, storyHeadline) +
 * /api/matches/{id}/detail (league, insight.summary).
 * Market/olasılık/oran backend değerleridir; frontend oran HESAPLAMAZ, % → oran çevirmez.
 */
export interface AIComboMatchVM {
  matchId: number;
  order: number;
  time: string;
  /** Gerçek lig adı (backend /detail → league). Yoksa null → gizlenir. */
  league: string | null;
  home: { name: string; logoUrl?: string | null };
  away: { name: string; logoUrl?: string | null };
  /** Gerçek AI seçimi (backend topPrediction.market). Yoksa null → seçim satırı gizlenir. */
  selectionLabel: string | null;
  /** Gerçek olasılık % (backend topPrediction.probability). */
  selectionProbability: number | null;
  /** GERÇEK market oranı (backend topPrediction.odd). Yoksa null → oran gösterilmez. */
  selectionOdd: number | null;
  /** Gerçek insight.summary. Yoksa null → "Neden Öne Çıkıyor?" gizlenir. */
  reasoning: string | null;
  /** Gerçek storyHeadline (emoji temizlenmiş). Yoksa null → etiket gizlenir. */
  badge: string | null;
  loading: boolean;
}

export function AIComboMatchCard({
  vm,
  onDetail,
}: {
  vm: AIComboMatchVM;
  onDetail: (matchId: number) => void;
}) {
  return (
    <article className="rounded-2xl border border-white/[0.08] bg-white/[0.03] p-3.5">
      {/* Üst satır: sıra · lig · zaman — tek satırda, ayrı bloklar yerine */}
      <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wide">
        <span className="tabular-nums text-neon">{String(vm.order).padStart(2, "0")}</span>
        {vm.league && (
          <>
            <span aria-hidden className="h-0.5 w-0.5 rounded-full bg-white/25" />
            <span className="min-w-0 truncate text-white/40">{vm.league}</span>
          </>
        )}
        {vm.time && <span className="ml-auto shrink-0 text-white/55">{vm.time}</span>}
      </div>

      {/* Takımlar — yatay şerit (eski dikey logo bloğu ~90px yer kaplıyordu) */}
      <div className="mt-2.5 flex items-center gap-2">
        <TeamCrest name={vm.home.name} logoUrl={vm.home.logoUrl} size={22} />
        <span className="min-w-0 flex-1 truncate text-[13px] font-semibold text-white">
          {vm.home.name}
        </span>
        <span className="shrink-0 text-[10px] font-bold uppercase text-white/30">vs</span>
        <span className="min-w-0 flex-1 truncate text-right text-[13px] font-semibold text-white">
          {vm.away.name}
        </span>
        <TeamCrest name={vm.away.name} logoUrl={vm.away.logoUrl} size={22} />
      </div>

      {/* Gerçek backend sinyali — ikincil, küçük çip (kartı bastırmaz) */}
      {vm.badge && (
        <p className="mt-2.5 inline-block max-w-full truncate rounded-full border border-white/[0.08] px-2 py-0.5 text-[9.5px] font-semibold uppercase tracking-wide text-white/45">
          {vm.badge}
        </p>
      )}

      {/* NEDEN ÖNE ÇIKIYOR? — backend metni, 2 satıra kırpılır (uzunu detayda) */}
      {vm.loading ? (
        <div className="mt-2.5 space-y-1.5">
          <div className="h-2.5 w-24 animate-pulse rounded bg-white/10" />
          <div className="h-2.5 w-full animate-pulse rounded bg-white/10" />
        </div>
      ) : vm.reasoning ? (
        <div className="mt-2.5">
          <p className="text-[9.5px] font-bold uppercase tracking-[0.12em] text-neon">
            Neden Öne Çıkıyor?
          </p>
          <p className="mt-1 line-clamp-2 text-[12.5px] leading-[1.5] text-white/70">
            {vm.reasoning}
          </p>
        </div>
      ) : null}

      {/* Market · olasılık · oran · MAÇI AÇ — tek satır */}
      <div className="mt-3 flex items-center justify-between gap-3 border-t border-white/[0.07] pt-2.5">
        {vm.selectionLabel ? (
          <div className="flex min-w-0 items-baseline gap-2">
            <span className="min-w-0 truncate text-[12px] font-semibold text-white/75">
              {vm.selectionLabel}
            </span>
            {vm.selectionProbability != null && (
              <span className="shrink-0 text-[15px] font-bold tabular-nums text-neon">
                %{vm.selectionProbability}
              </span>
            )}
            {/* GERÇEK sağlayıcı oranı. Yoksa hiç çıkmaz. */}
            {vm.selectionOdd != null && (
              <span className="shrink-0 text-[13px] font-bold tabular-nums text-white">
                {vm.selectionOdd.toFixed(2)}
              </span>
            )}
          </div>
        ) : (
          <span />
        )}

        <button
          type="button"
          onClick={() => onDetail(vm.matchId)}
          className="flex shrink-0 items-center gap-1 text-[11px] font-bold uppercase tracking-wide text-neon transition-opacity hover:opacity-80 active:scale-95"
          aria-label={`${vm.home.name} ${vm.away.name} maçını aç`}
        >
          Maçı Aç
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="M5 12h14M13 6l6 6-6 6" />
          </svg>
        </button>
      </div>
    </article>
  );
}

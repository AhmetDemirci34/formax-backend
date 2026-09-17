/**
 * Maç Detay Merkezi · yükleme iskeleti.
 *
 * `/detail` cevabı gelene kadar ekranın geri kalanı boş siyah alan ya da tek bir dönen
 * simge olarak kalmaz: Hero kartı, GOALAI Asistan kartı ve aksiyon düğmeleri kendi
 * ölçülerinde yer tutar. Böylece cevap geldiğinde yerleşim kaymaz.
 *
 * Animasyon yalnız CSS `animate-pulse`tır; içerik görünürlüğü hiçbir JS karesine
 * (requestAnimationFrame) bağlı değildir.
 */
export function MatchCenterSkeleton() {
  return (
    <div
      data-testid="match-center-skeleton"
      role="status"
      aria-live="polite"
      aria-busy="true"
      className="flex min-h-0 flex-1 flex-col"
    >
      <span className="sr-only">Maç merkezi hazırlanıyor...</span>

      {/* Hero kartı — MatchCenterHero ile aynı dış ölçü */}
      <div className="shrink-0 px-4 pb-1 pt-1.5">
        <div className="animate-pulse rounded-[26px] border border-white/5 bg-goalai-surface-bright px-4 py-2.5">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="h-5 w-5 rounded-full bg-white/10" />
              <div className="h-3 w-28 rounded bg-white/10" />
            </div>
            <div className="h-3.5 w-16 rounded bg-white/10" />
          </div>
          <div className="mt-1.5 flex items-start justify-between gap-2">
            <TeamPlaceholder />
            <div className="h-[80px] w-[80px] shrink-0 rounded-full border-[6px] border-white/10" />
            <TeamPlaceholder />
          </div>
        </div>
      </div>

      {/* Dashboard — AssistantCard + ActionGrid ile aynı düzen */}
      <div className="flex min-h-0 flex-1 flex-col gap-4 px-4 py-4">
        <div className="animate-pulse rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
          <div className="mb-3 h-3 w-32 rounded bg-white/10" />
          <div className="h-4 w-full rounded bg-white/10" />
          <div className="mt-2 h-4 w-2/3 rounded bg-white/10" />
        </div>
        <div className="flex flex-col gap-3">
          <div className="h-14 w-full animate-pulse rounded-2xl border border-goalai-border bg-goalai-surface-bright" />
          <div className="grid grid-cols-2 gap-3">
            <div className="h-24 animate-pulse rounded-2xl border border-goalai-border bg-goalai-surface-bright" />
            <div className="h-24 animate-pulse rounded-2xl border border-goalai-border bg-goalai-surface-bright" />
            <div className="h-24 animate-pulse rounded-2xl border border-goalai-border bg-goalai-surface-bright" />
          </div>
        </div>
      </div>
    </div>
  );
}

function TeamPlaceholder() {
  return (
    <div className="flex w-[84px] shrink-0 flex-col items-center gap-1">
      <div className="flex h-[80px] items-center justify-center">
        <div className="h-[63px] w-[63px] rounded-2xl bg-white/10" />
      </div>
      <div className="h-[26px] w-16 rounded bg-white/10" />
    </div>
  );
}

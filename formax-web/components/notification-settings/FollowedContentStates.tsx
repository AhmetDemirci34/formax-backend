/**
 * "Takip Ettiklerim" kartının veri dışı durumları.
 *
 * Ölçüler gerçek `NotificationItem` ile birebir aynıdır (72px satır, 16px
 * padding, 32px dairesel görsel) — veri geldiğinde layout kaymaz.
 */

/** Yükleniyor — kart satırları üzerinde shimmer (Handoff §13). */
export function FollowedContentSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div aria-busy="true" aria-live="polite">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i}>
          <div className="flex h-[72px] items-center gap-3 px-4">
            <div className="profile-shimmer h-8 w-8 shrink-0 rounded-full" />
            <div className="flex flex-1 flex-col gap-2">
              <div className="profile-shimmer h-4 w-1/2 rounded" />
              <div className="profile-shimmer h-2.5 w-1/5 rounded" />
            </div>
            <div className="profile-shimmer h-[31px] w-[51px] shrink-0 rounded-full" />
          </div>
          {i < rows - 1 ? (
            <div className="ml-4 h-px bg-[var(--profile-divider)]" role="presentation" />
          ) : null}
        </div>
      ))}
    </div>
  );
}

/** Takip edilen içerik yokken gösterilir. */
export function FollowedContentEmpty() {
  return (
    <p className="px-4 py-8 text-center text-[14px] leading-[1.45] text-profile-muted">
      Henüz takip ettiğin içerik bulunmuyor.
    </p>
  );
}

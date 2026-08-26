/**
 * ProfileSkeleton — `isLoading` sırasında Hero kartı ve Section listeleri yerine
 * gösterilen shimmer ekranı (1.5s linear wave).
 *
 * Yer tutucuların ölçüleri gerçek componentlerle BİREBİR aynıdır (kartlar 16px
 * içeride + radius 16, satır 72px, divider 52px inset); veri geldiğinde layout
 * kaymaz.
 */

function GroupSkeleton({ rows }: { rows: number }) {
  return (
    <section>
      <div className="px-6 pb-2">
        <div className="profile-shimmer h-3 w-24 rounded" />
      </div>

      <div className="mx-4 overflow-hidden rounded-2xl bg-profile-container">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i}>
            <div className="flex h-[78px] items-center gap-4 px-4">
              <div className="profile-shimmer h-5 w-5 shrink-0 rounded" />
              <div className="flex flex-1 flex-col gap-2">
                <div className="profile-shimmer h-3.5 w-1/3 rounded" />
                <div className="profile-shimmer h-3 w-3/5 rounded" />
              </div>
            </div>
            {i < rows - 1 ? <div className="ml-[52px] h-px bg-[var(--profile-divider)]" /> : null}
          </div>
        ))}
      </div>
    </section>
  );
}

export function ProfileSkeleton() {
  return (
    <div aria-busy="true" aria-live="polite">
      {/* Hero kartı */}
      <div className="px-4 pb-6 pt-4">
        <div className="profile-shimmer h-[186px] w-full rounded-2xl" />
      </div>

      <div className="flex flex-col gap-6">
        <GroupSkeleton rows={5} />
        <GroupSkeleton rows={5} />
      </div>

      {/* Çıkış kartı */}
      <div className="mx-4 mt-6">
        <div className="profile-shimmer h-14 w-full rounded-2xl" />
      </div>
    </div>
  );
}

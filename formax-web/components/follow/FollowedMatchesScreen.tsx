"use client";

// FORMAX · TAKİP ETTİĞİM MAÇLAR
//
// KESİN ÜRÜN KARARI (06.09.2026): bu ekran YALNIZ kullanıcının takip ettiği maçları
// gösterir. Kaldırılanlar — TÜMÜ/MAÇLAR/TAKIMLAR sekmeleri, "Yeni Gelişmeler" alanı,
// Takımlar kartı, Ligler kartı, istatistik kutuları ve genel gelişme akışı.
//
// KALDIRMA ≠ SİLME: takip edilen takım ve lig kayıtları backend'de DURUYOR (başka
// yüzeyler onları kullanıyor); bu ekran onları yalnız GÖSTERMİYOR.
//
// Sıra backend'in: yaklaşanlar en yakın maç üstte, tamamlananlar en yeni maç üstte.
// Burada yeniden sıralama YAPILMAZ. Sahte takip veya örnek kart GÖSTERİLMEZ.

import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/context/AuthContext";
import { TeamCrest } from "@/components/ui/TeamCrest";
import {
  getFollowedMatchesScreen,
  unfollowMatch,
  type FollowedMatchCardDto,
  type FollowedMatchesScreen as ScreenDto,
} from "@/lib/api/follows";
import { FOLLOW_IDS_KEY, FOLLOWED_MATCHES_KEY } from "@/hooks/useFollow";

export const FOLLOWED_SCREEN_KEY = ["follow", "screen"] as const;

export function FollowedMatchesScreen() {
  const { isLoggedIn, isHydrated } = useAuth();

  const { data, isLoading, isError } = useQuery<ScreenDto>({
    queryKey: FOLLOWED_SCREEN_KEY,
    queryFn: getFollowedMatchesScreen,
    enabled: isLoggedIn,
    staleTime: 30_000,
  });

  if (!isHydrated) return <ScreenSkeleton />;

  // GİRİŞ GEREKİYORSA AÇIKÇA SÖYLENİR. Sahte bir kullanıcı kimliği veya yalnız
  // tarayıcıda yaşayan bir liste üretilmez — takip kaydı kişiye aittir.
  if (!isLoggedIn) {
    return (
      <EmptyLike
        title="Takip ettiğin maçları görmek için giriş yapmalısın."
        subtitle="Takip kaydın hesabına bağlıdır; giriş yaptığında tüm cihazlarında aynı görünür."
      />
    );
  }

  if (isLoading) return <ScreenSkeleton />;

  if (isError) {
    return (
      <EmptyLike
        title="Takip listesi şu an yüklenemedi."
        subtitle="Bağlantı kurulduğunda liste burada görünecek."
      />
    );
  }

  const upcoming = data?.upcoming ?? [];
  const finished = data?.finished ?? [];

  if (upcoming.length === 0 && finished.length === 0) return <EmptyState />;

  return (
    <div className="flex flex-col gap-4 px-4 pb-[calc(var(--bottom-nav-height)+24px)] pt-2">
      {upcoming.length > 0 && (
        <Section title="YAKLAŞAN" matches={upcoming} />
      )}
      {finished.length > 0 && (
        <Section title="TAMAMLANAN" matches={finished} />
      )}
    </div>
  );
}

function Section({ title, matches }: { title: string; matches: FollowedMatchCardDto[] }) {
  return (
    <section className="flex flex-col gap-2">
      <h2 className="px-1 text-[11px] font-bold uppercase tracking-[0.16em] text-text-muted">
        {title}
      </h2>
      {matches.map((m) => (
        <FollowedCard key={m.matchId} match={m} />
      ))}
    </section>
  );
}

function FollowedCard({ match }: { match: FollowedMatchCardDto }) {
  const router = useRouter();
  const queryClient = useQueryClient();

  // TAKİBİ BIRAK — iyimser kaldırma, hatada GERİ GELİR.
  //
  // Kart anında listeden kalkar (kullanıcı beklemez); istek başarısız olursa
  // önceki liste geri yüklenir ve kısa bir hata gösterilir. Sessizce kaybolan
  // bir kart, kullanıcıya yapılmamış bir işi yapılmış gibi gösterirdi.
  const unfollow = useMutation({
    mutationFn: () => unfollowMatch(match.matchId),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: FOLLOWED_SCREEN_KEY });
      const prev = queryClient.getQueryData<ScreenDto>(FOLLOWED_SCREEN_KEY);
      if (prev) {
        queryClient.setQueryData<ScreenDto>(FOLLOWED_SCREEN_KEY, {
          upcoming: prev.upcoming.filter((m) => m.matchId !== match.matchId),
          finished: prev.finished.filter((m) => m.matchId !== match.matchId),
          totalCount: Math.max(0, prev.totalCount - 1),
        });
      }
      return { prev };
    },
    onError: (_err, _vars, context) => {
      if (context?.prev) queryClient.setQueryData(FOLLOWED_SCREEN_KEY, context.prev);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: FOLLOWED_SCREEN_KEY });
      queryClient.invalidateQueries({ queryKey: FOLLOW_IDS_KEY });
      queryClient.invalidateQueries({ queryKey: FOLLOWED_MATCHES_KEY });
    },
  });

  const isFinished = match.status?.toLowerCase() === "finished";

  return (
    <div className="rounded-[14px] bg-bg-glass p-3">
      <button
        type="button"
        onClick={() => router.push(`/match/${match.matchId}`)}
        className="flex w-full flex-col gap-2 text-left"
      >
        <div className="flex items-center justify-between gap-2">
          <span className="truncate text-[10.5px] font-semibold uppercase tracking-wide text-text-muted">
            {match.league}
          </span>
          <span className="shrink-0 text-[10.5px] tabular-nums text-text-muted">
            {formatKickoff(match.matchDateUtc)}
          </span>
        </div>

        <TeamRow
          name={match.homeTeam}
          logo={match.homeTeamLogoUrl}
          score={isFinished ? match.homeScore : null}
        />
        <TeamRow
          name={match.awayTeam}
          logo={match.awayTeamLogoUrl}
          score={isFinished ? match.awayScore : null}
        />

        <div className="pt-0.5">
          {isFinished ? (
            <span className="text-[10.5px] font-semibold uppercase tracking-wide text-text-muted">
              Tamamlandı
            </span>
          ) : (
            <Countdown kickoffUtc={match.matchDateUtc} />
          )}
        </div>
      </button>

      <div className="mt-2 flex items-center justify-between gap-2 border-t border-white/[0.06] pt-2">
        <button
          type="button"
          onClick={() => unfollow.mutate()}
          disabled={unfollow.isPending}
          className="text-[11.5px] font-semibold text-text-muted transition-colors hover:text-white disabled:opacity-50"
        >
          {unfollow.isPending ? "Kaldırılıyor…" : "Takibi bırak"}
        </button>
        {unfollow.isError && (
          <span className="text-[10.5px] text-formax-amber/90">
            Takip kaldırılamadı, tekrar dene.
          </span>
        )}
      </div>
    </div>
  );
}

function TeamRow({
  name,
  logo,
  score,
}: {
  name: string;
  logo?: string | null;
  score?: number | null;
}) {
  return (
    <div className="flex items-center gap-2.5">
      <TeamCrest name={name} logoUrl={logo ?? undefined} size={20} />
      <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium text-text-primary">
        {name}
      </span>
      {/* Skor YALNIZ bitmiş maçta gelir; başlamamış maçta satır hiç çıkmaz. */}
      {score != null && (
        <span className="shrink-0 text-[13.5px] font-bold tabular-nums text-text-primary">
          {score}
        </span>
      )}
    </div>
  );
}

/**
 * GERİ SAYIM — kickoff'a kalan süre. Kaynak tek bir gerçektir (matchDateUtc);
 * buradan CANLI durumu ÜRETİLMEZ, yalnız kalan süre yazılır.
 */
function Countdown({ kickoffUtc }: { kickoffUtc: string }) {
  const remainingMs = new Date(kickoffUtc).getTime() - Date.now();
  if (Number.isNaN(remainingMs)) return null;

  if (remainingMs <= 0) {
    return (
      <span className="text-[10.5px] font-semibold uppercase tracking-wide text-text-muted">
        Başladı
      </span>
    );
  }

  const totalMinutes = Math.floor(remainingMs / 60_000);
  const days = Math.floor(totalMinutes / (60 * 24));
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60);
  const minutes = totalMinutes % 60;

  const label =
    days > 0 ? `${days} gün ${hours} sa` : hours > 0 ? `${hours} sa ${minutes} dk` : `${minutes} dk`;

  return (
    <span className="text-[10.5px] font-semibold uppercase tracking-wide text-goalai-accent">
      {label} kaldı
    </span>
  );
}

function EmptyState() {
  const router = useRouter();
  return (
    <div className="flex flex-col items-center gap-3 px-8 pt-24 text-center">
      <p className="text-[15px] font-semibold text-text-primary">
        Henüz takip ettiğin bir maç yok.
      </p>
      <p className="text-[12.5px] leading-relaxed text-text-muted">
        Maçlar ekranından takip etmek istediğin karşılaşmaları seçebilirsin.
      </p>
      <button
        type="button"
        onClick={() => router.push("/maclar")}
        className="mt-1 rounded-full bg-goalai-accent px-5 py-2 text-[13px] font-bold text-[#0a0e16]"
      >
        Maçlara Git
      </button>
    </div>
  );
}

function EmptyLike({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="flex flex-col items-center gap-2 px-8 pt-24 text-center">
      <p className="text-[14px] font-semibold text-text-primary">{title}</p>
      <p className="text-[12.5px] leading-relaxed text-text-muted">{subtitle}</p>
    </div>
  );
}

function ScreenSkeleton() {
  return (
    <div className="flex flex-col gap-2 px-4 pt-4">
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-[104px] animate-pulse rounded-[14px] bg-white/[0.04]" />
      ))}
    </div>
  );
}

/** UTC damgasını Türkçe kısa tarih+saate çevirir. Bozuk değer boş döner. */
function formatKickoff(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("tr-TR", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

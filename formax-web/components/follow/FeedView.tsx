"use client";

import { useCallback, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { MenuIcon, GoalIcon, ShieldIcon, TrophyIcon, NewspaperIcon } from "@/components/discover/icons";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { useNotifications, useMarkAsRead } from "@/hooks/useNotifications";
import { useMyTeams } from "@/hooks/useTeams";
import { useFollowedMatchDetails } from "@/hooks/useFollowedMatchDetails";
import { FollowSegments, type SegmentTab } from "./FollowSegments";
import { FollowedMatchesSection } from "./FollowedMatchesSection";
import { FeedCard } from "./FeedCard";
import { SummaryCards } from "./SummaryCards";
import type { ActivityItem, FeedFilter, ManageFilter } from "./types";

const FEED_TABS: SegmentTab<FeedFilter>[] = [
  { key: "all", label: "Tümü", Icon: MenuIcon },
  { key: "matches", label: "Maçlar", Icon: GoalIcon },
  { key: "teams", label: "Takımlar", Icon: ShieldIcon },
  { key: "leagues", label: "Ligler", Icon: TrophyIcon },
  { key: "news", label: "Haberler", Icon: NewspaperIcon },
];

/**
 * FeedView — Activity Feed. Gerçek kaynak: /api/notifications/me.
 * NOT: notification DTO'sunda `category/event_type` YOK → segment filtreleme
 * yalnızca "Tümü" için gerçek veridir; diğer sekmeler boş görünür (uydurulmaz).
 */
export function FeedView({ onOpenManagement }: { onOpenManagement: (f: ManageFilter) => void }) {
  const router = useRouter();
  const [filter, setFilter] = useState<FeedFilter>("all");

  const { data: notifications, isLoading, isError, refetch } = useNotifications();
  const markAsRead = useMarkAsRead();
  const { data: teams } = useMyTeams();
  // Takip edilen maç sayısı, Maçlar ekranındaki ikonla AYNI kaynaktan gelir
  // (girişliyse backend, anonimken kalıcı yerel depo) — iki ayrı sayaç olmaz.
  const { matches } = useFollowedMatchDetails();

  const activities = useMemo<ActivityItem[]>(
    () =>
      (notifications ?? []).map((n) => ({
        id: n.id,
        matchId: n.matchId,
        title: n.title,
        description: n.message,
        createdAt: n.createdAt,
        isRead: n.isRead,
        targetUrl: `/match/${n.matchId}`,
      })),
    [notifications]
  );

  // Kategori alanı olmadığından yalnız "Tümü" gerçek veri döndürür.
  const visible = filter === "all" ? activities : [];
  const unreadCount = activities.filter((a) => !a.isRead).length;

  const handleOpen = useCallback(
    (item: ActivityItem) => {
      if (!item.isRead) markAsRead.mutate(item.id);
      router.push(item.targetUrl);
    },
    [markAsRead, router]
  );

  const handleMarkAll = useCallback(() => {
    activities.filter((a) => !a.isRead).forEach((a) => markAsRead.mutate(a.id));
  }, [activities, markAsRead]);

  return (
    <div className="flex flex-col gap-5 pb-4">
      <FollowSegments id="feed" tabs={FEED_TABS} active={filter} onChange={setFilter} />

      {/* TAKİP EDİLEN MAÇLAR — Maçlar ekranındaki takip ikonuyla aynı durumu okur. */}
      <FollowedMatchesSection />

      {/* YENİ GELİŞMELER */}
      <section className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-bold uppercase tracking-wide text-text-secondary">
            Yeni Gelişmeler
          </span>
          {unreadCount > 0 && (
            <button
              type="button"
              onClick={handleMarkAll}
              className="text-[12px] font-semibold text-goalai-accent active:opacity-70"
            >
              Tümünü okundu işaretle
            </button>
          )}
        </div>

        {isLoading ? (
          <SkeletonList />
        ) : isError ? (
          <ErrorState message="Gelişmeler yüklenirken bir hata oluştu." onRetry={() => refetch()} />
        ) : visible.length === 0 ? (
          <EmptyState
            title="Henüz gelişme yok"
            message="Takip ettiklerinle ilgili yeni gelişmeler burada görünecek."
          />
        ) : (
          <div className="flex flex-col gap-3">
            {visible.map((item, i) => (
              <motion.div
                key={item.id}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.24, delay: 0.05 * i, ease: "easeOut" }}
              >
                <FeedCard item={item} onOpen={handleOpen} />
              </motion.div>
            ))}
          </div>
        )}
      </section>

      {/* TAKİP ETTİKLERİM */}
      <section className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <span className="text-[13px] font-bold uppercase tracking-wide text-text-secondary">
            Takip Ettiklerim
          </span>
          <button
            type="button"
            onClick={() => onOpenManagement("all")}
            className="text-[12px] font-semibold text-goalai-accent active:opacity-70"
          >
            Tümünü Gör
          </button>
        </div>
        <SummaryCards
          teamsCount={teams?.length ?? 0}
          leaguesCount={0}
          matchesCount={matches.length}
          onOpen={onOpenManagement}
        />
      </section>
    </div>
  );
}

function SkeletonList() {
  return (
    <div className="flex flex-col gap-3">
      {[0, 1, 2, 3].map((i) => (
        <div key={i} className="h-16 w-full animate-pulse rounded-2xl bg-white/[0.04]" />
      ))}
    </div>
  );
}

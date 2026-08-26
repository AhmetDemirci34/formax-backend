"use client";

import { useCallback, useMemo, useState } from "react";
import { motion } from "framer-motion";
import { useQueryClient } from "@tanstack/react-query";
import { MenuIcon, ShieldIcon, TrophyIcon, GoalIcon } from "@/components/discover/icons";
import { EmptyState } from "@/components/ui/EmptyState";
import { useMyTeams, useSetMyTeams } from "@/hooks/useTeams";
import { useFollowedMatches, FOLLOWED_MATCHES_KEY, FOLLOW_IDS_KEY } from "@/hooks/useFollow";
import { unfollowMatch } from "@/lib/api/follows";
import type { TeamDto, FollowedMatchDto } from "@/types/api";
import { FollowSegments, type SegmentTab } from "./FollowSegments";
import { SubHeader } from "./SubHeader";
import { ManagementItem } from "./ManagementItem";
import type { ManageEntity, ManageFilter } from "./types";

const MGMT_TABS: SegmentTab<ManageFilter>[] = [
  { key: "all", label: "Tümü", Icon: MenuIcon },
  { key: "teams", label: "Takımlar", Icon: ShieldIcon },
  { key: "leagues", label: "Ligler", Icon: TrophyIcon },
  { key: "matches", label: "Maçlar", Icon: GoalIcon },
];

/**
 * ManagementView — "Takip Ettiklerim" tam ekran overlay (Bottom Nav'ı örter).
 * Gerçek kaynaklar: takımlar `/api/users/me/teams`, maçlar `/api/follows/me`.
 * NOT: Lig takibi backend'de YOK → "Ligler" sekmesi boş (uydurulmaz).
 * Takip kaldırma optimistic (anında UI güncellemesi).
 */
export function ManagementView({
  initialFilter,
  onBack,
}: {
  initialFilter: ManageFilter;
  onBack: () => void;
}) {
  const [filter, setFilter] = useState<ManageFilter>(initialFilter);
  const queryClient = useQueryClient();

  const { data: teams } = useMyTeams();
  const { data: matches } = useFollowedMatches();
  const setTeams = useSetMyTeams();

  const teamEntities = useMemo<ManageEntity[]>(
    () =>
      (teams ?? []).map((t) => ({
        key: `team:${t.id}`,
        type: "team",
        id: t.id,
        name: t.name,
        subtitle: "Takım",
        logoUrl: t.logoUrl,
      })),
    [teams]
  );

  const matchEntities = useMemo<ManageEntity[]>(
    () =>
      (matches ?? []).map((m) => ({
        key: `match:${m.matchId}`,
        type: "match",
        id: m.matchId,
        name: `${m.homeTeam} – ${m.awayTeam}`,
        subtitle: "Maç",
        logoUrl: null,
      })),
    [matches]
  );

  const list = useMemo<ManageEntity[]>(() => {
    if (filter === "teams") return teamEntities;
    if (filter === "matches") return matchEntities;
    if (filter === "leagues") return [];
    return [...teamEntities, ...matchEntities];
  }, [filter, teamEntities, matchEntities]);

  const handleToggle = useCallback(
    (entity: ManageEntity) => {
      if (entity.type === "team") {
        const remaining = (teams ?? []).filter((t) => t.id !== entity.id).map((t) => t.id);
        queryClient.setQueryData<TeamDto[]>(["teams", "mine"], (old) =>
          (old ?? []).filter((t) => t.id !== entity.id)
        );
        setTeams.mutate(remaining);
      } else if (entity.type === "match") {
        queryClient.setQueryData<FollowedMatchDto[]>(FOLLOWED_MATCHES_KEY, (old) =>
          (old ?? []).filter((m) => m.matchId !== entity.id)
        );
        unfollowMatch(entity.id)
          .then(() => queryClient.invalidateQueries({ queryKey: FOLLOW_IDS_KEY }))
          .catch(() => queryClient.invalidateQueries({ queryKey: FOLLOWED_MATCHES_KEY }));
      }
    },
    [teams, queryClient, setTeams]
  );

  return (
    <motion.div
      className="fixed inset-0 z-[60] bg-bg-base"
      initial={{ x: "100%" }}
      animate={{ x: 0 }}
      exit={{ x: "100%" }}
      transition={{ duration: 0.3, ease: "easeInOut" }}
      role="dialog"
      aria-label="Takip Ettiklerim"
    >
      <div className="mx-auto flex h-full w-full max-w-[var(--app-max-width)] flex-col">
        <SubHeader onBack={onBack} />
        <div className="px-4 pt-1">
          <FollowSegments id="mgmt" tabs={MGMT_TABS} active={filter} onChange={setFilter} />
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
          {list.length === 0 ? (
            <EmptyState
              title={filter === "leagues" ? "Lig takibi yok" : "Takip ettiğin içerik yok"}
              message={
                filter === "leagues"
                  ? "Şu an takip ettiğin bir lig bulunmuyor."
                  : "Keşfet ekranından takım ve maç takip edebilirsin."
              }
            />
          ) : (
            <div className="flex flex-col gap-3">
              {list.map((entity, i) => (
                <motion.div
                  key={entity.key}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.22, delay: 0.04 * i, ease: "easeOut" }}
                >
                  <ManagementItem entity={entity} isFollowing onToggle={handleToggle} />
                </motion.div>
              ))}
            </div>
          )}
        </div>
      </div>
    </motion.div>
  );
}

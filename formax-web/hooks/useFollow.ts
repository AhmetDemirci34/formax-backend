"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  followMatch,
  unfollowMatch,
  getFollowedMatchIds,
  getFollowedMatches,
} from "@/lib/api/follows";
import { trackInterest } from "@/lib/api/interests";
import { useAuth } from "@/context/AuthContext";
import { AUTH_GATE_ENABLED } from "@/lib/auth/authGate";

export const FOLLOW_IDS_KEY  = ["follow", "ids"] as const;
export const FOLLOWED_MATCHES_KEY = ["follow", "matches"] as const;

// ── Lightweight: just the ID list (used for follow-state badges) ──────────────

export function useFollowedIds() {
  const { isLoggedIn } = useAuth();
  return useQuery<number[]>({
    queryKey: FOLLOW_IDS_KEY,
    queryFn: getFollowedMatchIds,
    enabled: isLoggedIn,
    staleTime: 60_000,
    placeholderData: [],
  });
}

// ── Full list: used on /following page ───────────────────────────────────────

export function useFollowedMatches() {
  const { isLoggedIn } = useAuth();
  return useQuery({
    queryKey: FOLLOWED_MATCHES_KEY,
    queryFn: getFollowedMatches,
    enabled: isLoggedIn,
    staleTime: 60_000,
  });
}

// ── Per-match toggle with optimistic update ───────────────────────────────────

export function useFollow(matchId: number) {
  const queryClient = useQueryClient();
  const { isLoggedIn } = useAuth();
  const { data: followedIds = [] } = useFollowedIds();

  const isFollowing = followedIds.includes(matchId);

  const follow = useMutation({
    mutationFn: () => followMatch(matchId),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: FOLLOW_IDS_KEY });
      const prev = queryClient.getQueryData<number[]>(FOLLOW_IDS_KEY) ?? [];
      queryClient.setQueryData<number[]>(FOLLOW_IDS_KEY, [...prev, matchId]);
      return { prev };
    },
    onError: (_err, _v, ctx) => {
      if (ctx) queryClient.setQueryData(FOLLOW_IDS_KEY, ctx.prev);
    },
    // R.14.8 — follow başarılıysa güçlü ilgi (open). Rollback'te tetiklenmez.
    onSuccess: () => {
      trackInterest("open", matchId);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: FOLLOW_IDS_KEY });
      queryClient.invalidateQueries({ queryKey: FOLLOWED_MATCHES_KEY });
    },
  });

  const unfollow = useMutation({
    mutationFn: () => unfollowMatch(matchId),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: FOLLOW_IDS_KEY });
      const prev = queryClient.getQueryData<number[]>(FOLLOW_IDS_KEY) ?? [];
      queryClient.setQueryData<number[]>(
        FOLLOW_IDS_KEY,
        prev.filter((id) => id !== matchId)
      );
      return { prev };
    },
    onError: (_err, _v, ctx) => {
      if (ctx) queryClient.setQueryData(FOLLOW_IDS_KEY, ctx.prev);
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: FOLLOW_IDS_KEY });
      queryClient.invalidateQueries({ queryKey: FOLLOWED_MATCHES_KEY });
    },
  });

  const toggle = () => {
    if (!isLoggedIn) {
      // Auth kapısı pasifken takip sessizce yok sayılır (bkz. lib/auth/authGate.ts);
      // UI geliştirirken tek dokunuş kullanıcıyı login'e atmasın.
      if (AUTH_GATE_ENABLED) window.location.href = "/auth/login";
      return;
    }
    if (isFollowing) {
      unfollow.mutate();
    } else {
      follow.mutate();
    }
  };

  return {
    isFollowing,
    toggle,
    isPending: follow.isPending || unfollow.isPending,
    isLoggedIn,
  };
}

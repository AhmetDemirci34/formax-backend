"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getNotifications,
  getUnreadCount,
  markAsRead,
  type UserNotificationDto,
} from "@/lib/api/notifications";
import { useAuth } from "@/context/AuthContext";

export const NOTIFICATIONS_KEY   = ["notifications", "list"] as const;
export const UNREAD_COUNT_KEY    = ["notifications", "unread-count"] as const;

// ── Full notification list ────────────────────────────────────────────────────

export function useNotifications() {
  const { isLoggedIn } = useAuth();
  return useQuery<UserNotificationDto[]>({
    queryKey: NOTIFICATIONS_KEY,
    queryFn: getNotifications,
    enabled: isLoggedIn,
    staleTime: 30_000,
  });
}

// ── Unread count (used in BottomNav badge) ────────────────────────────────────

export function useUnreadCount() {
  const { isLoggedIn } = useAuth();
  return useQuery<number>({
    queryKey: UNREAD_COUNT_KEY,
    queryFn: getUnreadCount,
    enabled: isLoggedIn,
    staleTime: 30_000,
    placeholderData: 0,
    refetchInterval: 60_000,   // poll every 60s — no websocket in V1
  });
}

// ── Mark a single notification as read ────────────────────────────────────────

export function useMarkAsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => markAsRead(id),
    onMutate: async (id: number) => {
      // Optimistically update list
      await queryClient.cancelQueries({ queryKey: NOTIFICATIONS_KEY });
      const prev = queryClient.getQueryData<UserNotificationDto[]>(NOTIFICATIONS_KEY) ?? [];
      queryClient.setQueryData<UserNotificationDto[]>(
        NOTIFICATIONS_KEY,
        prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
      );
      // Optimistically decrement count
      const prevCount = queryClient.getQueryData<number>(UNREAD_COUNT_KEY) ?? 0;
      queryClient.setQueryData<number>(UNREAD_COUNT_KEY, Math.max(0, prevCount - 1));
      return { prev, prevCount };
    },
    onError: (_err, _id, ctx) => {
      if (ctx) {
        queryClient.setQueryData(NOTIFICATIONS_KEY, ctx.prev);
        queryClient.setQueryData(UNREAD_COUNT_KEY, ctx.prevCount);
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_KEY });
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_KEY });
    },
  });
}

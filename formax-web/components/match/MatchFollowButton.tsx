"use client";

import { useFollow } from "@/hooks/useFollow";

interface Props {
  matchId: number;
}

export function MatchFollowButton({ matchId }: Props) {
  const { isFollowing, toggle, isPending } = useFollow(matchId);

  return (
    <button
      onClick={toggle}
      disabled={isPending}
      aria-label={isFollowing ? "Takibi bırak" : "Takip et"}
      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors disabled:opacity-50 ${
        isFollowing
          ? "bg-formax-green/10 text-formax-green border border-formax-green/25 hover:bg-formax-green/20"
          : "bg-bg-elevated text-text-muted border border-border hover:text-formax-green hover:border-formax-green/30"
      }`}
    >
      {/* Simple circle indicator — no external icon library needed */}
      <span
        className={`w-1.5 h-1.5 rounded-full ${
          isFollowing ? "bg-formax-green" : "bg-text-muted/40"
        }`}
      />
      {isPending ? "..." : isFollowing ? "Takipte" : "Takip Et"}
    </button>
  );
}

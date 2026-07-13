"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/context/AuthContext";
import { useSubscription } from "@/hooks/useSubscription";
import { useMyTeams } from "@/hooks/useTeams";
import { useFollowedIds } from "@/hooks/useFollow";
import { useUnreadCount } from "@/hooks/useNotifications";
import type { TeamDto } from "@/types/api";

export default function ProfilePage() {
  const router = useRouter();
  const { isLoggedIn, isHydrated, logout } = useAuth();
  const { data: subscription } = useSubscription();
  const { data: teams = [], isLoading: teamsLoading } = useMyTeams();
  const { data: followedIds = [] } = useFollowedIds();
  const { data: unreadCount = 0 } = useUnreadCount();

  useEffect(() => {
    if (isHydrated && !isLoggedIn) {
      router.replace("/auth/login");
    }
  }, [isHydrated, isLoggedIn, router]);

  if (!isHydrated || !isLoggedIn) return null;

  const isPremium = subscription?.isPremium ?? false;

  function handleLogout() {
    logout();
    router.push("/");
  }

  return (
    <div className="min-h-screen bg-bg-base pb-20">
      {/* Header */}
      <header className="sticky top-0 z-10 bg-bg-base/90 backdrop-blur-sm border-b border-border-dim">
        <div className="max-w-md mx-auto px-4 py-3 flex items-center gap-2">
          <span className="text-accent font-black text-lg tracking-tight">FORMAX</span>
          <span className="text-text-muted text-xs">profil</span>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-4 space-y-3">
        {/* Account card */}
        <div className="bg-bg-card rounded-xl border border-border p-4 flex items-center gap-3">
          <div className="w-12 h-12 rounded-full bg-bg-elevated border border-border flex items-center justify-center shrink-0">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" strokeLinejoin="round" className="text-text-muted">
              <circle cx="12" cy="8" r="4" />
              <path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1" />
            </svg>
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-bold text-text-primary leading-tight">Hesap</p>
            <span
              className={`inline-block mt-1 text-[10px] font-semibold px-2 py-0.5 rounded border ${
                isPremium
                  ? "text-formax-amber bg-formax-amber/10 border-formax-amber/20"
                  : "text-text-muted bg-bg-elevated border-border"
              }`}
            >
              {isPremium ? "Premium" : "Free"}
            </span>
          </div>
        </div>

        {/* My teams */}
        <div className="bg-bg-card rounded-xl border border-border p-4">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
              Takımlarım
            </h3>
            <Link
              href="/onboarding/teams"
              className="text-xs font-semibold text-accent hover:underline"
            >
              Düzenle
            </Link>
          </div>

          {teamsLoading ? (
            <div className="flex gap-2">
              {[1, 2, 3].map((i) => (
                <div key={i} className="h-7 w-16 rounded-full bg-bg-elevated animate-pulse" />
              ))}
            </div>
          ) : teams.length === 0 ? (
            <Link
              href="/onboarding/teams"
              className="text-sm text-text-secondary hover:text-text-primary"
            >
              Henüz takım seçmedin. Takım seç →
            </Link>
          ) : (
            <div className="flex flex-wrap gap-2">
              {teams.map((team: TeamDto) => (
                <span
                  key={team.id}
                  className="text-xs font-medium text-text-primary bg-bg-elevated border border-border px-3 py-1 rounded-full"
                >
                  {team.name}
                </span>
              ))}
            </div>
          )}
        </div>

        {/* Quick links */}
        <div className="bg-bg-card rounded-xl border border-border divide-y divide-border-dim overflow-hidden">
          <Link
            href="/following"
            className="flex items-center gap-3 px-4 py-3 hover:bg-bg-elevated/50 transition-colors"
          >
            <span className="text-text-secondary">⭐</span>
            <span className="flex-1 text-sm text-text-primary">Takipler</span>
            <span className="text-sm font-semibold text-text-secondary tabular-nums">
              {followedIds.length}
            </span>
            <span className="text-text-muted">→</span>
          </Link>
          <Link
            href="/notifications"
            className="flex items-center gap-3 px-4 py-3 hover:bg-bg-elevated/50 transition-colors"
          >
            <span className="text-text-secondary">🔔</span>
            <span className="flex-1 text-sm text-text-primary">Bildirimler</span>
            {unreadCount > 0 && (
              <span className="min-w-[18px] h-[18px] bg-formax-red text-white text-[10px] font-bold rounded-full flex items-center justify-center px-1">
                {unreadCount > 99 ? "99+" : unreadCount}
              </span>
            )}
            <span className="text-text-muted">→</span>
          </Link>
        </div>

        {/* Logout */}
        <button
          onClick={handleLogout}
          className="w-full py-3 rounded-xl text-sm font-semibold text-formax-red bg-formax-red/5 border border-formax-red/20 hover:bg-formax-red/10 transition-colors"
        >
          Çıkış Yap
        </button>
      </main>
    </div>
  );
}

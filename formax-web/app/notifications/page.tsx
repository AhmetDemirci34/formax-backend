"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/context/AuthContext";
import { AUTH_GATE_ENABLED } from "@/lib/auth/authGate";
import { useNotifications, useMarkAsRead } from "@/hooks/useNotifications";
import { notificationHref, notificationTypeLabel, type UserNotificationDto } from "@/lib/api/notifications";

// ─────────────────────────────────────────────────────────────────────────────
// Relative time helper
// ─────────────────────────────────────────────────────────────────────────────

function relativeTime(isoDate: string): string {
  const diff = Date.now() - new Date(isoDate).getTime();
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) return "Az önce";
  if (minutes < 60) return `${minutes}dk önce`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}s önce`;
  const days = Math.floor(hours / 24);
  return `${days}g önce`;
}

// ─────────────────────────────────────────────────────────────────────────────
// NotificationRow
// ─────────────────────────────────────────────────────────────────────────────

function NotificationRow({
  notification,
  onRead,
}: {
  notification: UserNotificationDto;
  onRead: (id: number) => void;
}) {
  const { id, title, message, isRead, createdAt } = notification;
  const typeLabel = notificationTypeLabel(notification.type);

  return (
    <Link
      href={notificationHref(notification)}
      data-notification-type={notification.type ?? undefined}
      onClick={() => { if (!isRead) onRead(id); }}
      className={`flex items-start gap-3 px-4 py-3.5 border rounded-xl transition-colors ${
        isRead
          ? "bg-bg-card border-border"
          : "bg-bg-elevated border-accent/30"
      }`}
    >
      {/* Unread dot */}
      <div className="mt-1.5 shrink-0">
        {isRead ? (
          <div className="w-2 h-2 rounded-full bg-transparent" />
        ) : (
          <div className="w-2 h-2 rounded-full bg-accent" />
        )}
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        {typeLabel && (
          <p className="text-[10px] font-bold uppercase tracking-wide text-accent">{typeLabel}</p>
        )}
        <p className={`text-sm font-semibold leading-tight ${isRead ? "text-text-secondary" : "text-text-primary"}`}>
          {title}
        </p>
        <p className="text-xs text-text-muted mt-0.5 leading-snug">{message}</p>
      </div>

      {/* Time */}
      <span className="text-[10px] text-text-muted shrink-0 mt-0.5">
        {relativeTime(createdAt)}
      </span>
    </Link>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Page
// ─────────────────────────────────────────────────────────────────────────────

export default function NotificationsPage() {
  const router = useRouter();
  const { isLoggedIn, isHydrated } = useAuth();
  const { data: notifications, isLoading } = useNotifications();
  const { mutate: markRead } = useMarkAsRead();

  useEffect(() => {
    if (!AUTH_GATE_ENABLED) return;
    if (isHydrated && !isLoggedIn) {
      router.replace("/auth/login");
    }
  }, [isHydrated, isLoggedIn, router]);

  const unreadCount = (notifications ?? []).filter((n) => !n.isRead).length;

  // Auth kapısı pasifken (UI bitene kadar) giriş istenmez — bkz. lib/auth/authGate.ts
  if (AUTH_GATE_ENABLED && (!isHydrated || !isLoggedIn)) return null;

  return (
    <div className="min-h-screen bg-bg-base pb-20">
      <header className="sticky top-0 z-10 bg-bg-base/90 backdrop-blur-sm border-b border-border-dim">
        <div className="max-w-md mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-accent font-black text-lg tracking-tight">FORMAX</span>
            <span className="text-text-muted text-xs">bildirimler</span>
          </div>
          {unreadCount > 0 && (
            <span className="text-xs text-text-muted">
              {unreadCount} okunmamış
            </span>
          )}
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-4 space-y-2">
        {/* Loading */}
        {isLoading && (
          <div className="space-y-2">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-16 bg-bg-card border border-border rounded-xl animate-pulse" />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!isLoading && (notifications ?? []).length === 0 && (
          <div className="text-center py-16 space-y-3">
            <p className="text-4xl">🔔</p>
            <p className="text-sm text-text-secondary font-medium">Henüz bildirim yok</p>
            <p className="text-xs text-text-muted">
              Takip ettiğin maçlarda resmî kadrolar ve kritik gelişmeler burada görünecek
            </p>
            <Link
              href="/"
              className="inline-block text-accent text-sm font-semibold hover:underline mt-2"
            >
              Maçları Keşfet →
            </Link>
          </div>
        )}

        {/* Notification list */}
        {!isLoading && (notifications ?? []).map((n) => (
          <NotificationRow
            key={n.id}
            notification={n}
            onRead={(id) => markRead(id)}
          />
        ))}
      </main>
    </div>
  );
}

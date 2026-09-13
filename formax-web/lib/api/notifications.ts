import apiClient from "./client";

export interface UserNotificationDto {
  id: number;
  matchId: number;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  /** "MATCH_LINEUP_AVAILABLE" | "MATCH_CRITICAL_UPDATE" | null (eski satır). */
  type?: string | null;
  /** Basınca açılacak rota (backend verir); yoksa maç rotası. */
  route?: string | null;
}

/** Bildirimin güvenli iç rotası: yalnız "/" ile başlayan uygulama içi yol kabul edilir. */
export function notificationHref(n: Pick<UserNotificationDto, "route" | "matchId">): string {
  const r = n.route?.trim();
  if (r && r.startsWith("/") && !r.startsWith("//")) return r;
  return `/match/${n.matchId}`;
}

/** Kullanıcıya gösterilecek tür etiketi (teknik kod gösterilmez). */
export function notificationTypeLabel(type?: string | null): string | null {
  switch (type) {
    case "MATCH_LINEUP_AVAILABLE":
      return "Kadro";
    case "MATCH_CRITICAL_UPDATE":
      return "Kritik gelişme";
    default:
      return null;
  }
}

export interface NotificationPreferenceDto {
  key: string;
  enabled: boolean;
}

export async function getNotificationPreferences(): Promise<NotificationPreferenceDto[]> {
  const res = await apiClient.get<NotificationPreferenceDto[]>("/api/notifications/me/preferences");
  return res.data;
}

export async function putNotificationPreference(key: string, enabled: boolean): Promise<void> {
  await apiClient.put("/api/notifications/me/preferences", { key, enabled });
}

export async function getNotifications(): Promise<UserNotificationDto[]> {
  const res = await apiClient.get<UserNotificationDto[]>("/api/notifications/me");
  return res.data;
}

export async function getUnreadCount(): Promise<number> {
  const res = await apiClient.get<{ count: number }>("/api/notifications/me/unread-count");
  return res.data.count;
}

export async function markAsRead(id: number): Promise<void> {
  await apiClient.post(`/api/notifications/${id}/read`);
}

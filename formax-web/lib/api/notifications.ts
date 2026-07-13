import apiClient from "./client";

export interface UserNotificationDto {
  id: number;
  matchId: number;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
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

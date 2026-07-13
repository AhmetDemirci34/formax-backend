import apiClient from "./client";
import type { NotificationDto } from "@/types/api";

export async function getNotifications(): Promise<NotificationDto[]> {
  const res = await apiClient.get<NotificationDto[]>("/api/notifications/me");
  return res.data;
}

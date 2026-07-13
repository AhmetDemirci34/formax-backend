import apiClient from "./client";

export interface SubscriptionDto {
  isPremium: boolean;
  accessLevel: string; // "Free" | "Premium"
}

export async function getSubscription(): Promise<SubscriptionDto> {
  const res = await apiClient.get<SubscriptionDto>("/api/users/me/subscription");
  return res.data;
}

"use client";

import { useQuery } from "@tanstack/react-query";
import { getSubscription, type SubscriptionDto } from "@/lib/api/user";
import { useAuth } from "@/context/AuthContext";

export const SUBSCRIPTION_KEY = ["user", "subscription"] as const;

export function useSubscription() {
  const { isLoggedIn } = useAuth();
  return useQuery<SubscriptionDto>({
    queryKey: SUBSCRIPTION_KEY,
    queryFn: getSubscription,
    enabled: isLoggedIn,
    staleTime: 5 * 60 * 1000,
  });
}

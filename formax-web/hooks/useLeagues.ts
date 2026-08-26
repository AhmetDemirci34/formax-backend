"use client";

import { useQuery } from "@tanstack/react-query";
import { getMyLeagues, type LeagueDto } from "@/lib/api/leagues";
import { useAuth } from "@/context/AuthContext";

export const MY_LEAGUES_KEY = ["user", "leagues"] as const;

/** Kullanıcının takip ettiği ligler — GET /api/users/me/leagues. */
export function useMyLeagues() {
  const { isLoggedIn } = useAuth();
  return useQuery<LeagueDto[]>({
    queryKey: MY_LEAGUES_KEY,
    queryFn: getMyLeagues,
    enabled: isLoggedIn,
    staleTime: 5 * 60 * 1000,
  });
}

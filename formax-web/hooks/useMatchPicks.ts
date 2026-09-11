"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/context/AuthContext";
import {
  getPicksForMatch,
  togglePick,
  type PickToggleRequest,
  type PickToggleResponse,
  type UserPickDto,
} from "@/lib/api/picks";

export const MATCH_PICKS_KEY = (matchId: number) => ["picks", "match", matchId] as const;
export const MY_PREDICTIONS_KEY = ["picks", "me"] as const;

/**
 * BİR MAÇTAKİ KULLANICI SEÇİMLERİ.
 *
 * KALICILIK BACKEND'DEDİR — localStorage YOK. Sayfa yenilendiğinde durum bu
 * sorgudan geri gelir; yalnız yerel state'te yaşayan bir seçim, kullanıcıya
 * tutulmayan bir kalıcılık sözü verirdi.
 *
 * Girişsizken sorgu HİÇ ÇALIŞMAZ ve seçim yapılamaz: seçim kişiye ait bir
 * kayıttır, sahte bir kimlikle saklanmaz.
 */
export function useMatchPicks(matchId: number) {
  const { isLoggedIn } = useAuth();
  const queryClient = useQueryClient();

  const query = useQuery<UserPickDto[]>({
    queryKey: MATCH_PICKS_KEY(matchId),
    queryFn: () => getPicksForMatch(matchId),
    enabled: isLoggedIn && matchId > 0,
    staleTime: 30_000,
  });

  const toggle = useMutation<PickToggleResponse, Error, PickToggleRequest>({
    mutationFn: togglePick,
    onSuccess: (result) => {
      // SUNUCU CEVABI TEK DOĞRULUK KAYNAĞI: çakışan seçimin kaldırılması dâhil
      // bütün liste backend'den gelir. Arayüz kendi kopyasını türetmez, böylece
      // iki taraf çakışma kuralında ayrışamaz.
      queryClient.setQueryData(MATCH_PICKS_KEY(matchId), result.selections);
      queryClient.invalidateQueries({ queryKey: MY_PREDICTIONS_KEY });
    },
  });

  return {
    selections: query.data ?? [],
    isLoading: query.isLoading,
    isLoggedIn,
    toggle,
    /** Reddedilme sebebi (varsa) — arayüz sessizce "kaydedildi" DEMEZ. */
    rejection: toggle.data && !toggle.data.accepted ? toggle.data.reason ?? null : null,
  };
}

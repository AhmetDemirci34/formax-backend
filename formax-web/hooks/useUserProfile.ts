"use client";

import { useAuth } from "@/context/AuthContext";
import { useSubscription } from "@/hooks/useSubscription";

export interface UserProfileView {
  /** Ad-Soyad — backend alanı YOK (aşağıdaki TODO). */
  fullName: string | null;
  /** @KullanıcıAdı — backend alanı YOK (aşağıdaki TODO). */
  username: string | null;
  /** Profil fotoğrafı URL'i — backend alanı YOK (aşağıdaki TODO). */
  avatarUrl: string | null;
  /** Üyelik durumu — GERÇEK veri: GET /api/users/me/subscription. */
  isPremium: boolean;
  /** Oturumdaki kullanıcı kimliği (AuthContext). */
  userId: number | null;
  isLoading: boolean;
}

/**
 * Profil Merkezi kimlik alanının tek veri kaynağı.
 *
 * Interaction Spec §9 `GET /api/v1/user/profile` (UserProfileDTO) uçunu tanımlar;
 * FORMAX backend'inde böyle bir uç YOK — mevcut kullanıcı uçları yalnızca
 * `/api/users/me/subscription`, `/api/users/me/teams`, `/api/users/me/leagues`.
 *
 * TODO(backend): Ad-Soyad, @kullanıcıadı ve avatar için kullanıcı profili ucu
 * (ör. GET /api/users/me → UserProfileDto) eklenmeli. Uç hazır olduğunda bu
 * hook `lib/api/user.ts` içine eklenecek `getProfile()` çağrısına bağlanmalı;
 * ekranda hiçbir değişiklik gerekmez. Uç gelene kadar bu alanlar `null` döner
 * ve UI açık bir yer tutucu gösterir (uydurma/mock veri KULLANILMAZ).
 */
export function useUserProfile(): UserProfileView {
  const { userId } = useAuth();
  const { data: subscription, isLoading } = useSubscription();

  return {
    fullName: null,
    username: null,
    avatarUrl: null,
    isPremium: subscription?.isPremium ?? false,
    userId,
    isLoading,
  };
}

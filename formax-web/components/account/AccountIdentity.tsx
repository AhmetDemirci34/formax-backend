"use client";

import { useState } from "react";
import Image from "next/image";
import { UserIcon } from "@/components/discover/icons";
import { CameraIcon } from "./icons";
import { AvatarPhotoSheet } from "./AvatarPhotoSheet";
import { haptic } from "@/lib/utils/haptics";

interface AccountIdentityProps {
  fullName: string | null;
  username: string | null;
  avatarUrl: string | null;
  /** Üyelik durumu — avatarın sağ altındaki rozeti yönetir. */
  isPremium: boolean;
}

/** Backend alanı gelene kadar gösterilen açık yer tutucu (mock veri değil). */
const EMPTY = "—";

/**
 * Kırpılmış avatarı sunucuya yükler.
 *
 * TODO(backend): Avatar yükleme ucu YOK. Mevcut kullanıcı uçları yalnızca
 * `/api/users/me/subscription | teams | leagues`; medya tarafında da profil
 * fotoğrafı kabul eden bir uç tanımlı değil. Uç eklendiğinde (ör.
 * `POST /api/users/me/avatar`, multipart/form-data) burada `apiClient` ile
 * çağrılacak ve dönen URL `useUserProfile` üzerinden okunacak — böylece
 * kullanıcı ekrana tekrar geldiğinde yeni fotoğrafı görecek.
 *
 * Uç gelene kadar UI akışı tam çalışır ve bu adım hata yoluna düşer:
 * mevcut avatar korunur, kullanıcıya kısa bir mesaj gösterilir.
 */
async function uploadAvatar(blob: Blob): Promise<void> {
  void blob; // uç eklenene kadar kullanılmıyor
  throw new Error("AVATAR_UPLOAD_NOT_AVAILABLE");
}

/**
 * AccountIdentity — Avatar + üyelik rozeti + kamera butonu + Ad-Soyad +
 * @kullanıcıadı (Stitch görseli).
 *
 * Avatar 70px; rozet ve kamera ikonu sağ alt köşeye bindirilmiştir. Avatar
 * alanına veya kamera ikonuna dokunmak "Profil Fotoğrafını Güncelle" bottom
 * sheet'ini açar. Yükleme sırasında avatar üzerinde göstergesi belirir ve
 * kamera ikonu pasifleşir.
 */
export function AccountIdentity({
  fullName,
  username,
  avatarUrl,
  isPremium,
}: AccountIdentityProps) {
  const [sheetOpen, setSheetOpen] = useState(false);
  const [uploading, setUploading] = useState(false);

  const openSheet = () => {
    if (uploading) return;
    haptic();
    setSheetOpen(true);
  };

  const handleConfirm = async (blob: Blob) => {
    setUploading(true);
    try {
      await uploadAvatar(blob);
      // TODO(backend): başarı yolunda avatar sorgusu invalidate edilecek.
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="flex flex-col items-center px-4 pt-5">
      <div className="relative">
        <button
          type="button"
          onClick={openSheet}
          disabled={uploading}
          aria-label="Profil fotoğrafını güncelle"
          className="block rounded-full transition-transform duration-150 active:scale-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60 disabled:cursor-default"
        >
          <span className="flex h-[70px] w-[70px] items-center justify-center overflow-hidden rounded-full border-2 border-profile-border bg-profile-container">
            {avatarUrl ? (
              <Image
                src={avatarUrl}
                alt=""
                width={70}
                height={70}
                className="h-full w-full object-cover"
              />
            ) : (
              // TODO(backend): avatar alanı yok → fallback ikon. Bkz. useUserProfile.
              <UserIcon size={30} className="text-profile-muted" />
            )}
          </span>

          {/* Yükleme göstergesi — avatarın üzerinde */}
          {uploading ? (
            <span className="absolute inset-0 flex items-center justify-center rounded-full bg-black/55">
              <span className="h-5 w-5 animate-spin rounded-full border-2 border-white/25 border-t-profile-accent" />
            </span>
          ) : null}
        </button>

        {/* Üyelik rozeti — sağ altta, avatarın üzerine bindirilmiş */}
        <span
          className={`pointer-events-none absolute -bottom-0.5 -left-1 rounded-full px-1.5 py-[2px] text-[8px] font-bold uppercase leading-none tracking-[0.3px] ${
            isPremium ? "bg-profile-accent text-black" : "bg-profile-border text-white"
          }`}
        >
          {isPremium ? "Premium" : "Free"}
        </span>

        {/* Kamera butonu — sağ alt köşede küçük dairesel overlay */}
        <button
          type="button"
          onClick={openSheet}
          disabled={uploading}
          aria-label="Profil fotoğrafını değiştir"
          className="absolute -bottom-0.5 -right-1 flex h-6 w-6 items-center justify-center rounded-full border-2 border-profile-surface bg-profile-container text-white transition-transform duration-150 active:scale-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60 disabled:opacity-40"
        >
          <CameraIcon size={12} />
        </button>
      </div>

      {/* TODO(backend): Ad-Soyad alanı yok — GET /api/users/me eklendiğinde bağlanacak. */}
      <p className="mt-3 max-w-full truncate text-[24px] font-bold leading-none text-white">
        {fullName ?? EMPTY}
      </p>
      {/* TODO(backend): @kullanıcıadı alanı yok — aynı uçla gelecek. */}
      <p className="mt-[2px] max-w-full truncate text-[14px] leading-none text-profile-muted">
        {username ? `@${username}` : EMPTY}
      </p>

      <AvatarPhotoSheet
        open={sheetOpen}
        onClose={() => setSheetOpen(false)}
        onConfirm={handleConfirm}
      />
    </div>
  );
}

// FORMAX · Resmî maç özeti — ekran metni kararları (saf fonksiyonlar, test edilir).
//
// KURAL KAYNAĞI KALICI DEFTERDİR, SAAT DEĞİL (11.09.2026 ürün kuralı):
// "bulunamadı" ancak backend'in kalıcı defteri dört gerçek denemenin tamamlandığını
// ve doğrulanmış video bulunamadığını söylediğinde (videoSearch.status === "NotFound")
// gösterilir. Maçtan sonra geçen süre tek başına HİÇBİR ŞEY kanıtlamaz: iş hiç
// çalışmamış ya da plan/rate limit engeline takılmış olabilir.

import type { MatchVideoDto, VideoSearchDto } from "@/types/api";

// Metin "video" der, "maç özeti" demez: arama tam özeti ve gol kliplerini birlikte kapsar.
export const VIDEO_CHECKING_TEXT = "Resmî video kontrol ediliyor.";
export const VIDEO_NOT_FOUND_TEXT = "Uygulama içinde oynatılabilir resmî video bulunamadı.";

/** Ana özet türleri — büyük karta yalnız bunlar çıkar. */
export function isMainHighlight(videoType: string): boolean {
  return videoType === "MatchHighlights" || videoType === "ExtendedHighlights";
}

/** "Goller ve önemli anlar" listesine giren AYRI klip türleri. */
export function isMoment(videoType: string): boolean {
  return (
    videoType === "Goal" ||
    videoType === "Penalty" ||
    videoType === "RedCard" ||
    videoType === "VAR" ||
    videoType === "ImportantMoment"
  );
}

/** GOLLER bölümüne giren AYRI gol klipleri — tam özet ASLA bu listeye girmez. */
export function isGoalClip(videoType: string): boolean {
  return videoType === "Goal" || videoType === "Penalty";
}

/**
 * Video boş durumunun metni — YALNIZ defterden.
 * Defter bilgisi hiç gelmediyse (eski backend / okunamadı) "bulunamadı" DENMEZ.
 */
export function videoEmptyStateText(search?: VideoSearchDto | null): string {
  return search?.status === "NotFound" ? VIDEO_NOT_FOUND_TEXT : VIDEO_CHECKING_TEXT;
}

/**
 * Ekrandaki video yerleşimi. Backend'in sıralaması KORUNUR: ekran listeyi yeniden
 * sıralamaz, yalnız ilk oynatılabilir özeti ana karta alır. Ana özet "önemli anlar"
 * listesine TEKRAR düşmez.
 */
export function arrangeVideos(videos: readonly MatchVideoDto[]) {
  const main = videos.find((v) => v.canPlayInApp && isMainHighlight(v.videoType)) ?? null;
  const moments = videos.filter((v) => isMoment(v.videoType) && v !== main);
  const blocked = main ? [] : videos.filter((v) => !v.canPlayInApp && isMainHighlight(v.videoType));
  // GOLLER: yalnız OYNATILABİLİR gol klipleri (gömme/bölge engelli klip bu başlığa girmez).
  const goals = moments.filter((v) => v.canPlayInApp && isGoalClip(v.videoType));
  const otherMoments = moments.filter((v) => !isGoalClip(v.videoType));
  return { main, moments, goals, otherMoments, blocked };
}

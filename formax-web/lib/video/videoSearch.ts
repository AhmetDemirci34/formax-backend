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
export const VIDEO_NOT_AVAILABLE_YET_TEXT =
  "Bu maçın resmî özeti henüz bulunamadı. FORMAX doğrulanmış kaynakları aramaya devam ediyor.";
export const VIDEO_SOURCE_BLOCKED_TEXT =
  "Bu maçın bulunan resmî videosu uygulama içinde oynatılamıyor. FORMAX alternatif doğrulanmış kaynakları aramaya devam ediyor.";

/** Maç sonu analiz durumları — metin backend DTO'sundaki duruma göre; ekran cümle üretmez. */
export const ANALYSIS_INSUFFICIENT_TEXT =
  "Bu maç için ayrıntılı analiz oluşturacak yeterli doğrulanmış veri bulunamadı.";
export const ANALYSIS_PENDING_TEXT = "Maç sonu analizi arka planda hazırlanıyor.";
export const VIDEO_FAILED_TEXT = "Video kontrolü teknik bir hata nedeniyle tamamlanamadı; yeniden denenecek.";

/**
 * Backend kalıcı keşif kuyruğunun durumları (14.09.2026):
 * Searching | FullHighlightsAvailable | GoalClipsAvailable | NotAvailableYet | SourceBlocked | Failed.
 * Eski "Checking"/"NotFound" değerleri geriye uyum için aynı anlama eşlenir.
 */
export function videoEmptyStateText(search?: VideoSearchDto | null): string {
  switch (search?.status) {
    case "NotAvailableYet":
      return VIDEO_NOT_AVAILABLE_YET_TEXT;
    case "NotFound":
      return VIDEO_NOT_FOUND_TEXT;
    case "SourceBlocked":
      return VIDEO_SOURCE_BLOCKED_TEXT;
    case "Failed":
      return VIDEO_FAILED_TEXT;
    default:
      return VIDEO_CHECKING_TEXT;
  }
}

/**
 * Ekrandaki video yerleşimi. Backend'in sıralaması KORUNUR: ekran listeyi yeniden
 * sıralamaz, yalnız ilk oynatılabilir özeti ana karta alır. Ana özet "önemli anlar"
 * listesine TEKRAR düşmez.
 */
/** Oynatıcının "gömme/bölge engeli" kodları (YouTube IFrame API: 101, 150, 152). */
export function isEmbedOrRegionError(code: number): boolean {
  return code === 101 || code === 150 || code === 152;
}

/**
 * Oynatılabilir tam özet adayları — backend sırası korunur. İlk aday gösterilir; kullanıcının oynatıcısı
 * gömme/bölge engeli bildirirse (oEmbed 200 bu engeli göstermez, 14.09.2026 ölçümü) aynı maçın bir
 * sonraki doğrulanmış resmî özetine geçilir. Frontend yeni video üretmez; yalnız backend listesinden seçer.
 */
export function mainHighlightCandidates(videos: readonly MatchVideoDto[]): MatchVideoDto[] {
  return videos.filter((v) => v.canPlayInApp && isMainHighlight(v.videoType));
}

export function nextCandidateAfterError(count: number, index: number, code: number): number | null {
  // 100 (kaldırılmış/gizli) de aynı maçın bir sonraki doğrulanmış videosuna geçirir; backend kaydı SourceBlocked yapar.
  return (isEmbedOrRegionError(code) || code === 100) && index + 1 < count ? index + 1 : null;
}

/** Aynı videonun iki kez gösterilmemesi için kimlik: gömme adresi, yoksa kaynak sayfası. */
function videoKey(v: MatchVideoDto): string {
  return v.embedUrl ?? v.sourcePageUrl;
}

/**
 * GOLLER — dakika sırasıyla (uzatma dakikası dahil); aynı video ya da aynı gol (dakika + oyuncu) bir kez.
 * Dakika/oyuncu backend'in kanonik golünden gelir; ekran sıralama dışında hiçbir şey hesaplamaz.
 */
export function orderGoalClips(clips: readonly MatchVideoDto[]): MatchVideoDto[] {
  const seenVideo = new Set<string>();
  const seenGoal = new Set<string>();
  const sorted = [...clips].sort(
    (a, b) =>
      (a.eventMinute ?? 999) - (b.eventMinute ?? 999) || (a.eventExtraMinute ?? 0) - (b.eventExtraMinute ?? 0)
  );
  const out: MatchVideoDto[] = [];
  for (const v of sorted) {
    const k = videoKey(v);
    const g = v.eventMinute != null && v.eventPlayer ? `${v.eventMinute}+${v.eventExtraMinute ?? 0}|${v.eventPlayer}` : null;
    if (seenVideo.has(k) || (g && seenGoal.has(g))) continue;
    seenVideo.add(k);
    if (g) seenGoal.add(g);
    out.push(v);
  }
  return out;
}

export function arrangeVideos(videos: readonly MatchVideoDto[]) {
  const main = videos.find((v) => v.canPlayInApp && isMainHighlight(v.videoType)) ?? null;
  const mainKey = main ? videoKey(main) : null;
  // Ana özet ile aynı video ASLA ikinci kez (gol klibi/önemli an olarak) listelenmez.
  const moments = videos.filter((v) => isMoment(v.videoType) && v !== main && videoKey(v) !== mainKey);
  const blocked = main ? [] : videos.filter((v) => !v.canPlayInApp && isMainHighlight(v.videoType));
  // GOLLER: yalnız OYNATILABİLİR gol klipleri (gömme/bölge engelli klip bu başlığa girmez).
  const goals = orderGoalClips(moments.filter((v) => v.canPlayInApp && isGoalClip(v.videoType)));
  const otherMoments = moments.filter((v) => !isGoalClip(v.videoType));
  return { main, moments, goals, otherMoments, blocked };
}

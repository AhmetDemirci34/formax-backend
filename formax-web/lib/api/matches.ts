import apiClient from "./client";
import type {
  MatchDetailDto,
  MatchLiveFeedDto,
  NabizSectionDto,
} from "@/types/api";

export async function getMatchDetail(matchId: number): Promise<MatchDetailDto> {
  const res = await apiClient.get<MatchDetailDto>(
    `/api/matches/${matchId}/detail`
  );
  return res.data;
}

/**
 * Maçın SON DAKİKA haberleri, kullanıcının FORMAX dilinde.
 *
 * Liste `/detail` ile AYNI backend servisinden üretilir; tek fark çeviridir.
 * Çeviri backend'de yapılır ve (haber, dil) ile kalıcı önbelleğe alınır — istemci
 * çeviri istemez, metin üretmez.
 */
export async function getMatchNews(
  matchId: number,
  lang: string
): Promise<NabizSectionDto> {
  const res = await apiClient.get<NabizSectionDto>(
    `/api/matches/${matchId}/news`,
    {
      params: { lang },
      // Genel 10 sn'lik istemci zaman aşımı bu uç için YETERSİZ: bir maçın haberleri
      // ilk kez o dile çevrilirken (LLM) ölçülen süre ~12 sn. 10 sn'de kesilince istek
      // hata veriyor ve kullanıcı seçtiği dili hiç göremiyordu. Önbellek dolduktan
      // sonra aynı istek ~0,05 sn sürer; uzun zaman aşımı yalnız ilk isteği kurtarır.
      timeout: 60_000,
    }
  );
  return res.data;
}

/**
 * CANLI TAKİP — maçın canlı durumu, (varsa) canlı skor/dakika ve global kaynaklı
 * gelişmeler (backend zaten ters kronolojik döner: en yeni en üstte).
 *
 * AI Maç Analizi ile İLGİSİ YOKTUR: ayrı uç, ayrı zincir. Bu isteği atmak veya
 * yenilemek AI analizini yeniden üretmez.
 */
export async function getMatchLiveFeed(matchId: number): Promise<MatchLiveFeedDto> {
  const res = await apiClient.get<MatchLiveFeedDto>(
    `/api/matches/${matchId}/livefeed`
  );
  return res.data;
}


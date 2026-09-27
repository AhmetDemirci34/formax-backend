'use strict';

/**
 * Yerel fallback kanal kataloğu.
 *
 * İki bölüm:
 *  HARDCODED_STREAMS – TRT'nin resmî açık (lisans gerektirmeyen) yayın URL'leri.
 *                      Bu kanallar kaynak M3U indirilmese bile listeye girer.
 *  CHANNEL_NAME_SEEDS – Türkiye'deki tüm bilinen kanal adları.
 *                       Bot bu isimleri hedef olarak kullanır; açık kaynaklarda
 *                       eşleşen stream bulunursa listeye eklenir, bulunamazsa
 *                       sadece isim olarak kalır (stream'siz kanal yayınlanmaz).
 *
 * NOT: Dijitürk, beIN Sports, Exxen, D-Smart gibi şifreli/ücretli platformların
 * DRM anahtarları veya yetkisiz stream'leri burada yer almaz — hem yasadışı hem
 * teknik olarak sürdürülemez. Bu kanallar açık kaynaklarda listelenmişse (iptv-org
 * gibi) bot onları normal akışında tespit eder.
 */

// ─── TRT Resmî Açık Yayınlar ─────────────────────────────────────────────────
// TRT'nin devlet yayıncısı olarak ücretsiz yayına açtığı HLS endpoint'leri.
const HARDCODED_STREAMS = [
  {
    id: 'trt_1',
    name: 'TRT 1',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/TRT_1_logo_%282021%29.svg/200px-TRT_1_logo_%282021%29.svg.png',
    group: 'Genel',
    url: 'https://tv-trt1.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_2',
    name: 'TRT 2',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/TRT_2_logo_%282021%29.svg/200px-TRT_2_logo_%282021%29.svg.png',
    group: 'Genel',
    url: 'https://tv-trt2.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_kurdce',
    name: 'TRT Kurdî',
    logo: '',
    group: 'Genel',
    url: 'https://tv-trtkurdi.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_haber',
    name: 'TRT Haber',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/d/d8/TRT_Haber_logo_%282021%29.svg/200px-TRT_Haber_logo_%282021%29.svg.png',
    group: 'Haber',
    url: 'https://tv-trthaber.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_spor',
    name: 'TRT Spor',
    logo: '',
    group: 'Spor',
    url: 'https://tv-trtspor1.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_spor_yildiz',
    name: 'TRT Spor Yıldız',
    logo: '',
    group: 'Spor',
    url: 'https://tv-trtspor2.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_muzik',
    name: 'TRT Müzik',
    logo: '',
    group: 'Müzik',
    url: 'https://tv-trtmuzik.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_cocuk',
    name: 'TRT Çocuk',
    logo: '',
    group: 'Çocuk',
    url: 'https://tv-trtcocuk.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_belgesel',
    name: 'TRT Belgesel',
    logo: '',
    group: 'Belgesel',
    url: 'https://tv-trtbelgesel.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_world',
    name: 'TRT World',
    logo: '',
    group: 'Haber',
    url: 'https://tv-trtworld.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_avaz',
    name: 'TRT Avaz',
    logo: '',
    group: 'Genel',
    url: 'https://tv-trtavaz.medya.trt.com.tr/master.m3u8',
  },
];

// ─── Türkiye'deki tüm bilinen kanal adları (seed listesi) ──────────────────────
// Bot bu isimler için açık kaynaklarda stream arar.
// Bulunamayan kanallar çıktıya dahil edilmez.
const CHANNEL_NAME_SEEDS = [
  // Genel
  'ATV', 'Fox TV', 'Show TV', 'Kanal D', 'Star TV', 'TV8', 'Kanal 7',
  'TV 100', 'Kanalturk', 'Flash TV', 'Halk TV', 'TELE1', 'OdaTV',
  '360 TV', '24 TV', 'Kanal 24', 'TV360', 'Kanal D Int',
  // Haber
  'CNN Türk', 'NTV', 'HaberTürk', 'TGRT Haber', 'A Haber',
  'Bloomberg HT', 'Ulusal Kanal', 'SKY Türk', 'TV5', 'Teve2',
  'TV NET', 'Kanal D Haber',
  // Spor
  'beIN Sports 1', 'beIN Sports 2', 'beIN Sports 3', 'beIN Sports 4',
  'S Sport', 'S Sport Plus', 'Tivibu Spor 1', 'Tivibu Spor 2',
  'FB TV', 'GS TV', 'BJK TV', 'TS TV', 'A Spor',
  // Eğlence / Drama
  'ATV HD', 'Kanal D HD', 'Star TV HD', 'Show TV HD',
  'D-Smart 1', 'D-Smart 2', 'TV8,5',
  // Müzik
  'Kral TV', 'Number One TV', 'Number One Türk', 'Powertürk TV',
  'SoundMax', 'Kral Pop', 'Kral Rock',
  // Çocuk
  'Minika Go', 'Minika Çocuk', 'Cartoon Network Türkçe',
  'Nick Jr. Türkçe', 'TRT Çocuk',
  // Belgesel / Kültür
  'National Geographic', 'Discovery Channel', 'History Channel',
  'Animal Planet', 'TLC', 'Travel Channel', 'DMAX Türkiye',
  // Ücretli platform (sadece isim — DRM nedeniyle stream eklenmez)
  'Exxen', 'Disney+', 'Netflix', 'BluTV', 'MUBI',
  'D-Smart Go', 'Tivibu', 'Digiturk Play',
];

module.exports = { HARDCODED_STREAMS, CHANNEL_NAME_SEEDS };

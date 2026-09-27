'use strict';

/**
 * Yerel kanal kataloğu — iki bölüm:
 *
 * HARDCODED_STREAMS  : TRT'nin devlet yayıncısı olarak ücretsiz açtığı HLS endpoint'leri.
 *                      Kaynak M3U'lar çökse bile bu kanallar listede kalır.
 *
 * CHANNEL_NAME_SEEDS : Türkiye'deki tüm bilinen kanal adları (isim tohumu).
 *                      Bot bu adları hedef olarak kullanır; açık kaynak IPTV
 *                      depolarında (iptv-org vb.) eşleşen stream bulunursa listeye
 *                      eklenir, bulunamazsa çıktıya dahil edilmez.
 *
 * NOT: beIN Sports, beIN Movies, Sinema TV gibi abonelik kanalları için DRM token
 * veya yetkisiz stream eklenmez — hem yasadışı hem teknik olarak sürdürülemez.
 * Bu kanalların açık kaynaklarda test/free versiyonları varsa bot onları bulur.
 */

// ─── TRT Resmî Açık Yayınlar ─────────────────────────────────────────────────
const HARDCODED_STREAMS = [
  {
    id: 'trt_1', name: 'TRT 1', group: 'Genel',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/TRT_1_logo_%282021%29.svg/200px-TRT_1_logo_%282021%29.svg.png',
    url: 'https://tv-trt1.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_2', name: 'TRT 2', group: 'Genel',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/TRT_2_logo_%282021%29.svg/200px-TRT_2_logo_%282021%29.svg.png',
    url: 'https://tv-trt2.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_3', name: 'TRT 3', group: 'Genel', logo: '',
    url: 'https://tv-trtokul.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_haber', name: 'TRT Haber', group: 'Haber',
    logo: 'https://upload.wikimedia.org/wikipedia/commons/thumb/d/d8/TRT_Haber_logo_%282021%29.svg/200px-TRT_Haber_logo_%282021%29.svg.png',
    url: 'https://tv-trthaber.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_spor', name: 'TRT Spor', group: 'Spor', logo: '',
    url: 'https://tv-trtspor1.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_spor_yildiz', name: 'TRT Spor Yıldız', group: 'Spor', logo: '',
    url: 'https://tv-trtspor2.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_muzik', name: 'TRT Müzik', group: 'Müzik', logo: '',
    url: 'https://tv-trtmuzik.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_cocuk', name: 'TRT Çocuk', group: 'Çocuk', logo: '',
    url: 'https://tv-trtcocuk.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_belgesel', name: 'TRT Belgesel', group: 'Belgesel', logo: '',
    url: 'https://tv-trtbelgesel.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_world', name: 'TRT World', group: 'Haber', logo: '',
    url: 'https://tv-trtworld.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_avaz', name: 'TRT Avaz', group: 'Genel', logo: '',
    url: 'https://tv-trtavaz.medya.trt.com.tr/master.m3u8',
  },
  {
    id: 'trt_kurdce', name: 'TRT Kurdî', group: 'Genel', logo: '',
    url: 'https://tv-trtkurdi.medya.trt.com.tr/master.m3u8',
  },
];

// ─── Hedef kanal listesi — bot bu adlar için açık kaynaklarda stream arar ────
const CHANNEL_NAME_SEEDS = [
  // ── Genel yayın ─────────────────────────────────────────────────────────────
  'ATV', 'Fox TV', 'Show TV', 'Kanal D', 'Star TV', 'TV8', 'Kanal 7',
  'TV 100', 'Kanalturk', 'Flash TV', 'Halk TV', 'TELE1', 'OdaTV', 'Kanal D Int',
  '360 TV', '24 TV', 'Kanal 24', 'TV360', 'TV8,5', 'Teve2',

  // ── Haber ────────────────────────────────────────────────────────────────────
  'CNN Türk', 'NTV', 'HaberTürk', 'TGRT Haber', 'A Haber',
  'Bloomberg HT', 'Ulusal Kanal', 'SKY Türk', 'TV5', 'TV NET',

  // ── Spor ─────────────────────────────────────────────────────────────────────
  // beIN Sports: abonelik kanalı — bot açık depolardan stream bulursa ekler
  'beIN Sports 1', 'beIN Sports 2', 'beIN Sports 3', 'beIN Sports 4', 'beIN Sports 5',
  'beIN Sports Max 1', 'beIN Sports Max 2', 'beIN Sports Haber',
  'S Sport', 'S Sport Plus',
  'Tivibu Spor 1', 'Tivibu Spor 2',
  'A Spor', 'FB TV', 'GS TV', 'BJK TV', 'TS TV',

  // ── Film / Sinema ─────────────────────────────────────────────────────────────
  // beIN Movies: abonelik — DRM'li; açık depoda test stream varsa dahil edilir
  'beIN Movies Premiere', 'beIN Movies Premiere 2',
  'beIN Movies Turk', 'beIN Movies Stars', 'beIN Box Office 1',
  // Sinema TV kanalları (Digiturk/Teledünya)
  'Sinema TV', 'Sinema Komedi', 'Sinema TV 1001', 'Sinema TV 2',
  'Sinema Aile', 'Sinema Aile 2', 'Sinema Yerli', 'Sinema Yerli 2',
  'Sinema Aksiyon', 'Sinema Aksiyon 2', 'Sinema 1002',

  // ── Belgesel / Kültür ─────────────────────────────────────────────────────────
  'beIN İz TV', 'National Geographic', 'Nat Geo Wild',
  'BBC Earth', 'BBC World News', 'Tarih TV',
  'Discovery Channel', 'History Channel', 'Animal Planet', 'TLC', 'DMAX Türkiye',
  'Travel Channel', 'NatGeo People',

  // ── Diğer platformlar / kanallar ──────────────────────────────────────────────
  'TV+', 'tabii', 'FX', 'FX Movies',

  // ── Müzik ────────────────────────────────────────────────────────────────────
  'Kral TV', 'Number One TV', 'Number One Türk', 'Powertürk TV',
  'SoundMax', 'Kral Pop', 'Kral Rock',

  // ── Çocuk ────────────────────────────────────────────────────────────────────
  'Minika Go', 'Minika Çocuk', 'Cartoon Network',
  'Nick Jr.', 'Nickelodeon', 'Disney Channel',
];

module.exports = { HARDCODED_STREAMS, CHANNEL_NAME_SEEDS };

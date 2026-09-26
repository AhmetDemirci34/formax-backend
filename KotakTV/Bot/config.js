'use strict';

module.exports = {
  // ─── Taranacak açık kaynak M3U/M3U8 kaynakları ──────────────────────────
  sources: [
    {
      name: 'iptv-org/iptv — Türkiye',
      url: 'https://raw.githubusercontent.com/iptv-org/iptv/master/streams/tr.m3u',
    },
    {
      name: 'Free-TV/IPTV — Türkiye',
      url: 'https://raw.githubusercontent.com/Free-TV/IPTV/master/playlists/playlist_tr.m3u8',
    },
    {
      name: 'dtankdempsey/IPTV-m3u — Türkiye',
      url: 'https://raw.githubusercontent.com/dtankdempsey/IPTV-m3u/main/streaming/Turkey.m3u',
    },
    {
      name: 'gadget-insanity/IPTV — Türkiye',
      url: 'https://raw.githubusercontent.com/gadget-insanity/iptv/main/turkey.m3u',
    },
  ],

  // ─── Stream sağlık kontrol ayarları ─────────────────────────────────────
  checker: {
    timeoutMs: 8000,       // Her HTTP isteğinin maksimum süresi
    concurrency: 15,        // Eş zamanlı kontrol sayısı (sunucuyu sıkmamak için)
    retries: 1,             // Başarısız isteğin kaç kez tekrar deneneceği
    validateContent: true,  // M3U8 içerik başlığını doğrula (#EXTM3U / #EXT-X-)
  },

  // ─── Çıktı ──────────────────────────────────────────────────────────────
  // index.js'in bulunduğu Bot/ dizinine göre üst klasördeki kanallar.json
  outputFileName: 'kanallar.json',
  schemaVersion: '1.0.0',

  // ─── Kanal kategori tespiti (anahtar kelime eşleştirme) ─────────────────
  categoryRules: [
    {
      keywords: ['haber', 'news', 'cnn', 'ntv', 'tgrt', 'a haber', 'haberturk',
                 'bloomberg', 'teve2', 'ulusal', 'kanal24', 'tv360'],
      category: 'Haber',
    },
    {
      keywords: ['spor', 'sport', 'bein', 'tivibu spor', 's sport',
                 'fb tv', 'gs tv', 'trt spor', 'bjk tv'],
      category: 'Spor',
    },
    {
      keywords: ['belgesel', 'documentary', 'discovery', 'national geographic',
                 'natgeo', 'history', 'tlc', 'animal planet'],
      category: 'Belgesel',
    },
    {
      keywords: ['çocuk', 'cocuk', 'kids', 'cartoon', 'disney',
                 'nickelodeon', 'trt çocuk', 'minika', 'yumurcak'],
      category: 'Çocuk',
    },
    {
      keywords: ['müzik', 'muzik', 'music', 'number one', 'mtv', 'kral',
                 'powerturk', 'powertürk', 'soundmax', 'kanalturk muzik'],
      category: 'Müzik',
    },
    {
      keywords: ['sinema', 'cinema', 'film', 'movie', 'sinematurk',
                 'digiturk film', 'tivibu film'],
      category: 'Sinema',
    },
  ],
};

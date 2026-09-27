'use strict';

// ─── Özel kanal sıralama listesi ─────────────────────────────────────────────
// Bu listedeki kanallar kanallar.json'ın başına, buradaki sırayla yerleştirilir.
// İsimler küçük harfli ve kısmi eşleşme kullanır (ör. "trt 1" → "TRT 1 HD" bulur).
// Listede olmayan kanallar alfabetik olarak sona eklenir.
// Uygulamada ilk kanal otomatik oynatılır → TRT 1'i listenin başına koy.
const CHANNEL_ORDER = [
  'trt 1',
  'trt 2',
  'trt 3',
  'kanal d',
  'show tv',
  'star tv',
  'atv',
  'fox tv',
  'trt haber',
  'cnn türk',
  'ntv',
  'haberturk',
  'tgrt haber',
  'a haber',
  'trt spor',
  'bein sports 1',
  'bein sports 2',
  'bein sports 3',
  's sport',
  'tivibu spor',
  'trt müzik',
  'number one tv',
  'kral tv',
  'trt çocuk',
  'minika go',
  'cartoon network',
  'discovery channel',
  'national geographic',
  'tlc',
  'euronews',
  'tv8',
  'teve2',
  'bloomberg ht',
];

module.exports = {
  // ─── Özel sıralama (yukarıda düzenle) ───────────────────────────────────
  channelOrder: CHANNEL_ORDER,

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
    timeoutMs: 8000,
    concurrency: 15,
    retries: 1,
    validateContent: true,
  },

  // ─── Çıktı ──────────────────────────────────────────────────────────────
  outputFileName: 'kanallar.json',
  schemaVersion: '1.0.0',

  // ─── Kanal kategori tespiti ──────────────────────────────────────────────
  categoryRules: [
    {
      keywords: ['haber', 'news', 'cnn', 'ntv', 'tgrt', 'a haber', 'haberturk',
                 'bloomberg', 'teve2', 'ulusal', 'kanal24', 'tv360', 'euronews'],
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

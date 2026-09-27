'use strict';

// ─── Özel kanal sıralama listesi ─────────────────────────────────────────────
// İlk kanal uygulamada otomatik oynatılır → TRT 1 başta.
const CHANNEL_ORDER = [
  'trt 1', 'trt 2', 'trt 3', 'kanal d', 'show tv', 'star tv', 'atv', 'fox',
  'trt haber', 'cnn türk', 'ntv', 'haberturk', 'tgrt haber', 'a haber',
  'trt spor', 'bein sports 1', 'bein sports 2', 'bein sports 3',
  's sport', 'tivibu spor', 'a spor',
  'trt müzik', 'number one tv', 'kral tv', 'powertürk',
  'trt çocuk', 'minika', 'cartoon network',
  'discovery', 'national geographic', 'tlc', 'dmax',
  'euronews', 'tv8', 'teve2', 'bloomberg',
];

// ─── Açık kaynak M3U kaynakları ──────────────────────────────────────────────
// İndirilemeyen kaynaklar otomatik atlanır; hardcoded TRT stream'leri
// her zaman listeye eklenir (scraper.js/localChannels.js içinde).
const SOURCES = [
  {
    name: 'iptv-org/iptv — Türkiye',
    url: 'https://raw.githubusercontent.com/iptv-org/iptv/master/streams/tr.m3u',
  },
  {
    name: 'iptv-org — Türkiye (epg)',
    url: 'https://raw.githubusercontent.com/iptv-org/iptv/master/streams/tr.m3u',
  },
  {
    name: 'jnk44/iptv-tr — Türkiye',
    url: 'https://raw.githubusercontent.com/jnk44/iptv-tr/main/playlist.m3u8',
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
    name: 'gadget-insanity/iptv — Türkiye',
    url: 'https://raw.githubusercontent.com/gadget-insanity/iptv/main/turkey.m3u',
  },
  {
    name: 'streamlink-iptv — Türkiye',
    url: 'https://raw.githubusercontent.com/streamlink/streamlink/master/tests/resources/plugins/streams/hls/index.m3u8',
  },
  {
    name: 'azmm/iptv-tr',
    url: 'https://raw.githubusercontent.com/azimmermann/iptv-tr/main/TR.m3u',
  },
];

module.exports = {
  channelOrder: CHANNEL_ORDER,
  sources: SOURCES,

  // ─── Stream sağlık kontrol ayarları ───────────────────────────────────────
  checker: {
    timeoutMs: 10000,   // daha uzun — yavaş sunuculara tolerans
    concurrency: 12,    // daha az — rate-limit koruması
    retries: 2,         // her URL için 2 deneme
    validateContent: true,
  },

  outputFileName: 'kanallar.json',
  schemaVersion: '1.0.0',

  // ─── Kategori tespiti ──────────────────────────────────────────────────────
  categoryRules: [
    {
      keywords: ['haber', 'news', 'cnn', 'ntv', 'tgrt', 'a haber', 'haberturk',
                 'bloomberg', 'teve2', 'ulusal', 'kanal24', 'tv360', 'euronews', 'tv100', 'halk tv', 'tele1'],
      category: 'Haber',
    },
    {
      keywords: ['spor', 'sport', 'bein', 'tivibu spor', 's sport',
                 'fb tv', 'gs tv', 'trt spor', 'bjk tv', 'a spor', 'ts tv'],
      category: 'Spor',
    },
    {
      keywords: ['belgesel', 'documentary', 'discovery', 'national geographic',
                 'natgeo', 'history', 'tlc', 'animal planet', 'dmax'],
      category: 'Belgesel',
    },
    {
      keywords: ['çocuk', 'cocuk', 'kids', 'cartoon', 'disney',
                 'nickelodeon', 'trt çocuk', 'minika', 'yumurcak'],
      category: 'Çocuk',
    },
    {
      keywords: ['müzik', 'muzik', 'music', 'number one', 'mtv', 'kral',
                 'powerturk', 'powertürk', 'soundmax'],
      category: 'Müzik',
    },
    {
      keywords: ['sinema', 'cinema', 'film', 'movie'],
      category: 'Sinema',
    },
  ],
};

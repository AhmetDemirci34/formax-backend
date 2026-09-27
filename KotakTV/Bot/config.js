'use strict';

// ─── Özel kanal sıralama listesi ─────────────────────────────────────────────
const CHANNEL_ORDER = [
  // Genel yayın
  'trt 1', 'trt 2', 'trt 3', 'kanal d', 'show tv', 'star tv', 'atv', 'fox',
  'tv8', 'teve2', 'kanal 7',
  // Haber
  'trt haber', 'cnn türk', 'ntv', 'haberturk', 'tgrt haber', 'a haber', 'bloomberg',
  // Spor
  'trt spor', 'bein sports 1', 'bein sports 2', 'bein sports 3',
  'bein sports 4', 'bein sports 5', 'bein sports max 1', 'bein sports max 2',
  'bein sports haber', 's sport', 'tivibu spor', 'a spor',
  // Film / Sinema
  'bein movies premiere', 'bein movies turk', 'bein movies stars', 'bein box office',
  'sinema tv', 'sinema komedi', 'sinema yerli', 'sinema aksiyon', 'sinema aile',
  // Belgesel
  'bein iz tv', 'trt belgesel', 'national geographic', 'nat geo wild',
  'bbc earth', 'tarih tv', 'discovery', 'history', 'animal planet', 'tlc', 'dmax',
  // Müzik
  'trt müzik', 'number one tv', 'kral tv', 'powertürk',
  // Çocuk
  'trt çocuk', 'minika', 'cartoon network',
];

// ─── M3U kaynakları ───────────────────────────────────────────────────────────
const SOURCES = [
  {
    name: 'iptv-org/iptv — Türkiye',
    url: 'https://raw.githubusercontent.com/iptv-org/iptv/master/streams/tr.m3u',
  },
  {
    name: 'iptv-org — tüm kanallar (TR filtreli)',
    url: 'https://iptv-org.github.io/iptv/countries/tr.m3u',
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
];

module.exports = {
  channelOrder: CHANNEL_ORDER,
  sources: SOURCES,

  checker: {
    timeoutMs: 10000,
    concurrency: 12,
    retries: 2,
    validateContent: true,
  },

  outputFileName: 'kanallar.json',
  schemaVersion: '1.0.0',

  categoryRules: [
    {
      keywords: ['haber', 'news', 'cnn', 'ntv', 'tgrt', 'a haber', 'haberturk',
                 'bloomberg', 'teve2', 'ulusal', 'kanal24', 'tv360', 'euronews',
                 'tv100', 'halk tv', 'tele1', 'bein sports haber'],
      category: 'Haber',
    },
    {
      keywords: ['spor', 'sport', 'bein sports', 'tivibu spor', 's sport',
                 'fb tv', 'gs tv', 'trt spor', 'bjk tv', 'a spor', 'ts tv'],
      category: 'Spor',
    },
    {
      keywords: ['sinema', 'cinema', 'film', 'movie', 'movies', 'bein movie',
                 'box office', 'sinematurk', 'fx'],
      category: 'Sinema',
    },
    {
      keywords: ['belgesel', 'documentary', 'discovery', 'national geographic',
                 'natgeo', 'nat geo', 'history', 'tlc', 'animal planet', 'dmax',
                 'bbc earth', 'tarih tv', 'bein iz'],
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
  ],
};

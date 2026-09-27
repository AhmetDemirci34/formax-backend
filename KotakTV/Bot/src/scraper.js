'use strict';

const axios = require('axios');
const { parseM3U } = require('./parser');
const { HARDCODED_STREAMS } = require('./localChannels');

const FETCH_TIMEOUT_MS = 25_000;

// Rotating user-agent listesi — farklı cihaz tiplerini taklit eder
const USER_AGENTS = [
  'Mozilla/5.0 (SmartTV; Linux armv7l) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 OPR/80.0.0.0',
  'Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0) AppleWebKit/538.1 (KHTML, like Gecko) Version/6.0 TV Safari/538.1',
  'Mozilla/5.0 (Linux; Android 12; SmartTV) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36',
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15',
  'Dalvik/2.1.0 (Linux; U; Android 11; SmartTV Build/RKQ1.201004.002)',
  'VLC/3.0.18 LibVLC/3.0.18',
];

function randomUA() {
  return USER_AGENTS[Math.floor(Math.random() * USER_AGENTS.length)];
}

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

/**
 * Tüm M3U kaynaklarını indirir, parse eder ve tekrar eden URL'leri ayıklar.
 * Hardcoded TRT stream'lerini her zaman listeye ekler.
 */
async function scrapeAll(sources) {
  const seenUrls = new Set();
  const all = [];

  // ── Önce hardcoded TRT stream'lerini ekle ─────────────────────────────────
  for (const ch of HARDCODED_STREAMS) {
    if (!seenUrls.has(ch.url)) {
      seenUrls.add(ch.url);
      all.push({ ...ch, _source: 'hardcoded' });
    }
  }

  // ── Açık kaynak M3U'ları çek ──────────────────────────────────────────────
  for (let i = 0; i < sources.length; i++) {
    const source = sources[i];
    // İstekler arası rastgele gecikme (300-900ms) — rate-limit bypass
    if (i > 0) await sleep(300 + Math.random() * 600);

    const channels = await fetchSource(source);
    for (const ch of channels) {
      if (!ch.url || seenUrls.has(ch.url)) continue;
      seenUrls.add(ch.url);
      all.push({ ...ch, _source: source.name });
    }
  }

  return all;
}

// ─── İç ──────────────────────────────────────────────────────────────────────

async function fetchSource({ name, url }, attempt = 0) {
  const ua = randomUA();
  console.log(`  ↳ İndiriliyor: ${name}${attempt > 0 ? ` (deneme ${attempt + 1})` : ''}`);

  try {
    const { data } = await axios.get(url, {
      timeout: FETCH_TIMEOUT_MS,
      headers: {
        'User-Agent': ua,
        'Accept': 'application/vnd.apple.mpegurl, application/x-mpegurl, text/plain, */*',
        'Accept-Language': 'tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7',
        'Accept-Encoding': 'gzip, deflate, br',
        'Cache-Control': 'no-cache',
        'Pragma': 'no-cache',
        'Referer': 'https://www.google.com/',
        'DNT': '1',
        'Connection': 'keep-alive',
      },
      responseType: 'text',
      maxContentLength: 15 * 1024 * 1024,
      maxRedirects: 8,
    });

    const channels = parseM3U(data);
    console.log(`    ✓ ${channels.length} stream bulundu`);
    return channels;

  } catch (err) {
    const status = err.response?.status;
    const code   = err.code || '';

    // 404/410 → kalıcı hata, yeniden deneme yok
    if (status === 404 || status === 410) {
      console.warn(`    ✗ ${name}: ${status} (kaynak kaldırılmış)`);
      return [];
    }

    // Ağ hatası veya 5xx → exponential backoff ile yeniden dene (max 2 kez)
    if (attempt < 2 && (code.includes('ECONNREFUSED') || code.includes('ETIMEDOUT') ||
        code.includes('ENOTFOUND') || (status && status >= 500))) {
      const delay = (attempt + 1) * 2000 + Math.random() * 1000;
      console.warn(`    ↻ ${name}: ${status || code} — ${(delay / 1000).toFixed(1)}s sonra tekrar`);
      await sleep(delay);
      return fetchSource({ name, url }, attempt + 1);
    }

    console.warn(`    ✗ ${name} indirilemedi: ${status || err.message}`);
    return [];
  }
}

module.exports = { scrapeAll };

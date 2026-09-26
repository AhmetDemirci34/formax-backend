'use strict';

const axios = require('axios');
const { parseM3U } = require('./parser');

const FETCH_TIMEOUT_MS = 20_000;
const BOT_UA = 'KotakTV-Bot/1.0.0 (Node.js; +https://github.com/kotaktv)';

/**
 * Tüm M3U kaynaklarını indirir, parse eder ve tekrar eden URL'leri ayıklar.
 * @param {Array<{name: string, url: string}>} sources
 * @returns {Promise<Array>} Benzersiz kanal + stream nesneleri
 */
async function scrapeAll(sources) {
  const seenUrls = new Set();
  const all = [];

  for (const source of sources) {
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

async function fetchSource({ name, url }) {
  console.log(`  ↳ İndiriliyor: ${name}`);

  try {
    const { data } = await axios.get(url, {
      timeout: FETCH_TIMEOUT_MS,
      headers: { 'User-Agent': BOT_UA },
      responseType: 'text',
      maxContentLength: 10 * 1024 * 1024, // 10 MB yeterli
    });

    const channels = parseM3U(data);
    console.log(`    ✓ ${channels.length} stream bulundu`);
    return channels;

  } catch (err) {
    console.warn(`    ✗ ${name} indirilemedi: ${err.message}`);
    return [];
  }
}

module.exports = { scrapeAll };

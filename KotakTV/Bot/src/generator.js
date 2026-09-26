'use strict';

const fs = require('fs');
const path = require('path');
const { slugify } = require('./parser');
const config = require('../config');

/**
 * Canlı stream listesini alır, kanal başına gruplar ve kanallar.json yazar.
 *
 * Gruplama anahtarı: slugified kanal adı
 * Priority sıralaması: en hızlı yanıt veren stream = priority 1
 *
 * @param {Array} aliveStreams  - checker.js'ten gelen { ...channel, isAlive, responseTimeMs }
 * @param {string} outputDir   - kanallar.json'ın yazılacağı dizin
 * @returns {object}           - Yazılan JSON nesnesi
 */
function generateKanallarJson(aliveStreams, outputDir) {
  const channelMap = buildChannelMap(aliveStreams);
  const channels = finalizeChannels(channelMap);

  const payload = {
    _meta: {
      schema_version: config.schemaVersion,
      generated_at: new Date().toISOString(),
      generated_by: 'KotakTV-Bot/1.0.0 (Node.js)',
      ttl_seconds: 3600,
      total_channels: channels.length,
    },
    channels,
  };

  const outPath = path.join(outputDir, config.outputFileName);
  fs.writeFileSync(outPath, JSON.stringify(payload, null, 2), 'utf-8');

  return payload;
}

// ─── İç ──────────────────────────────────────────────────────────────────────

function buildChannelMap(streams) {
  const map = new Map();

  for (const s of streams) {
    const key = slugify(s.name || s.id || 'bilinmeyen');

    if (!map.has(key)) {
      map.set(key, {
        id: key,
        name: s.name || key,
        logo_url: resolveLogo(s.logo, key),
        category: detectCategory(s.name, s.group),
        tags: buildTags(s.name, s.group),
        epg_id: s.id || '',
        is_active: true,
        _streams: [],
      });
    }

    map.get(key)._streams.push({ url: s.url, responseTimeMs: s.responseTimeMs || 9999 });
  }

  return map;
}

function finalizeChannels(map) {
  const channels = [];

  for (const [, ch] of map) {
    // En hızlı yanıt vereni öne al
    ch._streams.sort((a, b) => a.responseTimeMs - b.responseTimeMs);

    const streams = ch._streams.map((s, i) => ({
      priority: i + 1,
      label: i === 0 ? 'primary-cdn' : `backup-cdn-${i}`,
      url: s.url,
      drm: null,
      resolution: 'auto',
      bitrate_kbps: null,
      headers: {},
    }));

    const { _streams, ...rest } = ch;
    channels.push({ ...rest, streams });
  }

  // Alfabetik sırala
  channels.sort((a, b) => a.name.localeCompare(b.name, 'tr'));
  return channels;
}

function resolveLogo(logoUrl, channelId) {
  if (logoUrl && /^https?:\/\//i.test(logoUrl)) return logoUrl;
  return `https://cdn.kotaktv.com/logos/${channelId}.png`;
}

function detectCategory(name = '', group = '') {
  const haystack = `${name} ${group}`.toLowerCase();

  for (const rule of config.categoryRules) {
    if (rule.keywords.some(kw => haystack.includes(kw))) {
      return rule.category;
    }
  }
  return 'Genel';
}

function buildTags(name = '', group = '') {
  const lower = `${name} ${group}`.toLowerCase();
  const tags = [];
  if (/hd|1080|720/.test(lower)) tags.push('hd');
  if (/canlı|live/.test(lower)) tags.push('canlı');
  if (/türk|turk|tr\b/.test(lower)) tags.push('yerli');
  return tags;
}

module.exports = { generateKanallarJson };

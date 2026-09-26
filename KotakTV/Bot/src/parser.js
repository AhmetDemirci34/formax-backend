'use strict';

/**
 * M3U/M3U8 playlist içeriğini parse eder.
 * Her stream için { id, name, logo, group, url } nesnesi döner.
 *
 * Örnek M3U formatı:
 *   #EXTINF:-1 tvg-id="TRT1.tr" tvg-name="TRT 1" tvg-logo="https://…" group-title="General",TRT 1
 *   https://stream.example.com/master.m3u8
 */
function parseM3U(content) {
  const channels = [];
  const lines = content.split('\n').map(l => l.trim()).filter(Boolean);

  let pending = null;

  for (const line of lines) {
    if (line.startsWith('#EXTINF:')) {
      pending = extractExtInf(line);
    } else if (!line.startsWith('#') && pending) {
      if (isValidUrl(line)) {
        channels.push({ ...pending, url: line });
      }
      pending = null;
    }
  }

  return channels;
}

// ─── İç yardımcılar ─────────────────────────────────────────────────────────

function extractExtInf(line) {
  const channel = {
    id: attr(line, 'tvg-id'),
    name: attr(line, 'tvg-name'),
    logo: attr(line, 'tvg-logo'),
    group: attr(line, 'group-title') || 'Genel',
  };

  // Satır sonundaki görünen kanal adı (son virgülden sonra)
  const lastComma = line.lastIndexOf(',');
  if (lastComma !== -1) {
    const displayName = line.slice(lastComma + 1).trim();
    if (displayName) channel.name = channel.name || displayName;
  }

  // id yoksa isimden üret
  if (!channel.id && channel.name) {
    channel.id = slugify(channel.name);
  }

  return channel;
}

function attr(line, key) {
  const re = new RegExp(`${key}="([^"]*)"`, 'i');
  const m = line.match(re);
  return m ? m[1].trim() : '';
}

function isValidUrl(str) {
  return /^https?:\/\/.+/i.test(str);
}

function slugify(str) {
  return str
    .toLowerCase()
    .replace(/ğ/g, 'g').replace(/ü/g, 'u').replace(/ş/g, 's')
    .replace(/ı/g, 'i').replace(/ö/g, 'o').replace(/ç/g, 'c')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '');
}

module.exports = { parseM3U, slugify };

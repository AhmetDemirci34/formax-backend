'use strict';

const axios = require('axios');

const M3U8_SIGNATURES = ['#EXTM3U', '#EXT-X-VERSION', '#EXT-X-STREAM-INF', '#EXT-X-TARGETDURATION'];

const BASE_HEADERS = {
  'User-Agent': 'Mozilla/5.0 (Linux; Android 9; SmartTV) KotakTV-Bot/1.0.0',
  'Accept': 'application/vnd.apple.mpegurl, application/x-mpegurl, */*',
  'Accept-Language': 'tr-TR,tr;q=0.9,en;q=0.8',
};

/**
 * Bir M3U8 stream URL'sinin erişilebilir olup olmadığını doğrular.
 *
 * Strateji:
 *  1. HEAD isteği (hızlı, düşük bant genişliği)
 *  2. HEAD başarısızsa → GET ile ilk 512 byte okunur
 *  3. validateContent=true ise M3U8 imzası aranır
 *
 * @returns {{ url, isAlive, statusCode, responseTimeMs, reason? }}
 */
async function checkStream(url, options = {}) {
  const { timeoutMs = 8000, validateContent = true, retries = 1 } = options;

  if (!url || !/^https?:\/\//i.test(url)) {
    return { url, isAlive: false, statusCode: null, responseTimeMs: 0, reason: 'invalid_url' };
  }

  for (let attempt = 0; attempt <= retries; attempt++) {
    const result = await _check(url, timeoutMs, validateContent);
    if (result.isAlive) return result;
    if (attempt < retries) await sleep(500);
  }

  return { url, isAlive: false, statusCode: null, responseTimeMs: 0, reason: 'all_retries_failed' };
}

// ─── İç ──────────────────────────────────────────────────────────────────────

async function _check(url, timeoutMs, validateContent) {
  const t0 = Date.now();

  // ── 1. HEAD ──────────────────────────────────────────────────────────────
  try {
    const res = await axios.head(url, {
      timeout: Math.min(timeoutMs, 5000),
      headers: BASE_HEADERS,
      validateStatus: () => true,
      maxRedirects: 5,
    });

    if (res.status >= 200 && res.status < 400) {
      // Sadece content-type bilgisi varsa doğrula
      if (validateContent) {
        const ct = (res.headers['content-type'] || '').toLowerCase();
        const isMediaType = ct.includes('mpegurl') || ct.includes('video') ||
                            ct.includes('audio') || ct.includes('octet-stream') || ct === '';
        if (!isMediaType) {
          // Şüpheli content-type → GET ile doğrula
          return await _getCheck(url, timeoutMs, t0, validateContent);
        }
      }
      return { url, isAlive: true, statusCode: res.status, responseTimeMs: Date.now() - t0 };
    }
  } catch {
    // HEAD desteklenmiyor veya ağ hatası → GET dene
  }

  // ── 2. GET (ilk 512 byte) ─────────────────────────────────────────────────
  return await _getCheck(url, timeoutMs, t0, validateContent);
}

async function _getCheck(url, timeoutMs, t0, validateContent) {
  return new Promise((resolve) => {
    axios.get(url, {
      timeout: timeoutMs,
      headers: { ...BASE_HEADERS, Range: 'bytes=0-511' },
      responseType: 'stream',
      validateStatus: () => true,
      maxRedirects: 5,
    })
      .then((res) => {
        const status = res.status;

        if (status < 200 || status >= 400) {
          res.data.destroy();
          return resolve({
            url, isAlive: false, statusCode: status,
            responseTimeMs: Date.now() - t0, reason: 'bad_status',
          });
        }

        if (!validateContent) {
          res.data.destroy();
          return resolve({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 });
        }

        // İlk chunk'ı oku, M3U8 imzasını kontrol et
        let buffer = '';
        let settled = false;

        const done = (result) => {
          if (settled) return;
          settled = true;
          res.data.destroy();
          resolve(result);
        };

        res.data.on('data', (chunk) => {
          buffer += chunk.toString('utf8');
          if (buffer.length >= 128) {
            const snippet = buffer.slice(0, 128).trimStart();
            const validM3U8 = M3U8_SIGNATURES.some(sig => snippet.startsWith(sig));
            done({
              url,
              isAlive: validM3U8 || snippet.length === 0, // boşsa status'a güven
              statusCode: status,
              responseTimeMs: Date.now() - t0,
              ...(!validM3U8 && snippet.length > 0 ? { reason: 'invalid_content' } : {}),
            });
          }
        });

        res.data.on('end', () => {
          done({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 });
        });

        res.data.on('error', (err) => {
          done({ url, isAlive: false, statusCode: null, responseTimeMs: Date.now() - t0, reason: err.message });
        });

        // Güvenlik zamanaşımı: 2 sn sonra status'a göre karar ver
        setTimeout(() => {
          done({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 });
        }, 2000);
      })
      .catch((err) => {
        resolve({
          url, isAlive: false, statusCode: err.response?.status || null,
          responseTimeMs: Date.now() - t0, reason: err.code || err.message,
        });
      });
  });
}

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

module.exports = { checkStream };

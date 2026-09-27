'use strict';

const axios = require('axios');

const M3U8_SIGNATURES = ['#EXTM3U', '#EXT-X-VERSION', '#EXT-X-STREAM-INF', '#EXT-X-TARGETDURATION'];

// Farklı UA'ler kullanarak engel yeme riskini azalt
const CHECK_USER_AGENTS = [
  'Mozilla/5.0 (SmartTV; Linux armv7l) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
  'Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0) AppleWebKit/538.1 (KHTML, like Gecko) Version/6.0 TV Safari/538.1',
  'Mozilla/5.0 (Linux; Android 12; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Mobile Safari/537.36',
  'VLC/3.0.18 LibVLC/3.0.18',
  'Kodi/20.0 (Windows; Windows 11 Home; x64) App_Bitness/64 Version/20.0',
];

function randomUA() {
  return CHECK_USER_AGENTS[Math.floor(Math.random() * CHECK_USER_AGENTS.length)];
}

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

/**
 * Bir M3U8 stream URL'sinin erişilebilir olup olmadığını doğrular.
 *
 * Strateji:
 *  1. HEAD isteği (hızlı, 3s timeout)
 *  2. HEAD başarısızsa → GET ile ilk 512 byte
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
    if (attempt > 0) await sleep(400 + Math.random() * 400);
    const result = await _check(url, timeoutMs, validateContent);
    if (result.isAlive) return result;
  }

  return { url, isAlive: false, statusCode: null, responseTimeMs: 0, reason: 'all_retries_failed' };
}

// ─── İç ──────────────────────────────────────────────────────────────────────

function buildHeaders() {
  return {
    'User-Agent': randomUA(),
    'Accept': 'application/vnd.apple.mpegurl, application/x-mpegurl, video/mp2t, */*',
    'Accept-Language': 'tr-TR,tr;q=0.9,en-US;q=0.8',
    'Accept-Encoding': 'gzip, deflate, br',
    'Cache-Control': 'no-cache',
    'Pragma': 'no-cache',
    'Connection': 'keep-alive',
    'DNT': '1',
  };
}

async function _check(url, timeoutMs, validateContent) {
  const t0 = Date.now();

  // ── 1. HEAD (max 4s) ─────────────────────────────────────────────────────
  try {
    const res = await axios.head(url, {
      timeout: Math.min(timeoutMs, 4000),
      headers: buildHeaders(),
      validateStatus: () => true,
      maxRedirects: 6,
    });

    if (res.status >= 200 && res.status < 400) {
      if (validateContent) {
        const ct = (res.headers['content-type'] || '').toLowerCase();
        const isMedia = ct.includes('mpegurl') || ct.includes('video') ||
                        ct.includes('audio') || ct.includes('octet-stream') || ct === '';
        if (!isMedia) return await _getCheck(url, timeoutMs, t0, validateContent);
      }
      return { url, isAlive: true, statusCode: res.status, responseTimeMs: Date.now() - t0 };
    }
  } catch {
    // HEAD desteklenmiyor veya ağ hatası → GET dene
  }

  // ── 2. GET (ilk 512 byte) ────────────────────────────────────────────────
  return await _getCheck(url, timeoutMs, t0, validateContent);
}

async function _getCheck(url, timeoutMs, t0, validateContent) {
  return new Promise((resolve) => {
    axios.get(url, {
      timeout: timeoutMs,
      headers: { ...buildHeaders(), Range: 'bytes=0-511' },
      responseType: 'stream',
      validateStatus: () => true,
      maxRedirects: 6,
    })
      .then((res) => {
        const status = res.status;

        if (status < 200 || status >= 400) {
          res.data.destroy();
          return resolve({ url, isAlive: false, statusCode: status, responseTimeMs: Date.now() - t0, reason: 'bad_status' });
        }

        if (!validateContent) {
          res.data.destroy();
          return resolve({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 });
        }

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
              isAlive: validM3U8 || snippet.length === 0,
              statusCode: status,
              responseTimeMs: Date.now() - t0,
              ...(!validM3U8 && snippet.length > 0 ? { reason: 'invalid_content' } : {}),
            });
          }
        });

        res.data.on('end', () => done({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 }));
        res.data.on('error', (err) => done({ url, isAlive: false, statusCode: null, responseTimeMs: Date.now() - t0, reason: err.message }));

        // 2.5s güvenlik timeout
        setTimeout(() => done({ url, isAlive: true, statusCode: status, responseTimeMs: Date.now() - t0 }), 2500);
      })
      .catch((err) => {
        resolve({ url, isAlive: false, statusCode: err.response?.status || null, responseTimeMs: Date.now() - t0, reason: err.code || err.message });
      });
  });
}

module.exports = { checkStream };

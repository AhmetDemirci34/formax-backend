package com.kotaktv.data

import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.util.concurrent.TimeUnit

/**
 * Buluttaki kanallar.json'u indirir, parse eder ve TTL tabanlı bellekiçi cache uygular.
 *
 * Thread güvenliği: tüm ağ işlemleri Dispatchers.IO üzerinde koşar.
 */
class ChannelRepository {

    private val client = OkHttpClient.Builder()
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .addInterceptor { chain ->
            val req = chain.request().newBuilder()
                .header("User-Agent", "KotakTV-AndroidApp/1.0.0")
                .header("Accept", "application/json")
                .build()
            chain.proceed(req)
        }
        .build()

    private val gson = Gson()

    // ─── Bellek içi TTL cache ────────────────────────────────────────────────
    @Volatile private var cachedResponse: KanallarResponse? = null
    @Volatile private var cacheExpiresAt: Long = 0L

    /**
     * Aktif kanalları döner.
     * Cache geçerliyse ağa gitmez.
     *
     * @param jsonUrl kanallar.json dosyasının tam URL'si
     */
    suspend fun getChannels(jsonUrl: String): Result<List<Channel>> =
        withContext(Dispatchers.IO) {
            // ── Cache hit ────────────────────────────────────────────────────
            val cached = cachedResponse
            if (cached != null && System.currentTimeMillis() < cacheExpiresAt) {
                return@withContext Result.success(cached.activeChannels())
            }

            // ── Ağ isteği ────────────────────────────────────────────────────
            runCatching {
                val request = Request.Builder().url(jsonUrl).build()
                val response = client.newCall(request).execute()

                if (!response.isSuccessful) {
                    error("Sunucu hatası: HTTP ${response.code}")
                }

                val body = response.body?.string()
                    ?: error("Sunucudan boş yanıt alındı")

                val parsed = gson.fromJson(body, KanallarResponse::class.java)

                // TTL'e göre cache'i güncelle (en az 60 sn)
                val ttl = maxOf(parsed.meta.ttlSeconds.toLong(), 60L)
                cachedResponse = parsed
                cacheExpiresAt = System.currentTimeMillis() + ttl * 1_000L

                parsed.activeChannels()
            }
        }

    /** Cache'i zorla geçersiz kıl (yenileme butonu için). */
    fun invalidateCache() {
        cacheExpiresAt = 0L
    }

    private fun KanallarResponse.activeChannels(): List<Channel> =
        channels.filter { it.isActive && it.streams.isNotEmpty() }
}

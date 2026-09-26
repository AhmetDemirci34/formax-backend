package com.kotaktv.failover

import android.os.Handler
import android.os.Looper
import android.util.Log
import androidx.annotation.MainThread
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.util.UnstableApi
import com.kotaktv.data.Channel
import com.kotaktv.data.Stream

private const val TAG = "FailoverEngine"

// ─── Durum makinesi ──────────────────────────────────────────────────────────

enum class FailoverState {
    IDLE,        // Kanal henüz yüklenmedi
    LOADING,     // İlk stream yükleniyor
    PLAYING,     // Akış sağlıklı
    SWITCHING,   // Yedek stream'e geçiliyor (overlay gösterilir)
    ALL_FAILED   // Tüm yedekler tükendi
}

// ─── Callback arayüzleri ─────────────────────────────────────────────────────

/**
 * @param stream Oynatılacak stream nesnesi
 * @param mediaItem Hazırlanmış MediaItem (DRM + headers dahil)
 */
typealias OnSwitchStream = (stream: Stream, mediaItem: MediaItem) -> Unit

/**
 * @param state     Yeni failover durumu
 * @param streamLabel Aktif stream'in etiketi (null = tüm kaynaklar tükendi)
 */
typealias OnStateChanged = (state: FailoverState, streamLabel: String?) -> Unit

// ─── Motor ───────────────────────────────────────────────────────────────────

/**
 * Kanal bazlı akıllı failover motoru.
 *
 * Tetikleme koşulları:
 *  - ExoPlayer onPlayerError → [onPlayerError]
 *  - STATE_BUFFERING ≥ STALL_TIMEOUT_MS süre → donma tespiti
 *  - STATE_ENDED (canlı yayın beklenmedik bitiş) → [onPlayerError]
 *
 * Geçiş kuralı: priority sırasına göre sıradaki stream denenir.
 * En hızlı yanıt veren stream (bot tarafından priority=1 olarak işaretlenmiş) her zaman ilk denenir.
 */
@UnstableApi
class FailoverEngine(
    private val onSwitchStream: OnSwitchStream,
    private val onStateChanged: OnStateChanged
) {
    // ─── Durum ───────────────────────────────────────────────────────────────

    private var channel: Channel? = null
    private var currentIndex: Int = 0            // sortedStreams içindeki aktif indeks
    private var state: FailoverState = FailoverState.IDLE

    // ─── Zamanlayıcılar ──────────────────────────────────────────────────────

    private val mainHandler = Handler(Looper.getMainLooper())

    /** Bu süre boyunca BUFFERING'de kalınırsa stream donmuş kabul edilir. */
    private val STALL_TIMEOUT_MS = 12_000L

    /** Hızlı ardışık hataların birden fazla failover tetiklemesini önler. */
    private val DEBOUNCE_MS = 1_500L
    private var lastFailoverAt = 0L

    private var stallRunnable: Runnable? = null

    // ─── Genel API ───────────────────────────────────────────────────────────

    /**
     * Yeni bir kanal yükler; en yüksek öncelikli stream'den başlar.
     */
    @MainThread
    fun loadChannel(newChannel: Channel) {
        Log.d(TAG, "loadChannel: ${newChannel.name} — ${newChannel.sortedStreams.size} stream")

        cancelStallTimer()
        channel = newChannel
        currentIndex = 0
        setState(FailoverState.LOADING, null)

        val first = newChannel.sortedStreams.firstOrNull() ?: run {
            Log.w(TAG, "Kanalın hiç stream'i yok: ${newChannel.id}")
            setState(FailoverState.ALL_FAILED, null)
            return
        }

        dispatchStream(first)
    }

    /** ExoPlayer hatası → failover tetikle. */
    @MainThread
    fun onPlayerError() {
        Log.w(TAG, "onPlayerError — state=$state index=$currentIndex")
        maybeFailover(reason = "PlayerError")
    }

    /** ExoPlayer BUFFERING durumuna girdi → donma zamanlayıcısını başlat. */
    @MainThread
    fun onBufferingStarted() {
        if (state == FailoverState.PLAYING || state == FailoverState.LOADING) {
            scheduleStallTimer()
        }
    }

    /** ExoPlayer STATE_READY → oynatma başladı, zamanlayıcıyı iptal et. */
    @MainThread
    fun onPlayingStarted() {
        cancelStallTimer()
        if (state == FailoverState.SWITCHING || state == FailoverState.LOADING) {
            setState(FailoverState.PLAYING, currentStream()?.label)
            Log.i(TAG, "Oynatma başladı: ${currentStream()?.label}")
        }
    }

    /** Activity yok edilirken kaynakları serbest bırak. */
    @MainThread
    fun release() {
        cancelStallTimer()
        channel = null
        state = FailoverState.IDLE
    }

    fun currentState(): FailoverState = state

    fun currentStream(): Stream? = channel?.sortedStreams?.getOrNull(currentIndex)

    // ─── Failover mantığı ────────────────────────────────────────────────────

    private fun maybeFailover(reason: String) {
        if (state == FailoverState.ALL_FAILED) return

        val now = System.currentTimeMillis()
        if (now - lastFailoverAt < DEBOUNCE_MS) {
            Log.d(TAG, "Debounce aktif — failover atlandı ($reason)")
            return
        }

        triggerFailover(reason)
    }

    private fun triggerFailover(reason: String) {
        cancelStallTimer()
        lastFailoverAt = System.currentTimeMillis()

        val streams = channel?.sortedStreams ?: return
        val nextIndex = currentIndex + 1

        if (nextIndex >= streams.size) {
            Log.e(TAG, "Tüm ${streams.size} stream denendi, hepsi başarısız.")
            setState(FailoverState.ALL_FAILED, null)
            return
        }

        currentIndex = nextIndex
        val next = streams[nextIndex]
        Log.i(TAG, "Failover [$reason]: priority=${next.priority} label=${next.label}")

        setState(FailoverState.SWITCHING, next.label)
        dispatchStream(next)
    }

    // ─── Stream gönderme ─────────────────────────────────────────────────────

    private fun dispatchStream(stream: Stream) {
        val mediaItem = buildMediaItem(stream)
        onSwitchStream(stream, mediaItem)
    }

    private fun buildMediaItem(stream: Stream): MediaItem {
        val builder = MediaItem.Builder().setUri(stream.url)

        // DRM yapılandırması (Widevine)
        stream.drm?.let { drm ->
            if (drm.type.equals("widevine", ignoreCase = true)) {
                val drmConfig = MediaItem.DrmConfiguration.Builder(C.WIDEVINE_UUID)
                    .setLicenseUri(drm.licenseUrl)
                    .apply {
                        if (drm.headers.isNotEmpty()) setLicenseRequestHeaders(drm.headers)
                    }
                    .build()
                builder.setDrmConfiguration(drmConfig)
            }
        }

        // Stream'e özel HTTP başlıkları
        if (stream.headers.isNotEmpty()) {
            builder.setRequestMetadata(
                MediaItem.RequestMetadata.Builder()
                    .setExtras(android.os.Bundle().apply {
                        stream.headers.forEach { (k, v) -> putString(k, v) }
                    })
                    .build()
            )
        }

        return builder.build()
    }

    // ─── Donma (stall) zamanlayıcısı ─────────────────────────────────────────

    private fun scheduleStallTimer() {
        cancelStallTimer()
        stallRunnable = Runnable {
            Log.w(TAG, "Stall tespiti — ${STALL_TIMEOUT_MS / 1000}s boyunca buffer'da kaldı")
            maybeFailover(reason = "StallTimeout")
        }.also { mainHandler.postDelayed(it, STALL_TIMEOUT_MS) }
    }

    private fun cancelStallTimer() {
        stallRunnable?.let { mainHandler.removeCallbacks(it) }
        stallRunnable = null
    }

    // ─── Durum yönetimi ──────────────────────────────────────────────────────

    private fun setState(newState: FailoverState, label: String?) {
        state = newState
        onStateChanged(newState, label)
    }
}

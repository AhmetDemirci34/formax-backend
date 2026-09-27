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
    IDLE,
    LOADING,
    PLAYING,
    SWITCHING,
    ALL_FAILED
}

typealias OnSwitchStream = (stream: Stream, mediaItem: MediaItem) -> Unit
typealias OnStateChanged = (state: FailoverState, streamLabel: String?) -> Unit

// ─── Motor ───────────────────────────────────────────────────────────────────

/**
 * Sessiz failover motoru: hata durumunda ekrana hiçbir mesaj basmaz,
 * yedek stream'leri sırayla dener. Tüm yedekler biterse 15s sonra
 * en baştan tekrar başlar.
 */
@UnstableApi
class FailoverEngine(
    private val onSwitchStream: OnSwitchStream,
    private val onStateChanged: OnStateChanged
) {
    private var channel: Channel? = null
    private var currentIndex: Int = 0
    private var state: FailoverState = FailoverState.IDLE

    private val mainHandler = Handler(Looper.getMainLooper())

    private val STALL_TIMEOUT_MS = 12_000L
    private val DEBOUNCE_MS = 1_500L
    private val RETRY_AFTER_ALL_FAILED_MS = 15_000L

    private var lastFailoverAt = 0L
    private var stallRunnable: Runnable? = null
    private var retryRunnable: Runnable? = null

    // ─── Genel API ───────────────────────────────────────────────────────────

    @MainThread
    fun loadChannel(newChannel: Channel) {
        Log.d(TAG, "loadChannel: ${newChannel.name} — ${newChannel.sortedStreams.size} stream")
        cancelStallTimer()
        cancelRetryTimer()
        channel = newChannel
        currentIndex = 0
        setState(FailoverState.LOADING, null)

        val first = newChannel.sortedStreams.firstOrNull() ?: run {
            scheduleRetry()
            return
        }
        dispatchStream(first)
    }

    @MainThread
    fun onPlayerError() {
        Log.w(TAG, "onPlayerError — state=$state index=$currentIndex")
        maybeFailover("PlayerError")
    }

    @MainThread
    fun onBufferingStarted() {
        if (state == FailoverState.PLAYING || state == FailoverState.LOADING) scheduleStallTimer()
    }

    @MainThread
    fun onPlayingStarted() {
        cancelStallTimer()
        if (state != FailoverState.PLAYING) {
            setState(FailoverState.PLAYING, currentStream()?.label)
            Log.i(TAG, "Oynatma başladı: ${currentStream()?.label}")
        }
    }

    @MainThread
    fun release() {
        cancelStallTimer()
        cancelRetryTimer()
        channel = null
        state = FailoverState.IDLE
    }

    fun currentState(): FailoverState = state
    fun currentStream(): Stream? = channel?.sortedStreams?.getOrNull(currentIndex)

    // ─── Failover mantığı ────────────────────────────────────────────────────

    private fun maybeFailover(reason: String) {
        if (state == FailoverState.ALL_FAILED) return
        val now = System.currentTimeMillis()
        if (now - lastFailoverAt < DEBOUNCE_MS) return
        triggerFailover(reason)
    }

    private fun triggerFailover(reason: String) {
        cancelStallTimer()
        lastFailoverAt = System.currentTimeMillis()

        val streams = channel?.sortedStreams ?: return
        val nextIndex = currentIndex + 1

        if (nextIndex >= streams.size) {
            Log.e(TAG, "Tüm ${streams.size} stream denendi — ${RETRY_AFTER_ALL_FAILED_MS / 1000}s sonra tekrar denenecek")
            setState(FailoverState.ALL_FAILED, null)
            scheduleRetry()
            return
        }

        currentIndex = nextIndex
        val next = streams[nextIndex]
        Log.i(TAG, "Failover [$reason]: priority=${next.priority} label=${next.label}")
        setState(FailoverState.SWITCHING, next.label)
        dispatchStream(next)
    }

    // ─── Otomatik yeniden deneme ─────────────────────────────────────────────

    private fun scheduleRetry() {
        cancelRetryTimer()
        retryRunnable = Runnable {
            val ch = channel ?: return@Runnable
            Log.i(TAG, "Auto-retry: ilk stream'den başlanıyor")
            currentIndex = 0
            setState(FailoverState.LOADING, null)
            ch.sortedStreams.firstOrNull()?.let { dispatchStream(it) }
        }.also { mainHandler.postDelayed(it, RETRY_AFTER_ALL_FAILED_MS) }
    }

    private fun cancelRetryTimer() {
        retryRunnable?.let { mainHandler.removeCallbacks(it) }
        retryRunnable = null
    }

    // ─── Stream gönderme ─────────────────────────────────────────────────────

    private fun dispatchStream(stream: Stream) {
        onSwitchStream(stream, buildMediaItem(stream))
    }

    private fun buildMediaItem(stream: Stream): MediaItem {
        val builder = MediaItem.Builder().setUri(stream.url)

        stream.drm?.let { drm ->
            if (drm.type.equals("widevine", ignoreCase = true)) {
                val drmConfig = MediaItem.DrmConfiguration.Builder(C.WIDEVINE_UUID)
                    .setLicenseUri(drm.licenseUrl)
                    .apply { if (drm.headers.isNotEmpty()) setLicenseRequestHeaders(drm.headers) }
                    .build()
                builder.setDrmConfiguration(drmConfig)
            }
        }

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

    // ─── Donma zamanlayıcısı ─────────────────────────────────────────────────

    private fun scheduleStallTimer() {
        cancelStallTimer()
        stallRunnable = Runnable {
            Log.w(TAG, "Stall tespiti — ${STALL_TIMEOUT_MS / 1000}s buffer'da kaldı")
            maybeFailover("StallTimeout")
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

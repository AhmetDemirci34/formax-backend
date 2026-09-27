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
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.Channel as KChannel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import java.net.HttpURLConnection
import java.net.URL

private const val TAG = "FailoverEngine"

enum class FailoverState { IDLE, LOADING, PLAYING, SWITCHING, ALL_FAILED }

typealias OnSwitchStream = (stream: Stream, mediaItem: MediaItem) -> Unit
typealias OnStateChanged  = (state: FailoverState, streamLabel: String?) -> Unit

/**
 * Paralel race failover motoru.
 * Kanal değişiminde tüm stream URL'leri eş zamanlı HEAD ile test edilir;
 * en hızlı yanıt veren sağlıklı link doğrudan ExoPlayer'a verilir.
 * Hata durumunda kalan URL'ler yeniden race edilir — ekrana hiçbir şey basılmaz.
 */
@UnstableApi
class FailoverEngine(
    private val onSwitchStream: OnSwitchStream,
    private val onStateChanged: OnStateChanged
) {
    private var channel: Channel? = null
    private var usedUrls = mutableSetOf<String>()
    private var state: FailoverState = FailoverState.IDLE

    private val mainHandler = Handler(Looper.getMainLooper())
    private val engineScope = CoroutineScope(Dispatchers.Main + SupervisorJob())

    private val PROBE_TIMEOUT_MS = 2_500L
    private val STALL_TIMEOUT_MS = 12_000L
    private val RETRY_DELAY_MS   = 15_000L
    private val DEBOUNCE_MS      = 1_500L

    private var lastFailoverAt   = 0L
    private var stallRunnable: Runnable? = null
    private var retryRunnable: Runnable? = null
    private var probeJob: Job? = null

    // ─── Genel API ───────────────────────────────────────────────────────────

    @MainThread
    fun loadChannel(newChannel: Channel) {
        Log.d(TAG, "loadChannel: ${newChannel.name} — ${newChannel.sortedStreams.size} stream")
        cancelAll()
        channel  = newChannel
        usedUrls = mutableSetOf()
        setState(FailoverState.LOADING, null)
        raceAndPlay(newChannel.sortedStreams)
    }

    @MainThread
    fun onPlayerError() {
        Log.w(TAG, "onPlayerError — state=$state")
        debounceFailover("PlayerError")
    }

    @MainThread
    fun onBufferingStarted() {
        if (state == FailoverState.PLAYING || state == FailoverState.LOADING) scheduleStallTimer()
    }

    @MainThread
    fun onPlayingStarted() {
        cancelStallTimer()
        if (state != FailoverState.PLAYING) setState(FailoverState.PLAYING, null)
    }

    @MainThread
    fun release() {
        cancelAll()
        engineScope.cancel()
        channel = null
        state = FailoverState.IDLE
    }

    fun currentState(): FailoverState = state

    // ─── Race motoru ─────────────────────────────────────────────────────────

    private fun raceAndPlay(streams: List<Stream>) {
        val candidates = streams.filter { it.url !in usedUrls }
        if (candidates.isEmpty()) {
            Log.e(TAG, "Tüm URL'ler denendi — ${RETRY_DELAY_MS / 1000}s sonra tekrar")
            setState(FailoverState.ALL_FAILED, null)
            scheduleRetry()
            return
        }

        probeJob = engineScope.launch {
            Log.i(TAG, "Race başladı: ${candidates.size} URL eş zamanlı probe ediliyor")
            val winner = withTimeoutOrNull(PROBE_TIMEOUT_MS) {
                raceStreams(candidates)
            }

            withContext(Dispatchers.Main) {
                if (winner != null) {
                    usedUrls.add(winner.url)
                    Log.i(TAG, "Race kazananı: ${winner.label} — ${winner.url}")
                    if (state != FailoverState.LOADING) setState(FailoverState.SWITCHING, winner.label)
                    dispatchStream(winner)
                } else {
                    val fallback = candidates.firstOrNull()
                    if (fallback != null) {
                        Log.w(TAG, "Probe timeout — fallback: ${fallback.label}")
                        usedUrls.add(fallback.url)
                        dispatchStream(fallback)
                    } else {
                        setState(FailoverState.ALL_FAILED, null)
                        scheduleRetry()
                    }
                }
            }
        }
    }

    // ─── Paralel HTTP probe ───────────────────────────────────────────────────

    private suspend fun raceStreams(streams: List<Stream>): Stream? {
        if (streams.isEmpty()) return null
        if (streams.size == 1) return if (probeUrl(streams[0].url)) streams[0] else null

        return withContext(Dispatchers.IO) {
            val resultCh = KChannel<Pair<Stream, Boolean>>(capacity = streams.size)
            val jobs = streams.map { stream ->
                launch {
                    val alive = probeUrl(stream.url)
                    resultCh.trySend(stream to alive)
                }
            }

            var winner: Stream? = null
            var received = 0
            while (received < streams.size && winner == null) {
                val (stream, alive) = resultCh.receive()
                received++
                if (alive) winner = stream
            }
            jobs.forEach { it.cancel() }
            winner
        }
    }

    private suspend fun probeUrl(url: String): Boolean = withContext(Dispatchers.IO) {
        try {
            val conn = URL(url).openConnection() as HttpURLConnection
            conn.requestMethod = "HEAD"
            conn.connectTimeout = PROBE_TIMEOUT_MS.toInt()
            conn.readTimeout    = PROBE_TIMEOUT_MS.toInt()
            conn.setRequestProperty("User-Agent", "KotakTV/2.0 (Android TV)")
            conn.connect()
            val code = conn.responseCode
            conn.disconnect()
            code in 200..399
        } catch (e: Exception) {
            Log.d(TAG, "Probe başarısız: $url — ${e.message}")
            false
        }
    }

    // ─── Failover tetikleyici ────────────────────────────────────────────────

    private fun debounceFailover(reason: String) {
        if (state == FailoverState.ALL_FAILED) return
        val now = System.currentTimeMillis()
        if (now - lastFailoverAt < DEBOUNCE_MS) return
        lastFailoverAt = now
        cancelStallTimer()
        probeJob?.cancel()
        val ch = channel ?: return
        Log.i(TAG, "Failover tetiklendi ($reason) — kalan URL'ler race ediliyor")
        setState(FailoverState.SWITCHING, null)
        raceAndPlay(ch.sortedStreams)
    }

    // ─── Stream gönderme ─────────────────────────────────────────────────────

    private fun dispatchStream(stream: Stream) {
        onSwitchStream(stream, buildMediaItem(stream))
    }

    private fun buildMediaItem(stream: Stream): MediaItem {
        val builder = MediaItem.Builder().setUri(stream.url)
        stream.drm?.let { drm ->
            if (drm.type.equals("widevine", ignoreCase = true)) {
                builder.setDrmConfiguration(
                    MediaItem.DrmConfiguration.Builder(C.WIDEVINE_UUID)
                        .setLicenseUri(drm.licenseUrl)
                        .apply { if (drm.headers.isNotEmpty()) setLicenseRequestHeaders(drm.headers) }
                        .build()
                )
            }
        }
        if (stream.headers.isNotEmpty()) {
            builder.setRequestMetadata(
                MediaItem.RequestMetadata.Builder()
                    .setExtras(android.os.Bundle().apply {
                        stream.headers.forEach { (k, v) -> putString(k, v) }
                    }).build()
            )
        }
        return builder.build()
    }

    // ─── Zamanlayıcılar ──────────────────────────────────────────────────────

    private fun scheduleStallTimer() {
        cancelStallTimer()
        stallRunnable = Runnable {
            Log.w(TAG, "Stall tespiti — ${STALL_TIMEOUT_MS / 1000}s buffer'da kaldı")
            debounceFailover("StallTimeout")
        }.also { mainHandler.postDelayed(it, STALL_TIMEOUT_MS) }
    }

    private fun cancelStallTimer() {
        stallRunnable?.let { mainHandler.removeCallbacks(it) }
        stallRunnable = null
    }

    private fun scheduleRetry() {
        cancelRetryTimer()
        retryRunnable = Runnable {
            Log.i(TAG, "Auto-retry: tüm URL'ler sıfırlanıyor")
            channel?.let { loadChannel(it) }
        }.also { mainHandler.postDelayed(it, RETRY_DELAY_MS) }
    }

    private fun cancelRetryTimer() {
        retryRunnable?.let { mainHandler.removeCallbacks(it) }
        retryRunnable = null
    }

    private fun cancelAll() {
        cancelStallTimer()
        cancelRetryTimer()
        probeJob?.cancel()
        probeJob = null
    }

    private fun setState(newState: FailoverState, label: String?) {
        state = newState
        onStateChanged(newState, label)
    }
}

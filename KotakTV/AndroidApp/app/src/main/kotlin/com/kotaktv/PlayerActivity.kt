package com.kotaktv

import android.media.AudioManager
import android.os.Bundle
import android.view.KeyEvent
import android.view.View
import android.view.WindowManager
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.media3.common.AudioAttributes
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.DefaultDataSource
import androidx.media3.datasource.okhttp.OkHttpDataSource
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.ui.PlayerView
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import com.kotaktv.data.Channel
import com.kotaktv.data.ChannelRepository
import com.kotaktv.data.Stream
import com.kotaktv.failover.FailoverEngine
import com.kotaktvapp.BuildConfig
import com.kotaktvapp.R
import kotlinx.coroutines.launch

/**
 * Klasik TV modu: uygulama açılır açılmaz ilk kanalı (TRT 1) tam ekran oynatır.
 *
 * Kumanda haritası:
 *   YUKARI      → bir sonraki kanal (zapping)
 *   AŞAĞI       → bir önceki kanal (zapping)
 *   SOL         → ses azalt
 *   SAĞ         → ses artır
 *   GERİ        → uygulamayı kapat (TV ana ekranına çık)
 */
@UnstableApi
class PlayerActivity : AppCompatActivity(), Player.Listener {

    private lateinit var playerView: PlayerView
    private lateinit var channelNameOverlay: TextView

    private lateinit var player: ExoPlayer
    private lateinit var failoverEngine: FailoverEngine
    private lateinit var audioManager: AudioManager
    private val repository = ChannelRepository()

    private var channels: List<Channel> = emptyList()
    private var currentChannelIndex: Int = 0

    private val hideChannelName = Runnable { channelNameOverlay.visibility = View.GONE }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContentView(R.layout.activity_player)

        playerView = findViewById(R.id.player_view)
        channelNameOverlay = findViewById(R.id.channel_name_overlay)
        audioManager = getSystemService(AUDIO_SERVICE) as AudioManager

        initFailoverEngine()
        initExoPlayer()
        loadAndStart()
    }

    override fun onStart() {
        super.onStart()
        if (::player.isInitialized) player.play()
    }

    override fun onStop() {
        super.onStop()
        if (::player.isInitialized) player.pause()
    }

    override fun onDestroy() {
        super.onDestroy()
        channelNameOverlay.removeCallbacks(hideChannelName)
        if (::player.isInitialized) {
            player.removeListener(this)
            player.release()
        }
        if (::failoverEngine.isInitialized) failoverEngine.release()
    }

    // ─── Kumanda girişi ──────────────────────────────────────────────────────

    override fun onKeyDown(keyCode: Int, event: KeyEvent?): Boolean {
        return when (keyCode) {
            KeyEvent.KEYCODE_DPAD_UP    -> { zapChannel(+1); true }
            KeyEvent.KEYCODE_DPAD_DOWN  -> { zapChannel(-1); true }
            KeyEvent.KEYCODE_DPAD_RIGHT -> {
                audioManager.adjustStreamVolume(
                    AudioManager.STREAM_MUSIC, AudioManager.ADJUST_RAISE, AudioManager.FLAG_SHOW_UI
                )
                true
            }
            KeyEvent.KEYCODE_DPAD_LEFT  -> {
                audioManager.adjustStreamVolume(
                    AudioManager.STREAM_MUSIC, AudioManager.ADJUST_LOWER, AudioManager.FLAG_SHOW_UI
                )
                true
            }
            KeyEvent.KEYCODE_BACK       -> { finish(); true }
            else                         -> super.onKeyDown(keyCode, event)
        }
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() = finish()

    // ─── Başlatma ────────────────────────────────────────────────────────────

    private fun initFailoverEngine() {
        failoverEngine = FailoverEngine(
            onSwitchStream = ::applyStream,
            onStateChanged = { _, _ -> }   // tüm failover arka planda, sessiz
        )
    }

    private fun initExoPlayer() {
        val httpClient = OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()
        val dataSourceFactory = DefaultDataSource.Factory(
            this, OkHttpDataSource.Factory(httpClient)
        )
        val audioAttrs = AudioAttributes.Builder()
            .setUsage(C.USAGE_MEDIA)
            .setContentType(C.AUDIO_CONTENT_TYPE_MOVIE)
            .build()
        player = ExoPlayer.Builder(this)
            .setMediaSourceFactory(DefaultMediaSourceFactory(dataSourceFactory))
            .setAudioAttributes(audioAttrs, true)
            .build()
            .also { exo ->
                playerView.player = exo
                playerView.useController = false
                exo.addListener(this)
                exo.playWhenReady = true
            }
    }

    // ─── Kanal yükleme ve oynatma ────────────────────────────────────────────

    private fun loadAndStart() {
        lifecycleScope.launch {
            repository.getChannels(BuildConfig.KANALLAR_JSON_URL).fold(
                onSuccess = { list ->
                    channels = list
                    currentChannelIndex = 0
                    playCurrentChannel()
                },
                onFailure = { /* siyah ekran — bir sonraki açılışta tekrar dener */ }
            )
        }
    }

    private fun zapChannel(direction: Int) {
        if (channels.isEmpty()) return
        currentChannelIndex = (currentChannelIndex + direction + channels.size) % channels.size
        playCurrentChannel()
    }

    private fun playCurrentChannel() {
        val channel = channels.getOrNull(currentChannelIndex) ?: return
        showChannelName(channel.name)
        failoverEngine.loadChannel(channel)
    }

    private fun showChannelName(name: String) {
        channelNameOverlay.text = name
        channelNameOverlay.visibility = View.VISIBLE
        channelNameOverlay.removeCallbacks(hideChannelName)
        channelNameOverlay.postDelayed(hideChannelName, 2500L)
    }

    // ─── Stream uygulama ─────────────────────────────────────────────────────

    private fun applyStream(@Suppress("UNUSED_PARAMETER") stream: Stream, mediaItem: MediaItem) {
        player.stop()
        player.setMediaItem(mediaItem)
        player.prepare()
        player.playWhenReady = true
    }

    // ─── Player.Listener ─────────────────────────────────────────────────────

    override fun onPlayerError(error: PlaybackException) {
        failoverEngine.onPlayerError()
    }

    override fun onPlaybackStateChanged(playbackState: Int) {
        when (playbackState) {
            Player.STATE_BUFFERING -> failoverEngine.onBufferingStarted()
            Player.STATE_READY     -> failoverEngine.onPlayingStarted()
            Player.STATE_ENDED     -> failoverEngine.onPlayerError()
            else                   -> {}
        }
    }
}

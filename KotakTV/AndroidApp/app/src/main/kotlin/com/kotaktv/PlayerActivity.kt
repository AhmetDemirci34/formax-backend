package com.kotaktv

import android.os.Bundle
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
import com.kotaktv.failover.FailoverState
import com.kotaktvapp.BuildConfig
import com.kotaktvapp.R
import kotlinx.coroutines.launch

/**
 * Android TV ana oynatıcı ekranı.
 *
 * Sorumluluğu:
 *  1. Kanal listesini ChannelRepository üzerinden indirir.
 *  2. FailoverEngine'i ExoPlayer ile köprüler.
 *  3. Failover sırasında kullanıcıya "Kanal güncelleniyor..." overlay'i gösterir.
 *  4. Player.Listener olaylarını (hata, donma, buffer) FailoverEngine'e iletir.
 *
 * Intent parametreleri:
 *  EXTRA_CHANNEL_ID  → Açılacak kanalın ID'si (ör. "trt1")
 *  EXTRA_JSON_URL    → Alternatif kanallar.json URL'si (opsiyonel)
 */
@UnstableApi
class PlayerActivity : AppCompatActivity(), Player.Listener {

    // ─── Görünümler ──────────────────────────────────────────────────────────
    private lateinit var playerView: PlayerView
    private lateinit var overlayContainer: View
    private lateinit var overlayMessage: TextView
    private lateinit var overlayProgressBar: View

    // ─── Motor bileşenleri ───────────────────────────────────────────────────
    private lateinit var player: ExoPlayer
    private lateinit var failoverEngine: FailoverEngine
    private val repository = ChannelRepository()

    companion object {
        const val EXTRA_CHANNEL_ID = "extra_channel_id"
        const val EXTRA_JSON_URL   = "extra_json_url"
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Tam ekran, her zaman açık (Android TV için standart)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        setContentView(R.layout.activity_player)
        bindViews()
        initFailoverEngine()
        initExoPlayer()
        loadChannelList()
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
        if (::player.isInitialized) {
            player.removeListener(this)
            player.release()
        }
        if (::failoverEngine.isInitialized) failoverEngine.release()
    }

    // ─── Başlatma ────────────────────────────────────────────────────────────

    private fun bindViews() {
        playerView         = findViewById(R.id.player_view)
        overlayContainer   = findViewById(R.id.overlay_container)
        overlayMessage     = findViewById(R.id.overlay_message)
        overlayProgressBar = findViewById(R.id.overlay_progress)
    }

    private fun initFailoverEngine() {
        failoverEngine = FailoverEngine(
            onSwitchStream  = ::applyStream,
            onStateChanged  = ::handleFailoverState
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
            .setAudioAttributes(audioAttrs, /* handleAudioFocus= */ true)
            .build()
            .also { exo ->
                playerView.player = exo
                playerView.useController = false
                exo.addListener(this)
                exo.playWhenReady = true
            }
    }

    // ─── Kanal yükleme ───────────────────────────────────────────────────────

    private fun loadChannelList() {
        val jsonUrl   = intent.getStringExtra(EXTRA_JSON_URL) ?: BuildConfig.KANALLAR_JSON_URL
        val channelId = intent.getStringExtra(EXTRA_CHANNEL_ID)

        showOverlay(message = "Kanal listesi alınıyor...", showSpinner = true)

        lifecycleScope.launch {
            repository.getChannels(jsonUrl).fold(
                onSuccess = { channels ->
                    val target = resolveChannel(channels, channelId)
                    if (target != null) {
                        hideOverlay()
                        failoverEngine.loadChannel(target)
                    } else {
                        showOverlay("Kanal bulunamadı.", showSpinner = false)
                    }
                },
                onFailure = { err ->
                    showOverlay(
                        "Kanal listesi yüklenemedi.\n${err.message}",
                        showSpinner = false
                    )
                }
            )
        }
    }

    private fun resolveChannel(channels: List<Channel>, channelId: String?): Channel? =
        if (channelId != null) channels.find { it.id == channelId } ?: channels.firstOrNull()
        else channels.firstOrNull()

    // ─── Stream uygulama (FailoverEngine → ExoPlayer) ────────────────────────

    /**
     * FailoverEngine'in belirlediği stream'i ExoPlayer'a yükler.
     * Canlı yayın olduğu için mevcut pozisyon korunmaz; yayın canlı ucundan başlar.
     */
    private fun applyStream(stream: Stream, mediaItem: MediaItem) {
        player.stop()
        player.setMediaItem(mediaItem)
        player.prepare()
        player.playWhenReady = true
    }

    // ─── FailoverEngine durum yöneticisi ─────────────────────────────────────

    private fun handleFailoverState(state: FailoverState, streamLabel: String?) {
        when (state) {
            FailoverState.LOADING ->
                showOverlay("Kanal yükleniyor...", showSpinner = true)

            FailoverState.SWITCHING ->
                showOverlay("Kanal güncelleniyor, lütfen bekleyiniz...", showSpinner = true)

            FailoverState.PLAYING ->
                hideOverlay()

            FailoverState.ALL_FAILED ->
                showOverlay(
                    "Tüm yayın kaynakları şu an kullanılamıyor.\n" +
                    "Lütfen daha sonra tekrar deneyin.",
                    showSpinner = false
                )

            FailoverState.IDLE -> { /* no-op */ }
        }
    }

    // ─── Player.Listener ─────────────────────────────────────────────────────

    /**
     * Fatal oynatıcı hatası → FailoverEngine devreye girer.
     */
    override fun onPlayerError(error: PlaybackException) {
        failoverEngine.onPlayerError()
    }

    /**
     * Oynatma durumu değişimi:
     *  STATE_BUFFERING → donma zamanlayıcısı başlatılır
     *  STATE_READY     → sağlıklı oynatma onaylanır, overlay gizlenir
     *  STATE_ENDED     → canlı yayın beklenmedik bitti → yedek dene
     */
    override fun onPlaybackStateChanged(playbackState: Int) {
        when (playbackState) {
            Player.STATE_BUFFERING -> failoverEngine.onBufferingStarted()
            Player.STATE_READY     -> failoverEngine.onPlayingStarted()
            Player.STATE_ENDED     -> failoverEngine.onPlayerError()
            Player.STATE_IDLE      -> { /* onPlayerError zaten yönetiyor */ }
        }
    }

    // ─── Overlay UI ──────────────────────────────────────────────────────────

    private fun showOverlay(message: String, showSpinner: Boolean) {
        overlayContainer.visibility = View.VISIBLE
        overlayMessage.text = message
        overlayProgressBar.visibility = if (showSpinner) View.VISIBLE else View.GONE
    }

    private fun hideOverlay() {
        overlayContainer.visibility = View.GONE
    }
}

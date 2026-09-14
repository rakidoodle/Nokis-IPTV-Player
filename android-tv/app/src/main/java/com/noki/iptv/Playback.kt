package com.noki.iptv

import android.content.Context
import android.net.Uri
import android.os.SystemClock
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.datasource.DefaultDataSource
import androidx.media3.datasource.DefaultHttpDataSource
import androidx.media3.exoplayer.DefaultLoadControl
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.session.MediaSession
import kotlinx.coroutines.*

/** A monotonic clock avoids recovery changes when device wall time changes. */
class PlaybackHealth {
    var attempts = 0
    private var lastPosition = -1L
    private var lastProgress = 0L
    private var startup = 0L
    private var healthySince = 0L
    private var hasProgress = false

    fun started(now: Long) {
        lastPosition = -1
        lastProgress = now
        startup = now
        healthySince = 0
        hasProgress = false
    }

    fun seek(now: Long) {
        lastProgress = now
        healthySince = 0
    }

    fun evaluate(
        now: Long,
        position: Long,
        paused: Boolean,
        ready: Boolean,
        ended: Boolean = false,
        live: Boolean = false,
    ): Boolean {
        if (ended && live && !paused) return true
        if (paused || ended) {
            lastProgress = now
            startup = now
            healthySince = 0
            return false
        }
        if (position != lastPosition && ready) {
            if (lastPosition >= 0) hasProgress = true
            lastPosition = position
            lastProgress = now
            if (healthySince == 0L) healthySince = now
            if (now - healthySince >= 30_000) attempts = 0
        } else if (!ready) healthySince = 0
        return if (hasProgress) now - lastProgress >= 8_000 else now - startup >= 20_000
    }

    fun consumeRetry(): Boolean {
        if (attempts >= 3) return false
        attempts++
        return true
    }
}

@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
class Playback(
    private val context: Context,
    private val scope: CoroutineScope,
    private val savePosition: (String, Long) -> Unit,
) {
    private val health = PlaybackHealth()
    private var job: Job? = null
    private var watchdog: Job? = null
    private var provider: Provider? = null
    var item by mutableStateOf<Media?>(null)
        private set

    var error by mutableStateOf<String?>(null)
        private set

    var status by mutableStateOf("")
        private set

    var reconnecting by mutableStateOf(false)
        private set

    private var active = false
    private var shouldPlay = true
    private var subtitle: Uri? = null
    private var resumePosition = 0L
    private var lastSaved = 0L
    val engine =
        ExoPlayer.Builder(context)
            .setLoadControl(
                DefaultLoadControl.Builder().setBufferDurationsMs(5000, 30000, 1500, 3000).build()
            )
            .build()
    private val session = MediaSession.Builder(context, engine).build()

    init {
        engine.addListener(
            object : Player.Listener {
                override fun onPlayWhenReadyChanged(playWhenReady: Boolean, reason: Int) {
                    shouldPlay = playWhenReady
                }

                override fun onPlayerError(e: PlaybackException) {
                    if (active) recover()
                }

                override fun onPlaybackStateChanged(state: Int) {
                    status =
                        when (state) {
                            Player.STATE_BUFFERING ->
                                if (reconnecting) "Reconnecting…" else "Buffering…"
                            Player.STATE_ENDED ->
                                if (item?.kind == Kind.LIVE && active) "Reconnecting…"
                                else "Finished"
                            else -> ""
                        }
                    if (state == Player.STATE_READY) reconnecting = false
                }

                override fun onPositionDiscontinuity(
                    old: Player.PositionInfo,
                    new: Player.PositionInfo,
                    reason: Int,
                ) {
                    if (reason == Player.DISCONTINUITY_REASON_SEEK)
                        health.seek(SystemClock.elapsedRealtime())
                }
            }
        )
        watchdog =
            scope.launch {
                while (isActive) {
                    delay(500)
                    if (active && job?.isActive != true) {
                        if (
                            (engine.playerError != null && shouldPlay) ||
                                health.evaluate(
                                    SystemClock.elapsedRealtime(),
                                    engine.currentPosition,
                                    !engine.playWhenReady,
                                    engine.playbackState == Player.STATE_READY,
                                    engine.playbackState == Player.STATE_ENDED,
                                    item?.kind == Kind.LIVE,
                                )
                        )
                            recover()
                        val current = item
                        if (
                            current != null &&
                                current.kind != Kind.LIVE &&
                                engine.playbackState == Player.STATE_READY &&
                                SystemClock.elapsedRealtime() - lastSaved > 10_000
                        ) {
                            lastSaved = SystemClock.elapsedRealtime()
                            savePosition(current.id, engine.currentPosition)
                        }
                    }
                }
            }
    }

    fun play(media: Media, p: Provider, position: Long = 0) {
        stop()
        item = media
        provider = p
        subtitle = null
        resumePosition = position
        health.attempts = 0
        active = true
        shouldPlay = true
        start()
    }

    private fun start(retryDelay: Long = 0) {
        val current = item ?: return
        val p = provider ?: return
        error = null
        job =
            scope.launch {
                try {
                    if (retryDelay > 0) delay(retryDelay)
                    status = if (reconnecting) "Reconnecting…" else "Opening stream…"
                    val stream =
                        withTimeout(30_000) { withContext(Dispatchers.IO) { p.resolve(current) } }
                    ensureActive()
                    require(
                        Uri.parse(stream.url).scheme in listOf("http", "https", "rtsp", "content")
                    ) {
                        "This stream protocol is not supported by this build."
                    }
                    val http =
                        DefaultHttpDataSource.Factory()
                            .setUserAgent("NokiIPTV/0.1.1 AndroidTV")
                            .setConnectTimeoutMs(15_000)
                            .setReadTimeoutMs(20_000)
                            .setAllowCrossProtocolRedirects(true)
                            .setDefaultRequestProperties(stream.headers)
                    val factory =
                        DefaultMediaSourceFactory(DefaultDataSource.Factory(context, http))
                    val builder = MediaItem.Builder().setMediaId(current.id).setUri(stream.url)
                    subtitle?.let { uri ->
                        val suffix =
                            runCatching {
                                    context.contentResolver
                                        .query(
                                            uri,
                                            arrayOf(android.provider.OpenableColumns.DISPLAY_NAME),
                                            null,
                                            null,
                                            null,
                                        )
                                        ?.use { cursor ->
                                            if (cursor.moveToFirst()) cursor.getString(0) else null
                                        }
                                }
                                .getOrNull()
                                ?.lowercase() ?: uri.toString().substringBefore('?').lowercase()
                        builder.setSubtitleConfigurations(
                            listOf(
                                MediaItem.SubtitleConfiguration.Builder(uri)
                                    .setMimeType(
                                        if (suffix.endsWith(".srt")) MimeTypes.APPLICATION_SUBRIP
                                        else if (suffix.endsWith(".ass") || suffix.endsWith(".ssa"))
                                            MimeTypes.TEXT_SSA
                                        else MimeTypes.TEXT_VTT
                                    )
                                    .setLanguage("und")
                                    .setLabel("External subtitles")
                                    .setSelectionFlags(
                                        androidx.media3.common.C.SELECTION_FLAG_DEFAULT
                                    )
                                    .build()
                            )
                        )
                    }
                    engine.setMediaSource(factory.createMediaSource(builder.build()))
                    engine.prepare()
                    if (current.kind != Kind.LIVE && resumePosition > 0)
                        engine.seekTo(resumePosition)
                    engine.playWhenReady = shouldPlay
                    health.started(SystemClock.elapsedRealtime())
                } catch (e: Exception) {
                    if (e is CancellationException && e !is TimeoutCancellationException) throw e
                    if (reconnecting && e !is IllegalArgumentException && health.attempts < 3) {
                        // A failed fresh-link request must not terminate an otherwise recoverable
                        // session.
                        health.attempts++
                        start(1000L shl (health.attempts - 1))
                        return@launch
                    }
                    active = false
                    reconnecting = false
                    error =
                        if (e is IllegalArgumentException) e.message
                        else
                            "Unable to open this stream. Check your provider or network, then retry."
                    status = ""
                }
            }
    }

    private fun recover() {
        if (job?.isActive == true) return
        if (!health.consumeRetry()) {
            active = false
            engine.pause()
            reconnecting = false
            error =
                "Playback could not recover after three attempts. Check your network or try another channel."
            return
        }
        resumePosition =
            if (item?.kind != Kind.LIVE && !engine.currentTimeline.isEmpty)
                engine.currentPosition.coerceAtLeast(0)
            else if (item?.kind == Kind.LIVE) 0 else resumePosition
        reconnecting = true
        engine.stop()
        start(1000L shl (health.attempts - 1))
    }

    fun retry() {
        job?.cancel()
        health.attempts = 0
        active = true
        shouldPlay = true
        if (item?.kind != Kind.LIVE && !engine.currentTimeline.isEmpty)
            resumePosition = engine.currentPosition.coerceAtLeast(0)
        engine.stop()
        start()
    }

    fun setSubtitle(uri: Uri) {
        subtitle = uri
        resumePosition = engine.currentPosition
        job?.cancel()
        engine.stop()
        start()
    }

    fun pauseForBackground() {
        shouldPlay = false
        engine.pause()
    }

    fun togglePause() {
        if (engine.isPlaying || engine.playWhenReady) engine.pause() else engine.play()
    }

    fun stop() {
        item?.takeIf { it.kind != Kind.LIVE }?.let { savePosition(it.id, engine.currentPosition) }
        active = false
        job?.cancel()
        engine.stop()
        engine.clearMediaItems()
        item = null
        error = null
        reconnecting = false
        status = ""
    }

    fun release() {
        stop()
        watchdog?.cancel()
        session.release()
        engine.release()
    }
}

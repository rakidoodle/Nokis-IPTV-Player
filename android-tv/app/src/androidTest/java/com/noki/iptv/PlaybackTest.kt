package com.noki.iptv

import android.graphics.SurfaceTexture
import android.view.Surface
import androidx.media3.common.Player
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.*
import okhttp3.mockwebserver.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
class PlaybackTest {
    @Test
    fun actualVideoPauseSeekAndResume() {
        val ins = InstrumentationRegistry.getInstrumentation()
        val bytes = ins.context.assets.open("sample.mp4").use { it.readBytes() }
        MockWebServer().use { server ->
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest): MockResponse {
                        val start =
                            request
                                .getHeader("Range")
                                ?.substringAfter("bytes=")
                                ?.substringBefore('-')
                                ?.toIntOrNull() ?: 0
                        if (start >= bytes.size) return MockResponse().setResponseCode(416)
                        return MockResponse()
                            .setResponseCode(if (start > 0) 206 else 200)
                            .setHeader("Content-Type", "video/mp4")
                            .setHeader("Accept-Ranges", "bytes")
                            .apply {
                                if (start > 0)
                                    setHeader(
                                        "Content-Range",
                                        "bytes $start-${bytes.size-1}/${bytes.size}",
                                    )
                            }
                            .setBody(okio.Buffer().write(bytes, start, bytes.size - start))
                    }
                }
            server.start()
            val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
            lateinit var playback: Playback
            lateinit var surface: Surface
            lateinit var texture: SurfaceTexture
            val rendered = CountDownLatch(1)
            val ready = CountDownLatch(1)
            val item =
                Media(
                    "fixture",
                    "test",
                    "one",
                    "Playback fixture",
                    Kind.MOVIE,
                    location = server.url("/sample.mp4").toString(),
                )
            val provider =
                object : Provider {
                    override suspend fun catalog() = Catalog(listOf(item))

                    override suspend fun episodes(item: Media) = emptyList<Media>()

                    override suspend fun resolve(item: Media) = Stream(item.location)

                    override suspend fun guide(items: List<Media>, url: String) =
                        emptyList<Programme>()
                }
            ins.runOnMainSync {
                texture = SurfaceTexture(false)
                surface = Surface(texture)
                playback = Playback(ins.targetContext, scope) { _, _ -> }
                playback.engine.setVideoSurface(surface)
                playback.engine.addListener(
                    object : Player.Listener {
                        override fun onRenderedFirstFrame() {
                            rendered.countDown()
                        }

                        override fun onPlaybackStateChanged(state: Int) {
                            if (state == Player.STATE_READY) ready.countDown()
                        }
                    }
                )
                playback.play(item, provider)
            }
            try {
                assertTrue("Video never became ready", ready.await(15, TimeUnit.SECONDS))
                assertTrue("No video frame rendered", rendered.await(10, TimeUnit.SECONDS))
                ins.runOnMainSync {
                    assertNotNull(playback.engine.videoFormat)
                    assertNotNull(playback.engine.audioFormat)
                    playback.engine.pause()
                    assertFalse(playback.engine.playWhenReady)
                    playback.engine.seekTo(5000)
                }
                Thread.sleep(1200)
                ins.runOnMainSync {
                    assertTrue(playback.engine.currentPosition >= 4900)
                    assertNull(playback.error)
                    playback.engine.play()
                }
                Thread.sleep(1000)
                ins.runOnMainSync {
                    assertTrue(playback.engine.currentPosition > 5000)
                    playback.stop()
                    assertNull(playback.item)
                }
            } finally {
                ins.runOnMainSync {
                    playback.release()
                    surface.release()
                    texture.release()
                    scope.cancel()
                }
            }
        }
    }

    @Test
    fun earlyLiveEndReconnectsAndStopsAfterRetryBudget() {
        val ins = InstrumentationRegistry.getInstrumentation()
        val bytes = ins.context.assets.open("sample.mp4").use { it.readBytes() }
        MockWebServer().use { server ->
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest) =
                        MockResponse()
                            .setHeader("Content-Type", "video/mp4")
                            .setBody(okio.Buffer().write(bytes))
                }
            server.start()
            val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
            val resolutions = java.util.concurrent.atomic.AtomicInteger()
            val finished = CountDownLatch(1)
            val item =
                Media(
                    "live-fixture",
                    "test",
                    "live",
                    "Short live connection",
                    Kind.LIVE,
                    location = server.url("/live").toString(),
                )
            val provider =
                object : Provider {
                    override suspend fun catalog() = Catalog(listOf(item))

                    override suspend fun episodes(item: Media) = emptyList<Media>()

                    override suspend fun guide(items: List<Media>, url: String) =
                        emptyList<Programme>()

                    override suspend fun resolve(item: Media): Stream {
                        if (resolutions.incrementAndGet() == 2)
                            throw java.io.IOException("Fixture link refresh interrupted")
                        return Stream(item.location)
                    }
                }
            lateinit var playback: Playback
            lateinit var surface: Surface
            lateinit var texture: SurfaceTexture
            val trace = java.util.concurrent.CopyOnWriteArrayList<String>()
            var lastSeek = 0
            ins.runOnMainSync {
                texture = SurfaceTexture(false)
                surface = Surface(texture)
                playback = Playback(ins.targetContext, scope) { _, _ -> }
                playback.engine.setVideoSurface(surface)
                playback.engine.addListener(
                    object : Player.Listener {
                        override fun onPlaybackStateChanged(state: Int) {
                            trace.add(
                                "state=$state links=${resolutions.get()} pos=${playback.engine.currentPosition} play=${playback.engine.playWhenReady}"
                            )
                            if (state == Player.STATE_READY && resolutions.get() != lastSeek) {
                                lastSeek = resolutions.get()
                                playback.engine.seekTo(19_800)
                            }
                        }
                    }
                )
                playback.play(item, provider)
                scope.launch {
                    while (isActive) {
                        delay(100)
                        if (playback.error != null) {
                            finished.countDown()
                            break
                        }
                    }
                }
            }
            try {
                val complete = finished.await(30, TimeUnit.SECONDS)
                assertTrue("Early live EOF did not reach bounded retries: $trace", complete)
                assertEquals("Initial stream plus three fresh links", 4, resolutions.get())
                ins.runOnMainSync {
                    assertTrue(playback.error.orEmpty().contains("three attempts"))
                    assertFalse(playback.engine.playWhenReady)
                }
            } finally {
                ins.runOnMainSync {
                    playback.release()
                    surface.release()
                    texture.release()
                    scope.cancel()
                }
            }
        }
    }
}

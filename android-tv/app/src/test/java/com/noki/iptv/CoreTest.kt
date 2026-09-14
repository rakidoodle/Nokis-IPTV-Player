package com.noki.iptv

import java.io.ByteArrayOutputStream
import java.util.zip.GZIPOutputStream
import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.*
import org.junit.Test

class CoreTest {
    private val playlist =
        """
        #EXTM3U x-tvg-url="https://guide.example/epg.xml.gz"
        #EXTINF:-1 tvg-id="news" group-title="News, World" tvg-logo="https://img.example/a.png",News HD
        #EXTVLCOPT:http-user-agent=Noki Test
        https://streams.example/live.ts
        #EXTINF:-1 group-title='Cinema',A Film
        movie.mp4|Referer=https%3A%2F%2Fexample.com
    """
            .trimIndent()

    @Test
    fun playlistMetadataHeadersAndRelativeUrls() {
        val c = M3U.parse(playlist, "a", "https://streams.example/list.m3u")
        assertEquals(2, c.items.size)
        assertEquals("News, World", c.items[0].group)
        assertEquals("Noki Test", c.items[0].headers["User-Agent"])
        assertEquals(Kind.MOVIE, c.items[1].kind)
        assertEquals("https://streams.example/movie.mp4", c.items[1].location)
        assertEquals("https://example.com", c.items[1].headers["Referer"])
        assertTrue(c.epgUrl.endsWith(".gz"))
    }

    @Test
    fun playlistCanSkipVodAndOldSourcesKeepDefaults() {
        val catalog =
            M3U.parse(playlist.reader().buffered(), "a", "https://streams.example/list.m3u", false)
        assertEquals(listOf(Kind.LIVE), catalog.items.map { it.kind })
        val legacy =
            json.decodeFromString<Source>("""{"name":"Old","endpoint":"https://fixture.example"}""")
        assertTrue(legacy.includeVod)
        assertTrue(legacy.includeSeries)
    }

    @Test
    fun xtreamSkipsDisabledCatalogRequests() = runBlocking {
        MockWebServer().use { server ->
            val actions = java.util.concurrent.CopyOnWriteArrayList<String>()
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest): MockResponse {
                        val action = request.requestUrl!!.queryParameter("action").orEmpty()
                        actions += action
                        return MockResponse()
                            .setBody(
                                when (action) {
                                    "" -> """{"user_info":{"auth":1,"status":"Active"}}"""
                                    "get_live_categories" -> "[]"
                                    "get_live_streams" -> """[{"stream_id":1,"name":"Live"}]"""
                                    else -> error("Disabled catalog was requested: $action")
                                }
                            )
                    }
                }
            server.start()
            val catalog =
                XtreamProvider(
                        Source(
                            type = SourceType.XTREAM,
                            endpoint = server.url("/").toString(),
                            includeVod = false,
                            includeSeries = false,
                        ),
                        Network(),
                    )
                    .catalog()
            assertEquals(1, catalog.items.size)
            assertEquals(listOf("", "get_live_categories", "get_live_streams"), actions)
        }
        Unit
    }

    @Test
    fun hlsIsOneStream() {
        val c =
            M3U.parse(
                "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXTINF:10,\npart.ts",
                "a",
                "https://s.example/live.m3u8",
            )
        assertEquals(1, c.items.size)
        assertEquals("https://s.example/live.m3u8", c.items[0].location)
    }

    @Test(expected = IllegalArgumentException::class)
    fun invalidPlaylistRejected() {
        M3U.parse("<html>error</html>", "a", "https://s.example/list")
    }

    @Test
    fun duplicateChannelsCollapse() {
        val c =
            M3U.parse(
                "#EXTM3U\n#EXTINF:-1,One\nhttps://s.example/1\n#EXTINF:-1,One\nhttps://s.example/1",
                "a",
                "https://s.example/list",
            )
        assertEquals(1, c.items.size)
    }

    @Test
    fun sourceIdsKeepFavoritesSeparate() {
        val a = M3U.parse(playlist, "a", "https://s.example/list")
        val b = M3U.parse(playlist, "b", "https://s.example/list")
        assertNotEquals(a.items[0].id, b.items[0].id)
    }

    @Test
    fun bomAndMetadataReset() {
        val c =
            M3U.parse(
                "\uFEFF#EXTM3U\n#EXTINF:-1 tvg-id=\"a\",A\n#EXTVLCOPT:http-user-agent=secret\nhttps://s.example/a\n#EXTINF:-1,B\nhttps://s.example/b",
                "a",
                "https://s.example/list",
            )
        assertTrue(c.items[1].headers.isEmpty())
        assertEquals("", c.items[1].epgId)
    }

    private val xml =
        """<tv><programme channel="news" start="20260909160000 +0800" stop="20260909170000 +0800"><title>News &amp; Weather</title><desc>Today</desc></programme></tv>"""

    @Test
    fun guideTimeZonesAndText() {
        val p = XMLTV.parse(xml.toByteArray()).single()
        assertEquals("News & Weather", p.title)
        assertEquals(XMLTV.date("20260909080000 +0000"), p.start)
        assertEquals("Today", p.detail)
    }

    @Test
    fun guideRejectsExternalEntities() {
        try {
            XMLTV.parse(
                "<!DOCTYPE tv [<!ENTITY leak SYSTEM 'file:///etc/passwd'>]><tv/>".toByteArray()
            )
            fail("XXE accepted")
        } catch (_: Exception) {}
    }

    @Test
    fun compressedGuide() = runBlocking {
        MockWebServer().use { s ->
            val b = ByteArrayOutputStream()
            GZIPOutputStream(b).use { it.write(xml.toByteArray()) }
            s.enqueue(MockResponse().setBody(okio.Buffer().write(b.toByteArray())))
            s.start()
            assertEquals(1, Network().guide(s.url("/guide.gz").toString()).size)
        }
    }

    @Test
    fun sourceValidation() {
        assertEquals(
            "Home",
            Source(name = " Home ", endpoint = "https://example.com/list").validated().name,
        )
        for (s in
            listOf(
                Source(name = "X", endpoint = "file:///etc/passwd"),
                Source(name = "X", type = SourceType.XTREAM, endpoint = "https://example.com"),
                Source(
                    name = "X",
                    type = SourceType.STALKER,
                    endpoint = "https://example.com",
                    mac = "bad",
                ),
            )) {
            try {
                s.validated()
                fail("Invalid source accepted")
            } catch (_: IllegalArgumentException) {}
        }
    }

    @Test
    fun xtreamCatalogEpisodesAndEncodedCredentials() = runBlocking {
        MockWebServer().use { s ->
            s.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(r: RecordedRequest): MockResponse {
                        assertEquals("a+b", r.requestUrl!!.queryParameter("username"))
                        assertEquals("secret&value", r.requestUrl!!.queryParameter("password"))
                        val body =
                            when (r.requestUrl!!.queryParameter("action")) {
                                null -> """{"user_info":{"auth":1,"status":"Active"}}"""
                                "get_live_categories",
                                "get_vod_categories",
                                "get_series_categories" ->
                                    """[{"category_id":"1","category_name":"Test"}]"""
                                "get_live_streams" ->
                                    """[{"stream_id":1,"name":"Live","category_id":"1"}]"""
                                "get_vod_streams" ->
                                    """[{"stream_id":2,"name":"Film","category_id":"1","container_extension":"mkv"}]"""
                                "get_series" ->
                                    """[{"series_id":3,"name":"Series","category_id":"1"}]"""
                                "get_series_info" ->
                                    """{"episodes":{"2":[{"id":"9","title":"Episode","episode_num":1}]}}"""
                                else -> "[]"
                            }
                        return MockResponse().setBody(body)
                    }
                }
            s.start()
            val p =
                XtreamProvider(
                    Source(
                        id = "xc",
                        name = "X",
                        type = SourceType.XTREAM,
                        endpoint = s.url("/").toString(),
                        username = "a+b",
                        password = "secret&value",
                    ),
                    Network(),
                )
            val c = p.catalog()
            assertEquals(3, c.items.size)
            assertTrue(c.items[1].location.endsWith("2.mkv"))
            val eps = p.episodes(c.items[2])
            assertEquals(2, eps.single().season)
            assertTrue(eps.single().location.contains("/series/"))
        }
    }

    @Test
    fun xtreamRejectedLoginDoesNotExposeCredentials() = runBlocking {
        MockWebServer().use { s ->
            s.enqueue(MockResponse().setBody("""{"user_info":{"auth":0}}"""))
            s.start()
            try {
                XtreamProvider(
                        Source(name = "x", endpoint = s.url("/").toString(), password = "private"),
                        Network(),
                    )
                    .catalog()
                fail("Login accepted")
            } catch (e: IllegalArgumentException) {
                assertFalse(e.message.orEmpty().contains("private"))
            }
        }
    }

    @Test
    fun stalkerPaginationAndFreshLink() = runBlocking {
        MockWebServer().use { s ->
            s.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(r: RecordedRequest): MockResponse {
                        val q = r.requestUrl!!
                        val value =
                            when (q.queryParameter("action")) {
                                "handshake" -> """{"token":"abc"}"""
                                "get_profile" -> """{"id":1}"""
                                "get_genres",
                                "get_categories" -> "[]"
                                "get_all_channels" ->
                                    """{"data":[{"id":"1","name":"News","cmd":"ffmpeg http://localhost/live"}]}"""
                                "get_ordered_list" ->
                                    if (q.queryParameter("type") == "series") "[]"
                                    else
                                        """{"data":[{"id":"${q.queryParameter("p")}","name":"Movie","cmd":"ffmpeg http://localhost/movie"}],"total_items":2,"max_page_items":1}"""
                                "create_link" -> """{"cmd":"ffmpeg https://stream.example/fresh"}"""
                                else -> "{}"
                            }
                        return MockResponse().setBody("{\"js\":$value}")
                    }
                }
            s.start()
            val p =
                StalkerProvider(
                    Source(
                        name = "s",
                        endpoint = s.url("/c/").toString(),
                        mac = "00:1A:79:00:00:00",
                    ),
                    Network(),
                )
            val c = p.catalog()
            assertEquals(3, c.items.size)
            assertEquals("https://stream.example/fresh", p.resolve(c.items[0]).url)
        }
    }

    @Test
    fun recoveryToleratesBriefBuffering() {
        val h = PlaybackHealth()
        h.started(1000)
        assertFalse(h.evaluate(1500, 0, false, true))
        assertFalse(h.evaluate(2000, 500, false, true))
        assertFalse(h.evaluate(7000, 500, false, false))
        assertTrue(h.evaluate(10_000, 500, false, false))
    }

    @Test
    fun recoveryDoesNotReconnectPausedOrEnded() {
        val h = PlaybackHealth()
        h.started(1000)
        assertFalse(h.evaluate(60_000, 0, true, false))
        assertFalse(h.evaluate(70_000, 0, false, true, true))
    }

    @Test
    fun startupWatchdog() {
        val h = PlaybackHealth()
        h.started(1000)
        assertFalse(h.evaluate(20_000, 0, false, false))
        assertTrue(h.evaluate(21_000, 0, false, false))
    }

    @Test
    fun retriesAreBoundedAndResetAfterHealthyPlayback() {
        val h = PlaybackHealth()
        repeat(3) { assertTrue(h.consumeRetry()) }
        assertFalse(h.consumeRetry())
        h.started(1000)
        h.evaluate(1500, 1, false, true)
        h.evaluate(32_000, 31_000, false, true)
        assertEquals(0, h.attempts)
        assertTrue(h.consumeRetry())
    }

    @Test
    fun seekGetsRecoveryGrace() {
        val h = PlaybackHealth()
        h.started(1000)
        h.evaluate(1500, 0, false, true)
        h.evaluate(2000, 500, false, true)
        h.seek(9000)
        assertFalse(h.evaluate(10_000, 500, false, false))
        assertTrue(h.evaluate(17_000, 500, false, false))
    }

    @Test
    fun liveEndReconnectsButVodEndAndPauseDoNot() {
        val health = PlaybackHealth()
        health.started(1000)
        assertTrue(health.evaluate(2000, 1000, false, false, ended = true, live = true))
        assertFalse(health.evaluate(2000, 1000, false, false, ended = true, live = false))
        assertFalse(health.evaluate(2000, 1000, true, false, ended = true, live = true))
    }
}

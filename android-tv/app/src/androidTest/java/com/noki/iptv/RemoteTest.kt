package com.noki.iptv

import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.uiautomator.UiDevice
import java.time.Instant
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import okhttp3.mockwebserver.*
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class RemoteTest {
    val rule = createAndroidComposeRule<MainActivity>()
    private val isolatedLibrary =
        object : org.junit.rules.ExternalResource() {
            private lateinit var storage: Storage
            private lateinit var previous: LibraryData

            override fun before() {
                storage = Storage(InstrumentationRegistry.getInstrumentation().targetContext)
                previous = storage.load()
                storage.save(LibraryData())
            }

            override fun after() {
                storage.save(previous)
            }
        }
    @get:Rule
    val rules: org.junit.rules.RuleChain =
        org.junit.rules.RuleChain.outerRule(isolatedLibrary).around(rule)
    private val device
        get() = UiDevice.getInstance(InstrumentationRegistry.getInstrumentation())

    private fun activate(text: String) {
        rule.waitUntil(30000) { rule.onAllNodesWithText(text).fetchSemanticsNodes().isNotEmpty() }
        rule.onNodeWithText(text).performSemanticsAction(SemanticsActions.RequestFocus) { it() }
        rule.waitForIdle()
        device.waitForIdle()
        rule.onNodeWithText(text).assertIsFocused()
        device.pressDPadCenter()
        device.waitForIdle()
    }

    @Test
    fun remoteOpensAndDismissesSourceEditor() {
        activate("+ Add source")
        rule.onNodeWithText("Use phone / show QR").assertIsDisplayed()
        device.pressDPadDown()
        device.pressDPadDown()
        device.pressDPadRight()
        device.pressDPadCenter()
        device.pressBack()
        // Back first dismisses the keyboard when a text field was selected.
        if (rule.onAllNodesWithText("Use phone / show QR").fetchSemanticsNodes().isNotEmpty())
            device.pressBack()
        rule.onNodeWithText("+ Add source").assertIsDisplayed()
    }

    @Test
    fun qrCanBeCancelledWithRemote() {
        activate("+ Add source")
        activate("Use phone / show QR")
        rule.waitUntil(15000) {
            rule
                .onAllNodesWithText("Add a source from your phone")
                .fetchSemanticsNodes()
                .isNotEmpty()
        }
        rule
            .onNodeWithContentDescription(
                "Scan this QR code with your phone to enter source details"
            )
            .assertIsDisplayed()
        device.pressBack()
        rule.onNodeWithText("Use phone / show QR").assertIsDisplayed()
        device.pressBack()
    }

    @Test
    fun guideFailureDoesNotOpenModal() {
        MockWebServer().use { server ->
            server.start()
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest) =
                        if (request.path == "/list")
                            MockResponse()
                                .setBody(
                                    "#EXTM3U\n#EXTINF:-1,Guide Failure Fixture\nhttps://fixture.example/live"
                                )
                        else MockResponse().setResponseCode(503)
                }
            val vm = androidx.lifecycle.ViewModelProvider(rule.activity)[Library::class.java]
            activate("+ Add source")
            val source = Source(name = "Failed guide fixture", endpoint = server.url("/list").toString(), epg = server.url("/guide").toString())
            rule.runOnIdle { vm.saveSource(source) }
            rule.waitUntil(15000) { vm.guideError != null }
            rule.onNodeWithText("Please check").assertDoesNotExist()
            rule.runOnIdle { org.junit.Assert.assertNull(vm.message) }
            activate("TV Guide")
            rule.onNodeWithText(vm.guideError!!).assertIsDisplayed()
        }
    }

    @Test
    fun largeCatalogLoadsAndSearchesOnTv() {
        MockWebServer().use { server ->
            val body = okio.Buffer().writeUtf8("#EXTM3U\n")
            repeat(50_000) { index ->
                body.writeUtf8(
                    "#EXTINF:-1 group-title=\"Group ${index % 100}\",Stress Channel $index\nhttps://fixture.example/live/$index\n"
                )
            }
            server.enqueue(MockResponse().setBody(body))
            server.start()
            activate("+ Add source")
            rule.onNodeWithContentDescription("Source name").performTextInput("Large catalog")
            rule
                .onNodeWithContentDescription("Playlist / server / portal URL")
                .performTextInput(server.url("/large.m3u").toString())
            device.pressBack()
            rule.onNodeWithText("Save & load").performScrollTo()
            activate("Save & load")
            rule.waitUntil(60000) {
                rule.onAllNodesWithText("Stress Channel 0").fetchSemanticsNodes().isNotEmpty()
            }
            rule
                .onNodeWithContentDescription("Search live tv")
                .performTextInput("Stress Channel 49999")
            rule.waitUntil(15000) {
                rule.onAllNodesWithText("Stress Channel 49999").fetchSemanticsNodes().isNotEmpty()
            }
            rule.onNode(hasText("Stress Channel 49999") and !hasSetTextAction()).assertIsDisplayed()
            device.pressBack()
            rule.runOnIdle {
                val vm = androidx.lifecycle.ViewModelProvider(rule.activity)[Library::class.java]
                vm.source?.let(vm::removeSource)
            }
            rule.waitForIdle()
        }
    }

    @Test
    fun libraryFavoriteGuideAndChannelRemote() {
        val ins = InstrumentationRegistry.getInstrumentation()
        val video = ins.context.assets.open("sample.mp4").use { it.readBytes() }
        MockWebServer().use { server ->
            server.start()
            val root = server.url("/").toString()
            val fmt = DateTimeFormatter.ofPattern("yyyyMMddHHmmss Z").withZone(ZoneOffset.UTC)
            val start = fmt.format(Instant.now().minusSeconds(600))
            val end = fmt.format(Instant.now().plusSeconds(3000))
            server.dispatcher =
                object : Dispatcher() {
                    override fun dispatch(request: RecordedRequest): MockResponse {
                        return when (request.requestUrl?.encodedPath) {
                            "/list.m3u" ->
                                MockResponse()
                                    .setBody(
                                        "#EXTM3U x-tvg-url=\"${root}guide.xml\"\n#EXTINF:-1 tvg-id=\"news\" group-title=\"News\",World News\n${root}live1\n#EXTINF:-1 tvg-id=\"sport\" group-title=\"Sports\",Arena Sports\n${root}live2\n#EXTINF:-1 group-title=\"Cinema\",Movie fixture\n${root}movie.mp4"
                                    )
                            "/guide.xml" ->
                                MockResponse()
                                    .setBody(
                                        "<tv><programme channel=\"news\" start=\"$start\" stop=\"$end\"><title>Evening bulletin</title><desc>Local test guide</desc></programme></tv>"
                                    )
                            else ->
                                MockResponse()
                                    .setHeader("Content-Type", "video/mp4")
                                    .setBody(okio.Buffer().write(video))
                        }
                    }
                }
            activate("+ Add source")
            rule.onNodeWithContentDescription("Source name").performTextInput("TV test library")
            rule
                .onNodeWithContentDescription("Playlist / server / portal URL")
                .performTextInput(root + "list.m3u")
            device.pressBack()
            rule.onNodeWithText("Save & load").performScrollTo()
            activate("Save & load")
            rule.waitUntil(15000) {
                rule.onAllNodesWithText("World News").fetchSemanticsNodes().isNotEmpty()
            }
            activate("All categories ▾")
            device.executeShellCommand("screencap -p /sdcard/Download/noki-categories-dropdown.png")
            activate("News")
            rule.onNodeWithText("World News").assertIsDisplayed()
            rule.onNodeWithText("Arena Sports").assertDoesNotExist()
            activate("News ▾")
            activate("All categories")
            rule.waitUntil(10000) {
                rule.onAllNodesWithText("Evening bulletin").fetchSemanticsNodes().isNotEmpty()
            }
            device.executeShellCommand("screencap -p /sdcard/Download/noki-qa-library.png")
            rule.onAllNodesWithText("☆ Favorite")[0].performSemanticsAction(
                SemanticsActions.RequestFocus
            ) {
                it()
            }
            device.pressDPadCenter()
            activate("Favorites")
            rule.onNodeWithText("World News").assertIsDisplayed()
            activate("TV Guide")
            rule.onNodeWithText("Evening bulletin").assertIsDisplayed()
            activate("Watch channel")
            rule.waitUntil(10000) {
                rule.onAllNodesWithText("‹ Library").fetchSemanticsNodes().isNotEmpty()
            }
            rule.waitUntil(8000) {
                rule.onAllNodesWithText("‹ Library").fetchSemanticsNodes().isEmpty()
            }
            device.pressDPadCenter()
            rule.waitUntil(3000) {
                rule.onAllNodesWithText("‹ Library").fetchSemanticsNodes().isNotEmpty()
            }
            // Hiding must also work while a Compose toolbar button owns focus.
            rule.onNodeWithText("Options").performSemanticsAction(SemanticsActions.RequestFocus) {
                it()
            }
            rule.waitUntil(8000) {
                rule.onAllNodesWithText("Options").fetchSemanticsNodes().isEmpty()
            }
            device.pressDPadCenter()
            rule.waitUntil(3000) {
                rule.onAllNodesWithText("Options").fetchSemanticsNodes().isNotEmpty()
            }
            device.pressKeyCode(android.view.KeyEvent.KEYCODE_MEDIA_NEXT)
            rule.waitUntil(10000) {
                rule.onAllNodesWithText("Arena Sports").fetchSemanticsNodes().isNotEmpty()
            }
            device.executeShellCommand("screencap -p /sdcard/Download/noki-qa-player.png")
            device.pressKeyCode(android.view.KeyEvent.KEYCODE_MEDIA_PAUSE)
            rule.waitForIdle()
            device.pressBack()
            rule.waitForIdle()
            rule.onNodeWithText("‹ Library").assertDoesNotExist()
            rule.runOnIdle {
                val vm = androidx.lifecycle.ViewModelProvider(rule.activity)[Library::class.java]
                org.junit.Assert.assertNotNull(vm.playback.item)
                org.junit.Assert.assertFalse(vm.playback.engine.playWhenReady)
            }
            device.pressBack()
            rule.onNodeWithText("Watch channel").assertIsDisplayed()
        }
    }
}

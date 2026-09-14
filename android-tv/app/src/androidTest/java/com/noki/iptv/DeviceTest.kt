package com.noki.iptv

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.security.KeyFactory
import java.security.SecureRandom
import java.security.spec.MGF1ParameterSpec
import java.security.spec.X509EncodedKeySpec
import java.util.Base64
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.OAEPParameterSpec
import javax.crypto.spec.PSource
import javax.crypto.spec.SecretKeySpec
import kotlinx.serialization.encodeToString
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DeviceTest {
    private val context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun encryptedStorageRoundTrip() {
        val store = Storage(context)
        val previous = store.load()
        try {
            val source =
                Source(
                    name = "Device test",
                    endpoint = "https://fixture.example/playlist?secret=PRIVATE_FIXTURE",
                    password = "PRIVATE_FIXTURE",
                )
            val data = LibraryData(listOf(source), setOf("favorite"), source.id)
            store.save(data)
            assertEquals(data, store.load())
            val bytes = File(context.noBackupFilesDir, "library/library.enc").readBytes()
            assertFalse(bytes.toString(Charsets.ISO_8859_1).contains("PRIVATE_FIXTURE"))
            val cache =
                Cache(
                    Catalog(
                        listOf(
                            Media("item", source.id, "one", "Fixture", location = source.endpoint)
                        )
                    )
                )
            store.saveCache(source.id, cache)
            assertEquals(cache, store.cache(source.id))
            store.deleteCache(source.id)
            assertNull(store.cache(source.id))
        } finally {
            store.save(previous)
        }
    }

    @Test
    fun largeCatalogStreamsAndReopensEncryptedCache() =
        kotlinx.coroutines.runBlocking<Unit> {
            val fixture = File(context.cacheDir, "large-catalog.m3u")
            val id = "large-stress-fixture"
            val store = Storage(context)
            try {
                fixture.bufferedWriter().use { writer ->
                    writer.appendLine("#EXTM3U")
                    repeat(50_000) { index ->
                        writer.appendLine(
                            "#EXTINF:-1 tvg-id=\"channel-$index\" group-title=\"Group ${index % 100}\",Channel $index"
                        )
                        writer.appendLine(
                            "https://fixture.example/live/$index?token=PRIVATE_FIXTURE"
                        )
                    }
                }
                val source = Source(id = id, name = "Stress", endpoint = "content://fixture/large")
                val network = Network(localOpen = { fixture.inputStream() })
                val catalog = PlaylistProvider(source, network).catalog()
                assertEquals(50_000, catalog.items.size)
                store.saveCache(id, Cache(catalog))
                val restored = store.cache(id)!!
                assertEquals(catalog, restored.catalog)
                assertEquals("Channel 49999", restored.catalog.items.last().name)
                val file = File(context.noBackupFilesDir, "library/${stableId(id)}.enc")
                // Tampering and truncation must fail even when compressed JSON is otherwise
                // complete.
                val size = file.length()
                java.io.RandomAccessFile(file, "rw").use { it.setLength(size - 1) }
                assertTrue(runCatching { store.cache(id) }.isFailure)
                android.util.Log.i(
                    "LargeCatalogTest",
                    "50000 channels parsed, encrypted and reopened; heap limit=${Runtime.getRuntime().maxMemory()}; cache bytes=$size",
                )
            } finally {
                fixture.delete()
                store.deleteCache(id)
            }
        }

    private fun encrypt(session: PairingSession, source: Source): String {
        val fragment = android.net.Uri.parse(session.url).fragment!!
        val values =
            fragment.split('&').associate { it.substringBefore('=') to it.substringAfter('=') }
        val publicKey =
            KeyFactory.getInstance("RSA")
                .generatePublic(
                    X509EncodedKeySpec(Base64.getUrlDecoder().decode(values.getValue("k")))
                )
        val key = ByteArray(32).also { SecureRandom().nextBytes(it) }
        val iv = ByteArray(12).also { SecureRandom().nextBytes(it) }
        val aes = Cipher.getInstance("AES/GCM/NoPadding")
        aes.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(128, iv))
        val rsa = Cipher.getInstance("RSA/ECB/OAEPWithSHA-256AndMGF1Padding")
        rsa.init(
            Cipher.ENCRYPT_MODE,
            publicKey,
            OAEPParameterSpec(
                "SHA-256",
                "MGF1",
                MGF1ParameterSpec.SHA256,
                PSource.PSpecified.DEFAULT,
            ),
        )
        val encoder = Base64.getEncoder()
        return JSONObject()
            .put("key", encoder.encodeToString(rsa.doFinal(key)))
            .put("iv", encoder.encodeToString(iv))
            .put(
                "data",
                encoder.encodeToString(aes.doFinal(json.encodeToString(source).toByteArray())),
            )
            .toString()
    }

    private fun post(
        session: PairingSession,
        body: String,
        token: String? = null,
        origin: String? = null,
    ): Pair<Int, String> {
        val target = session.url.substringBefore('#') + "source"
        val actualToken = token ?: session.url.substringAfter("#t=").substringBefore('&')
        val c = URL(target).openConnection() as HttpURLConnection
        c.requestMethod = "POST"
        c.connectTimeout = 5000
        c.readTimeout = 5000
        c.doOutput = true
        c.setRequestProperty("Content-Type", "application/json")
        c.setRequestProperty("Authorization", "Bearer $actualToken")
        origin?.let { c.setRequestProperty("Origin", it) }
        c.outputStream.use { it.write(body.toByteArray()) }
        return try {
            val code = c.responseCode
            code to (if (code == 200) c.inputStream else c.errorStream).bufferedReader().readText()
        } finally {
            c.disconnect()
        }
    }

    @Test
    fun pairingDecryptsOnlyOneSource() {
        var received: Source? = null
        val latch = CountDownLatch(1)
        PairingSession(context) {
                received = it
                latch.countDown()
            }
            .use { session ->
                val source =
                    Source(
                        name = "Phone fixture",
                        endpoint = "https://fixture.example/playlist?token=private",
                    )
                val body = encrypt(session, source)
                assertEquals(403, post(session, body, "wrong").first)
                assertNull(received)
                assertEquals(403, post(session, body, origin = "https://untrusted.example").first)
                assertNull(received)
                assertEquals(200, post(session, body).first)
                assertTrue(latch.await(2, TimeUnit.SECONDS))
                assertEquals(source, received)
                assertEquals(409, post(session, body).first)
            }
    }

    @Test
    fun tamperedPairingDataIsRejected() {
        var received = false
        PairingSession(context) { received = true }
            .use { session ->
                val body =
                    JSONObject(
                        encrypt(
                            session,
                            Source(name = "Fixture", endpoint = "https://fixture.example"),
                        )
                    )
                body.put("data", Base64.getEncoder().encodeToString(ByteArray(32)))
                assertEquals(400, post(session, body.toString()).first)
                assertFalse(received)
            }
    }

    @Test
    fun pairingPageIsLocalAndHasNoCache() {
        PairingSession(context) {}
            .use { session ->
                val c = URL(session.url.substringBefore('#')).openConnection() as HttpURLConnection
                try {
                    assertEquals(200, c.responseCode)
                    assertEquals("no-store", c.getHeaderField("Cache-Control"))
                    val html = c.inputStream.bufferedReader().readText()
                    assertTrue(html.contains("Send to TV"))
                    assertTrue(html.contains("/forge.min.js"))
                    assertFalse(html.contains("cdn.jsdelivr"))
                } finally {
                    c.disconnect()
                }
            }
    }

    @Test
    fun phonePageEncryptsAndSendsSource() {
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val latch = CountDownLatch(1)
        var received: Source? = null
        var webView: android.webkit.WebView? = null
        PairingSession(context) {
                received = it
                latch.countDown()
            }
            .use { session ->
                instrumentation.runOnMainSync {
                    webView =
                        android.webkit.WebView(context).apply {
                            settings.javaScriptEnabled = true
                            webViewClient =
                                object : android.webkit.WebViewClient() {
                                    override fun onPageFinished(
                                        view: android.webkit.WebView,
                                        url: String,
                                    ) {
                                        view.evaluateJavascript(
                                            "document.getElementById('name').value='Phone browser fixture';document.getElementById('endpoint').value='https://fixture.example/list?private=token';document.getElementById('includeVod').value='false';document.getElementById('includeSeries').value='false';document.getElementById('form').requestSubmit();",
                                            null,
                                        )
                                    }
                                }
                            loadUrl(session.url)
                        }
                }
                try {
                    assertTrue(
                        "Phone JavaScript did not deliver encrypted source",
                        latch.await(20, TimeUnit.SECONDS),
                    )
                    assertEquals("Phone browser fixture", received?.name)
                    assertEquals(false, received?.includeVod)
                    assertEquals(false, received?.includeSeries)
                    assertEquals("https://fixture.example/list?private=token", received?.endpoint)
                } finally {
                    instrumentation.runOnMainSync { webView?.destroy() }
                }
            }
    }

    @Test
    fun xmltvWorksOnAndroidAndRejectsDoctypes() {
        val xml =
            "<tv><programme channel=\"one\" start=\"20260909160000 +0800\" stop=\"20260909170000 +0800\"><title>News &amp; Weather</title></programme></tv>"
        assertEquals("News & Weather", XMLTV.parse(xml.toByteArray()).single().title)
        try {
            XMLTV.parse(
                "<!DOCTYPE tv [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><tv/>".toByteArray()
            )
            fail("DTD accepted")
        } catch (_: org.xml.sax.SAXException) {}
    }
}

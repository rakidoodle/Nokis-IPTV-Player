package com.noki.iptv

import java.util.concurrent.TimeUnit
import java.util.zip.GZIPInputStream
import kotlin.coroutines.coroutineContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.withContext
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.json.DecodeSequenceMode
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.decodeToSequence
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONArray
import org.json.JSONObject

fun java.io.InputStream.boundedBytes(limit: Int): ByteArray {
    val out = java.io.ByteArrayOutputStream()
    val buffer = ByteArray(8192)
    while (true) {
        val count = read(buffer)
        if (count < 0) break
        require(out.size() + count <= limit) { "The provider response is too large." }
        out.write(buffer, 0, count)
    }
    return out.toByteArray()
}

// Bound response bytes without retaining the response in memory.
class LimitedInputStream(input: java.io.InputStream, private val limit: Long = 64L * 1024 * 1024) :
    java.io.FilterInputStream(input) {
    private var total = 0L

    private fun count(value: Int): Int {
        if (value > 0) total += value
        require(total <= limit) { "The provider response is too large." }
        return value
    }

    override fun read(): Int = `in`.read().also { if (it >= 0) count(1) }

    override fun read(b: ByteArray, off: Int, len: Int): Int = count(`in`.read(b, off, len))
}

class Network(
    val client: OkHttpClient =
        OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .callTimeout(45, TimeUnit.SECONDS)
            .build(),
    val localRead: ((String) -> ByteArray)? = null,
    val localOpen: ((String) -> java.io.InputStream?)? = null,
) {
    suspend fun bytes(url: String, headers: Map<String, String> = emptyMap()): ByteArray =
        withContext(Dispatchers.IO) {
            if (url.startsWith("content:"))
                return@withContext localRead?.invoke(url)
                    ?: error("Choose the playlist file again.")
            val request =
                Request.Builder()
                    .url(url)
                    .apply { headers.forEach { (k, v) -> header(k, v) } }
                    .build()
            client.newCall(request).execute().use { response ->
                require(response.isSuccessful) {
                    "The provider returned HTTP ${response.code}. Check your source settings."
                }
                val body = response.body ?: error("The provider returned an empty response.")
                require(body.contentLength() <= 64L * 1024 * 1024) {
                    "The provider response is too large."
                }
                body.byteStream().use { input ->
                    val bytes = input.boundedBytes(64 * 1024 * 1024)
                    require(bytes.size <= 64 * 1024 * 1024) {
                        "The provider response is too large."
                    }
                    bytes
                }
            }
        }

    suspend fun <T> consume(url: String, block: (java.io.InputStream) -> T): T =
        withContext(Dispatchers.IO) {
            if (url.startsWith("content:")) {
                val input =
                    localOpen?.invoke(url)
                        ?: localRead?.invoke(url)?.inputStream()
                        ?: error("Choose the playlist file again.")
                return@withContext input.use { block(LimitedInputStream(it)) }
            }
            client.newCall(Request.Builder().url(url).build()).execute().use { response ->
                require(response.isSuccessful) { "The provider returned HTTP ${response.code}." }
                val body = response.body ?: error("The provider returned an empty response.")
                body.byteStream().use { block(LimitedInputStream(it)) }
            }
        }

    suspend fun guide(url: String): List<Programme> {
        if (url.isBlank()) return emptyList()
        return consume(url) { input ->
            val buffered = input.buffered()
            buffered.mark(2)
            val gzip = buffered.read() == 0x1f && buffered.read() == 0x8b
            buffered.reset()
            val data = if (gzip) GZIPInputStream(buffered) else buffered
            XMLTV.parse(LimitedInputStream(data))
        }
    }
}

interface Provider {
    suspend fun catalog(): Catalog

    suspend fun episodes(item: Media): List<Media>

    suspend fun resolve(item: Media): Stream

    suspend fun guide(items: List<Media>, url: String): List<Programme>
}

fun provider(source: Source, network: Network): Provider =
    when (source.type) {
        SourceType.M3U -> PlaylistProvider(source, network)
        SourceType.XTREAM -> XtreamProvider(source, network)
        SourceType.STALKER -> StalkerProvider(source, network)
    }

class PlaylistProvider(val source: Source, val network: Network) : Provider {
    override suspend fun catalog(): Catalog {
        suspend fun parse(charset: java.nio.charset.Charset) =
            network.consume(source.endpoint) {
                M3U.parse(
                    java.io.InputStreamReader(it, charset.newDecoder()).buffered(),
                    source.id,
                    source.endpoint,
                    source.includeVod,
                )
            }
        return try {
            parse(Charsets.UTF_8)
        } catch (_: java.nio.charset.CharacterCodingException) {
            parse(Charsets.ISO_8859_1)
        }
    }

    override suspend fun episodes(item: Media) = emptyList<Media>()

    override suspend fun resolve(item: Media) = Stream(item.location, item.headers)

    override suspend fun guide(items: List<Media>, url: String) = network.guide(url)
}

fun JSONArray.objects() = (0 until length()).mapNotNull { optJSONObject(it) }

fun rows(value: Any?): List<JSONObject> =
    when (value) {
        is JSONArray -> value.objects()
        is JSONObject -> value.optJSONArray("data")?.objects().orEmpty()
        else -> emptyList()
    }

fun JSONObject.str(key: String) = if (isNull(key)) "" else optString(key, "")

class XtreamProvider(val source: Source, val network: Network) : Provider {
    private fun base() =
        source.endpoint
            .toHttpUrl()
            .newBuilder()
            .query(null)
            .fragment(null)
            .apply {
                if (source.endpoint.toHttpUrl().pathSegments.last().endsWith(".php"))
                    removePathSegment(source.endpoint.toHttpUrl().pathSegments.lastIndex)
            }
            .build()
            .toString()
            .trimEnd('/')

    fun endpoint(file: String, params: Map<String, String> = emptyMap()) =
        (base() + "/" + file)
            .toHttpUrl()
            .newBuilder()
            .addQueryParameter("username", source.username)
            .addQueryParameter("password", source.password)
            .apply { params.forEach { (k, v) -> addQueryParameter(k, v) } }
            .build()
            .toString()

    suspend fun api(action: String = "", params: Map<String, String> = emptyMap()): Any {
        val text =
            network
                .bytes(
                    endpoint(
                        "player_api.php",
                        if (action.isBlank()) params else params + ("action" to action),
                    )
                )
                .toString(Charsets.UTF_8)
        return if (text.trim().startsWith('[')) JSONArray(text) else JSONObject(text)
    }

    @OptIn(ExperimentalSerializationApi::class)
    private suspend fun catalogRows(action: String, consume: (JSONObject) -> Unit) {
        network.consume(endpoint("player_api.php", mapOf("action" to action))) { input ->
            json.decodeToSequence<JsonObject>(input, DecodeSequenceMode.ARRAY_WRAPPED).forEach {
                consume(JSONObject(it.toString()))
            }
        }
    }

    fun stream(folder: String, id: String, ext: String) =
        (base() + "/")
            .toHttpUrl()
            .newBuilder()
            .addPathSegment(folder)
            .addPathSegment(source.username)
            .addPathSegment(source.password)
            .addPathSegment("$id.$ext")
            .build()
            .toString()

    override suspend fun catalog(): Catalog {
        val info = (api() as? JSONObject)?.optJSONObject("user_info")
        require(
            info?.optInt("auth") == 1 &&
                info.str("status").lowercase() !in listOf("expired", "disabled", "banned")
        ) {
            "Xtream login failed or the subscription is inactive."
        }
        val items = mutableListOf<Media>()
        val warnings = mutableListOf<String>()
        for ((kind, action, catAction) in
            listOf(
                Triple(Kind.LIVE, "get_live_streams", "get_live_categories"),
                Triple(Kind.MOVIE, "get_vod_streams", "get_vod_categories"),
                Triple(Kind.SERIES, "get_series", "get_series_categories"),
            )) {
            coroutineContext.ensureActive()
            if (
                (kind == Kind.MOVIE && !source.includeVod) ||
                    (kind == Kind.SERIES && !source.includeSeries)
            )
                continue
            try {
                val categories =
                    rows(api(catAction)).associate {
                        it.str("category_id") to it.str("category_name")
                    }
                catalogRows(action) { row ->
                    val id = row.str(if (kind == Kind.SERIES) "series_id" else "stream_id")
                    if (id.isBlank()) return@catalogRows
                    items +=
                        Media(
                            "${source.id}:$kind:$id",
                            source.id,
                            id,
                            row.str("name"),
                            kind,
                            categories[row.str("category_id")] ?: "Uncategorized",
                            row.str("stream_icon").ifBlank { row.str("cover") },
                            row.str("epg_channel_id"),
                            if (kind == Kind.SERIES) ""
                            else
                                stream(
                                    if (kind == Kind.LIVE) "live" else "movie",
                                    id,
                                    row.str("container_extension").ifBlank {
                                        if (kind == Kind.LIVE) "ts" else "mp4"
                                    },
                                ),
                            summary = row.str("plot"),
                        )
                }
            } catch (e: Exception) {
                coroutineContext.ensureActive()
                warnings += "$kind could not be loaded. Refresh to retry."
            }
        }
        require(items.isNotEmpty() || warnings.isEmpty()) { warnings.joinToString(" ") }
        return Catalog(items.distinctBy { it.id }, endpoint("xmltv.php"), warnings)
    }

    override suspend fun episodes(item: Media): List<Media> {
        val seasons =
            (api("get_series_info", mapOf("series_id" to item.providerId)) as? JSONObject)
                ?.optJSONObject("episodes") ?: return emptyList()
        return seasons
            .keys()
            .asSequence()
            .flatMap { season ->
                seasons.optJSONArray(season)?.objects().orEmpty().asSequence().map { row ->
                    val id = row.str("id")
                    Media(
                        "${source.id}:episode:$id",
                        source.id,
                        id,
                        row.str("title"),
                        Kind.EPISODE,
                        item.name,
                        item.logo,
                        location =
                            stream("series", id, row.str("container_extension").ifBlank { "mp4" }),
                        season = season.toIntOrNull() ?: row.optInt("season", 1),
                        episode = row.optInt("episode_num"),
                        summary = row.optJSONObject("info")?.str("plot").orEmpty(),
                    )
                }
            }
            .toList()
            .distinctBy { it.id }
            .sortedWith(compareBy({ it.season }, { it.episode }))
    }

    override suspend fun resolve(item: Media) = Stream(item.location, item.headers)

    override suspend fun guide(items: List<Media>, url: String) =
        network.guide(url.ifBlank { endpoint("xmltv.php") })
}

class StalkerProvider(val source: Source, val network: Network) : Provider {
    private var token = ""
    private val agent = "Mozilla/5.0 (QtEmbedded; U; Linux; C) AppleWebKit/533.3 MAG254 stbapp"

    private fun portal(): String {
        var url = source.endpoint.trimEnd('/')
        if (url.endsWith("/index.html")) url = url.removeSuffix("/index.html")
        if (url.endsWith("/c")) url = url.removeSuffix("/c")
        return if (url.endsWith(".php")) url else "$url/server/load.php"
    }

    private suspend fun handshake() {
        token =
            (request("stb", "handshake", mapOf("token" to ""), false) as? JSONObject)
                ?.str("token")
                .orEmpty()
        require(token.isNotBlank()) {
            "Stalker handshake failed. Check the portal and registered MAC."
        }
    }

    private suspend fun request(
        type: String,
        action: String,
        params: Map<String, String> = emptyMap(),
        renew: Boolean = true,
    ): Any {
        if (token.isBlank() && action != "handshake") handshake()
        val url =
            portal()
                .toHttpUrl()
                .newBuilder()
                .addQueryParameter("type", type)
                .addQueryParameter("action", action)
                .addQueryParameter("JsHttpRequest", "1-xml")
                .apply { params.forEach { (k, v) -> addQueryParameter(k, v) } }
                .build()
                .toString()
        val headers =
            mapOf(
                "Cookie" to
                    "mac=${java.net.URLEncoder.encode(source.mac,"UTF-8")}; stb_lang=en; timezone=${java.util.TimeZone.getDefault().id}",
                "User-Agent" to agent,
                "X-User-Agent" to "Model: MAG254; Link: Ethernet",
                "Referer" to source.endpoint,
            ) + if (token.isNotBlank()) mapOf("Authorization" to "Bearer $token") else emptyMap()
        try {
            val root = JSONObject(network.bytes(url, headers).toString(Charsets.UTF_8))
            val value = root.opt("js")
            require(value != null && value != JSONObject.NULL) {
                "The portal rejected this device."
            }
            if (value is JSONObject)
                require(value.str("error").isBlank()) {
                    "The portal rejected the request. Check device registration."
                }
            return value
        } catch (e: Exception) {
            coroutineContext.ensureActive()
            if (renew && action != "handshake") {
                token = ""
                handshake()
                return request(type, action, params, false)
            }
            throw e
        }
    }

    private suspend fun paged(
        type: String,
        params: Map<String, String> = emptyMap(),
    ): List<JSONObject> {
        val result = mutableListOf<JSONObject>()
        val seen = mutableSetOf<String>()
        for (page in 1..10000) {
            coroutineContext.ensureActive()
            val raw =
                request(
                    type,
                    "get_ordered_list",
                    params + mapOf("p" to "$page", "sortby" to "name", "not_ended" to "0"),
                )
            val batch = rows(raw)
            if (batch.isEmpty()) break
            require(
                seen.add(
                    stableId(batch.joinToString { it.str("id") + it.str("name") + it.str("cmd") })
                )
            ) {
                "The portal repeated a catalog page."
            }
            result += batch
            val obj = raw as? JSONObject
            val total = obj?.optInt("total_items") ?: 0
            val count = obj?.optInt("max_page_items") ?: 0
            if (
                (total > 0 && result.size >= total) ||
                    (count > 0 && batch.size < count) ||
                    (total == 0 && count == 0)
            )
                break
        }
        return result
    }

    override suspend fun catalog(): Catalog {
        handshake()
        if (source.username.isNotBlank())
            require(
                request(
                    "stb",
                    "do_auth",
                    mapOf("login" to source.username, "password" to source.password),
                ) != false
            ) {
                "Stalker login failed."
            }
        val profile =
            request(
                "stb",
                "get_profile",
                mapOf(
                    "hd" to "1",
                    "stb_type" to "MAG254",
                    "sn" to source.serial,
                    "device_id" to source.deviceId,
                    "device_id2" to source.deviceId2,
                ),
            )
                as? JSONObject
        require(profile?.optInt("blocked") != 1 && profile?.str("status") != "blocked") {
            "This Stalker device is blocked."
        }
        val items = mutableListOf<Media>()
        val warnings = mutableListOf<String>()
        for ((kind, type) in
            listOf(Kind.LIVE to "itv", Kind.MOVIE to "vod", Kind.SERIES to "series")) {
            if (
                (kind == Kind.MOVIE && !source.includeVod) ||
                    (kind == Kind.SERIES && !source.includeSeries)
            )
                continue
            try {
                val categories =
                    rows(request(type, if (kind == Kind.LIVE) "get_genres" else "get_categories"))
                        .associate { it.str("id") to it.str("title").ifBlank { it.str("name") } }
                val records =
                    if (kind == Kind.LIVE) rows(request(type, "get_all_channels")) else paged(type)
                items +=
                    records.mapNotNull { row ->
                        val id = row.str("id")
                        if (id.isBlank()) null
                        else
                            Media(
                                "${source.id}:$kind:$id",
                                source.id,
                                id,
                                row.str("name"),
                                kind,
                                categories[
                                    row.str("tv_genre_id").ifBlank { row.str("category_id") }]
                                    ?: "Uncategorized",
                                row.str("logo").ifBlank { row.str("screenshot_uri") },
                                row.str("xmltv_id").ifBlank { id },
                                row.str("cmd"),
                                summary = row.str("description"),
                            )
                    }
            } catch (e: Exception) {
                coroutineContext.ensureActive()
                warnings += "$kind could not be loaded from this portal."
            }
        }
        require(items.isNotEmpty() || warnings.isEmpty()) { warnings.joinToString(" ") }
        return Catalog(items.distinctBy { it.id }, warnings = warnings)
    }

    override suspend fun episodes(item: Media): List<Media> {
        val output = mutableListOf<Media>()
        for (season in paged("series", mapOf("movie_id" to item.providerId))) {
            val seasonId = season.str("id")
            val number = season.optInt("season_number", season.optInt("number", 1))
            val embedded = season.optJSONArray("series")
            if (embedded != null)
                for (i in 0 until embedded.length()) {
                    val row = embedded.optJSONObject(i)
                    val ep = row?.optInt("series_number", row.optInt("id")) ?: embedded.optInt(i)
                    output +=
                        Media(
                            "${source.id}:episode:$seasonId:$ep",
                            source.id,
                            seasonId,
                            row?.str("name").orEmpty().ifBlank { "Episode $ep" },
                            Kind.EPISODE,
                            item.name,
                            item.logo,
                            location = season.str("cmd"),
                            season = number,
                            episode = ep,
                        )
                }
            else
                for (row in
                    paged(
                        "series",
                        mapOf("movie_id" to item.providerId, "season_id" to seasonId),
                    )) {
                    val id = row.str("id")
                    output +=
                        Media(
                            "${source.id}:episode:$id",
                            source.id,
                            id,
                            row.str("name"),
                            Kind.EPISODE,
                            item.name,
                            item.logo,
                            location = row.str("cmd"),
                            season = number,
                            episode = row.optInt("series_number", row.optInt("number")),
                        )
                }
        }
        return output.distinctBy { it.id }.sortedWith(compareBy({ it.season }, { it.episode }))
    }

    override suspend fun resolve(item: Media): Stream {
        val params =
            mutableMapOf(
                "cmd" to item.location,
                "forced_storage" to "0",
                "disable_ad" to "0",
                "download" to "0",
            )
        if (item.kind == Kind.EPISODE) params["series"] = "${item.episode}"
        val result =
            request(if (item.kind == Kind.LIVE) "itv" else "vod", "create_link", params)
                as? JSONObject
        val url =
            Regex("(?:https?|rtsp|rtmp)://[^\\s]+").find(result?.str("cmd").orEmpty())?.value
                ?: error("The portal did not return a playable link.")
        return Stream(url, mapOf("User-Agent" to agent, "Referer" to source.endpoint))
    }

    override suspend fun guide(items: List<Media>, url: String): List<Programme> {
        if (url.isNotBlank()) return network.guide(url)
        val raw =
            request("itv", "get_epg_info", mapOf("period" to "24")) as? JSONObject
                ?: return emptyList()
        val data = raw.optJSONObject("data") ?: raw
        val ids = items.associate { it.providerId to it.epgId }
        return data
            .keys()
            .asSequence()
            .flatMap { channel ->
                data.optJSONArray(channel)?.objects().orEmpty().asSequence().map { row ->
                    Programme(
                        ids[channel] ?: channel,
                        row.str("name").ifBlank { row.str("title") },
                        row.optLong("start_timestamp") * 1000,
                        row.optLong("stop_timestamp", row.optLong("end_timestamp")) * 1000,
                        row.str("descr"),
                    )
                }
            }
            .filter { it.start > 0 && it.end > it.start }
            .toList()
    }
}

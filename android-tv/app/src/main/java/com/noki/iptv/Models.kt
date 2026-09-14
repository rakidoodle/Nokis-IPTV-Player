package com.noki.iptv

import java.net.URI
import java.security.MessageDigest
import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter
import javax.xml.parsers.SAXParserFactory
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import org.xml.sax.Attributes
import org.xml.sax.InputSource
import org.xml.sax.SAXException
import org.xml.sax.ext.DefaultHandler2

val json = Json {
    ignoreUnknownKeys = true
    encodeDefaults = true
}

@Serializable
enum class SourceType {
    M3U,
    XTREAM,
    STALKER,
}

@Serializable
data class Source(
    val id: String = java.util.UUID.randomUUID().toString(),
    val name: String = "",
    val type: SourceType = SourceType.M3U,
    val endpoint: String = "",
    val username: String = "",
    val password: String = "",
    val mac: String = "",
    val epg: String = "",
    val serial: String = "",
    val deviceId: String = "",
    val deviceId2: String = "",
    val includeVod: Boolean = true,
    val includeSeries: Boolean = true,
)

@Serializable
enum class Kind {
    LIVE,
    MOVIE,
    SERIES,
    EPISODE,
}

@Serializable
data class Media(
    val id: String,
    val sourceId: String,
    val providerId: String,
    val name: String,
    val kind: Kind = Kind.LIVE,
    val group: String = "Uncategorized",
    val logo: String = "",
    val epgId: String = "",
    val location: String = "",
    val headers: Map<String, String> = emptyMap(),
    val season: Int = 1,
    val episode: Int = 0,
    val summary: String = "",
)

@Serializable
data class Programme(
    val channel: String,
    val title: String,
    val start: Long,
    val end: Long,
    val detail: String = "",
)

@Serializable
data class Catalog(
    val items: List<Media> = emptyList(),
    val epgUrl: String = "",
    val warnings: List<String> = emptyList(),
)

@Serializable
data class Cache(
    val catalog: Catalog,
    val guide: List<Programme> = emptyList(),
    val guideUpdated: Long = 0,
)

@Serializable
data class LibraryData(
    val sources: List<Source> = emptyList(),
    val favorites: Set<String> = emptySet(),
    val selected: String? = null,
    val positions: Map<String, Long> = emptyMap(),
)

data class Stream(val url: String, val headers: Map<String, String> = emptyMap())

fun stableId(value: String): String {
    val hex = "0123456789abcdef"
    val digest = MessageDigest.getInstance("SHA-256").digest(value.toByteArray())
    return buildString(64) {
        for (byte in digest) {
            val value = byte.toInt() and 255
            append(hex[value ushr 4])
            append(hex[value and 15])
        }
    }
}

fun Source.validated(): Source {
    require(name.isNotBlank()) { "Enter a source name." }
    val uri = runCatching { URI(endpoint.trim()) }.getOrNull()
    require(
        uri != null &&
            ((uri.scheme in listOf("http", "https") && !uri.host.isNullOrBlank()) ||
                (type == SourceType.M3U && uri.scheme == "content"))
    ) {
        "Enter a valid HTTP(S) source URL or choose a playlist file."
    }
    if (type == SourceType.XTREAM)
        require(username.isNotBlank() && password.isNotBlank()) {
            "Enter your provider username and password."
        }
    if (type == SourceType.STALKER)
        require(Regex("(?i)[0-9a-f]{2}(:[0-9a-f]{2}){5}").matches(mac.trim())) {
            "Enter the MAC registered with your provider (00:1A:79:…)."
        }
    if (epg.isNotBlank())
        require(
            runCatching {
                    URI(epg.trim()).let { it.scheme in listOf("http", "https") && it.host != null }
                }
                .getOrDefault(false)
        ) {
            "Enter a valid HTTP(S) guide URL."
        }
    return copy(name = name.trim(), endpoint = endpoint.trim(), mac = mac.trim(), epg = epg.trim())
}

object M3U {
    fun attributes(line: String) =
        Regex("""([\w-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')""").findAll(line).associate {
            it.groupValues[1].lowercase() to (it.groups[2]?.value ?: it.groups[3]?.value.orEmpty())
        }

    fun parse(text: String, sourceId: String, base: String): Catalog =
        parse(text.reader().buffered(), sourceId, base)

    fun parse(
        reader: java.io.BufferedReader,
        sourceId: String,
        base: String,
        includeVod: Boolean = true,
    ): Catalog {
        val lines = reader.lineSequence().iterator()
        var first = ""
        while (lines.hasNext() && first.isBlank()) first =
            lines.next().trim().removePrefix("\uFEFF")
        require(first.startsWith("#EXTM3U")) { "This is not an M3U playlist." }
        var attrs = emptyMap<String, String>()
        var title = ""
        var group = "Uncategorized"
        var headers = mutableMapOf<String, String>()
        var epg = ""
        val items = mutableListOf<Media>()
        (sequenceOf(first) + lines.asSequence()).forEach { raw ->
            if (raw.trim().startsWith("#EXT-X-"))
                return Catalog(
                    listOf(Media("$sourceId:hls", sourceId, "hls", "Live stream", location = base))
                )
            val line = raw.trim().removePrefix("\uFEFF")
            when {
                line.startsWith("#EXTM3U") -> {
                    val a = attributes(line)
                    epg = a["x-tvg-url"] ?: a["url-tvg"].orEmpty()
                }
                line.startsWith("#EXTINF:") -> {
                    attrs = attributes(line)
                    headers = mutableMapOf()
                    group = attrs["group-title"] ?: "Uncategorized"
                    var quote: Char? = null
                    var comma = -1
                    for ((i, c) in line.withIndex()) {
                        if (c == '\'' || c == '"') {
                            if (quote == c) quote = null else if (quote == null) quote = c
                        }
                        if (c == ',' && quote == null) {
                            comma = i
                            break
                        }
                    }
                    title =
                        if (comma >= 0) line.substring(comma + 1)
                        else attrs["tvg-name"] ?: "Channel"
                }
                line.startsWith("#EXTGRP:") -> group = line.substringAfter(':')
                line.startsWith("#EXTVLCOPT:") -> {
                    val p = line.substringAfter(':').split('=', limit = 2)
                    if (p.size == 2)
                        when (p[0]) {
                            "http-user-agent" -> headers["User-Agent"] = p[1]
                            "http-referrer" -> headers["Referer"] = p[1]
                        }
                }
                line.isNotEmpty() && !line.startsWith('#') -> {
                    val p = line.split('|', limit = 2)
                    val url =
                        runCatching { URI(base).resolve(p[0]).toString() }.getOrNull()
                            ?: return@forEach
                    if (
                        URI(url).scheme !in
                            listOf("http", "https", "rtsp", "rtmp", "udp", "rtp", "content")
                    )
                        return@forEach
                    p.getOrNull(1)?.split('&')?.forEach { h ->
                        val kv = h.split('=', limit = 2)
                        if (kv.size == 2)
                            headers[kv[0]] = java.net.URLDecoder.decode(kv[1], "UTF-8")
                    }
                    val key = attrs["tvg-id"].orEmpty().ifBlank { url }
                    val kind =
                        if (
                            Regex("(?i)\\.(mp4|mkv|avi|mov|webm)$")
                                .containsMatchIn(URI(url).path.orEmpty())
                        )
                            Kind.MOVIE
                        else Kind.LIVE
                    if (kind != Kind.MOVIE || includeVod)
                        items +=
                            Media(
                                "$sourceId:${stableId("$key|$title")}",
                                sourceId,
                                key,
                                title.ifBlank { "Channel" },
                                kind,
                                group.ifBlank { "Uncategorized" },
                                attrs["tvg-logo"].orEmpty(),
                                attrs["tvg-id"].orEmpty(),
                                url,
                                headers.toMap(),
                            )
                    attrs = emptyMap()
                    title = ""
                    headers = mutableMapOf()
                    group = "Uncategorized"
                }
            }
        }
        require(items.isNotEmpty() || !includeVod) { "No playable entries were found." }
        return Catalog(items.distinctBy { it.id }, epg)
    }
}

object XMLTV {
    fun date(s: String): Long =
        runCatching {
                OffsetDateTime.parse(s.trim(), DateTimeFormatter.ofPattern("yyyyMMddHHmmss Z"))
                    .toInstant()
                    .toEpochMilli()
            }
            .getOrDefault(0)

    fun parse(bytes: ByteArray): List<Programme> = parse(bytes.inputStream())

    fun parse(input: java.io.InputStream): List<Programme> {
        val result = mutableListOf<Programme>()
        var current: Programme? = null
        var content = StringBuilder()
        val factory = SAXParserFactory.newInstance()
        runCatching {
            factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true)
        }
        runCatching {
            factory.setFeature("http://xml.org/sax/features/external-general-entities", false)
        }
        runCatching {
            factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false)
        }
        val handler =
            object : DefaultHandler2() {
                override fun startDTD(name: String?, publicId: String?, systemId: String?) {
                    throw SAXException("DTDs are not permitted in guides.")
                }

                override fun resolveEntity(publicId: String?, systemId: String?): InputSource {
                    throw SAXException("External entities are not permitted.")
                }

                override fun resolveEntity(
                    name: String?,
                    publicId: String?,
                    baseURI: String?,
                    systemId: String?,
                ): InputSource {
                    throw SAXException("External entities are not permitted.")
                }

                override fun startElement(
                    uri: String?,
                    localName: String?,
                    qName: String,
                    a: Attributes,
                ) {
                    content = StringBuilder()
                    if (qName == "programme")
                        current =
                            Programme(
                                a.getValue("channel").orEmpty(),
                                "",
                                date(a.getValue("start").orEmpty()),
                                date(a.getValue("stop").orEmpty()),
                            )
                }

                override fun characters(ch: CharArray, start: Int, length: Int) {
                    content.append(ch, start, length)
                }

                override fun endElement(uri: String?, localName: String?, qName: String) {
                    when (qName) {
                        "title" -> current = current?.copy(title = content.toString())
                        "desc" -> current = current?.copy(detail = content.toString())
                        "programme" -> {
                            current?.takeIf { it.start > 0 && it.end > it.start }?.let(result::add)
                            current = null
                        }
                    }
                }
            }
        factory
            .newSAXParser()
            .xmlReader
            .apply {
                contentHandler = handler
                entityResolver = handler
                setProperty("http://xml.org/sax/properties/lexical-handler", handler)
            }
            .parse(InputSource(input))
        return result.sortedBy { it.start }
    }
}

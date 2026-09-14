package com.noki.iptv

import android.content.Context
import android.os.SystemClock
import java.net.Inet4Address
import java.net.NetworkInterface
import java.net.ServerSocket
import java.net.Socket
import java.security.KeyPairGenerator
import java.security.MessageDigest
import java.security.SecureRandom
import java.security.spec.MGF1ParameterSpec
import java.util.Base64
import java.util.concurrent.Executors
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.OAEPParameterSpec
import javax.crypto.spec.PSource
import javax.crypto.spec.SecretKeySpec
import org.json.JSONObject

class PairingSession(private val context: Context, private val onSource: (Source) -> Unit) :
    AutoCloseable {
    private val random = SecureRandom()
    private val token =
        ByteArray(32).also(random::nextBytes).let {
            Base64.getUrlEncoder().withoutPadding().encodeToString(it)
        }
    private val keys =
        KeyPairGenerator.getInstance("RSA").apply { initialize(2048) }.generateKeyPair()
    private val started = SystemClock.elapsedRealtime()
    private val server = ServerSocket(0)
    private val pool = Executors.newFixedThreadPool(3)
    @Volatile private var closed = false
    @Volatile private var used = false
    val expiresInSeconds
        get() = ((300_000 - (SystemClock.elapsedRealtime() - started)) / 1000).coerceAtLeast(0)

    private val addresses =
        NetworkInterface.getNetworkInterfaces()
            .toList()
            .filter { it.isUp && !it.isLoopback }
            .flatMap { it.inetAddresses.toList() }
            .filterIsInstance<Inet4Address>()
            .filter { it.isSiteLocalAddress }
    val address: String = addresses.firstOrNull()?.hostAddress ?: ""
    val url: String
        get() =
            "http://$address:${server.localPort}/#t=$token&k=${Base64.getUrlEncoder().withoutPadding().encodeToString(keys.public.encoded)}"

    init {
        if (address.isEmpty()) {
            close()
            error("Connect the TV to Wi-Fi or Ethernet, then try again.")
        }
        pool.execute {
            while (!closed) try {
                val client = server.accept()
                if ((pool as java.util.concurrent.ThreadPoolExecutor).queue.size > 8) client.close()
                else pool.execute { handle(client) }
            } catch (_: Exception) {
                if (!closed) close()
            }
        }
    }

    private fun handle(socket: Socket) {
        socket.use { s ->
            try {
                s.soTimeout = 8000
                val input = s.getInputStream().buffered()
                fun line(): String {
                    val bytes = java.io.ByteArrayOutputStream()
                    while (bytes.size() < 8192) {
                        val c = input.read()
                        if (c < 0) error("Incomplete request")
                        if (c == 10) return bytes.toString("UTF-8").trimEnd('\r')
                        bytes.write(c)
                    }
                    error("Header too large")
                }
                val request = line().split(' ')
                require(request.size == 3)
                val headers = mutableMapOf<String, String>()
                var count = 0
                while (true) {
                    val h = line()
                    if (h.isEmpty()) break
                    require(++count <= 40)
                    val p = h.split(':', limit = 2)
                    require(p.size == 2)
                    headers[p[0].lowercase()] = p[1].trim()
                }
                fun respond(code: Int, type: String, body: ByteArray) {
                    val out = s.getOutputStream()
                    out.write(
                        ("HTTP/1.1 $code ${if(code==200) "OK" else "Error"}\r\nContent-Type: $type\r\nContent-Length: ${body.size}\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nContent-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; form-action 'none'\r\nConnection: close\r\n\r\n")
                            .toByteArray()
                    )
                    out.write(body)
                    out.flush()
                }
                fun message(code: Int, text: String) =
                    respond(code, "text/plain; charset=utf-8", text.toByteArray())
                if (closed || expiresInSeconds == 0L) {
                    message(410, "Pairing expired. Open a new QR code on your TV.")
                    return
                }
                // Reject DNS-rebinding hosts and browser cross-origin requests.
                if (
                    headers["host"] != "$address:${server.localPort}" &&
                        headers["host"] != "127.0.0.1:${server.localPort}"
                ) {
                    message(403, "Invalid host")
                    return
                }
                val origin = headers["origin"]
                if (origin != null && origin != "http://${headers["host"]}") {
                    message(403, "Invalid origin")
                    return
                }
                val path = request[1]
                if (request[0] == "GET") {
                    when (path) {
                        "/" ->
                            respond(
                                200,
                                "text/html; charset=utf-8",
                                context.assets.open("pair.html").use { it.readBytes() },
                            )
                        "/pair.js" ->
                            respond(
                                200,
                                "application/javascript",
                                context.assets.open("pair.js").use { it.readBytes() },
                            )
                        "/forge.min.js" ->
                            respond(
                                200,
                                "application/javascript",
                                context.assets.open("forge.min.js").use { it.readBytes() },
                            )
                        else -> message(404, "Not found")
                    }
                    return
                }
                if (request[0] != "POST" || path != "/source") {
                    message(405, "Not allowed")
                    return
                }
                if (
                    !MessageDigest.isEqual(
                        headers["authorization"].orEmpty().toByteArray(),
                        "Bearer $token".toByteArray(),
                    )
                ) {
                    message(403, "Pairing code is invalid.")
                    return
                }
                val length = headers["content-length"]?.toIntOrNull() ?: 0
                if (length !in 1..65536 || headers.containsKey("transfer-encoding")) {
                    message(413, "Source details are too large.")
                    return
                }
                val bytes = ByteArray(length)
                var offset = 0
                while (offset < length) {
                    val n = input.read(bytes, offset, length - offset)
                    require(n > 0)
                    offset += n
                }
                val envelope = JSONObject(bytes.toString(Charsets.UTF_8))
                val decoder = Base64.getDecoder()
                val rsa = Cipher.getInstance("RSA/ECB/OAEPWithSHA-256AndMGF1Padding")
                rsa.init(
                    Cipher.DECRYPT_MODE,
                    keys.private,
                    OAEPParameterSpec(
                        "SHA-256",
                        "MGF1",
                        MGF1ParameterSpec.SHA256,
                        PSource.PSpecified.DEFAULT,
                    ),
                )
                val aesKey = rsa.doFinal(decoder.decode(envelope.getString("key")))
                require(aesKey.size == 32)
                val iv = decoder.decode(envelope.getString("iv"))
                require(iv.size == 12)
                val aes = Cipher.getInstance("AES/GCM/NoPadding")
                aes.init(
                    Cipher.DECRYPT_MODE,
                    SecretKeySpec(aesKey, "AES"),
                    GCMParameterSpec(128, iv),
                )
                val source =
                    json
                        .decodeFromString<Source>(
                            aes.doFinal(decoder.decode(envelope.getString("data")))
                                .toString(Charsets.UTF_8)
                        )
                        .validated()
                require(source.endpoint.startsWith("http"))
                synchronized(this) {
                    if (used) {
                        message(409, "Already sent. Finish setup on your TV.")
                        return
                    }
                    if (expiresInSeconds == 0L || closed) {
                        message(410, "Pairing expired.")
                        return
                    }
                    used = true
                }
                onSource(source)
                message(200, "Sent to your TV. Review the source there and choose Save & load.")
            } catch (_: Exception) {
                runCatching {
                    s.getOutputStream()
                        .write(
                            "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Length: 32\r\n\r\nInvalid request. Please try again"
                                .toByteArray()
                        )
                }
            }
        }
    }

    override fun close() {
        closed = true
        runCatching { server.close() }
        pool.shutdownNow()
    }
}

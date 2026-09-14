package com.noki.iptv

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.AtomicFile
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.File
import java.io.InputStream
import java.io.OutputStream
import java.security.KeyStore
import java.security.SecureRandom
import java.util.zip.GZIPInputStream
import java.util.zip.GZIPOutputStream
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.decodeFromStream
import kotlinx.serialization.json.encodeToStream

class Storage(context: Context) {
    private val directory = File(context.noBackupFilesDir, "library").apply { mkdirs() }
    private val alias = "noki.iptv.storage.v1"

    @Synchronized
    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        return (store.getKey(alias, null) as? SecretKey)
            ?: KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
                .apply {
                    init(
                        KeyGenParameterSpec.Builder(
                                alias,
                                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                            )
                            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                            .build()
                    )
                }
                .generateKey()
    }

    @Synchronized
    private fun write(name: String, text: String) {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, key())
        val bytes = byteArrayOf(1) + cipher.iv + cipher.doFinal(text.toByteArray())
        val file = AtomicFile(File(directory, name))
        val out = file.startWrite()
        try {
            out.write(bytes)
            file.finishWrite(out)
        } catch (e: Exception) {
            file.failWrite(out)
            throw e
        }
    }

    @Synchronized
    private fun read(name: String): String? {
        val file = AtomicFile(File(directory, name))
        if (!file.baseFile.exists()) return null
        val bytes = file.readFully()
        require(bytes.size >= 29 && bytes[0] == 1.toByte())
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes.copyOfRange(1, 13)))
        return cipher.doFinal(bytes.copyOfRange(13, bytes.size)).toString(Charsets.UTF_8)
    }

    fun load(): LibraryData =
        read("library.enc")?.let { json.decodeFromString<LibraryData>(it) } ?: LibraryData()

    fun save(data: LibraryData) = write("library.enc", json.encodeToString(data))

    // Version 2 streams compressed JSON through individually authenticated 64 KiB chunks.
    // A per-file nonce and sequence number prevent substitution/reordering of chunks.
    @OptIn(ExperimentalSerializationApi::class)
    @Synchronized
    fun cache(id: String): Cache? {
        val file = AtomicFile(File(directory, "${stableId(id)}.enc"))
        if (!file.baseFile.exists()) return null
        file.openRead().use { raw ->
            val input = DataInputStream(raw)
            val version = input.readUnsignedByte()
            if (version == 1) {
                // Old caches are disposable; avoid allocating several copies of a large legacy
                // file.
                if (file.baseFile.length() > 8 * 1024 * 1024) return null
                return read("${stableId(id)}.enc")?.let { json.decodeFromString<Cache>(it) }
            }
            require(version == 2) { "Unsupported cache format." }
            val nonce = ByteArray(16).also(input::readFully)
            val secret = key()
            val chunks =
                object : InputStream() {
                    var sequence = 0
                    var block = ByteArray(0)
                    var position = 0
                    var ended = false

                    fun next(): Boolean {
                        if (ended) return false
                        val size = input.readInt()
                        require(size in 16..65552) { "Invalid cache chunk." }
                        val iv = ByteArray(12).also(input::readFully)
                        val encrypted = ByteArray(size).also(input::readFully)
                        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
                        cipher.init(Cipher.DECRYPT_MODE, secret, GCMParameterSpec(128, iv))
                        cipher.updateAAD(nonce)
                        cipher.updateAAD(java.nio.ByteBuffer.allocate(4).putInt(sequence++).array())
                        block = cipher.doFinal(encrypted)
                        position = 0
                        if (block.isEmpty()) {
                            ended = true
                            require(input.read() == -1) { "Trailing cache data." }
                        }
                        return !ended
                    }

                    override fun read(): Int {
                        if (position == block.size && !next()) return -1
                        return block[position++].toInt() and 255
                    }

                    override fun read(b: ByteArray, off: Int, len: Int): Int {
                        if (len == 0) return 0
                        if (position == block.size && !next()) return -1
                        val count = minOf(len, block.size - position)
                        block.copyInto(b, off, position, position + count)
                        position += count
                        return count
                    }
                }
            val value = json.decodeFromStream<Cache>(GZIPInputStream(chunks))
            // GZIP may finish before requesting the authenticated end marker.
            while (chunks.read() != -1) {}
            return value
        }
    }

    @OptIn(ExperimentalSerializationApi::class)
    @Synchronized
    fun saveCache(id: String, cache: Cache) {
        val file = AtomicFile(File(directory, "${stableId(id)}.enc"))
        val raw = file.startWrite()
        try {
            val output = DataOutputStream(raw)
            output.writeByte(2)
            val nonce = ByteArray(16).also { SecureRandom().nextBytes(it) }
            output.write(nonce)
            val secret = key()
            val chunks =
                object : OutputStream() {
                    val buffer = ByteArray(65536)
                    var used = 0
                    var sequence = 0

                    fun emit() {
                        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
                        cipher.init(Cipher.ENCRYPT_MODE, secret)
                        cipher.updateAAD(nonce)
                        cipher.updateAAD(java.nio.ByteBuffer.allocate(4).putInt(sequence++).array())
                        val encrypted = cipher.doFinal(buffer, 0, used)
                        output.writeInt(encrypted.size)
                        output.write(cipher.iv)
                        output.write(encrypted)
                        used = 0
                    }

                    override fun write(value: Int) {
                        buffer[used++] = value.toByte()
                        if (used == buffer.size) emit()
                    }

                    override fun write(b: ByteArray, off: Int, len: Int) {
                        var offset = off
                        var remaining = len
                        while (remaining > 0) {
                            val count = minOf(remaining, buffer.size - used)
                            b.copyInto(buffer, used, offset, offset + count)
                            used += count
                            offset += count
                            remaining -= count
                            if (used == buffer.size) emit()
                        }
                    }
                }
            val compressed = GZIPOutputStream(chunks)
            json.encodeToStream(cache, compressed)
            compressed.close()
            if (chunks.used > 0) chunks.emit()
            chunks.emit() // Authenticated end marker; truncated writes are rejected.
            file.finishWrite(raw)
        } catch (e: Exception) {
            file.failWrite(raw)
            throw e
        }
    }

    @Synchronized
    fun deleteCache(id: String) {
        AtomicFile(File(directory, "${stableId(id)}.enc")).delete()
    }
}

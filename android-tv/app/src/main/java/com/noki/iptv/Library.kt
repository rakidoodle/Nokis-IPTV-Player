package com.noki.iptv

import android.app.Application
import android.net.Uri
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import java.util.concurrent.CancellationException
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel

class Library(application: Application) : AndroidViewModel(application) {
    private val storage = Storage(application)
    val network =
        Network(localOpen = { url -> application.contentResolver.openInputStream(Uri.parse(url)) })

    var data by mutableStateOf(LibraryData())
        private set

    var catalog by mutableStateOf(Catalog())
        private set

    var guide by mutableStateOf(emptyList<Programme>())
        private set

    var guideError by mutableStateOf<String?>(null)
        private set

    private var guideAttempt = 0L

    var guideUpdated by mutableStateOf(0L)
        private set

    var loading by mutableStateOf(false)
        private set

    var guideLoading by mutableStateOf(false)
        private set

    var message by mutableStateOf<String?>(null)
    var section by mutableStateOf("Live TV")
    var query by mutableStateOf("")
    var group by mutableStateOf("All categories")
    var series by mutableStateOf<Media?>(null)
        private set

    var episodes by mutableStateOf(emptyList<Media>())
        private set

    var episodeLoading by mutableStateOf(false)
        private set

    var season by mutableStateOf(1)
    var editor by mutableStateOf<Source?>(null)
    var pair by mutableStateOf<PairingSession?>(null)
        private set

    var paired by mutableStateOf(false)
    var lastFocusedId by mutableStateOf<String?>(null)
    private var storageAvailable = false
    private var pairingGeneration = 0L
    private var pairingJob: Job? = null
    private var job: Job? = null
    private var guideJob: Job? = null
    private var episodeJob: Job? = null
    private val saveQueue = Channel<LibraryData>(Channel.CONFLATED)
    private val cacheQueue = Channel<Pair<String, Cache>>(Channel.CONFLATED)
    private val guideIndex by derivedStateOf { guide.groupBy { it.channel } }
    val source
        get() = data.sources.firstOrNull { it.id == data.selected }

    val playback =
        Playback(application, viewModelScope) { id, position ->
            if (position > 0) {
                data = data.copy(positions = data.positions + (id to position))
                persist()
            }
        }

    init {
        viewModelScope.launch {
            for ((id, snapshot) in cacheQueue) {
                try {
                    withContext(Dispatchers.IO) { storage.saveCache(id, snapshot) }
                } catch (_: Exception) {
                    message = "The source cache could not be saved."
                }
            }
        }
        viewModelScope.launch {
            for (snapshot in saveQueue) {
                try {
                    withContext(Dispatchers.IO) { storage.save(snapshot) }
                } catch (_: Exception) {
                    message = "Changes could not be saved. Check device storage."
                }
            }
        }
        viewModelScope.launch {
            try {
                data = withContext(Dispatchers.IO) { storage.load() }
                storageAvailable = true
                source?.let { select(it.id) }
            } catch (_: Exception) {
                message =
                    "Saved sources could not be read. Existing encrypted files have been preserved."
            }
        }
        viewModelScope.launch {
            while (isActive) {
                delay(60_000)
                if (
                    source != null &&
                        !guideLoading &&
                        android.os.SystemClock.elapsedRealtime() - guideAttempt >= 30 * 60 * 1000 &&
                        System.currentTimeMillis() - guideUpdated > 6 * 60 * 60 * 1000
                )
                    refreshGuide()
            }
        }
    }

    private fun persist() {
        if (storageAvailable) saveQueue.trySend(data)
    }

    fun saveSource(value: Source) {
        if (!storageAvailable) {
            message = "Storage is not available yet. Reopen the app before saving sources."
            return
        }
        try {
            val valid = value.validated()
            data =
                data.copy(
                    sources = data.sources.filterNot { it.id == valid.id } + valid,
                    selected = valid.id,
                )
            persist()
            editor = null
            paired = false
            closePairing()
            select(valid.id, true)
        } catch (e: Exception) {
            message = e.message ?: "Check the source details."
        }
    }

    fun removeSource(value: Source) {
        playback.stop()
        val remaining = data.sources.filterNot { it.id == value.id }
        data =
            data.copy(
                sources = remaining,
                selected = remaining.firstOrNull()?.id,
                favorites = data.favorites.filterNot { it.startsWith(value.id + ":") }.toSet(),
                positions = data.positions.filterKeys { !it.startsWith(value.id + ":") },
            )
        persist()
        viewModelScope.launch(Dispatchers.IO) { storage.deleteCache(value.id) }
        editor = null
        select(data.selected)
    }

    fun select(id: String?, force: Boolean = false) {
        job?.cancel()
        guideJob?.cancel()
        episodeJob?.cancel()
        playback.stop()
        data = data.copy(selected = id)
        persist()
        catalog = Catalog()
        guide = emptyList()
        guideUpdated = 0
        guideAttempt = 0
        guideError = null
        series = null
        episodes = emptyList()
        query = ""
        group = "All categories"
        lastFocusedId = null
        guideLoading = false
        episodeLoading = false
        val selected =
            source
                ?: run {
                    loading = false
                    return
                }
        loading = true
        job =
            viewModelScope.launch {
                try {
                    val cache =
                        if (force) null
                        else
                            withContext(Dispatchers.IO) {
                                runCatching { storage.cache(selected.id) }.getOrNull()
                            }
                    ensureActive()
                    if (cache != null && !force) {
                        catalog = cache.catalog
                        guide = cache.guide
                        guideUpdated = cache.guideUpdated
                    }
                    if (cache == null || force) loadCatalog(selected)
                    else if (System.currentTimeMillis() - guideUpdated > 6 * 60 * 60 * 1000)
                        refreshGuide()
                } catch (e: Exception) {
                    if (e is CancellationException) throw e
                    message = "Cached data could not be loaded. Refresh this source."
                } finally {
                    if (isActive) loading = false
                }
            }
    }

    fun refresh() {
        val selected = source ?: return
        job?.cancel()
        guideJob?.cancel()
        guideLoading = false
        loading = true
        job =
            viewModelScope.launch {
                try {
                    loadCatalog(selected)
                } catch (e: Exception) {
                    if (e is CancellationException) throw e
                    message = safeError(e)
                } finally {
                    if (isActive) loading = false
                }
            }
    }

    private suspend fun loadCatalog(selected: Source) {
        val value = withContext(Dispatchers.IO) { provider(selected, network).catalog() }
        currentCoroutineContext().ensureActive()
        if (source?.id != selected.id) return
        catalog = value
        saveCache()
        refreshGuide()
    }

    private fun saveCache() {
        val id = source?.id ?: return
        val cache = Cache(catalog, guide, guideUpdated)
        cacheQueue.trySend(id to cache)
    }

    fun refreshGuide() {
        val selected = source ?: return
        guideAttempt = android.os.SystemClock.elapsedRealtime()
        guideError = null
        guideJob?.cancel()
        guideLoading = true
        val items = catalog.items
        val url = selected.epg.ifBlank { catalog.epgUrl }
        guideJob =
            viewModelScope.launch {
                try {
                    val value =
                        withContext(Dispatchers.IO) {
                            provider(selected, network).guide(items, url)
                        }
                    ensureActive()
                    if (source?.id == selected.id) {
                        guide = value
                        guideUpdated = System.currentTimeMillis()
                        saveCache()
                    }
                } catch (e: Exception) {
                    if (e is CancellationException) throw e
                    guideError =
                        "Guide unavailable. Check the guide URL or retry later. Saved schedules are still available."
                } finally {
                    if (isActive) guideLoading = false
                }
            }
    }

    fun openSeries(item: Media) {
        series = item
        episodes = emptyList()
        episodeLoading = true
        season = 1
        val selected = source ?: return
        episodeJob?.cancel()
        episodeJob =
            viewModelScope.launch {
                try {
                    episodes =
                        withContext(Dispatchers.IO) { provider(selected, network).episodes(item) }
                    ensureActive()
                    season = episodes.firstOrNull()?.season ?: 1
                } catch (e: Exception) {
                    if (e is CancellationException) throw e
                    message = "Episodes could not be loaded. Try opening the series again."
                } finally {
                    if (isActive) episodeLoading = false
                }
            }
    }

    fun closeSeries() {
        episodeJob?.cancel()
        series = null
        episodes = emptyList()
        episodeLoading = false
    }

    fun favorite(item: Media) {
        data =
            data.copy(
                favorites =
                    if (item.id in data.favorites) data.favorites - item.id
                    else data.favorites + item.id
            )
        persist()
    }

    fun play(item: Media) {
        if (item.kind == Kind.SERIES) {
            openSeries(item)
            return
        }
        val selected = source ?: return
        lastFocusedId = item.id
        playback.play(item, provider(selected, network), data.positions[item.id] ?: 0)
    }

    fun nextChannel(delta: Int) {
        val channels =
            catalog.items.filter {
                it.kind == Kind.LIVE && (group == "All categories" || it.group == group)
            }
        val index = channels.indexOfFirst { it.id == playback.item?.id }
        if (channels.isNotEmpty() && index >= 0)
            play(channels[(index + delta + channels.size) % channels.size])
    }

    fun programmes(item: Media) =
        guideIndex[item.epgId.ifBlank { item.providerId }]
            .orEmpty()
            .filter { it.end > System.currentTimeMillis() }
            .sortedBy { it.start }

    fun startPairing() {
        closePairing()
        val existing = editor
        val generation = pairingGeneration
        pairingJob =
            viewModelScope.launch {
                var created: PairingSession? = null
                try {
                    withContext(Dispatchers.IO) {
                        created =
                            PairingSession(getApplication()) { incoming ->
                                viewModelScope.launch {
                                    if (generation == pairingGeneration && pair != null) {
                                        editor =
                                            incoming.copy(
                                                id =
                                                    existing?.id
                                                        ?: java.util.UUID.randomUUID().toString()
                                            )
                                        paired = true
                                    }
                                }
                            }
                    }
                    ensureActive()
                    pair = created
                } catch (e: Exception) {
                    created?.close()
                    if (e is CancellationException) throw e
                    message =
                        "Phone pairing could not start. Connect your TV to Wi-Fi or Ethernet and try again."
                }
            }
    }

    fun closePairing() {
        pairingGeneration++
        pairingJob?.cancel()
        pair?.close()
        pair = null
    }

    fun safeError(e: Exception): String =
        if (e is IllegalArgumentException && e.message?.contains(Regex("https?://")) != true)
            e.message ?: "Check source details."
        else "The source could not be loaded. Check provider details and network connectivity."

    override fun onCleared() {
        closePairing()
        playback.release()
        super.onCleared()
    }
}

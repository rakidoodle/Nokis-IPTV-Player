@file:OptIn(androidx.tv.material3.ExperimentalTvMaterial3Api::class)

package com.noki.iptv

import android.content.Intent
import android.graphics.Bitmap
import android.os.Bundle
import android.view.KeyEvent
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.*
import androidx.compose.foundation.lazy.grid.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.*
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.tv.material3.*
import coil.compose.AsyncImage
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.MultiFormatWriter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlinx.coroutines.delay

val Background = Color(0xFF0C111B)
val Panel = Color(0xFF141E2D)
val Accent = Color(0xFF66E1C4)
val Muted = Color(0xFF9BABBF)

class MainActivity : ComponentActivity() {
    private val library: Library by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent {
            MaterialTheme(
                colorScheme =
                    darkColorScheme(
                        primary = Accent,
                        background = Background,
                        surface = Panel,
                        onSurface = Color.White,
                    )
            ) {
                CompositionLocalProvider(LocalContentColor provides Color.White) { App(library) }
            }
        }
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent): Boolean {
        if (library.playback.item != null && event.action == KeyEvent.ACTION_DOWN)
            when (event.keyCode) {
                KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE,
                KeyEvent.KEYCODE_BUTTON_START -> {
                    library.playback.togglePause()
                    return true
                }
                KeyEvent.KEYCODE_MEDIA_PLAY -> {
                    library.playback.engine.play()
                    return true
                }
                KeyEvent.KEYCODE_MEDIA_PAUSE -> {
                    library.playback.engine.pause()
                    return true
                }
                KeyEvent.KEYCODE_CHANNEL_UP,
                KeyEvent.KEYCODE_MEDIA_NEXT -> {
                    library.nextChannel(1)
                    return true
                }
                KeyEvent.KEYCODE_CHANNEL_DOWN,
                KeyEvent.KEYCODE_MEDIA_PREVIOUS -> {
                    library.nextChannel(-1)
                    return true
                }
                KeyEvent.KEYCODE_MEDIA_STOP -> {
                    library.playback.stop()
                    return true
                }
            }
        return super.onKeyDown(keyCode, event)
    }

    override fun onStop() {
        library.playback.pauseForBackground()
        library.closePairing()
        super.onStop()
    }
}

@Composable
fun Action(
    text: String,
    modifier: Modifier = Modifier,
    selected: Boolean = false,
    onClick: () -> Unit,
) {
    Button(
        onClick = onClick,
        modifier = modifier,
        shape = ButtonDefaults.shape(RoundedCornerShape(10.dp)),
        colors =
            ButtonDefaults.colors(
                containerColor = if (selected) Color(0xFF223D40) else Panel,
                contentColor = if (selected) Accent else Color.White,
                focusedContainerColor = Accent,
                focusedContentColor = Background,
            ),
        scale = ButtonDefaults.scale(focusedScale = 1.025f),
    ) {
        Text(text, fontSize = 13.sp, maxLines = 1)
    }
}

@Composable
fun App(vm: Library) {
    var exit by remember { mutableStateOf(false) }
    BackHandler {
        when {
            vm.message != null -> vm.message = null
            vm.pair != null -> vm.closePairing()
            vm.editor != null -> {
                vm.editor = null
                vm.paired = false
            }
            vm.playback.item != null -> vm.playback.stop()
            vm.series != null -> vm.closeSeries()
            vm.section != "Live TV" -> {
                vm.section = "Live TV"
                vm.query = ""
                vm.group = "All categories"
            }
            else -> exit = true
        }
    }
    Box(Modifier.fillMaxSize().background(Background)) {
        if (vm.playback.item != null) PlayerScreen(vm)
        else
            Row(Modifier.fillMaxSize().padding(24.dp)) {
                Column(
                    Modifier.width(174.dp).fillMaxHeight().padding(end = 20.dp),
                    verticalArrangement = Arrangement.spacedBy(7.dp),
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(bottom = 20.dp),
                    ) {
                        Image(
                            painterResource(R.drawable.brand),
                            "Noki’s IPTV",
                            Modifier.size(45.dp),
                            contentScale = ContentScale.Fit,
                        )
                        Column(Modifier.padding(start = 9.dp)) {
                            Text("Noki’s IPTV", fontWeight = FontWeight.Bold, fontSize = 17.sp)
                            Text(
                                "PLAYER FOR TV",
                                fontSize = 9.sp,
                                color = Accent,
                                letterSpacing = 2.sp,
                            )
                        }
                    }
                    Text(
                        "LIBRARY",
                        fontSize = 10.sp,
                        letterSpacing = 2.sp,
                        color = Muted,
                        modifier = Modifier.padding(bottom = 6.dp),
                    )
                    listOf("Live TV", "Movies", "Series", "Favorites", "TV Guide", "Settings")
                        .forEach { section ->
                            Action(section, Modifier.fillMaxWidth(), vm.section == section) {
                                vm.section = section
                                vm.closeSeries()
                                vm.query = ""
                                vm.group = "All categories"
                            }
                        }
                    Spacer(Modifier.weight(1f))
                    Text(
                        vm.source?.name ?: "No source connected",
                        fontSize = 12.sp,
                        color = Muted,
                        maxLines = 2,
                    )
                    Text(
                        "ANDROID TV · GOOGLE TV",
                        fontSize = 8.sp,
                        color = Muted,
                        letterSpacing = 1.sp,
                        modifier = Modifier.padding(top = 8.dp),
                    )
                }
                Column(Modifier.weight(1f).fillMaxHeight()) {
                    Row(
                        Modifier.fillMaxWidth().padding(bottom = 14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Column(Modifier.weight(1f)) {
                            Text(
                                vm.series?.name ?: vm.section,
                                fontSize = 27.sp,
                                fontWeight = FontWeight.Bold,
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                            Text(
                                if (vm.loading) "Loading your library…"
                                else if (vm.series != null) "Choose a season and episode"
                                else "Your next watch starts here.",
                                fontSize = 12.sp,
                                color = Muted,
                            )
                        }
                        Action("+ Add source") { vm.editor = Source() }
                    }
                    if (vm.section == "Settings") Settings(vm)
                    else if (vm.data.sources.isEmpty()) EmptyLibrary(vm)
                    else {
                        Row(
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.padding(bottom = 12.dp),
                        ) {
                            Field(
                                "Search ${vm.section.lowercase()}",
                                vm.query,
                                { vm.query = it },
                                Modifier.weight(1f),
                                singleLine = true,
                            )
                            Action(if (vm.loading) "Loading…" else "Refresh") {
                                if (!vm.loading) vm.refresh()
                            }
                        }
                        if (vm.catalog.warnings.isNotEmpty())
                            Text(
                                vm.catalog.warnings.joinToString(" "),
                                color = Color(0xFFF2C879),
                                fontSize = 11.sp,
                                modifier = Modifier.padding(bottom = 8.dp),
                            )
                        if (vm.series != null) Episodes(vm)
                        else if (vm.section == "TV Guide") Guide(vm) else CatalogScreen(vm)
                    }
                }
            }
        if (vm.editor != null && vm.pair == null) Editor(vm)
        if (vm.pair != null) PairScreen(vm)
        if (vm.message != null)
            Modal(onDismiss = { vm.message = null }) {
                Text("Please check", fontSize = 23.sp, fontWeight = FontWeight.Bold)
                Text(
                    vm.message.orEmpty(),
                    color = Muted,
                    modifier = Modifier.padding(vertical = 18.dp),
                )
                Action("OK", Modifier.focusRequester(rememberInitialFocus())) { vm.message = null }
            }
        if (exit)
            Modal(onDismiss = { exit = false }) {
                Text("Exit Noki’s IPTV?", fontSize = 23.sp, fontWeight = FontWeight.Bold)
                Row(
                    Modifier.padding(top = 20.dp),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Action("Keep watching", Modifier.focusRequester(rememberInitialFocus())) {
                        exit = false
                    }
                    val activity = androidx.activity.compose.LocalActivity.current
                    Action("Exit") { activity?.finish() }
                }
            }
    }
}

@Composable
fun rememberInitialFocus(): FocusRequester {
    val requester = remember { FocusRequester() }
    LaunchedEffect(Unit) {
        delay(100)
        runCatching { requester.requestFocus() }
    }
    return requester
}

@Composable
fun Modal(onDismiss: () -> Unit = {}, content: @Composable ColumnScope.() -> Unit) {
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false),
    ) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Column(
                Modifier.widthIn(max = 680.dp)
                    .fillMaxHeight(.94f)
                    .padding(20.dp)
                    .clip(RoundedCornerShape(20.dp))
                    .background(Panel)
                    .padding(24.dp),
                verticalArrangement = Arrangement.Center,
                content = content,
            )
        }
    }
}

@Composable
fun EmptyLibrary(vm: Library) {
    Column(
        Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Image(painterResource(R.drawable.brand), null, Modifier.height(130.dp))
        Text(
            "Make yourself at home.",
            fontSize = 26.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(top = 14.dp),
        )
        Text(
            "Connect your IPTV provider to bring your library to the big screen.",
            fontSize = 13.sp,
            color = Muted,
            modifier = Modifier.padding(vertical = 12.dp),
        )
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Action("Scan QR from phone") {
                vm.editor = Source()
                vm.startPairing()
            }
            Action("Enter with remote") { vm.editor = Source() }
        }
    }
}

@Composable
fun Field(
    label: String,
    value: String,
    onChange: (String) -> Unit,
    modifier: Modifier = Modifier,
    password: Boolean = false,
    singleLine: Boolean = false,
) {
    var focused by remember { mutableStateOf(false) }
    Column(modifier) {
        Text(
            label,
            color = if (focused) Accent else Muted,
            fontSize = 11.sp,
            modifier = Modifier.padding(bottom = 5.dp),
        )
        BasicTextField(
            value,
            onChange,
            Modifier.fillMaxWidth()
                .semantics { contentDescription = label }
                .onFocusChanged { focused = it.isFocused }
                .border(
                    if (focused) 2.dp else 1.dp,
                    if (focused) Accent else Color(0xFF344256),
                    RoundedCornerShape(8.dp),
                )
                .background(Background, RoundedCornerShape(8.dp))
                .padding(horizontal = 12.dp, vertical = 10.dp),
            textStyle = TextStyle(color = Color.White, fontSize = 14.sp),
            cursorBrush = SolidColor(Accent),
            singleLine = singleLine,
            maxLines = if (singleLine) 1 else 4,
            visualTransformation =
                if (password) PasswordVisualTransformation() else VisualTransformation.None,
            keyboardOptions =
                KeyboardOptions(
                    keyboardType = if (password) KeyboardType.Password else KeyboardType.Text
                ),
        )
    }
}

@Composable
fun CatalogScreen(vm: Library) {
    val candidates =
        remember(vm.catalog, vm.section, vm.data.favorites) {
            vm.catalog.items.filter {
                when (vm.section) {
                    "Movies" -> it.kind == Kind.MOVIE
                    "Series" -> it.kind == Kind.SERIES
                    "Favorites" -> it.id in vm.data.favorites
                    else -> it.kind == Kind.LIVE
                }
            }
        }
    val groups =
        remember(candidates) {
            listOf("All categories") + candidates.map { it.group }.distinct().sorted()
        }
    var categoriesOpen by remember(vm.section, vm.catalog) { mutableStateOf(false) }
    val categoryFocus = remember { FocusRequester() }
    var categoryHeight by remember { mutableIntStateOf(0) }
    LaunchedEffect(groups) { if (vm.group !in groups) vm.group = "All categories" }
    Box(Modifier.padding(bottom = 14.dp)) {
        Action(
            "${vm.group} ▾",
            Modifier.focusRequester(categoryFocus).onSizeChanged { categoryHeight = it.height },
        ) {
            categoriesOpen = true
        }
        if (categoriesOpen) {
            androidx.compose.ui.window.Popup(
                alignment = Alignment.TopStart,
                offset = androidx.compose.ui.unit.IntOffset(0, categoryHeight),
                properties = androidx.compose.ui.window.PopupProperties(focusable = true),
                onDismissRequest = {
                    categoriesOpen = false
                    categoryFocus.requestFocus()
                },
            ) {
                val listState =
                    rememberLazyListState(
                        initialFirstVisibleItemIndex = groups.indexOf(vm.group).coerceAtLeast(0)
                    )
                val selectedFocus = rememberInitialFocus()
                LazyColumn(
                    state = listState,
                    modifier =
                        Modifier.width(360.dp)
                            .heightIn(max = 320.dp)
                            .background(Panel, RoundedCornerShape(12.dp))
                            .padding(12.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    items(groups) { g ->
                        Action(
                            g,
                            Modifier.fillMaxWidth()
                                .then(
                                    if (g == vm.group) Modifier.focusRequester(selectedFocus)
                                    else Modifier
                                ),
                            selected = vm.group == g,
                        ) {
                            vm.group = g
                            categoriesOpen = false
                            categoryFocus.requestFocus()
                        }
                    }
                }
            }
        }
    }
    val filtered =
        remember(candidates, vm.query, vm.group) {
            candidates.filter {
                (vm.group == "All categories" || it.group == vm.group) &&
                    it.name.contains(vm.query, true)
            }
        }
    if (filtered.isEmpty()) {
        Text(
            if (vm.loading) "Loading…" else "Nothing here yet. Try another category or search.",
            color = Muted,
            modifier = Modifier.padding(top = 30.dp),
        )
        return
    }
    val state = rememberLazyGridState()
    val restore = remember { FocusRequester() }
    val saved = vm.lastFocusedId
    LaunchedEffect(vm.section, saved) {
        val index = filtered.indexOfFirst { it.id == saved }
        if (index >= 0) {
            state.scrollToItem(index)
            delay(150)
            runCatching { restore.requestFocus() }
        }
    }
    val isLive =
        vm.section == "Live TV" ||
            (vm.section == "Favorites" && filtered.all { it.kind == Kind.LIVE })
    LazyVerticalGrid(
        columns = GridCells.Fixed(if (isLive) 2 else 4),
        state = state,
        verticalArrangement = Arrangement.spacedBy(12.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        contentPadding = PaddingValues(4.dp),
    ) {
        items(filtered, key = { it.id }) { item ->
            MediaTile(
                vm,
                item,
                isLive,
                if (item.id == saved) Modifier.focusRequester(restore) else Modifier,
            )
        }
    }
}

@Composable
fun MediaTile(vm: Library, item: Media, live: Boolean, modifier: Modifier = Modifier) {
    Column {
        Button(
            onClick = { vm.play(item) },
            modifier = modifier.fillMaxWidth(),
            shape = ButtonDefaults.shape(RoundedCornerShape(12.dp)),
            contentPadding = PaddingValues(10.dp),
            colors =
                ButtonDefaults.colors(
                    containerColor = Panel,
                    focusedContainerColor = Color(0xFF245047),
                    focusedContentColor = Color.White,
                ),
            scale = ButtonDefaults.scale(focusedScale = 1.025f),
        ) {
            if (live)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Artwork(item, Modifier.size(46.dp))
                    Column(Modifier.padding(start = 12.dp).weight(1f)) {
                        Text(
                            item.name,
                            fontSize = 14.sp,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                        Text(
                            vm.programmes(item).firstOrNull()?.title ?: item.group,
                            fontSize = 10.sp,
                            color = Muted,
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                    }
                }
            else
                Column {
                    Artwork(item, Modifier.fillMaxWidth().height(140.dp))
                    Text(
                        item.name,
                        fontSize = 12.sp,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.padding(top = 8.dp),
                    )
                    Text(item.group, fontSize = 10.sp, color = Muted, maxLines = 1)
                }
        }
        Action(
            if (item.id in vm.data.favorites) "★ Saved" else "☆ Favorite",
            Modifier.padding(top = 4.dp),
        ) {
            vm.favorite(item)
        }
    }
}

@Composable
fun Artwork(item: Media, modifier: Modifier) {
    Box(
        modifier.clip(RoundedCornerShape(8.dp)).background(Color(0xFF203044)),
        contentAlignment = Alignment.Center,
    ) {
        Text(item.name.take(1).uppercase(), fontSize = 28.sp, color = Accent)
        if (item.logo.isNotBlank())
            AsyncImage(item.logo, null, Modifier.fillMaxSize(), contentScale = ContentScale.Fit)
    }
}

@Composable
fun Episodes(vm: Library) {
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Action("‹ Back") { vm.closeSeries() }
        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(vm.episodes.map { it.season }.distinct()) { season ->
                Action("Season $season", selected = vm.season == season) { vm.season = season }
            }
        }
    }
    if (vm.episodeLoading)
        Text("Loading episodes…", color = Muted, modifier = Modifier.padding(20.dp))
    else if (vm.episodes.isEmpty())
        Text(
            "No episodes available from this provider.",
            color = Muted,
            modifier = Modifier.padding(20.dp),
        )
    else
        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(8.dp),
            modifier = Modifier.padding(top = 12.dp),
        ) {
            items(
                vm.episodes.filter { it.season == vm.season && it.name.contains(vm.query, true) },
                key = { it.id },
            ) { item ->
                Action("${item.episode}.  ${item.name}", Modifier.fillMaxWidth()) { vm.play(item) }
            }
        }
}

@Composable
fun Guide(vm: Library) {
    var channelId by remember(vm.source?.id) { mutableStateOf<String?>(null) }
    val channels =
        vm.catalog.items.filter { it.kind == Kind.LIVE && it.name.contains(vm.query, true) }
    val channel = channels.firstOrNull { it.id == channelId } ?: channels.firstOrNull()
    Row(Modifier.fillMaxSize()) {
        LazyColumn(
            Modifier.width(215.dp).fillMaxHeight(),
            verticalArrangement = Arrangement.spacedBy(7.dp),
        ) {
            items(channels, key = { it.id }) { item ->
                Action(item.name, Modifier.fillMaxWidth(), channel?.id == item.id) {
                    channelId = item.id
                }
            }
        }
        Column(Modifier.weight(1f).padding(start = 16.dp)) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Action(if (vm.guideLoading) "Refreshing…" else "Refresh guide") {
                    if (!vm.guideLoading) vm.refreshGuide()
                }
                if (channel != null) Action("Watch channel") { vm.play(channel) }
            }
            vm.guideError?.let {
                Text(
                    it,
                    color = Muted,
                    fontSize = 12.sp,
                    modifier = Modifier.padding(vertical = 8.dp),
                )
            }
            if (channel != null) {
                Text(
                    channel.name,
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(vertical = 12.dp),
                )
                val entries = vm.programmes(channel)
                if (entries.isEmpty())
                    Text(
                        "No schedule available. Add a guide URL in source settings or refresh.",
                        color = Muted,
                        fontSize = 13.sp,
                    )
                LazyColumn(verticalArrangement = Arrangement.spacedBy(9.dp)) {
                    items(entries) { p ->
                        Column(
                            Modifier.fillMaxWidth()
                                .clip(RoundedCornerShape(10.dp))
                                .background(Panel)
                                .padding(14.dp)
                                .focusable()
                        ) {
                            Text(
                                "${time(p.start)} – ${time(p.end)}",
                                color = Accent,
                                fontSize = 11.sp,
                            )
                            Text(
                                p.title,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.Medium,
                                modifier = Modifier.padding(vertical = 5.dp),
                            )
                            if (p.detail.isNotBlank())
                                Text(p.detail, color = Muted, fontSize = 12.sp)
                        }
                    }
                }
            }
        }
    }
}

fun time(ms: Long) = SimpleDateFormat("EEE HH:mm", Locale.getDefault()).format(Date(ms))

@Composable
fun Settings(vm: Library) {
    Column(
        Modifier.verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("Sources", fontSize = 19.sp, fontWeight = FontWeight.Bold)
        vm.data.sources.forEach { source ->
            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Action(
                    (if (source.id == vm.source?.id) "●  " else "") + source.name,
                    Modifier.weight(1f),
                    source.id == vm.source?.id,
                ) {
                    vm.select(source.id)
                }
                Text(source.type.name, fontSize = 11.sp, color = Muted)
                Action("Edit") { vm.editor = source }
            }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Action("Add with phone") {
                vm.editor = Source()
                vm.startPairing()
            }
            Action("Add with remote") { vm.editor = Source() }
        }
        Text(
            "Remote controls",
            fontSize = 19.sp,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(top = 15.dp),
        )
        Text(
            "D-pad: navigate  •  OK: select / show player controls\nBack: return  •  Play/Pause: playback\nChannel +/− or Next/Previous: change live channel\nLeft/Right in player: seek when supported\nVolume: your TV’s volume controls",
            color = Muted,
            fontSize = 13.sp,
            lineHeight = 23.sp,
        )
        Text(
            "Noki’s IPTV ${BuildConfig.VERSION_NAME} · Android 8.0+\nSources and cached catalogs are encrypted on this TV.\nPhone pairing uses your local network; no cloud account is required.",
            color = Muted,
            fontSize = 11.sp,
            lineHeight = 19.sp,
            modifier = Modifier.padding(top = 14.dp),
        )
    }
}

@Composable
fun Editor(vm: Library) {
    val original = vm.editor ?: return
    var value by remember(original) { mutableStateOf(original) }
    var deleting by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val file =
        rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
            if (uri != null) {
                runCatching {
                    context.contentResolver.takePersistableUriPermission(
                        uri,
                        Intent.FLAG_GRANT_READ_URI_PERMISSION,
                    )
                }
                value = value.copy(endpoint = uri.toString())
            }
        }
    Modal(
        onDismiss = {
            vm.editor = null
            vm.paired = false
        }
    ) {
        Text(
            if (vm.paired) "Source received from phone"
            else if (vm.data.sources.any { it.id == original.id }) "Edit source"
            else "Add your source",
            fontSize = 23.sp,
            fontWeight = FontWeight.Bold,
        )
        Text(
            if (vm.paired) "Review the details below, then save to load your library."
            else "Enter details with your remote, or scan a QR code from your phone.",
            color = Muted,
            fontSize = 12.sp,
            modifier = Modifier.padding(vertical = 8.dp),
        )
        Column(
            Modifier.weight(1f, false).verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Action("Use phone / show QR", Modifier.focusRequester(rememberInitialFocus())) {
                vm.editor = value
                vm.startPairing()
            }
            Field("Source name", value.name, { value = value.copy(name = it) }, singleLine = true)
            Row(horizontalArrangement = Arrangement.spacedBy(7.dp)) {
                SourceType.entries.forEach { type ->
                    Action(type.name, selected = value.type == type) {
                        value = value.copy(type = type)
                    }
                }
            }
            Field(
                "Playlist / server / portal URL",
                value.endpoint,
                { value = value.copy(endpoint = it) },
            )
            if (value.type == SourceType.M3U)
                Action("Choose playlist file") {
                    try {
                        file.launch(arrayOf("*/*"))
                    } catch (_: Exception) {
                        vm.message =
                            "No file picker is available on this TV. Use a playlist URL through phone setup."
                    }
                }
            if (value.type != SourceType.M3U) {
                Field(
                    "Username${if(value.type==SourceType.STALKER)" (optional)" else ""}",
                    value.username,
                    { value = value.copy(username = it) },
                    singleLine = true,
                )
                Field(
                    "Password",
                    value.password,
                    { value = value.copy(password = it) },
                    password = true,
                    singleLine = true,
                )
            }
            if (value.type == SourceType.STALKER) {
                Field(
                    "Provider-registered MAC",
                    value.mac,
                    { value = value.copy(mac = it) },
                    singleLine = true,
                )
                Field(
                    "Serial (optional)",
                    value.serial,
                    { value = value.copy(serial = it) },
                    singleLine = true,
                )
                Field("Device ID (optional)", value.deviceId, { value = value.copy(deviceId = it) })
                Field(
                    "Device ID 2 (optional)",
                    value.deviceId2,
                    { value = value.copy(deviceId2 = it) },
                )
            }
            Text("Download catalog", color = Muted)
            Action("Include Movies (VOD): ${if (value.includeVod) "On" else "Off"}") {
                value = value.copy(includeVod = !value.includeVod)
            }
            if (value.type != SourceType.M3U)
                Action("Include Series: ${if (value.includeSeries) "On" else "Off"}") {
                    value = value.copy(includeSeries = !value.includeSeries)
                }
            if (value.type == SourceType.M3U)
                Text(
                    "M3U downloads one playlist; recognized video files follow the Movies option. Separate series catalogs require Xtream or Stalker.",
                    color = Muted,
                    fontSize = 12.sp,
                )
            Field("Guide URL (optional)", value.epg, { value = value.copy(epg = it) })
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Action("Save & load") { vm.saveSource(value) }
                Action("Cancel") {
                    vm.editor = null
                    vm.paired = false
                }
                if (vm.data.sources.any { it.id == original.id })
                    Action("Delete source") { deleting = true }
            }
        }
    }
    if (deleting)
        Modal(onDismiss = { deleting = false }) {
            Text("Delete ${original.name}?", fontSize = 22.sp)
            Text(
                "This removes its saved favorites and cached guide from this TV.",
                color = Muted,
                modifier = Modifier.padding(vertical = 12.dp),
            )
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Action("Cancel", Modifier.focusRequester(rememberInitialFocus())) {
                    deleting = false
                }
                Action("Delete") { vm.removeSource(original) }
            }
        }
}

@Composable
fun PairScreen(vm: Library) {
    val pair = vm.pair ?: return
    var seconds by remember(pair) { mutableStateOf(pair.expiresInSeconds) }
    LaunchedEffect(pair) {
        while (seconds > 0) {
            delay(1000)
            seconds = pair.expiresInSeconds
        }
        vm.closePairing()
        vm.message = "Pairing expired. Open a new QR code to try again."
    }
    if (vm.paired) {
        LaunchedEffect(Unit) {
            delay(1800)
            vm.closePairing()
        }
    }
    Modal(onDismiss = { vm.closePairing() }) {
        Text(
            if (vm.paired) "Source received" else "Add a source from your phone",
            fontSize = 23.sp,
            fontWeight = FontWeight.Bold,
        )
        Row(
            Modifier.padding(top = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(24.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            val bitmap =
                remember(pair) {
                    val matrix =
                        MultiFormatWriter()
                            .encode(
                                pair.url,
                                BarcodeFormat.QR_CODE,
                                420,
                                420,
                                mapOf(EncodeHintType.MARGIN to 2),
                            )
                    Bitmap.createBitmap(420, 420, Bitmap.Config.ARGB_8888).apply {
                        for (y in 0 until 420) for (x in 0 until 420) setPixel(
                            x,
                            y,
                            if (matrix[x, y]) android.graphics.Color.BLACK
                            else android.graphics.Color.WHITE,
                        )
                    }
                }
            Image(
                bitmap.asImageBitmap(),
                "Scan this QR code with your phone to enter source details",
                Modifier.size(230.dp),
            )
            Column(Modifier.width(260.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(
                    if (vm.paired) "Review and save the source on the next screen."
                    else
                        "1. Connect your phone to the same network.\n\n2. Scan this code with the phone camera.\n\n3. Paste your source details and tap Send to TV.",
                    fontSize = 14.sp,
                    lineHeight = 21.sp,
                )
                Text(
                    "Expires in ${seconds/60}:${(seconds%60).toString().padStart(2,'0')}",
                    color = Accent,
                    fontSize = 12.sp,
                )
                Text(
                    "Use a trusted local network. Guest Wi-Fi may block phone-to-TV connections.",
                    color = Muted,
                    fontSize = 11.sp,
                )
                Action(
                    if (vm.paired) "Review source" else "Cancel",
                    Modifier.focusRequester(rememberInitialFocus()),
                ) {
                    vm.closePairing()
                }
            }
        }
    }
}

@androidx.annotation.OptIn(androidx.media3.common.util.UnstableApi::class)
@Composable
fun PlayerScreen(vm: Library) {
    val playback = vm.playback
    var tools by remember { mutableStateOf(false) }
    var controlsVisible by remember { mutableStateOf(true) }
    var lastInteraction by remember { mutableLongStateOf(android.os.SystemClock.elapsedRealtime()) }
    val viewRef = remember { arrayOfNulls<androidx.media3.ui.PlayerView>(1) }
    fun hideControls() {
        controlsVisible = false
        viewRef[0]?.requestFocus()
        viewRef[0]?.hideController()
    }
    BackHandler(enabled = !tools && playback.error == null && vm.message == null) {
        if (controlsVisible) hideControls() else playback.stop()
    }
    fun wakeControls(event: KeyEvent): Boolean {
        if (event.keyCode == KeyEvent.KEYCODE_BACK) return false
        if (event.action != KeyEvent.ACTION_DOWN) return false
        val wasHidden = !controlsVisible
        lastInteraction = android.os.SystemClock.elapsedRealtime()
        controlsVisible = true
        viewRef[0]?.showController()
        return wasHidden &&
            event.keyCode in
                listOf(
                    KeyEvent.KEYCODE_DPAD_CENTER,
                    KeyEvent.KEYCODE_ENTER,
                    KeyEvent.KEYCODE_DPAD_UP,
                    KeyEvent.KEYCODE_DPAD_DOWN,
                    KeyEvent.KEYCODE_DPAD_LEFT,
                    KeyEvent.KEYCODE_DPAD_RIGHT,
                )
    }
    LaunchedEffect(playback.item?.id, tools) {
        lastInteraction = android.os.SystemClock.elapsedRealtime()
        while (true) {
            delay(200)
            if (
                !tools &&
                    playback.engine.isPlaying &&
                    controlsVisible &&
                    android.os.SystemClock.elapsedRealtime() - lastInteraction >= 3500
            ) {
                controlsVisible = false
                // Move focus off the disappearing Compose toolbar, then hide BOTH control layers.
                viewRef[0]?.requestFocus()
                viewRef[0]?.hideController()
            }
        }
    }
    DisposableEffect(playback.engine) {
        val listener =
            object : androidx.media3.common.Player.Listener {
                override fun onIsPlayingChanged(isPlaying: Boolean) {
                    if (isPlaying) lastInteraction = android.os.SystemClock.elapsedRealtime()
                    else if (!playback.engine.playWhenReady) {
                        controlsVisible = true
                        viewRef[0]?.showController()
                    }
                }
            }
        playback.engine.addListener(listener)
        onDispose { playback.engine.removeListener(listener) }
    }
    val picker =
        rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
            if (uri != null) playback.setSubtitle(uri)
        }
    LifecycleResumeEffect(Unit) { onPauseOrDispose { playback.pauseForBackground() } }
    Box(
        Modifier.fillMaxSize().background(Color.Black).onPreviewKeyEvent {
            wakeControls(it.nativeKeyEvent)
        }
    ) {
        AndroidView(
            factory = { context ->
                object : androidx.media3.ui.PlayerView(context) {
                        override fun dispatchKeyEvent(event: KeyEvent): Boolean {
                            if (event.keyCode == KeyEvent.KEYCODE_BACK) return false
                            if (wakeControls(event)) return true
                            if (
                                event.keyCode in
                                    listOf(
                                        KeyEvent.KEYCODE_CHANNEL_UP,
                                        KeyEvent.KEYCODE_CHANNEL_DOWN,
                                        KeyEvent.KEYCODE_MEDIA_NEXT,
                                        KeyEvent.KEYCODE_MEDIA_PREVIOUS,
                                    )
                            ) {
                                if (event.action == KeyEvent.ACTION_DOWN && event.repeatCount == 0)
                                    vm.nextChannel(
                                        if (
                                            event.keyCode == KeyEvent.KEYCODE_CHANNEL_UP ||
                                                event.keyCode == KeyEvent.KEYCODE_MEDIA_NEXT
                                        )
                                            1
                                        else -1
                                    )
                                return true
                            }
                            return super.dispatchKeyEvent(event)
                        }
                    }
                    .apply {
                        player = playback.engine
                        useController = true
                        setShowSubtitleButton(true)
                        setShowNextButton(false)
                        setShowPreviousButton(false)
                        viewRef[0] = this
                        controllerShowTimeoutMs = 0
                        controllerAutoShow = false
                        keepScreenOn = true
                        requestFocus()
                    }
            },
            modifier = Modifier.fillMaxSize(),
            update = { it.player = playback.engine },
        )
        if (controlsVisible)
            Row(
                Modifier.align(Alignment.TopStart).padding(18.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Action("‹ Library") { playback.stop() }
                Action("Options") { tools = !tools }
                Text(
                    playback.item?.name.orEmpty(),
                    color = Color.White,
                    fontSize = 16.sp,
                    modifier = Modifier.padding(10.dp).background(Color.Black.copy(alpha = .55f)),
                    maxLines = 1,
                )
            }
        if (playback.status.isNotBlank())
            Text(
                playback.status,
                Modifier.align(Alignment.Center)
                    .background(Panel, RoundedCornerShape(12.dp))
                    .padding(18.dp),
                color = Accent,
            )
        if (tools)
            Modal(onDismiss = { tools = false }) {
                Text(playback.item?.name.orEmpty(), fontSize = 22.sp)
                Row(
                    Modifier.padding(top = 18.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    Action("External subtitle", Modifier.focusRequester(rememberInitialFocus())) {
                        try {
                            picker.launch(arrayOf("*/*"))
                            tools = false
                        } catch (_: Exception) {
                            vm.message =
                                "No file picker is installed. Embedded subtitles are available in the player caption menu."
                        }
                    }
                    Action("Favorite") { playback.item?.let { vm.favorite(it) } }
                    Action("Close") { tools = false }
                }
                Text(
                    "Use the player settings for audio tracks and embedded subtitles.",
                    fontSize = 12.sp,
                    color = Muted,
                    modifier = Modifier.padding(top = 15.dp),
                )
            }
        if (playback.error != null)
            Modal(onDismiss = { playback.stop() }) {
                Text("Playback interrupted", fontSize = 23.sp)
                Text(
                    playback.error.orEmpty(),
                    color = Muted,
                    modifier = Modifier.padding(vertical = 16.dp),
                )
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Action("Retry", Modifier.focusRequester(rememberInitialFocus())) {
                        playback.retry()
                    }
                    Action("Back to library") { playback.stop() }
                }
            }
    }
}

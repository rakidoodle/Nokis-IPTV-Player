import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct MotionButton:ButtonStyle {
    @Environment(\.accessibilityReduceMotion) var reduce
    func makeBody(configuration:Configuration) -> some View { configuration.label.padding(7).contentShape(RoundedRectangle(cornerRadius:7)).background(configuration.isPressed ? Color.accentColor.opacity(0.2) : Color.primary.opacity(0.05),in:RoundedRectangle(cornerRadius:7)).scaleEffect(configuration.isPressed && !reduce ? 0.94 : 1).animation(reduce ? nil : .easeOut(duration:0.13),value:configuration.isPressed) }
}
struct SourceEditor:View {
    @Environment(\.dismiss) var dismiss
    @ObservedObject var library:Library
    @State var source:Source
    @State var credentials:Credentials
    @State private var error:String?
    @State private var advanced=false
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack { Image(systemName:"antenna.radiowaves.left.and.right").font(.title).foregroundStyle(.tint); VStack(alignment:.leading) { Text("Source settings").font(.title2.bold()); Text("Connect your playlist or IPTV provider").foregroundStyle(.secondary) } }
            Form {
                TextField("Source name",text:$source.name)
                Picker("Source type",selection:$source.kind) { ForEach(SourceKind.allCases) { Text($0.rawValue).tag($0) } }
                URLInput(title:source.kind == .m3u ? "Playlist URL or file" : "Server / portal URL",text:$credentials.endpoint)
                if source.kind == .m3u { Button("Choose playlist file…") { let p=NSOpenPanel(); p.allowedContentTypes=[.init(filenameExtension:"m3u")!, .init(filenameExtension:"m3u8")!]; if p.runModal() == .OK,let url=p.url { credentials.endpoint=url.absoluteString; if source.name.isEmpty { source.name=url.deletingPathExtension().lastPathComponent } } } }
                if source.kind == .xtream { TextField("Username",text:$credentials.username); SecureField("Password",text:$credentials.password) }
                if source.kind == .stalker {
                    TextField("Registered MAC",text:$credentials.mac).help("Use the MAC registered with your IPTV provider")
                    DisclosureGroup("Additional device credentials",isExpanded:$advanced) { TextField("Serial number",text:$credentials.serial); TextField("Device ID",text:$credentials.deviceID); TextField("Device ID 2",text:$credentials.deviceID2); TextField("Username (if required)",text:$credentials.username); SecureField("Password (if required)",text:$credentials.password) }
                }
                URLInput(title:"EPG URL (optional)",text:$credentials.epg)
            }.textFieldStyle(.roundedBorder)
            Text("Credentials are stored in your Mac’s Keychain. Your provider supplies channels, movies, series, and guide availability.").font(.caption).foregroundStyle(.secondary)
            if let error { Label(error,systemImage:"exclamationmark.triangle").foregroundStyle(.red).font(.callout) }
            HStack { Spacer(); Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction); Button("Save & Connect") { do { try library.saveSource(source,credentials:credentials); dismiss() } catch { self.error=(error as? PlayerError)?.localizedDescription ?? "Source settings could not be saved." } }.keyboardShortcut(.defaultAction).buttonStyle(.borderedProminent) }
        }.padding(24).frame(width:620)
    }
}
struct PlayerPane:View {
    @ObservedObject var playback:Playback
    @ObservedObject var library:Library
    @Environment(\.accessibilityReduceMotion) var reduce
    var body:some View {
        ZStack {
            Color.black
            VideoSurface(playback:playback)
            if playback.item == nil {
                VStack(spacing:14) { Image(nsImage:NSImage(named:"AppIcon") ?? NSImage()).resizable().scaledToFit().frame(width:100,height:100); Text("Your next watch starts here").font(.title2.bold()); Text("Choose a channel, movie, or episode to play.").foregroundStyle(.white.opacity(0.65)) }.foregroundStyle(.white)
            }
            if playback.buffering { VStack(spacing:12) { ProgressView().controlSize(.large).colorScheme(.dark); Text(playback.reconnecting ? "Reconnecting…" : "Buffering…").font(.callout.weight(.medium)) }.padding(22).background(.black.opacity(0.7),in:RoundedRectangle(cornerRadius:14)).foregroundStyle(.white).accessibilityLabel("Video buffering") }
            if let error=playback.error { VStack(spacing:12) { Image(systemName:"exclamationmark.triangle").font(.title); Text(error).multilineTextAlignment(.center).frame(maxWidth:380); Button("Retry playback") { playback.retry() }.buttonStyle(.borderedProminent) }.padding(24).background(.black.opacity(0.85),in:RoundedRectangle(cornerRadius:14)).foregroundStyle(.white) }
            if let item=playback.item, playback.controls || !playback.fullscreen {
                VStack {
                    HStack { VStack(alignment:.leading,spacing:4) { Text(item.name).font(.headline); Text(item.kind == .live ? "LIVE • \(item.group)" : item.group).font(.caption).foregroundStyle(.white.opacity(0.7)) }; Spacer(); Button { playback.stop() } label: { Image(systemName:"xmark") }.help("Close playback").accessibilityLabel("Close playback") }.padding().background(LinearGradient(colors:[.black.opacity(0.8),.clear],startPoint:.top,endPoint:.bottom))
                    Spacer()
                    VStack(spacing:10) {
                        if playback.seekable { PlaybackScrubber(playback:playback,timeline:playback.timeline) }
                        HStack(spacing:12) {
                            if item.kind == .live { control("backward.end.fill","Previous channel") { playback.onChannel?(-1) } }
                            control(playback.playing ? "pause.fill" : "play.fill",playback.playing ? "Pause" : "Play") { playback.togglePause() }.keyboardShortcut(.space,modifiers:[])
                            if item.kind == .live { control("forward.end.fill","Next channel") { playback.onChannel?(1) } }
                            control(playback.muted ? "speaker.slash.fill" : "speaker.wave.2.fill","Mute / unmute") { playback.muted.toggle() }
                            Slider(value:$playback.volume,in:0...100).frame(width:90).accessibilityLabel("Volume")
                            Spacer(minLength:4)
                            control(library.favorites.contains(item.id) ? "star.fill" : "star","Toggle favorite") { library.toggleFavorite(item) }
                            Menu {
                                Button("Off") { playback.setSubtitle(-1) }
                                ForEach(playback.tracks.filter { $0.id != -1 }) { track in Button((playback.subtitle == track.id ? "✓ " : "")+track.name) { playback.setSubtitle(track.id) } }
                                ForEach(item.subtitles.keys.sorted(),id: \.self) { name in Button(name) { if let url=URL(string:item.subtitles[name] ?? "") { playback.loadSubtitle(url) } } }
                                Divider(); Button("Load subtitle file…") { playback.importSubtitle() }
                            } label: { Image(systemName:"captions.bubble").font(.title3) }.menuStyle(.borderlessButton).frame(width:32).help("Subtitles").accessibilityLabel("Subtitles")
                            control(playback.fullscreen ? "arrow.down.right.and.arrow.up.left" : "arrow.up.left.and.arrow.down.right",playback.fullscreen ? "Exit fullscreen" : "Fullscreen") { playback.toggleFullscreen() }
                        }
                    }.padding().background(LinearGradient(colors:[.clear,.black.opacity(0.9)],startPoint:.top,endPoint:.bottom))
                }.foregroundStyle(.white).buttonStyle(MotionButton()).transition(.opacity)
            }
        }.environment(\.colorScheme,.dark).clipped().animation(reduce ? nil : .easeInOut(duration:0.2),value:playback.controls).onHover { _ in playback.wakeControls() }
    }
    func control(_ icon:String,_ title:String,action:@escaping ()->Void) -> some View { Button(action:action) { Image(systemName:icon).font(.title3).frame(width:22,height:22) }.help(title).accessibilityLabel(title) }
}
struct LibraryRow:View {
    let item:MediaItem; let favorite:Bool; let now:String?
    let activate:()->Void; let toggle:()->Void
    var body:some View {
        HStack(spacing:12) {
            Button(action:activate) { HStack(spacing:12) {
                AsyncImage(url:URL(string:item.logo)) { image in image.resizable().scaledToFit() } placeholder: { Image(systemName:item.kind == .live ? "tv" : item.kind == .series ? "rectangle.stack" : "film").font(.title2).foregroundStyle(.secondary) }.frame(width:44,height:44).background(Color.primary.opacity(0.04),in:RoundedRectangle(cornerRadius:8))
                VStack(alignment:.leading,spacing:4) { Text(item.name).font(.body.weight(.medium)).lineLimit(1); Text(now ?? item.group).font(.caption).foregroundStyle(.secondary).lineLimit(1) }; Spacer(); Image(systemName:item.kind == .series ? "chevron.right" : "play.circle").foregroundStyle(.tint)
            }.contentShape(Rectangle()) }.buttonStyle(.plain)
            Button(action:toggle) { Image(systemName:favorite ? "star.fill" : "star").foregroundStyle(favorite ? Color.yellow : .secondary) }.buttonStyle(.plain).help("Toggle favorite").accessibilityLabel("Favorite \(item.name)")
        }.padding(.vertical,5)
    }
}
struct RootView:View {
    @ObservedObject var library:Library
    @ObservedObject var playback:Playback
    @State private var editor:Source?
    @State private var editCredentials=Credentials()
    @State private var selectedGuide:MediaItem?
    @State private var season:Int=1
    @State private var deleteSource=false
    var body:some View {
        HStack(spacing:0) {
            if !playback.fullscreen { sidebar.frame(width:205); Divider() }
            VStack(spacing:0) {
                PlayerPane(playback:playback,library:library).frame(maxHeight:playback.fullscreen ? .infinity : 370).frame(minHeight:240)
                if !playback.fullscreen { Divider(); browser }
            }
        }.frame(minWidth:820,minHeight:620).background(Color(nsColor:.windowBackgroundColor))
        .toolbar { if !playback.fullscreen {
            ToolbarItem(placement:.primaryAction) {
                HStack(spacing:8) {
                    Button { library.refresh() } label: {
                        ZStack {
                            Image(systemName:"arrow.clockwise").opacity(library.loading ? 0 : 1)
                            if library.loading { ProgressView().controlSize(.small).frame(width:16,height:16) }
                        }.frame(width:32,height:32).contentShape(Circle())
                    }.help(library.loading ? "Loading library" : "Refresh library").accessibilityLabel("Refresh library").disabled(library.source == nil || library.loading)
                    Button { addSource() } label: { Image(systemName:"plus").frame(width:32,height:32).contentShape(Circle()) }.help("Add source").accessibilityLabel("Add source")
                }.buttonStyle(.plain).font(.system(size:16,weight:.medium)).padding(.horizontal,6).fixedSize()
            }
        } }
        .sheet(item:$editor) { source in SourceEditor(library:library,source:source,credentials:editCredentials) }
        .alert("Unable to complete action",isPresented:Binding(get:{library.error != nil},set:{if !$0 {library.error=nil}})) { Button("OK") { library.error=nil } } message: { Text(library.error ?? "") }
        .confirmationDialog("Remove this source and its saved credentials?",isPresented:$deleteSource) { Button("Remove source",role:.destructive) { if let source=library.source { playback.stop(); library.removeSource(source) } } }
        .onChange(of:library.selectedSourceID) { _,_ in playback.stop() }
        .onChange(of:library.section) { _,_ in library.group="All groups"; library.series=nil }
        .onAppear { playback.onChannel = { offset in let channels=library.catalog.items.filter { $0.kind == .live }; if let id=playback.item?.id,let index=channels.firstIndex(where:{$0.id == id}),!channels.isEmpty { play(channels[(index+offset+channels.count)%channels.count]) } } }
    }
    var sidebar:some View {
        VStack(alignment:.leading,spacing:20) {
            HStack(spacing:10) { Image(nsImage:NSImage(named:"AppIcon") ?? NSImage()).resizable().scaledToFit().frame(width:42,height:46); VStack(alignment:.leading) { Text("Noki’s IPTV").font(.headline); Text("PLAYER FOR MACOS").font(.system(size:9,weight:.semibold)).foregroundStyle(.secondary) } }.padding(.top,8)
            VStack(alignment:.leading,spacing:8) { Text("SOURCE").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                if library.sources.isEmpty { Button("Add your first source…") { addSource() }.buttonStyle(.borderedProminent) }
                else { Picker("Source",selection:$library.selectedSourceID) { ForEach(library.sources) { Text($0.name).tag(Optional($0.id)) } }.labelsHidden()
                    HStack { Button("Edit") { editSource() }; Spacer(); Button { deleteSource=true } label: { Image(systemName:"trash") }.help("Remove source") }.buttonStyle(.borderless)
                }
            }
            VStack(spacing:5) { nav("Live TV","tv"); nav("Movies","film"); nav("Series","rectangle.stack"); nav("Favorites","star"); nav("TV Guide","calendar") }
            Spacer()
            if library.guideLoading { ProgressView().controlSize(.small) }
            Text(library.connectionStatus).font(.caption).foregroundStyle(.secondary)
            Text(library.guideStatus).font(.caption).foregroundStyle(.secondary)
            Button { library.refreshGuide() } label: { Label("Refresh guide",systemImage:"arrow.clockwise") }.buttonStyle(.borderless).disabled(library.source == nil || library.guideLoading)
        }.padding(16).background(Color(nsColor:.controlBackgroundColor))
    }
    func nav(_ name:String,_ icon:String) -> some View { Button { library.section=name } label: { HStack(spacing:10) { Image(systemName:icon).frame(width:20); Text(name); Spacer(); if name == "Favorites" { Text("\(library.favorites.count)").font(.caption) } }.padding(10).contentShape(RoundedRectangle(cornerRadius:8)).background(library.section == name ? Color.accentColor.opacity(0.16) : .clear,in:RoundedRectangle(cornerRadius:8)) }.buttonStyle(.plain).foregroundStyle(library.section == name ? Color.accentColor : Color.primary) }
    var browser:some View {
        VStack(spacing:0) {
            HStack { Text(library.series?.name ?? library.section).font(.title3.bold()); Spacer(); TextField("Search library",text:$library.query).textFieldStyle(.roundedBorder).frame(maxWidth:230); if library.section != "TV Guide" && library.series == nil { Picker("Group",selection:$library.group) { ForEach(library.groups,id:\.self) { Text($0).tag($0) } }.labelsHidden().frame(maxWidth:180) } }.padding()
            if !library.catalog.warnings.isEmpty { Text(library.catalog.warnings.joined(separator:" ")).font(.caption).foregroundStyle(.orange).padding(.horizontal) }
            if library.sources.isEmpty { empty("Add your IPTV source","Import an M3U playlist or connect to Xtream Codes or a Stalker portal.",icon:"plus.rectangle.on.folder").overlay(alignment:.bottom) { Button("Add Source") { addSource() }.buttonStyle(.borderedProminent).padding(.bottom,24) } }
            else if library.section == "TV Guide" { guideView }
            else if library.series != nil { episodeView }
            else if library.filtered.isEmpty { empty(library.loading ? "Loading your library…" : "Nothing here yet",library.section == "Favorites" ? "Click a star beside a channel, movie, or series to save it." : "Try another category or search, or refresh your source.",icon:library.loading ? "arrow.triangle.2.circlepath" : "rectangle.stack") }
            else { List(library.filtered) { item in LibraryRow(item:item,favorite:library.favorites.contains(item.id),now:item.kind == .live ? library.guide(item).first?.title : nil,activate:{ if item.kind == .series { library.openSeries(item); season=1 } else { play(item) } },toggle:{library.toggleFavorite(item)}) }.listStyle(.inset) }
        }.frame(maxWidth:.infinity,maxHeight:.infinity)
    }
    var episodeView:some View { VStack {
        HStack { Button { library.series=nil } label: { Label("Back to library",systemImage:"chevron.left") }; Spacer(); Picker("Season",selection:$season) { ForEach(Array(Set(library.episodes.map { $0.season ?? 1 })).sorted(),id:\.self) { Text("Season \($0)").tag($0) } }.frame(width:170) }.padding(.horizontal)
        if library.episodeLoading { ProgressView("Loading episodes…").frame(maxWidth:.infinity,maxHeight:.infinity) }
        else if library.episodes.isEmpty { empty("No episodes available","The provider did not supply episodes for this series.",icon:"rectangle.stack") }
        else { List(library.episodes.filter { ($0.season ?? 1) == season && (library.query.isEmpty || $0.name.localizedCaseInsensitiveContains(library.query)) }) { item in LibraryRow(item:item,favorite:library.favorites.contains(item.id),now:"Episode \(item.episode ?? 0)",activate:{play(item)},toggle:{library.toggleFavorite(item)}) }.listStyle(.inset) }
    }.onChange(of:library.episodes) { _,items in season=items.map { $0.season ?? 1 }.min() ?? 1 } }
    var guideView:some View { HSplitView {
        List(selection:$selectedGuide) { ForEach(library.catalog.items.filter { $0.kind == .live && (library.query.isEmpty || $0.name.localizedCaseInsensitiveContains(library.query)) }) { item in VStack(alignment:.leading,spacing:3) { Text(item.name); Text(library.guide(item).first?.title ?? "No guide available").font(.caption).foregroundStyle(.secondary) }.tag(item) } }.frame(minWidth:200)
        if let item=selectedGuide { VStack(alignment:.leading) { HStack { Text(item.name).font(.headline); Spacer(); Button("Watch") { play(item) }; Button { library.toggleFavorite(item) } label: { Image(systemName:library.favorites.contains(item.id) ? "star.fill" : "star") }.help("Favorite channel") }.padding()
            ScrollView { LazyVStack(alignment:.leading,spacing:14) { ForEach(library.guide(item)) { p in VStack(alignment:.leading,spacing:4) { Text("\(p.start.formatted(date:.abbreviated,time:.shortened)) – \(p.end.formatted(date:.omitted,time:.shortened))").font(.caption).foregroundStyle(.secondary); Text(p.title).font(.headline); if !p.detail.isEmpty { Text(p.detail).font(.callout).foregroundStyle(.secondary) } }; Divider() } }.padding() }
        }.frame(minWidth:260) } else { empty("Program guide","Select a channel to see its schedule.",icon:"calendar").frame(minWidth:260) }
    } }
    func empty(_ title:String,_ detail:String,icon:String) -> some View { VStack(spacing:10) { Image(systemName:icon).font(.system(size:32)).foregroundStyle(.tertiary); Text(title).font(.headline); Text(detail).font(.callout).foregroundStyle(.secondary).multilineTextAlignment(.center).frame(maxWidth:390) }.frame(maxWidth:.infinity,maxHeight:.infinity).padding() }
    func play(_ item:MediaItem) { guard let provider=library.provider else { return }; playback.play(item,provider:provider) }
    func addSource() { editCredentials=Credentials(); editor=Source(name:"",kind:.m3u) }
    func editSource() { guard let source=library.source else { return }; do { editCredentials=try SecureStore.credentials(source.id); editor=source } catch { editCredentials=Credentials(); editor=source } }
}

struct URLInput:View {
    let title:String
    @Binding var text:String
    var body:some View {
        VStack(alignment:.leading,spacing:6) {
            Text(title).font(.callout.weight(.medium))
            TextField("https://…",text:$text,axis:.vertical)
                .labelsHidden().textFieldStyle(.plain).font(.system(.body,design:.monospaced))
                .lineLimit(2...4).foregroundStyle(Color.primary).tint(.accentColor)
                .padding(10).frame(maxWidth:.infinity,alignment:.leading)
                .background(Color(nsColor:.textBackgroundColor),in:RoundedRectangle(cornerRadius:8))
                .overlay(RoundedRectangle(cornerRadius:8).stroke(Color.primary.opacity(0.2)))
                .accessibilityLabel(title)
        }.frame(maxWidth:.infinity,alignment:.leading)
    }
}
struct PlaybackScrubber:View {
    let playback:Playback
    @ObservedObject var timeline:PlaybackTimeline
    var body:some View {
        HStack {
            Text(timeline.elapsed).monospacedDigit().font(.caption)
            Slider(value:Binding(get:{timeline.position},set:{playback.seek($0)}),in:0...1).accessibilityLabel("Playback position")
            Text(timeline.duration).monospacedDigit().font(.caption)
        }
    }
}

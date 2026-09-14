import SwiftUI
import AppKit
import VLCKit
import UniformTypeIdentifiers

/// Only the scrubber observes rapidly changing time values; the library does not.
@MainActor final class PlaybackTimeline: ObservableObject {
    @Published var position:Double=0
    @Published var elapsed="00:00"
    @Published var duration="00:00"
}

@MainActor final class Playback: NSObject, ObservableObject {
    let engine=VLCMediaPlayer(options:["--quiet","--no-video-title-show","--avcodec-hw=any","--network-caching=2000"])
    let surface=VLCVideoView(frame:.zero)
    let timeline=PlaybackTimeline()
    @Published var item:MediaItem?
    @Published var playing=false
    @Published var buffering=false
    @Published var reconnecting=false
    @Published var error:String?
    @Published var seekable=false
    @Published var volume:Double=80 { didSet { engine.audio?.volume=Int32(volume) } }
    @Published var muted=false { didSet { engine.audio?.isMuted=muted } }
    @Published var tracks:[SubtitleTrack]=[]
    @Published var subtitle:Int32 = -1
    @Published var fullscreen=false
    @Published var controls=true
    var lastInteraction=Date()
    var provider:Provider?
    private var timer:Timer?
    private var task:Task<Void,Never>?
    private var attempts=0
    private var generation=UUID()
    private var active=false
    private var resolving=false
    private var userPaused=false
    private var health=PlaybackHealth(now:ProcessInfo.processInfo.systemUptime)
    private var resumeTime:Int32?
    private var lastPlayableTime:Int32=0
    private var externalSubtitle:URL?
    private var pendingSubtitle:URL?
    private var selectedSubtitleName:String?
    private var restoreSubtitle=false
    private(set) var recoveryCount=0
    var onChannel:((Int)->Void)?
    var eventMonitor:Any?
    struct SubtitleTrack:Identifiable, Equatable { var id:Int32; var name:String }
    override init() {
        super.init(); surface.fillScreen=false; engine.drawable=surface; engine.audio?.volume=80
        let timer=Timer(timeInterval:0.5,repeats:true) { [weak self] _ in Task { @MainActor in self?.tick() } }
        RunLoop.main.add(timer,forMode:.common); self.timer=timer
        eventMonitor=NSEvent.addLocalMonitorForEvents(matching:[.mouseMoved,.leftMouseDown,.keyDown]) { [weak self] event in
            guard let self else { return event }; self.wakeControls()
            if event.type == .keyDown, let window=NSApp.keyWindow, window.sheetParent == nil, window.attachedSheet == nil, !(window.firstResponder is NSTextView) {
                if event.keyCode == 53 && self.fullscreen { self.toggleFullscreen(); return nil }
                if event.keyCode == 49 && self.item != nil { self.togglePause(); return nil }
            }; return event
        }
        NotificationCenter.default.addObserver(forName:NSWindow.didEnterFullScreenNotification,object:nil,queue:.main) { [weak self] _ in Task { @MainActor in self?.fullscreen=true; self?.wakeControls() } }
        NotificationCenter.default.addObserver(forName:NSWindow.didExitFullScreenNotification,object:nil,queue:.main) { [weak self] _ in Task { @MainActor in self?.fullscreen=false; self?.wakeControls() } }
    }
    func wakeControls() { lastInteraction=Date(); if !controls { controls=true } }
    func toggleFullscreen() { surface.window?.toggleFullScreen(nil) }
    func play(_ item:MediaItem,provider:Provider) {
        task?.cancel(); generation=UUID(); active=false; engine.stop()
        self.item=item; self.provider=provider; attempts=0; recoveryCount=0; error=nil; tracks=[]; subtitle = -1
        timeline.position=0; timeline.elapsed="00:00"; timeline.duration="00:00"
        resumeTime=nil; lastPlayableTime=0; externalSubtitle=nil; pendingSubtitle=nil; selectedSubtitleName=nil; restoreSubtitle=false
        userPaused=false; active=true; start()
    }
    func start() {
        guard let item,let provider else { return }
        let id=generation; buffering=true; resolving=true
        health=PlaybackHealth(now:ProcessInfo.processInfo.systemUptime)
        task=Task { do {
            // Resolve again on every retry so expiring Stalker links are renewed.
            let stream=try await provider.resolve(item); try Task.checkCancellation(); guard id == generation else { return }
            let media=VLCMedia(url:stream.url)
            media.addOption(":network-caching=\(min(5000,2000+attempts*1000))")
            if let agent=stream.headers["User-Agent"] { media.addOption(":http-user-agent=\(agent)") }
            if let ref=stream.headers["Referer"] { media.addOption(":http-referrer=\(ref)") }
            engine.media=media; resolving=false; health=PlaybackHealth(now:ProcessInfo.processInfo.systemUptime); engine.play()
        } catch { guard !Task.isCancelled,id == generation else { return }; recover() } }
    }
    func stop() {
        task?.cancel(); generation=UUID(); active=false; resolving=false; userPaused=false
        engine.stop(); playing=false; buffering=false; reconnecting=false; item=nil; error=nil; resumeTime=nil
    }
    func togglePause() {
        wakeControls(); guard item != nil else { return }
        if resolving { return }
        if engine.isPlaying { userPaused=true; engine.pause(); if buffering { buffering=false } }
        else { userPaused=false; if !active { retry() } else { health.didSeek(now:ProcessInfo.processInfo.systemUptime); engine.play() } }
    }
    func retry() { task?.cancel(); attempts=0; error=nil; userPaused=false; active=true; engine.stop(); start() }
    /// Shared path for watchdog stalls, transport errors, and ended live streams.
    func recover() {
        guard active, !userPaused else { return }
        if item?.kind != .live && lastPlayableTime > 0 { resumeTime=lastPlayableTime }
        pendingSubtitle=externalSubtitle; restoreSubtitle=true
        resolving=true; engine.stop()
        if attempts < 3 {
            attempts += 1; recoveryCount += 1; buffering=true; reconnecting=true; let id=generation
            task=Task {
                try? await Task.sleep(nanoseconds:UInt64(min(attempts,3))*1_000_000_000)
                guard !Task.isCancelled,id == generation,active else { return }; start()
            }
        } else {
            active=false; resolving=false; buffering=false; reconnecting=false; playing=false
            error="The stream is still unavailable after 3 reconnect attempts. Check your connection or retry."
        }
    }
    func tick() {
        let now=ProcessInfo.processInfo.systemUptime
        let isPlaying=engine.isPlaying
        if playing != isPlaying { playing=isPlaying }
        if seekable != engine.isSeekable { seekable=engine.isSeekable }
        let nextPosition=Double(engine.position)
        if abs(timeline.position-nextPosition) > 0.0001 { timeline.position=nextPosition }
        let elapsed=engine.time.stringValue; let duration=engine.media?.length.stringValue ?? "00:00"
        if timeline.elapsed != elapsed { timeline.elapsed=elapsed }
        if timeline.duration != duration { timeline.duration=duration }
        let time=engine.time.intValue
        if active && !resolving {
            if isPlaying, let resumeTime,engine.isSeekable {
                engine.time=VLCTime(int:resumeTime); self.resumeTime=nil; health.didSeek(now:now)
            } else if isPlaying && time > 0 { lastPlayableTime=time }
            if isPlaying,let pendingSubtitle { _ = engine.addPlaybackSlave(pendingSubtitle,type:.subtitle,enforce:true); self.pendingSubtitle=nil }
            let state=engine.state
            let frames=engine.media?.statistics.displayedPictures ?? 0
            let paused=userPaused || state == .paused
            let action=health.evaluate(now:now,time:time,frames:frames,paused:paused,failed:state == .error)
            if state == .ended && !paused {
                if item?.kind == .live || !health.started { recover() }
                else { active=false; if buffering { buffering=false }; if reconnecting { reconnecting=false } }
            } else if action == .reconnect { recover() }
            else {
                let waiting=action == .buffering
                if buffering != waiting { buffering=waiting }
                if action == .healthy && reconnecting { reconnecting=false }
                if health.stable(at:now) { attempts=0 }
            }
        }
        // Subtitles rarely change. Publish only an actual track/selection change.
        let names=engine.videoSubTitlesNames as? [String] ?? []
        let indexes=engine.videoSubTitlesIndexes as? [NSNumber] ?? []
        let newTracks=zip(indexes,names).map { SubtitleTrack(id:$0.0.int32Value,name:$0.1) }
        if tracks != newTracks { tracks=newTracks }
        if restoreSubtitle && isPlaying && !newTracks.isEmpty {
            if let name=selectedSubtitleName,let track=newTracks.first(where:{$0.name == name}) { engine.currentVideoSubTitleIndex=track.id; restoreSubtitle=false }
            else if selectedSubtitleName == nil && externalSubtitle == nil { engine.currentVideoSubTitleIndex = -1; restoreSubtitle=false }
        }
        if subtitle != engine.currentVideoSubTitleIndex { subtitle=engine.currentVideoSubTitleIndex }
        if fullscreen && playing && !buffering && controls && Date().timeIntervalSince(lastInteraction)>3 { controls=false }
    }
    func seek(_ value:Double) {
        if engine.isSeekable { engine.position=Float(value); resumeTime=nil; health.didSeek(now:ProcessInfo.processInfo.systemUptime) }; wakeControls()
    }
    func setSubtitle(_ id:Int32) {
        engine.currentVideoSubTitleIndex=id; subtitle=id; selectedSubtitleName=tracks.first { $0.id == id && id != -1 }?.name
        if id == -1 { externalSubtitle=nil; pendingSubtitle=nil }; wakeControls()
    }
    func loadSubtitle(_ url:URL) {
        if engine.addPlaybackSlave(url,type:.subtitle,enforce:true) != 0 { error="This subtitle could not be loaded. Try another track or file." }
        else { externalSubtitle=url }; wakeControls()
    }
    func importSubtitle() {
        let panel=NSOpenPanel(); panel.allowedContentTypes=[.init(filenameExtension:"srt")!, .init(filenameExtension:"vtt")!, .init(filenameExtension:"ass")!]; panel.allowsMultipleSelection=false
        guard panel.runModal() == .OK, let url=panel.url else { return }; loadSubtitle(url)
    }
}
struct VideoSurface:NSViewRepresentable {
    let playback:Playback
    func makeNSView(context:Context) -> VLCVideoView { playback.surface }
    func updateNSView(_ view:VLCVideoView,context:Context) {}
}

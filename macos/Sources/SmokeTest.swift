import Foundation
import AppKit
import VLCKit
import Combine

@MainActor enum SmokeTest {
    static var started=false
    static func run(playback:Playback,directory:URL) {
        guard !started else { return }; started=true
        Task { @MainActor in
            try? await Task.sleep(nanoseconds:1_000_000_000)
            var checks:[String:Bool]=[:]
            let source=Source(id:"smoke",name:"Local test",kind:.m3u)
            let movie=directory.appendingPathComponent("sample.mp4")
            let item=MediaItem(id:"smoke:movie",sourceID:source.id,providerID:"movie",name:"Playback & Subtitle Test",kind:.movie,group:"Local fixtures",location:movie.absoluteString)
            let provider=M3UProvider(source:source,credentials:Credentials(endpoint:movie.absoluteString))
            playback.play(item,provider:provider)
            for _ in 0..<60 { if playback.engine.isPlaying && playback.engine.time.intValue > 0 { break }; try? await Task.sleep(nanoseconds:250_000_000) }
            try? await Task.sleep(nanoseconds:500_000_000)
            checks["video_started"]=playback.engine.isPlaying && playback.engine.time.intValue > 0
            checks["video_track"]=playback.engine.numberOfVideoTracks > 0
            checks["audio_track"]=playback.engine.numberOfAudioTracks > 0
            checks["seekable"]=playback.engine.isSeekable
            checks["buffering_clears"] = !playback.buffering
            var fullInterfaceUpdates=0
            let observation=playback.objectWillChange.sink { fullInterfaceUpdates += 1 }
            try? await Task.sleep(nanoseconds:1_500_000_000)
            observation.cancel()
            checks["progress_does_not_redraw_library"]=fullInterfaceUpdates <= 2
            playback.engine.pause(); try? await Task.sleep(nanoseconds:600_000_000)
            checks["pause"] = !playback.engine.isPlaying
            playback.engine.play(); playback.seek(0.35); try? await Task.sleep(nanoseconds:1_000_000_000)
            checks["seek"]=playback.engine.position > 0.25
            checks["srt_import"]=playback.engine.addPlaybackSlave(directory.appendingPathComponent("sample.srt"),type:.subtitle,enforce:true) == 0
            try? await Task.sleep(nanoseconds:1_000_000_000)
            checks["subtitle_track"]=playback.engine.numberOfSubtitlesTracks > 0
            playback.setSubtitle(-1); checks["subtitle_off"]=playback.engine.currentVideoSubTitleIndex == -1
            checks["vtt_import"]=playback.engine.addPlaybackSlave(directory.appendingPathComponent("sample.vtt"),type:.subtitle,enforce:true) == 0
            playback.volume=35; playback.muted=true
            checks["volume_and_mute"]=playback.engine.audio?.volume == 35 && playback.engine.audio?.isMuted == true
            playback.toggleFullscreen(); try? await Task.sleep(nanoseconds:2_000_000_000)
            checks["fullscreen_enter"]=playback.fullscreen
            playback.lastInteraction=Date().addingTimeInterval(-5); playback.tick(); checks["controls_autohide"] = !playback.controls
            playback.wakeControls(); checks["controls_reappear"]=playback.controls
            playback.toggleFullscreen(); try? await Task.sleep(nanoseconds:2_000_000_000)
            checks["fullscreen_exit"] = !playback.fullscreen
            playback.stop(); checks["stop"]=playback.item == nil && !playback.buffering
            var missing=item; missing.location=directory.appendingPathComponent("does-not-exist.mp4").absoluteString
            playback.play(missing,provider:provider); checks["loading_indicator"]=playback.buffering
            for _ in 0..<48 { if playback.error != nil { break }; try? await Task.sleep(nanoseconds:250_000_000) }
            checks["unavailable_stream_bounded_retry"]=playback.error != nil && !playback.buffering
            playback.play(item,provider:provider)
            for _ in 0..<32 { if playback.engine.isPlaying && playback.engine.time.intValue > 0 { break }; try? await Task.sleep(nanoseconds:250_000_000) }
            checks["recovery_after_error"]=playback.engine.isPlaying && playback.error == nil
            playback.loadSubtitle(directory.appendingPathComponent("sample.srt"))
            playback.seek(0.4)
            try? await Task.sleep(nanoseconds:1_000_000_000)
            let recoveryBefore=playback.recoveryCount
            playback.recover()
            checks["reconnect_indicator"]=playback.reconnecting && playback.buffering
            for _ in 0..<32 { if playback.engine.isPlaying && playback.engine.time.intValue > 7000 && !playback.reconnecting { break }; try? await Task.sleep(nanoseconds:250_000_000) }
            checks["reconnect_resumes_vod"]=playback.engine.isPlaying && playback.engine.time.intValue > 7000
            checks["reconnect_reopens_stream"]=playback.recoveryCount == recoveryBefore+1
            checks["reconnect_restores_subtitles"]=playback.engine.numberOfSubtitlesTracks > 0
            playback.togglePause()
            let pausedRecoveryCount=playback.recoveryCount
            playback.recover()
            checks["manual_pause_does_not_reconnect"]=playback.recoveryCount == pausedRecoveryCount
            playback.stop()
            if let data=try? JSONSerialization.data(withJSONObject:checks,options:[.prettyPrinted,.sortedKeys]) { try? data.write(to:directory.appendingPathComponent("playback-results.json")) }
        }
    }
}

import Foundation

/// A monotonic-clock watchdog. Brief buffering and intentional pauses never restart playback.
struct PlaybackHealth {
    enum Action: Equatable { case healthy, buffering, reconnect }
    var openedAt: TimeInterval
    var lastProgress: TimeInterval
    var lastFrameProgress: TimeInterval
    var lastTime: Int32 = -1
    var lastFrames: Int32 = 0
    var started = false
    var healthySince: TimeInterval?
    init(now: TimeInterval) { openedAt=now; lastProgress=now; lastFrameProgress=now }
    mutating func evaluate(now:TimeInterval, time:Int32, frames:Int32, paused:Bool, failed:Bool) -> Action {
        if paused {
            lastProgress=now; lastFrameProgress=now; healthySince=nil
            lastTime=time; lastFrames=frames
            return .healthy
        }
        if failed { healthySince=nil; return .reconnect }
        if time != lastTime {
            if time > 0 { started=true }
            lastTime=time; lastProgress=now
        }
        if frames != lastFrames { lastFrames=frames; lastFrameProgress=now; if frames > 0 { started=true } }
        // A zero frame counter is also valid for audio-only streams and unavailable statistics.
        let stalledFor=frames > 0 ? now-lastFrameProgress : now-lastProgress
        if (!started && now-openedAt >= 20) || (started && stalledFor >= 8) {
            healthySince=nil; return .reconnect
        }
        if !started || stalledFor >= 2 {
            healthySince=nil; return .buffering
        }
        if healthySince == nil { healthySince=now }
        return .healthy
    }
    func stable(at now:TimeInterval) -> Bool { healthySince.map { now-$0 >= 30 } ?? false }
    mutating func didSeek(now:TimeInterval) { lastProgress=now; lastFrameProgress=now; healthySince=nil }
}

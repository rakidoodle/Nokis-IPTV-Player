namespace MyIPTV.Core.Models;

public enum MediaPlaybackState
{
    Idle,
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped,
    Ended,
    Error,
}

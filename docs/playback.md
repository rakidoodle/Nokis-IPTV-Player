# Media playback

MyIPTV uses the stable VideoLAN LibVLC 3.x engine through LibVLCSharp. The Windows native runtime ships with the application, so a separate VLC installation is not required.

## Supported controls

- Pause and resume
- Stop
- Volume and mute
- One-shot reconnect (no endless retry loop)
- Full screen, with `Escape` to return to the previous window state
- Default, 16:9, 4:3, 21:9, and 1:1 aspect ratios
- Audio track selection when the stream exposes tracks
- Subtitle selection, including the disabled track when LibVLC exposes it

The player displays the content title, logo reference, current program, and next program when providers or EPG data supply them. Missing program data receives a readable placeholder.

## Stream behavior

The playback boundary accepts HTTP and HTTPS media URLs. LibVLC handles common IPTV transports delivered over those URLs, including HLS/M3U8 and MPEG transport streams, subject to the codecs and demuxers in the bundled VideoLAN runtime. A 1.5-second network cache is used as a conservative starting point.

Opening, buffering, playing, paused, stopped, ended, and error states are reported without exposing a stream URL. Reconnect repeats only the current authorized playback request once. TLS certificate validation is not disabled.

## WPF integration

WPF uses a native child window for performant video rendering. The empty/error overlay is hosted inside `VideoView`, following VideoLAN's documented workaround for WPF's airspace limitation. Transport controls stay outside the video surface and remain accessible to keyboard and assistive technology.

## References and licenses

- [VideoLAN LibVLCSharp documentation](https://docs.videolan.me/libvlcsharp/docs/home.html)
- [VideoLAN WPF getting-started and airspace guidance](https://docs.videolan.me/libvlcsharp/docs/getting_started.html)
- `LibVLCSharp.WPF` 3.10.1 — LGPL-2.1-or-later
- `VideoLAN.LibVLC.Windows` 3.0.23.1 — LGPL-2.1-or-later

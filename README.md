# Noki’s IPTV Player

<a href="https://paypal.me/RuffyTrinidad" class="btn" target="_blank">Support to feed my baby.</a>


IPTV players for **Windows, macOS, Android TV, and Google TV**. Add your own M3U playlist, Xtream account, or supported Stalker portal to browse live channels, movies, series, favorites, and TV guides.

No channels, subscriptions, or provider credentials are included.

## Download

| Platform | Version | Download | Requirements |
| --- | --- | --- | --- |
| Android TV / Google TV | 0.1.3 preview | [APK](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/download/cross-platform-preview-2026.09.14/Noki-IPTV-TV-0.1.3.apk) | Android 8.0+; TV interface and remote |
| macOS | 1.1.0 preview | [DMG](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/download/cross-platform-preview-2026.09.14/Noki-IPTV-1.1.0-arm64.dmg) | Apple Silicon; macOS 14+ |
| Windows | 1.2.0-beta.6 | [Windows x64 ZIP](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/download/v1.2.0-beta.6/Nokis-IPTV-Player-1.2.0-beta.6-win-x64.zip) | Windows 10/11, 64-bit |

[macOS + Android TV release and checksums](https://github.com/rakidoodle/Nokis-IPTV-Player/releases/tag/cross-platform-preview-2026.09.14) · [All releases](https://github.com/rakidoodle/Nokis-IPTV-Player/releases) · [Report an issue](https://github.com/rakidoodle/Nokis-IPTV-Player/issues)

Choose the download for your device. The release also includes a combined ZIP containing the macOS DMG and Android TV APK.

## Install

### Android TV and Google TV

Transfer the APK to your TV and open it with an APK installer, or install over ADB:

```sh
adb install -r Noki-IPTV-TV-0.1.3.apk
```

Install over your existing Noki’s IPTV app to retain sources and favorites. This is a debug-signed preview, not a Play Store release. Phones and tablets do not have a dedicated layout.

Choose **Add source** to enter details with the remote, or **Use phone / show QR** to paste the source from a phone on the same trusted network. Choose whether to include Movies (VOD) and Series before saving. Categories use a dropdown. During playback, **Back hides visible controls first**, including while paused; another Back returns to the library.

[Android TV setup, remote controls, and build instructions](android-tv/README.md)

### macOS

Open the DMG and drag **Noki’s IPTV Player for MacOS** to Applications. VLCKit is included; a separate VLC installation is unnecessary. This Apple Silicon build is ad-hoc signed and not notarized. If macOS blocks opening it, review the download and use **System Settings → Privacy & Security → Open Anyway**. Do not disable Gatekeeper globally.

[macOS setup and build instructions](macos/README.md)

### Windows

Extract the entire ZIP and run `MyIPTV.App.exe`. The portable package includes .NET and LibVLC. The Windows application and its existing release remain unchanged.

[Windows installation, features, and development guide](WINDOWS.md)

## Source code

These are separate native applications; features and provider compatibility can differ.

| Project | Location | Stack |
| --- | --- | --- |
| Android TV / Google TV | [android-tv/](android-tv/) | Kotlin, Compose for TV, Media3 |
| macOS | [macos/](macos/) | SwiftUI, VLCKit |
| Windows | [src/](src/), `MyIPTV.sln` | .NET, WPF, LibVLC |

Clone this repository or download a source archive from a release. SDKs, build caches, signing keys, and private IPTV data are excluded. Use the platform guide for build prerequisites.

## Preview validation

The Android 0.1.3 verification covers 21 JVM checks and 14 Android TV instrumentation scenarios, including a 50,000-channel catalog, encrypted caching, phone setup, and playback controls. The macOS 1.1.0 report records 41 core checks and 26 local playback checks. These fixture results do not guarantee compatibility with every provider or device.

- [Android verification](android-tv/TEST-REPORT-0.1.3.md)
- [macOS verification](macos/TEST-REPORT-1.1.md)
- [Windows testing](docs/testing.md)

## Dependencies and content

Bundled dependencies retain their upstream licenses and notices: [Android](android-tv/THIRD-PARTY-NOTICES.md), [macOS VLCKit](macos/Vendor/DEPENDENCY.txt), and [Windows](docs/dependencies.md). Bring sources you are authorized to access. This project supplies players, not television services or IPTV accounts.

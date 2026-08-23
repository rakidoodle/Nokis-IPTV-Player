# Development data

The Settings page can load a synthetic `Development Demo` profile containing three channels, two movies, one series, and two episodes. Every media address uses the reserved `example.invalid` domain, so the fixture cannot contact or impersonate a real IPTV service.

Use **Settings > Data > Load demo library** to preview populated screens. Choose **Remove demo library** to delete only the fixed demo profile and its in-memory catalog. Real profiles and user history are not changed.

`samples/demo-playlist.m3u` is the equivalent parser fixture for manual import testing. It contains no credentials and no playable media.

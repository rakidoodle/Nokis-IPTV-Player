#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${NOKI_SKIP_BUILD:-0}" != "1" ]]; then Scripts/build.sh; fi
NAME="Noki’s IPTV Player for MacOS"
STAGE="${NOKI_BUILD_ROOT:-/tmp/noki-iptv-build-$UID}/dmg-stage"
mkdir -p "$STAGE"
# This directory only holds generated installer content.
if [[ -d "$STAGE/$NAME.app" ]]; then rm -rf "$STAGE/$NAME.app"; fi
ditto --noextattr --norsrc "${NOKI_BUILD_ROOT:-/tmp/noki-iptv-build-$UID}/$NAME.app" "$STAGE/$NAME.app"
ln -sfn /Applications "$STAGE/Applications"
cp README.md "$STAGE/Read Me.txt"
hdiutil create -volname "Noki IPTV Player" -srcfolder "$STAGE" -ov -format UDZO "dist/Noki-IPTV-1.1.0-arm64.dmg"
hdiutil verify "dist/Noki-IPTV-1.1.0-arm64.dmg"
shasum -a 256 "dist/Noki-IPTV-1.1.0-arm64.dmg" > "dist/Noki-IPTV-1.1.0-arm64.dmg.sha256"

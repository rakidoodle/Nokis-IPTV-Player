#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
NAME="Noki’s IPTV Player for MacOS"
mkdir -p dist
BUILD_ROOT="${NOKI_BUILD_ROOT:-/tmp/noki-iptv-build-$UID}"
APP="$BUILD_ROOT/$NAME.app"
FRAMEWORK="$(find Vendor -name VLCKit.framework -type d -maxdepth 4 | head -1)"
if [[ -z "$FRAMEWORK" ]]; then
  echo 'Download and extract pinned VLCKit 3.7.3 first (see README).'; exit 1
fi
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$APP/Contents/Frameworks"
/usr/bin/ditto --noextattr --norsrc "$FRAMEWORK" "$APP/Contents/Frameworks/VLCKit.framework"
swiftc -swift-version 5 -O -target arm64-apple-macosx14.0 -F "$(dirname "$FRAMEWORK")" -framework VLCKit -Xlinker -rpath -Xlinker @executable_path/../Frameworks Sources/*.swift -o "$APP/Contents/MacOS/NokiIPTV"
cp Assets/AppIcon.icns Assets/Brand.png Assets/favicon.ico Assets/favicon.png "$APP/Contents/Resources/"
cp "Vendor/VLCKit - binary package/COPYING.txt" "$APP/Contents/Resources/VLCKit-UPSTREAM-NOTICES.txt"
cp Vendor/VLCKit-LICENSE.txt "$APP/Contents/Resources/"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.noki.iptv</string>
<key>CFBundleName</key><string>Noki’s IPTV Player for MacOS</string>
<key>CFBundleDisplayName</key><string>Noki’s IPTV Player for MacOS</string>
<key>CFBundleExecutable</key><string>NokiIPTV</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.1.0</string>
<key>CFBundleVersion</key><string>2</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSAppTransportSecurity</key><dict><key>NSAllowsArbitraryLoads</key><true/></dict>
</dict></plist>
PLIST
# Remove resource forks from build output before local signing.
find "$APP" -name '._*' -type f -delete
xattr -cr "$APP"
find "$APP/Contents/Frameworks" -type f \( -name '*.dylib' -o -name 'VLCKit' \) -print0 | while IFS= read -r -d '' file; do codesign --force --sign - "$file" 2>/dev/null; done
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
/usr/bin/ditto --noextattr --norsrc "$APP" "dist/$NAME.app"
echo "Built $APP"

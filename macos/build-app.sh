#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  printf '%s\n' 'Native app compilation requires macOS with Xcode.' >&2
  exit 1
fi
node tools/build.cjs
node macos/build-extension.cjs
node --test
root="$PWD"
dist="$root/package/macos-dist"
mkdir -p "$dist"
app="$dist/FlowBridge-Setup.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -O -sdk "$sdk" -target "$arch-apple-macosx13.0" \
    -framework AppKit -framework SafariServices macos/FlowBridge.swift -o "$dist/FlowBridge-$arch"
done
xcrun lipo -create "$dist/FlowBridge-arm64" "$dist/FlowBridge-x86_64" -output "$app/Contents/MacOS/FlowBridge"
cp -R package/macos/chromium package/macos/safari package/macos/FlowBridge-0.1.1-signed.xpi "$app/Contents/Resources/"
cp assets/flowbridge-512.png "$app/Contents/Resources/"
python3 - "$app/Contents/Info.plist" <<'PY'
import plistlib,sys
with open(sys.argv[1], 'wb') as out:
    plistlib.dump(dict(CFBundleExecutable='FlowBridge', CFBundleIdentifier='org.flowbridge.macos.preview',
        CFBundleName='FlowBridge', CFBundlePackageType='APPL', CFBundleShortVersionString='0.2.0',
        CFBundleVersion='0.2.0-preview1', LSMinimumSystemVersion='13.0', NSHighResolutionCapable=True), out)
PY
codesign --force --sign - "$app"
codesign --verify --deep --strict "$app"
xcrun lipo -verify_arch arm64 x86_64 "$app/Contents/MacOS/FlowBridge"
ditto -c -k --sequesterRsrc --keepParent "$app" "$dist/FlowBridge-macOS-Preview.zip"
printf '%s\n' 'Built Universal 2 macOS preview. UI installation and live Flow require human testing.'

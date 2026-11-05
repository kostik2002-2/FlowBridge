#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD"
if [[ "$(uname -s)" != Darwin ]]; then exit 1; fi
packager=safari-web-extension-packager
if ! xcrun --find "$packager" >/dev/null 2>&1; then packager=safari-web-extension-converter; fi
project="$root/package/safari-xcode"
if [[ -e "$project" ]]; then printf '%s\n' 'Safari project exists; use a fresh build directory.' >&2; exit 1; fi
xcrun "$packager" "$root/package/macos/safari" --project-location "$project" \
  --app-name 'FlowBridge Safari Preview' --bundle-identifier org.flowbridge.safari.preview \
  --swift --macos-only --copy-resources --no-open --no-prompt
project_file="$(python3 - "$project" <<'PY'
import pathlib,sys
projects=list(pathlib.Path(sys.argv[1]).rglob('*.xcodeproj'))
assert len(projects)==1, projects
print(projects[0])
PY
)"
xcodebuild -list -json -project "$project_file" > "$project/schemes.json"
scheme="$(python3 - "$project/schemes.json" <<'PY'
import json,sys
schemes=json.load(open(sys.argv[1]))['project']['schemes']
choices=[s for s in schemes if 'Extension' not in s and ('macOS' in s or s=='FlowBridge Safari Preview')]
assert len(choices)==1, schemes
print(choices[0])
PY
)"
xcodebuild -project "$project_file" -scheme "$scheme" -configuration Release \
  -derivedDataPath "$root/package/safari-derived" CODE_SIGNING_ALLOWED=NO \
  'ARCHS=arm64 x86_64' ONLY_ACTIVE_ARCH=NO build
container="$root/package/safari-derived/Build/Products/Release/FlowBridge Safari Preview.app"
test -d "$container"
ditto -c -k --sequesterRsrc --keepParent "$container" "$root/package/macos-dist/FlowBridge-Safari-Unsigned-Preview.zip"
printf '%s\n' 'Safari container compiled unsigned. Safari must allow unsigned extensions to load it.'

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
# The converter can derive a different app ID from the display name. Normalize the
# two target IDs before building so the extension is prefixed by its containing app.
python3 - "$project_file/project.pbxproj" <<'PY'
import json,subprocess,plistlib,sys
file=sys.argv[1]
data=json.loads(subprocess.check_output(['plutil','-convert','json','-o','-',file]))
objects=data['objects']
for target in objects.values():
    if target.get('isa')!='PBXNativeTarget': continue
    kind=target.get('productType','')
    if kind=='com.apple.product-type.application': identifier='org.flowbridge.safari.preview'
    elif kind=='com.apple.product-type.app-extension': identifier='org.flowbridge.safari.preview.Extension'
    else: continue
    configs=objects[target['buildConfigurationList']]['buildConfigurations']
    for config in configs:
        objects[config].setdefault('buildSettings',{})['PRODUCT_BUNDLE_IDENTIFIER']=identifier
with open(file,'wb') as out: plistlib.dump(data,out,sort_keys=False)
PY
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
  'ARCHS=arm64 x86_64' ONLY_ACTIVE_ARCH=NO MACOSX_DEPLOYMENT_TARGET=13.0 build
container="$root/package/safari-derived/Build/Products/Release/FlowBridge Safari Preview.app"
test -d "$container"
python3 - "$container" <<'PY'
import plistlib,pathlib,sys
app=pathlib.Path(sys.argv[1])
info=plistlib.load(open(app/'Contents/Info.plist','rb'))
extensions=list((app/'Contents/PlugIns').glob('*.appex'))
assert len(extensions)==1, extensions
extension=plistlib.load(open(extensions[0]/'Contents/Info.plist','rb'))
assert extension['CFBundleIdentifier'].startswith(info['CFBundleIdentifier']+'.')
print('Safari bundle identifiers verified:',info['CFBundleIdentifier'],extension['CFBundleIdentifier'])
PY
# Ad-hoc signing permits local development execution; it is not an Apple Developer
# signature and Safari still needs its explicit Allow Unsigned Extensions setting.
for extension in "$container/Contents/PlugIns/"*.appex; do codesign --force --sign - "$extension"; done
codesign --force --sign - "$container"
codesign --verify --deep --strict "$container"
installer="$root/package/macos-dist/FlowBridge-Setup.app"
cp -R "$container" "$installer/Contents/Resources/"
codesign --force --sign - "$installer"
codesign --verify --deep --strict "$installer"
ditto -c -k --sequesterRsrc --keepParent "$installer" "$root/package/macos-dist/FlowBridge-macOS-Preview.zip"
ditto -c -k --sequesterRsrc --keepParent "$container" "$root/package/macos-dist/FlowBridge-Safari-Unsigned-Preview.zip"
printf '%s\n' 'Safari container compiled unsigned. Safari must allow unsigned extensions to load it.'

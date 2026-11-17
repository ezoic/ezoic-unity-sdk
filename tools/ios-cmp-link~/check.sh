#!/usr/bin/env bash
# Builds a stand-in for Unity's UnityFramework target (project.yml) twice and checks the
# "Include Ezoic CMP" Xcode edit end to end, without a Unity Editor:
#   1. as EDM's iOS Resolver leaves it (product EzoicAdsSDK), with the setting on (a no-op):
#      the built-in CMP's provider class must be linked;
#   2. after the setting-off edit (product EzoicAdsSDKWithoutCMP), applied twice to prove it
#      is idempotent: no CMP class or EzoicCmpSDKBinary symbol may be linked, and the
#      com.ezoic.ads bridge must still compile and link.
# Needs Xcode, XcodeGen and the .NET SDK. Usage: tools/ios-cmp-link~/check.sh [work dir]
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
work="${1:-$here/.work}"
dotnet="${DOTNET:-dotnet}"

version="$(python3 -c "import sys, xml.etree.ElementTree as ET; print(ET.parse(sys.argv[1]).find('.//remoteSwiftPackage').get('version'))" "$repo/Editor/EzoicDependencies.xml")"
echo "ezoic-swift-sdk-dist $version"

rm -rf "$work"
mkdir -p "$work/Bridge"
cp "$repo"/Runtime/Plugins/iOS/*.swift "$work/Bridge/"
sed "s/EZOIC_DIST_VERSION/$version/" "$here/project.yml" > "$work/project.yml"
(cd "$work" && xcodegen generate --quiet)
pbxproj="$work/Unity-iPhone.xcodeproj/project.pbxproj"
binary="$work/DerivedData/Build/Products/Debug-iphonesimulator/UnityFramework.framework/UnityFramework"

transform() {
  "$dotnet" run --project "$repo/tools/cmp-transform~" -- pbxproj "$pbxproj" "$1"
}

build() {
  xcodebuild build \
    -project "$work/Unity-iPhone.xcodeproj" \
    -scheme UnityFramework \
    -destination 'generic/platform=iOS Simulator' \
    -derivedDataPath "$work/DerivedData" \
    -skipPackagePluginValidation \
    CODE_SIGNING_ALLOWED=NO \
    COMPILER_INDEX_STORE_ENABLE=NO \
    -quiet
}

symbols() {
  test -f "$binary" || { echo "::error::no $binary" >&2; exit 1; }
  nm "$binary" > "$work/symbols.txt"
}

provider_classes() {
  grep -c 'OBJC_CLASS_\$_EZBuiltInCmpProvider' "$work/symbols.txt" || true
}

cmp_symbols() {
  grep -c 'EzoicCmpSDKBinary' "$work/symbols.txt" || true
}

echo "::group::Include Ezoic CMP on (EzoicAdsSDK)"
out="$(transform include)"; echo "$out"
grep -q 'switched=0 alreadyWanted=1 ' <<<"$out" || { echo "::error::the setting-on edit changed the project"; exit 1; }
build
symbols
with_cmp="$(shasum "$binary")"
echo "EZBuiltInCmpProvider classes: $(provider_classes); EzoicCmpSDKBinary symbols: $(cmp_symbols)"
if [ "$(provider_classes)" -eq 0 ]; then
  echo "::error::the default build does not link the built-in CMP (no EZBuiltInCmpProvider class)"
  exit 1
fi
echo "::endgroup::"

echo "::group::Include Ezoic CMP off (EzoicAdsSDKWithoutCMP)"
out="$(transform exclude)"; echo "$out"
grep -q 'switched=1 alreadyWanted=0 ' <<<"$out" || { echo "::error::the setting-off edit did not switch the product"; exit 1; }
out="$(transform exclude)"; echo "$out"
grep -q 'switched=0 alreadyWanted=1 ' <<<"$out" || { echo "::error::the setting-off edit is not idempotent"; exit 1; }
grep -q 'productName = EzoicAdsSDKWithoutCMP;' "$pbxproj"
build
symbols
if [ "$(shasum "$binary")" = "$with_cmp" ]; then
  echo "::error::UnityFramework was not relinked after the product switch"
  exit 1
fi
echo "EZBuiltInCmpProvider classes: $(provider_classes); EzoicCmpSDKBinary symbols: $(cmp_symbols)"
if [ "$(provider_classes)" -ne 0 ] || [ "$(cmp_symbols)" -ne 0 ]; then
  echo "::error::the build without the CMP still links CMP code"
  exit 1
fi
if ! grep -q '_ezoic_unity_is_built_in_cmp_included' "$work/symbols.txt"; then
  echo "::error::the com.ezoic.ads bridge is missing from the build without the CMP"
  exit 1
fi
echo "::endgroup::"
echo "CMP LINK CHECK PASS: EzoicAdsSDK links the CMP; EzoicAdsSDKWithoutCMP links none of it."

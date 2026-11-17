#!/usr/bin/env bash
# Checks the "Include Ezoic CMP" Gradle edit end to end, without a Unity Editor: resolves the
# runtime classpath of a stand-in for Unity's generated Gradle project (project/) as is, then
# after the setting-off edit (tools/cmp-transform~) of unityLibrary/build.gradle and
# launcher/build.gradle, applied twice to prove it is idempotent.
#   on:  ezoic-ads-sdk, ezoic-consent-core and ezoic-cmp-sdk resolve;
#   off: ezoic-ads-sdk and ezoic-consent-core resolve, ezoic-cmp-sdk doesn't.
# Needs Gradle and the .NET SDK. Usage: tools/android-cmp-exclude~/check.sh [work dir]
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
work="${1:-$here/.work}"
dotnet="${DOTNET:-dotnet}"
gradle="${GRADLE:-gradle}"

version="$(python3 -c "import sys, xml.etree.ElementTree as ET; print(ET.parse(sys.argv[1]).find('.//androidPackage').get('spec').split(':')[2])" "$repo/Editor/EzoicDependencies.xml")"
echo "com.ezoic.sdk:ezoic-ads-sdk:$version"

rm -rf "$work"
cp -R "$here/project" "$work"
sed -i.bak "s/EZOIC_ANDROID_VERSION/$version/" "$work/unityLibrary/build.gradle"
rm "$work/unityLibrary/build.gradle.bak"

transform() {
  for module in unityLibrary launcher; do
    "$dotnet" run --project "$repo/tools/cmp-transform~" -- gradle "$work/$module/build.gradle" "$1"
  done
}

classpath() {
  "$gradle" --quiet --project-dir "$work" ":$1:dependencies" --configuration runtimeClasspath
}

expect() { # expect <module> <present|absent> <artifact>
  if classpath "$1" | grep -q "com.ezoic.sdk:$3:"; then found=present; else found=absent; fi
  echo "  :$1 runtimeClasspath: com.ezoic.sdk:$3 $found"
  if [ "$found" != "$2" ]; then
    echo "::error:::$1 runtimeClasspath: com.ezoic.sdk:$3 is $found, expected $2"
    exit 1
  fi
}

echo "::group::Include Ezoic CMP on"
transform include | tee "$work/on.log"
if grep -q 'changed=True' "$work/on.log"; then echo "::error::the setting-on edit changed a build file"; exit 1; fi
for module in unityLibrary launcher; do
  expect "$module" present ezoic-ads-sdk
  expect "$module" present ezoic-consent-core
  expect "$module" present ezoic-cmp-sdk
done
echo "::endgroup::"

echo "::group::Include Ezoic CMP off"
transform exclude | tee "$work/off.log"
transform exclude | tee "$work/off-again.log"
if [ "$(grep -c 'changed=True excluded=True' "$work/off.log")" -ne 2 ]; then echo "::error::the setting-off edit did not add the exclude to both modules"; exit 1; fi
if grep -q 'changed=True' "$work/off-again.log"; then echo "::error::the setting-off edit is not idempotent"; exit 1; fi
cat "$work/launcher/build.gradle"
for module in unityLibrary launcher; do
  expect "$module" present ezoic-ads-sdk
  expect "$module" present ezoic-consent-core
  expect "$module" absent ezoic-cmp-sdk
done
echo "::endgroup::"
echo "GRADLE CMP EXCLUDE CHECK PASS: ezoic-cmp-sdk resolves by default and not with Include Ezoic CMP off."

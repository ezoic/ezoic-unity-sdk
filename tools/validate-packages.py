#!/usr/bin/env python3
"""Validate the repository's two UPM packages and keep their pins in agreement.

  * com.ezoic.ads      at the repository root
  * com.ezoic.cmp-sdk  in com.ezoic.cmp-sdk~/ (installed with ?path=/com.ezoic.cmp-sdk~)

Checks:
  * package.json and the asmdef of each package parse; the names are right and both packages
    carry the same version (they are released from one vX.Y.Z tag).
  * Each EDM manifest pins its own Maven artifact (ezoic-ads-sdk / ezoic-cmp-sdk) at the same
    native version, and declares exactly one remoteSwiftPackage: the ezoic-swift-sdk-dist URL,
    upToNextMajor, at that native version, with its product (EzoicAdsSDK / EzoicCmpSDK).
    EDM merges the two entries for one URL into a single package reference.
  * No iosPod: from 1.22.0 there is no CocoaPod to fall back to.
  * tools/swift-typecheck/Package.swift pins the same URL and version.
  * The CMP README's install URL names this version.
  * Every tracked asset Unity imports from either package has a .meta file. Packages
    installed from git are immutable, and Unity ignores an asset without one.

Exit code 0 with a summary when everything agrees; 1 with every problem otherwise.
Standard library only.
"""

import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

DIST_URL = "https://github.com/ezoic/ezoic-swift-sdk-dist.git"
CMP_DIR = "com.ezoic.cmp-sdk~"

PACKAGES = [
    # (root, package name, asmdef, EDM manifest, Maven artifact, Swift products)
    (".", "com.ezoic.ads", "Runtime/Ezoic.Ads.asmdef", "Editor/EzoicDependencies.xml",
     "ezoic-ads-sdk", [("EzoicAdsSDK", "EzoicAdsSDK")]),
    (CMP_DIR, "com.ezoic.cmp-sdk", "Runtime/Ezoic.Cmp.asmdef", "Editor/EzoicCmpDependencies.xml",
     "ezoic-cmp-sdk", [("EzoicCmpSDK", None)]),
]


def unity_imported(path):
    """Whether Unity imports path: no hidden part, no part ending in "~", not a .meta."""
    parts = path.split("/")
    if any(part.startswith(".") or part.endswith("~") for part in parts):
        return False
    return not path.endswith(".meta")


def missing_metas(package_root, tracked):
    """Tracked paths under package_root (files and their folders) that lack a .meta."""
    prefix = "" if package_root == "." else package_root.rstrip("/") + "/"
    assets = set()
    for path in tracked:
        if not path.startswith(prefix):
            continue
        relative = path[len(prefix):]
        if not unity_imported(relative):
            continue
        parts = relative.split("/")
        for i in range(1, len(parts) + 1):
            assets.add(prefix + "/".join(parts[:i]))
    return sorted(a for a in assets if a + ".meta" not in tracked)


def main():
    repo_root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    os.chdir(repo_root)
    # Tracked and not-ignored new files: what a release tag would ship.
    listing = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        stdout=subprocess.PIPE, check=True, universal_newlines=True).stdout
    tracked = set(line for line in listing.splitlines() if os.path.exists(line))
    problems = []
    versions = {}
    native_versions = {}

    for root, name, asmdef, manifest, artifact, products in PACKAGES:
        with open(os.path.join(root, "package.json"), encoding="utf-8") as f:
            package = json.load(f)
        if package.get("name") != name:
            problems.append("%s/package.json: name is %r, expected %r" % (root, package.get("name"), name))
        versions[name] = package.get("version")

        with open(os.path.join(root, asmdef), encoding="utf-8") as f:
            json.load(f)

        deps = ET.parse(os.path.join(root, manifest)).getroot()
        label = os.path.normpath(os.path.join(root, manifest))

        specs = [p.get("spec") for p in deps.iter("androidPackage")]
        if len(specs) != 1 or not re.fullmatch(r"com\.ezoic\.sdk:%s:[0-9][^:]*" % re.escape(artifact), specs[0] or ""):
            problems.append("%s: expected one androidPackage com.ezoic.sdk:%s:<version>, found %r" % (label, artifact, specs))
        else:
            native_versions[label] = specs[0].rsplit(":", 1)[1]

        spm = list(deps.iter("remoteSwiftPackage"))
        if len(spm) != 1:
            problems.append("%s: expected one remoteSwiftPackage, found %d" % (label, len(spm)))
        else:
            spm = spm[0]
            if spm.get("url") != DIST_URL:
                problems.append("%s: remoteSwiftPackage url is %r" % (label, spm.get("url")))
            if spm.get("upToNextMajor") != "true":
                problems.append("%s: remoteSwiftPackage must set upToNextMajor=\"true\"" % label)
            found = [(p.get("name"), p.get("replacesPod")) for p in spm.findall("swiftPackage")]
            if found != products:
                problems.append("%s: swiftPackage products are %r, expected %r" % (label, found, products))
            native_versions[label + " (Swift package)"] = spm.get("version")

        if list(deps.iter("iosPod")):
            problems.append("%s: iosPod is not supported from 1.22.0 (CocoaPods trunk is frozen)" % label)

        for entry in missing_metas(root, tracked):
            problems.append("%s: no .meta file (Unity ignores it in an installed package)" % entry)

    if len(set(versions.values())) != 1:
        problems.append("package versions differ: %r" % versions)
    if len(set(native_versions.values())) != 1:
        problems.append("native versions differ: %r" % native_versions)

    native = next(iter(native_versions.values()), None)
    with open("tools/swift-typecheck/Package.swift", encoding="utf-8") as f:
        manifest_text = f.read()
    m = re.search(r'url: "([^"]+)",\s*\.upToNextMajor\(from: "([^"]+)"\)', manifest_text)
    if not m:
        problems.append("tools/swift-typecheck/Package.swift must use .upToNextMajor(from:)")
    elif m.groups() != (DIST_URL, native):
        problems.append("tools/swift-typecheck/Package.swift pins %r, the EDM manifests pin %r" % (m.groups(), (DIST_URL, native)))

    version = versions.get("com.ezoic.cmp-sdk")
    install_url = "https://github.com/ezoic/ezoic-unity-sdk.git?path=/%s#v%s" % (CMP_DIR, version)
    for readme in (os.path.join(CMP_DIR, "README.md"), "README.md"):
        with open(readme, encoding="utf-8") as f:
            if install_url not in f.read():
                problems.append("%s: does not give the install URL %s" % (readme, install_url))

    if problems:
        print("PACKAGE VALIDATION FAILED: %d issue(s):" % len(problems))
        for p in problems:
            print("  - %s" % p)
        return 1

    print("PACKAGE VALIDATION PASS: com.ezoic.ads and com.ezoic.cmp-sdk %s, native %s, "
          "Swift package %s up to next major." % (version, native, DIST_URL))
    return 0


if __name__ == "__main__":
    sys.exit(main())

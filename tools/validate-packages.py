#!/usr/bin/env python3
"""Validate the repository's two UPM packages and keep their pins in agreement.

  * com.ezoic.ads      at the repository root
  * com.ezoic.cmp-sdk  in com.ezoic.cmp-sdk~/, published on its own by the release workflow as
                       the `git subtree split` tag cmp-sdk/vX.Y.Z (and branch upm/cmp-sdk)

Checks:
  * package.json and the asmdef of each package parse; the names are right and both packages
    carry the same version (they are released from one vX.Y.Z tag).
  * Each EDM manifest pins its own Maven artifact (ezoic-ads-sdk / ezoic-cmp-sdk) at the same
    native version, and declares exactly one remoteSwiftPackage: the ezoic-swift-sdk-dist URL,
    upToNextMajor, at that native version, with its product (EzoicAdsSDK / EzoicCmpSDK).
    EDM merges the two entries for one URL into a single package reference.
  * No iosPod: from 1.22.0 there is no CocoaPod to fall back to.
  * tools/swift-typecheck/Package.swift pins the same URL and version.
  * Both READMEs give the CMP install URL ...ezoic-unity-sdk.git#cmp-sdk/v<version>.
  * Every tracked asset Unity imports from either package has a .meta file. Packages
    installed from git are immutable, and Unity ignores an asset without one.

Options:
  --standalone-cmp DIR  validate DIR, a checkout of the split tree, as a package on its own:
                        package.json at the root, the CMP checks above, a .meta per asset.
  --expect-version V    also require the package version(s) to be V (the release tag minus v).

Exit code 0 with a summary when everything agrees; 1 with every problem otherwise.
Standard library only.
"""

import argparse
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

DIST_URL = "https://github.com/ezoic/ezoic-swift-sdk-dist.git"
CMP_DIR = "com.ezoic.cmp-sdk~"
# The release workflow splits CMP_DIR into this tag prefix (and the branch upm/cmp-sdk).
CMP_TAG_PREFIX = "cmp-sdk/v"

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


def listed_files(root):
    """Files git would ship from root (tracked plus untracked, not ignored), relative to root."""
    listing = subprocess.run(
        ["git", "-C", root, "ls-files", "--cached", "--others", "--exclude-standard"],
        stdout=subprocess.PIPE, check=True, universal_newlines=True).stdout
    return set(line for line in listing.splitlines() if os.path.exists(os.path.join(root, line)))


def check_package(root, spec, tracked, problems):
    """Checks one package rooted at root. tracked holds repo-relative paths ("<root>/..." unless
    root is "."). Returns (package version, {manifest label: native version})."""
    _, name, asmdef, manifest, artifact, products = spec
    native_versions = {}
    with open(os.path.join(root, "package.json"), encoding="utf-8") as f:
        package = json.load(f)
    if package.get("name") != name:
        problems.append("%s/package.json: name is %r, expected %r" % (root, package.get("name"), name))

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
    return package.get("version"), native_versions


def cmp_install_url(version):
    """The documented install URL: the split tag the release workflow pushes."""
    return "https://github.com/ezoic/ezoic-unity-sdk.git#%s%s" % (CMP_TAG_PREFIX, version)


def report(problems, summary):
    if problems:
        print("PACKAGE VALIDATION FAILED: %d issue(s):" % len(problems))
        for p in problems:
            print("  - %s" % p)
        return 1
    print("PACKAGE VALIDATION PASS: " + summary)
    return 0


def validate_standalone_cmp(tree, expect_version):
    """Checks a com.ezoic.cmp-sdk tree on its own: the `git subtree split` the release pushes."""
    problems = []
    tracked = set(os.path.join(tree, path) for path in listed_files(tree))
    if not os.path.isfile(os.path.join(tree, "package.json")):
        return report(["%s: no package.json at the root" % tree], "")
    version, native_versions = check_package(tree, PACKAGES[1], tracked, problems)
    if len(set(native_versions.values())) != 1:
        problems.append("native versions differ: %r" % native_versions)
    if expect_version and version != expect_version:
        problems.append("package version is %r, the release tag says %r" % (version, expect_version))
    with open(os.path.join(tree, "README.md"), encoding="utf-8") as f:
        if cmp_install_url(version) not in f.read():
            problems.append("README.md: does not give the install URL %s" % cmp_install_url(version))
    return report(problems, "standalone com.ezoic.cmp-sdk %s at %s (%d files)." % (version, tree, len(tracked)))


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--standalone-cmp", metavar="DIR",
                        help="validate DIR as a standalone com.ezoic.cmp-sdk tree (the release split)")
    parser.add_argument("--expect-version", metavar="X.Y.Z",
                        help="also require this package version (the release tag without its v)")
    args = parser.parse_args(argv)

    if args.standalone_cmp:
        return validate_standalone_cmp(os.path.abspath(args.standalone_cmp), args.expect_version)

    repo_root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    os.chdir(repo_root)
    tracked = listed_files(".")
    problems = []
    versions = {}
    native_versions = {}

    for spec in PACKAGES:
        root = spec[0]
        version, natives = check_package(root, spec, tracked, problems)
        versions[spec[1]] = version
        native_versions.update(natives)

    if len(set(versions.values())) != 1:
        problems.append("package versions differ: %r" % versions)
    if args.expect_version and set(versions.values()) != {args.expect_version}:
        problems.append("package versions %r, the release tag says %r" % (versions, args.expect_version))
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
    for readme in (os.path.join(CMP_DIR, "README.md"), "README.md"):
        with open(readme, encoding="utf-8") as f:
            if cmp_install_url(version) not in f.read():
                problems.append("%s: does not give the install URL %s" % (readme, cmp_install_url(version)))

    return report(problems, "com.ezoic.ads and com.ezoic.cmp-sdk %s, native %s, "
                  "Swift package %s up to next major." % (version, native, DIST_URL))


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

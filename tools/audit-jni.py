#!/usr/bin/env python3
"""Audit the JNI names the Unity Android bridges use against the published Ezoic AARs.

The C# bridges never hard-code a Java class or member name inline; every name lives as a
``const string`` in one names file per package:

  * ``Runtime/Android/JniNames.cs``                            (com.ezoic.ads)
  * ``com.ezoic.cmp-sdk~/Runtime/Android/CmpJniNames.cs``      (com.ezoic.cmp-sdk)

This script parses both and, for every Ezoic SDK class they declare, uses ``javap`` against the
extracted AAR classes to confirm the class exists and that every member name the bridge relies
on is present. From native 1.22.0 the ``com.ezoic.ads.sdk.privacy.cmp`` classes live in the
``ezoic-cmp-sdk`` AAR, so the classpath must hold both AARs' classes. If a bridge and the
shipped SDK ever drift, this fails loudly.

Structure of JniNames.cs (the mapping is encoded by the file itself, no cleverness needed):

    internal static class JniNames        # container, skipped (it nests other classes)
    {
        internal static class EzoicAds    # a leaf group == one Java class
        {
            internal const string Class = "com.ezoic.ads.sdk.core.EzoicAds";  # the FQCN
            internal const string getInstance = "getInstance";                # a member name
            ...
        }
        internal static class Framework   # no `Class` const -> Unity/Android names, not audited
        {
            internal const string UnityPlayer = "com.unity3d.player.UnityPlayer";
            ...
        }
    }

Audit rule (matches the doc comment in JniNames.cs):
  * A leaf group is audited iff it has a `Class` const whose value starts with "com.ezoic".
  * The `Class` value is the fully-qualified class name passed to javap.
  * Every other `const string` value in that group is a member name that must appear in the
    javap dump of that class.

Environment:
  AAR_CLASSES  (required)  directory with the extracted classes of both AARs (the classpath
                           root), or several such directories joined with os.pathsep.
  JAVAP        (optional)  path to the javap binary; defaults to "javap" on PATH.
  JNI_NAMES    (optional)  names files to audit (repo-relative, joined with os.pathsep);
                           defaults to both. CI also audits the CMP file alone against the
                           ezoic-cmp-sdk AAR alone, proving com.ezoic.cmp-sdk needs no ads classes.

Exit code 0 and a PASS summary when every class and member is found; exit code 1 with a clear
list of misses otherwise. Standard library only.
"""

import os
import re
import subprocess
import sys

# Only classes whose FQCN starts with this prefix are audited against the Ezoic AAR. Platform
# names (Unity player, android.*) are guaranteed by the player/OS, not the Ezoic SDK.
EZOIC_PREFIX = "com.ezoic"

CLASS_CONST_NAME = "Class"

# Matches: internal static class <Name> {   (captures the name and the opening brace position).
CLASS_DECL_RE = re.compile(r"internal\s+static\s+class\s+(\w+)\s*\{")

# Matches: [internal] const string <Name> = "<Value>";  (const modifiers may vary in order).
CONST_STRING_RE = re.compile(r'const\s+string\s+(\w+)\s*=\s*"([^"]*)"\s*;')


def find_class_blocks(text):
    """Yield (name, body) for every `internal static class` block, brace-matched.

    Returns both container classes (whose body still contains nested class declarations) and
    leaf classes. Callers filter to leaves.
    """
    blocks = []
    for m in CLASS_DECL_RE.finditer(text):
        name = m.group(1)
        brace_start = m.end() - 1  # index of the '{'
        depth = 0
        i = brace_start
        while i < len(text):
            ch = text[i]
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        if depth != 0:
            raise ValueError("unbalanced braces while parsing class '%s'" % name)
        body = text[brace_start + 1:i]
        blocks.append((name, body))
    return blocks


def parse_audited_classes(source):
    """Parse JniNames.cs text into a list of (fqcn, [member_name, ...]) for audited classes."""
    audited = []
    for name, body in find_class_blocks(source):
        # Skip container classes (they nest further class declarations, e.g. JniNames itself).
        if CLASS_DECL_RE.search(body):
            continue

        consts = CONST_STRING_RE.findall(body)  # list of (const_name, value)
        if not consts:
            continue

        by_const_name = {cn: val for cn, val in consts}
        fqcn = by_const_name.get(CLASS_CONST_NAME)
        if fqcn is None:
            # No `Class` const -> platform group (Framework); intentionally not audited.
            continue
        if not fqcn.startswith(EZOIC_PREFIX):
            continue

        members = [val for cn, val in consts if cn != CLASS_CONST_NAME]
        audited.append((fqcn, members))
    return audited


def run_javap(javap, aar_classes, fqcn):
    """Return (ok, output) from `javap -classpath <aar_classes> <fqcn>`.

    ok is False when javap fails to load the class (missing class, bad classpath, etc.).
    """
    try:
        proc = subprocess.run(
            [javap, "-classpath", aar_classes, fqcn],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            universal_newlines=True,
        )
    except FileNotFoundError:
        sys.stderr.write(
            "ERROR: javap binary not found: %r. Set JAVAP to a valid javap path.\n" % javap
        )
        sys.exit(1)

    output = proc.stdout or ""
    # javap exits non-zero and/or omits the FQCN when the class cannot be loaded.
    ok = proc.returncode == 0 and fqcn in output
    return ok, output


def member_present(member, javap_output):
    """True if `member` appears as a whole identifier token in the javap dump."""
    return re.search(r"\b" + re.escape(member) + r"\b", javap_output) is not None


# One JNI names file per package, relative to the repository root.
JNI_NAMES_FILES = [
    os.path.join("Runtime", "Android", "JniNames.cs"),
    os.path.join("com.ezoic.cmp-sdk~", "Runtime", "Android", "CmpJniNames.cs"),
]


def main():
    aar_classes = os.environ.get("AAR_CLASSES")
    if not aar_classes:
        sys.stderr.write("ERROR: AAR_CLASSES is not set (path to extracted AAR classes).\n")
        return 1
    for entry in aar_classes.split(os.pathsep):
        if not os.path.isdir(entry):
            sys.stderr.write("ERROR: AAR_CLASSES entry is not a directory: %s\n" % entry)
            return 1

    javap = os.environ.get("JAVAP", "javap")

    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.normpath(os.path.join(script_dir, ".."))

    # (fqcn, members) pairs; a class named in both files is audited once per file, which is
    # cheap and keeps each miss attributable to its package.
    audited = []
    selected = os.environ.get("JNI_NAMES")
    names_files = selected.split(os.pathsep) if selected else JNI_NAMES_FILES
    for relative in names_files:
        path = os.path.join(repo_root, relative)
        if not os.path.isfile(path):
            sys.stderr.write("ERROR: cannot find %s\n" % path)
            return 1
        with open(path, "r", encoding="utf-8") as f:
            parsed = parse_audited_classes(f.read())
        if not parsed:
            sys.stderr.write("ERROR: no audited (com.ezoic) classes parsed from %s.\n" % relative)
            return 1
        print("Auditing JNI names from %s" % relative)
        audited.extend(parsed)

    print("  javap:       %s" % javap)
    print("  AAR classes: %s" % aar_classes)
    print("")

    misses = []          # human-readable miss descriptions
    class_count = 0
    member_count = 0

    for fqcn, members in sorted(set((fqcn, tuple(members)) for fqcn, members in audited)):
        class_count += 1
        ok, output = run_javap(javap, aar_classes, fqcn)
        if not ok:
            misses.append("class NOT FOUND: %s" % fqcn)
            print("  MISS  %s (class not found by javap)" % fqcn)
            continue

        missing_members = [m for m in members if not member_present(m, output)]
        member_count += len(members)
        if missing_members:
            for m in missing_members:
                misses.append("%s: missing member '%s'" % (fqcn, m))
            print(
                "  MISS  %s (%d/%d members; missing: %s)"
                % (fqcn, len(members) - len(missing_members), len(members), ", ".join(missing_members))
            )
        else:
            print("  OK    %s (%d members)" % (fqcn, len(members)))

    print("")
    if misses:
        print("JNI AUDIT FAILED: %d issue(s):" % len(misses))
        for miss in misses:
            print("  - %s" % miss)
        return 1

    print(
        "JNI AUDIT PASS: %d classes, %d member names verified against the AARs."
        % (class_count, member_count)
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

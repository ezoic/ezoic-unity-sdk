#!/usr/bin/env python3
"""Audit the iOS C-ABI surface: Swift @_cdecl exports vs C# [DllImport] externs.

The Unity iOS bridge is two halves of one C ABI that must stay in lockstep:

  * Swift side  -- Runtime/Plugins/iOS/*.swift declares the native entry points with
                   ``@_cdecl("ezoic_unity_...")`` above a ``func`` whose parameter list is
                   the ABI signature.
  * C# side     -- Runtime/iOS/*.cs declares the managed callers with
                   ``[DllImport("__Internal", EntryPoint = "ezoic_unity_...")]`` above an
                   ``extern`` method whose parameter list is the ABI signature.

If either side adds, removes, renames, or changes the arity of an entry point without the
other following, the player links against a symbol that does not exist (crash) or calls it
with the wrong stack shape (memory corruption). This script fails loudly on any such drift.

Audit rules:
  * The set of Swift @_cdecl names MUST equal the set of C# __Internal entry-point names
    (an entry point name is EntryPoint="..." when present, otherwise the extern method name).
  * For every shared name, the Swift func parameter count MUST equal the C# extern parameter
    count. (Type marshaling is not checked here -- the Swift type-check CI job covers the
    Swift half, and IL2CPP the C# half; this guards names and arity, the two things no
    compiler on either side can catch alone.)

The parser is resilient to multi-line declarations: it works on whole-file text and
brace/paren-matches, counting only top-level commas (ignoring nested (), <>, and []).

Exit code 0 and a PASS summary when both sides agree; exit code 1 with a precise diff
otherwise. Standard library only.
"""

import os
import re
import sys

# Swift: @_cdecl("name")
CDECL_RE = re.compile(r'@_cdecl\(\s*"([^"]+)"\s*\)')
# Swift: the func whose params form the ABI signature (name irrelevant, first '(' matters).
SWIFT_FUNC_RE = re.compile(r"\bfunc\s+\w+\s*\(")

# C#: [DllImport("__Internal" ... )]  -- capture the attribute argument tail up to ']'.
DLLIMPORT_RE = re.compile(r'\[DllImport\(\s*"__Internal"(?P<attrs>[^\]]*)\]')
# C#: EntryPoint = "name" inside the attribute tail (optional).
ENTRYPOINT_RE = re.compile(r'EntryPoint\s*=\s*"([^"]+)"')
# C#: the extern method decl following the attribute; capture the method name (token before '(').
EXTERN_RE = re.compile(r"\bextern\b[^;(]*?(\w+)\s*\(")


def strip_comments(text):
    """Remove // line comments and /* */ block comments, preserving string literals and newlines.

    Both Swift and C# use // and /* */ (and C# /// doc comments start with //). Doc comments
    routinely quote code fragments like [DllImport("__Internal")] or "UTF-8 (LPUTF8Str)"; without
    stripping them the regexes below would parse prose as declarations. String literals are kept
    verbatim so tokens like "__Internal" survive; newlines are preserved so line structure (and
    thus any error offsets) stay meaningful.
    """
    out = []
    i = 0
    n = len(text)
    in_line = in_block = in_str = False
    str_ch = ""
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if in_line:
            if c == "\n":
                in_line = False
                out.append(c)
            i += 1
        elif in_block:
            if c == "*" and nxt == "/":
                in_block = False
                i += 2
            else:
                if c == "\n":
                    out.append(c)
                i += 1
        elif in_str:
            out.append(c)
            if c == "\\" and nxt:
                out.append(nxt)
                i += 2
            else:
                if c == str_ch:
                    in_str = False
                i += 1
        elif c == '"' or c == "'":
            in_str = True
            str_ch = c
            out.append(c)
            i += 1
        elif c == "/" and nxt == "/":
            in_line = True
            i += 2
        elif c == "/" and nxt == "*":
            in_block = True
            i += 2
        else:
            out.append(c)
            i += 1
    return "".join(out)


def count_top_level_params(param_str):
    """Count parameters in a raw parameter-list string (between the outer parens).

    Splits on commas that sit at depth zero across (), <>, and [] nesting so that
    generic types (UnsafePointer<CChar>), attributes ([MarshalAs(...)]), and nested
    closure/tuple types do not inflate the count. ``->`` is skipped so a return arrow
    inside a closure type never unbalances the angle-bracket depth. Empty list => 0.
    """
    s = param_str.strip()
    if not s:
        return 0

    paren = angle = bracket = 0
    commas = 0
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c == "-" and i + 1 < n and s[i + 1] == ">":
            i += 2  # skip the '->' token; its '>' is not an angle close
            continue
        if c == "(":
            paren += 1
        elif c == ")":
            paren -= 1
        elif c == "<":
            angle += 1
        elif c == ">":
            angle -= 1
        elif c == "[":
            bracket += 1
        elif c == "]":
            bracket -= 1
        elif c == "," and paren == 0 and angle == 0 and bracket == 0:
            commas += 1
        i += 1
    return commas + 1


def _match_paren(text, open_index):
    """Return the index of the ')' matching the '(' at open_index."""
    depth = 0
    i = open_index
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError("unbalanced parentheses starting at index %d" % open_index)


def parse_swift_exports(text, source_name):
    """Return {export_name: param_count} for every @_cdecl in the Swift text."""
    text = strip_comments(text)
    exports = {}
    for m in CDECL_RE.finditer(text):
        name = m.group(1)
        fm = SWIFT_FUNC_RE.search(text, m.end())
        if not fm:
            raise ValueError(
                '%s: @_cdecl("%s") is not followed by a func declaration' % (source_name, name)
            )
        open_paren = fm.end() - 1
        close_paren = _match_paren(text, open_paren)
        count = count_top_level_params(text[open_paren + 1:close_paren])
        if name in exports:
            raise ValueError('%s: duplicate @_cdecl export "%s"' % (source_name, name))
        exports[name] = count
    return exports


def parse_cs_externs(text, source_name):
    """Return {entry_point_name: param_count} for every __Internal DllImport in the C# text."""
    text = strip_comments(text)
    externs = {}
    for m in DLLIMPORT_RE.finditer(text):
        ep = ENTRYPOINT_RE.search(m.group("attrs"))
        em = EXTERN_RE.search(text, m.end())
        if not em:
            raise ValueError(
                "%s: [DllImport(\"__Internal\")] is not followed by an extern declaration "
                "(near offset %d)" % (source_name, m.start())
            )
        method_name = em.group(1)
        name = ep.group(1) if ep else method_name  # EntryPoint wins; else the method name
        open_paren = em.end() - 1
        close_paren = _match_paren(text, open_paren)
        count = count_top_level_params(text[open_paren + 1:close_paren])
        if name in externs:
            raise ValueError('%s: duplicate __Internal entry point "%s"' % (source_name, name))
        externs[name] = count
    return externs


def collect(dir_path, suffix, parse_fn):
    """Parse every file ending in suffix under dir_path; merge into one {name: count} dict."""
    merged = {}
    if not os.path.isdir(dir_path):
        sys.stderr.write("ERROR: directory not found: %s\n" % dir_path)
        return None
    for entry in sorted(os.listdir(dir_path)):
        if not entry.endswith(suffix):
            continue
        path = os.path.join(dir_path, entry)
        with open(path, "r", encoding="utf-8") as f:
            parsed = parse_fn(f.read(), os.path.relpath(path))
        for name, count in parsed.items():
            if name in merged:
                sys.stderr.write('ERROR: "%s" declared in more than one file under %s\n'
                                 % (name, dir_path))
                return None
            merged[name] = count
    return merged


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.normpath(os.path.join(script_dir, ".."))
    swift_dir = os.path.join(repo_root, "Runtime", "Plugins", "iOS")
    cs_dir = os.path.join(repo_root, "Runtime", "iOS")

    print("Auditing iOS C-ABI surface")
    print("  Swift @_cdecl:   %s/*.swift" % os.path.relpath(swift_dir, repo_root))
    print("  C# [DllImport]:  %s/*.cs" % os.path.relpath(cs_dir, repo_root))
    print("")

    try:
        swift = collect(swift_dir, ".swift", parse_swift_exports)
        cs = collect(cs_dir, ".cs", parse_cs_externs)
    except ValueError as e:
        sys.stderr.write("ERROR: %s\n" % e)
        return 1

    if swift is None or cs is None:
        return 1

    if not swift:
        sys.stderr.write("ERROR: no @_cdecl exports found under %s\n" % swift_dir)
        return 1
    if not cs:
        sys.stderr.write("ERROR: no __Internal externs found under %s\n" % cs_dir)
        return 1

    swift_names = set(swift)
    cs_names = set(cs)

    only_swift = sorted(swift_names - cs_names)
    only_cs = sorted(cs_names - swift_names)
    shared = sorted(swift_names & cs_names)

    arity_mismatches = [
        (name, swift[name], cs[name]) for name in shared if swift[name] != cs[name]
    ]

    for name in shared:
        if swift[name] == cs[name]:
            print("  OK    %s (%d args)" % (name, swift[name]))

    problems = []
    if only_swift:
        for name in only_swift:
            print("  MISS  %s (Swift export has no C# DllImport)" % name)
            problems.append("%s: exported in Swift but no matching C# [DllImport]" % name)
    if only_cs:
        for name in only_cs:
            print("  MISS  %s (C# DllImport has no Swift export)" % name)
            problems.append("%s: imported in C# but no matching Swift @_cdecl" % name)
    for name, sc, cc in arity_mismatches:
        print("  MISS  %s (Swift %d args vs C# %d args)" % (name, sc, cc))
        problems.append("%s: parameter count differs (Swift %d, C# %d)" % (name, sc, cc))

    print("")
    if problems:
        print("EXTERN AUDIT FAILED: %d issue(s):" % len(problems))
        for p in problems:
            print("  - %s" % p)
        return 1

    print(
        "EXTERN AUDIT PASS: %d entry points match by name and arity across the C ABI."
        % len(shared)
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

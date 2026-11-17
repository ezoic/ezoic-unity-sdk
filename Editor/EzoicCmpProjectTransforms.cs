using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// The build-time edits behind the "Include Ezoic CMP" setting, as plain text transforms on the
// generated Gradle and Xcode project files. Nothing here references UnityEngine or UnityEditor,
// so the unit tests (tools/tests~) and the CI project checks (tools/cmp-transform~) run this file
// under plain .NET. EzoicCmpBuildProcessor.cs calls it from the Unity build callbacks.
namespace Ezoic.Ads.Editor
{
    /// <summary>
    /// Adds (or removes) the Gradle exclude that leaves <c>com.ezoic.sdk:ezoic-cmp-sdk</c> out of
    /// an Android build: <c>configurations.all { exclude group: 'com.ezoic.sdk', module: 'ezoic-cmp-sdk' }</c>.
    /// The block sits between marker comments, so applying it again changes nothing and applying
    /// it with <c>includeCmp</c> true takes it out again.
    /// </summary>
    internal static class GradleCmpExclusion
    {
        internal const string Group = "com.ezoic.sdk";
        internal const string Module = "ezoic-cmp-sdk";
        internal const string BeginMarker = "// BEGIN Ezoic CMP exclusion";
        internal const string EndMarker = "// END Ezoic CMP exclusion";

        private static readonly Regex BlockPattern = new Regex(
            @"(?:\r?\n)?[ \t]*" + Regex.Escape(BeginMarker) + @"[^\n]*\n.*?" + Regex.Escape(EndMarker) + @"[^\n]*(?:\n|$)",
            RegexOptions.Singleline);

        /// <summary>
        /// Returns <paramref name="script"/> (a module's build.gradle, or build.gradle.kts when
        /// <paramref name="kotlinDsl"/> is true) with the exclusion block present when
        /// <paramref name="includeCmp"/> is false and absent when it is true.
        /// </summary>
        internal static string Apply(string script, bool includeCmp, bool kotlinDsl = false)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            string text = BlockPattern.Replace(script, string.Empty);
            if (includeCmp)
            {
                return text;
            }

            string newline = script.Contains("\r\n") ? "\r\n" : "\n";
            var block = new StringBuilder(text);
            if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal))
            {
                block.Append(newline);
            }

            block.Append(newline)
                .Append(BeginMarker)
                .Append(" (Include Ezoic CMP is off in Project Settings > Ezoic; added by com.ezoic.ads at build time)")
                .Append(newline)
                .Append("configurations.all {").Append(newline)
                .Append(kotlinDsl
                    ? "    exclude(group = \"" + Group + "\", module = \"" + Module + "\")"
                    : "    exclude group: '" + Group + "', module: '" + Module + "'")
                .Append(newline)
                .Append('}').Append(newline)
                .Append(EndMarker).Append(newline);
            return block.ToString();
        }

        /// <summary>Whether <paramref name="script"/> carries the exclusion block.</summary>
        internal static bool HasExclusion(string script)
        {
            return script != null && BlockPattern.IsMatch(script);
        }
    }

    /// <summary>
    /// Switches the Swift package product of <c>ezoic-swift-sdk-dist</c> that the generated Xcode
    /// project links: <c>EzoicAdsSDK</c> (ads and the built-in CMP) to <c>EzoicAdsSDKWithoutCMP</c>
    /// when the CMP is left out, and back when it is included. Only
    /// <c>XCSwiftPackageProductDependency</c> objects of the dist package reference change (their
    /// <c>productName</c>, plus the matching comments); every target that links the product
    /// follows. Other products of the package (<c>EzoicCmpSDK</c>) and other packages stay as they
    /// are, so applying it twice changes nothing.
    /// </summary>
    internal static class XcodeCmpProduct
    {
        internal const string WithCmp = "EzoicAdsSDK";
        internal const string WithoutCmp = "EzoicAdsSDKWithoutCMP";
        internal const string DistRepository = "ezoic-swift-sdk-dist";

        internal struct Result
        {
            /// <summary>The project text after the switch (the input when nothing changed or on error).</summary>
            public string Text;

            /// <summary>Product dependencies switched by this call.</summary>
            public int Switched;

            /// <summary>Product dependencies that already named the wanted product.</summary>
            public int AlreadyWanted;

            /// <summary>Whether the project references the ezoic-swift-sdk-dist package at all.</summary>
            public bool DistPackageFound;

            /// <summary>Set when the project could not be parsed; Text is then the input.</summary>
            public string Error;
        }

        /// <summary>
        /// Links <c>EzoicAdsSDKWithoutCMP</c> instead of <c>EzoicAdsSDK</c> when
        /// <paramref name="includeCmp"/> is false, and the reverse when it is true.
        /// </summary>
        internal static Result Apply(string pbxproj, bool includeCmp)
        {
            if (pbxproj == null)
            {
                throw new ArgumentNullException(nameof(pbxproj));
            }

            string from = includeCmp ? WithoutCmp : WithCmp;
            string to = includeCmp ? WithCmp : WithoutCmp;
            var result = new Result { Text = pbxproj };

            Dictionary<string, PbxValue> objects;
            try
            {
                objects = PbxParser.ParseObjects(pbxproj);
            }
            catch (FormatException e)
            {
                result.Error = e.Message;
                return result;
            }

            var distPackages = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in objects)
            {
                if (entry.Value.Isa == "XCRemoteSwiftPackageReference")
                {
                    string url = entry.Value.Scalar("repositoryURL");
                    if (url != null && url.IndexOf(DistRepository, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        distPackages.Add(entry.Key);
                    }
                }
            }

            result.DistPackageFound = distPackages.Count > 0;
            var edits = new List<PbxValue>();
            var switchedIds = new List<string>();
            foreach (var entry in objects)
            {
                PbxValue dependency = entry.Value;
                if (dependency.Isa != "XCSwiftPackageProductDependency")
                {
                    continue;
                }

                string package = dependency.Scalar("package");
                if (package == null || !distPackages.Contains(package))
                {
                    continue;
                }

                PbxValue productName;
                if (!dependency.Entries.TryGetValue("productName", out productName) || productName.Text == null)
                {
                    continue;
                }

                if (productName.Text == to)
                {
                    result.AlreadyWanted++;
                }
                else if (productName.Text == from)
                {
                    edits.Add(productName);
                    switchedIds.Add(entry.Key);
                }
            }

            if (edits.Count == 0)
            {
                return result;
            }

            edits.Sort((a, b) => b.Start.CompareTo(a.Start));
            var text = new StringBuilder(pbxproj);
            foreach (PbxValue value in edits)
            {
                text.Remove(value.Start, value.End - value.Start).Insert(value.Start, to);
            }

            string updated = text.ToString();
            foreach (string id in switchedIds)
            {
                // "<id> /* EzoicAdsSDK */" wherever the dependency is referenced.
                updated = Regex.Replace(updated,
                    @"(?<![0-9A-Za-z_])" + Regex.Escape(id) + @"(\s*)/\*\s*" + Regex.Escape(from) + @"\s*\*/",
                    m => id + m.Groups[1].Value + "/* " + to + " */");

                // "<build file id> /* EzoicAdsSDK in Frameworks */" for the build files that link it.
                foreach (var entry in objects)
                {
                    if (entry.Value.Isa == "PBXBuildFile" && entry.Value.Scalar("productRef") == id)
                    {
                        string buildFile = entry.Key;
                        updated = Regex.Replace(updated,
                            @"(?<![0-9A-Za-z_])" + Regex.Escape(buildFile) + @"(\s*)/\*\s*" + Regex.Escape(from) + @"( in [^*]*?)\s*\*/",
                            m => buildFile + m.Groups[1].Value + "/* " + to + m.Groups[2].Value + " */");
                    }
                }
            }

            result.Text = updated;
            result.Switched = edits.Count;
            return result;
        }
    }

    /// <summary>A parsed value of an old-style (OpenStep) property list, with its source span.</summary>
    internal sealed class PbxValue
    {
        /// <summary>The unquoted string for a scalar; null for a dictionary or an array.</summary>
        public string Text;

        /// <summary>Dictionary entries; null for a scalar or an array.</summary>
        public Dictionary<string, PbxValue> Entries;

        /// <summary>Source span of the value (for a quoted scalar, including the quotes).</summary>
        public int Start;
        public int End;

        public string Isa => Scalar("isa");

        public string Scalar(string key)
        {
            PbxValue value;
            return Entries != null && Entries.TryGetValue(key, out value) ? value.Text : null;
        }
    }

    /// <summary>
    /// A small reader for project.pbxproj (an old-style property list): dictionaries, arrays,
    /// quoted and bare strings, <c>/* */</c> and <c>//</c> comments. Enough to find objects by
    /// <c>isa</c> and edit a value in place without rewriting the rest of the file.
    /// </summary>
    internal sealed class PbxParser
    {
        private readonly string _s;
        private int _i;

        private PbxParser(string s)
        {
            _s = s;
        }

        /// <summary>Parses the whole file and returns its <c>objects</c> dictionary.</summary>
        /// <exception cref="FormatException">The text is not a property list with an objects dictionary.</exception>
        internal static Dictionary<string, PbxValue> ParseObjects(string pbxproj)
        {
            var parser = new PbxParser(pbxproj);
            PbxValue root = parser.ParseValue();
            parser.SkipTrivia();
            if (parser._i != parser._s.Length)
            {
                throw parser.Error("unexpected text after the root dictionary");
            }

            PbxValue objects;
            if (root.Entries == null || !root.Entries.TryGetValue("objects", out objects) || objects.Entries == null)
            {
                throw new FormatException("project.pbxproj has no objects dictionary");
            }

            return objects.Entries;
        }

        private FormatException Error(string message)
        {
            return new FormatException(string.Format(CultureInfo.InvariantCulture,
                "project.pbxproj: {0} at offset {1}", message, _i));
        }

        private void SkipTrivia()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsWhiteSpace(c))
                {
                    _i++;
                }
                else if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*')
                {
                    int end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        throw Error("unterminated comment");
                    }

                    _i = end + 2;
                }
                else if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                {
                    int end = _s.IndexOf('\n', _i);
                    _i = end < 0 ? _s.Length : end + 1;
                }
                else
                {
                    return;
                }
            }
        }

        private PbxValue ParseValue()
        {
            SkipTrivia();
            if (_i >= _s.Length)
            {
                throw Error("unexpected end of file");
            }

            switch (_s[_i])
            {
                case '{':
                    return ParseDictionary();
                case '(':
                    return ParseArray();
                default:
                    return ParseString();
            }
        }

        private PbxValue ParseDictionary()
        {
            var value = new PbxValue { Start = _i, Entries = new Dictionary<string, PbxValue>(StringComparer.Ordinal) };
            _i++;
            while (true)
            {
                SkipTrivia();
                if (_i >= _s.Length)
                {
                    throw Error("unterminated dictionary");
                }

                if (_s[_i] == '}')
                {
                    _i++;
                    break;
                }

                string key = ParseString().Text;
                Expect('=');
                PbxValue entry = ParseValue();
                Expect(';');
                value.Entries[key] = entry;
            }

            value.End = _i;
            return value;
        }

        private PbxValue ParseArray()
        {
            var value = new PbxValue { Start = _i };
            _i++;
            while (true)
            {
                SkipTrivia();
                if (_i >= _s.Length)
                {
                    throw Error("unterminated array");
                }

                if (_s[_i] == ')')
                {
                    _i++;
                    break;
                }

                ParseValue();
                SkipTrivia();
                if (_i < _s.Length && _s[_i] == ',')
                {
                    _i++;
                }
            }

            value.End = _i;
            return value;
        }

        private PbxValue ParseString()
        {
            SkipTrivia();
            int start = _i;
            if (_i < _s.Length && _s[_i] == '"')
            {
                var text = new StringBuilder();
                _i++;
                while (true)
                {
                    if (_i >= _s.Length)
                    {
                        throw Error("unterminated string");
                    }

                    char c = _s[_i++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c == '\\' && _i < _s.Length)
                    {
                        char escaped = _s[_i++];
                        text.Append(escaped == 'n' ? '\n' : escaped == 't' ? '\t' : escaped);
                    }
                    else
                    {
                        text.Append(c);
                    }
                }

                return new PbxValue { Text = text.ToString(), Start = start, End = _i };
            }

            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (char.IsWhiteSpace(c) || c == '{' || c == '}' || c == '(' || c == ')' || c == ';' || c == ',' ||
                    c == '=' || c == '"' || (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*'))
                {
                    break;
                }

                _i++;
            }

            if (_i == start)
            {
                throw Error("expected a value");
            }

            return new PbxValue { Text = _s.Substring(start, _i - start), Start = start, End = _i };
        }

        private void Expect(char c)
        {
            SkipTrivia();
            if (_i >= _s.Length || _s[_i] != c)
            {
                throw Error("expected '" + c + "'");
            }

            _i++;
        }
    }
}

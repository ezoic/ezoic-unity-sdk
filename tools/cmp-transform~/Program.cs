using System;
using System.IO;
using Ezoic.Ads.Editor;

// Applies the "Include Ezoic CMP" project edit to one generated file, in place, the way
// Editor/EzoicCmpBuildProcessor.cs does during a Unity build.
//   exit 0: done (the summary line says what changed)
//   exit 1: bad arguments or an unreadable project
//   exit 3: pbxproj with exclude, but the project links no EzoicAdsSDK product of ezoic-swift-sdk-dist
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3 || (args[2] != "include" && args[2] != "exclude"))
        {
            Console.Error.WriteLine("usage: cmp-transform gradle|pbxproj <file> include|exclude");
            return 1;
        }

        string kind = args[0];
        string path = args[1];
        bool includeCmp = args[2] == "include";
        string before = File.ReadAllText(path);

        if (kind == "gradle")
        {
            string after = GradleCmpExclusion.Apply(before, includeCmp, path.EndsWith(".kts", StringComparison.Ordinal));
            File.WriteAllText(path, after);
            Console.WriteLine("gradle {0}: changed={1} excluded={2}", path, after != before, GradleCmpExclusion.HasExclusion(after));
            return 0;
        }

        if (kind == "pbxproj")
        {
            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(before, includeCmp);
            if (result.Error != null)
            {
                Console.Error.WriteLine(result.Error);
                return 1;
            }

            File.WriteAllText(path, result.Text);
            Console.WriteLine("pbxproj {0}: switched={1} alreadyWanted={2} distPackageFound={3}",
                path, result.Switched, result.AlreadyWanted, result.DistPackageFound);
            return !includeCmp && result.Switched == 0 && result.AlreadyWanted == 0 ? 3 : 0;
        }

        Console.Error.WriteLine("unknown kind " + kind);
        return 1;
    }
}

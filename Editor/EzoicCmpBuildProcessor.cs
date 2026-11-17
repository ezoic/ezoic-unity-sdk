using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Ezoic.Ads.Editor
{
    /// <summary>
    /// Applies <see cref="EzoicAdsSettings.IncludeCmp"/> to Android and iOS builds.
    /// <list type="bullet">
    /// <item>Android: after Unity generates the Gradle project, adds (setting off) or removes
    /// (setting on) <c>configurations.all { exclude group: 'com.ezoic.sdk', module: 'ezoic-cmp-sdk' }</c>
    /// in <c>unityLibrary/build.gradle</c> and <c>launcher/build.gradle</c>.</item>
    /// <item>iOS: after the External Dependency Manager's iOS Resolver has added the
    /// <c>ezoic-swift-sdk-dist</c> Swift package (its post-process step runs at order 35), links
    /// the <c>EzoicAdsSDKWithoutCMP</c> product instead of <c>EzoicAdsSDK</c> (setting off), or
    /// switches a previous build's <c>EzoicAdsSDKWithoutCMP</c> back (setting on).</item>
    /// </list>
    /// With the setting on (the default) a fresh build is left exactly as Unity and EDM wrote it.
    /// </summary>
    public sealed class EzoicCmpBuildProcessor : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        /// <summary>
        /// After every EDM iOS Resolver step (refresh 10 … Swift packages 35 … pods 50, deps 60).
        /// </summary>
        internal const int IosPostProcessOrder = 100;

        private const string LogPrefix = "Ezoic Ads: ";

        // Assembly of the standalone com.ezoic.cmp-sdk package (Ezoic.Cmp.asmdef).
        private const string StandaloneCmpAssembly = "Ezoic.Cmp";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if ((target != BuildTarget.Android && target != BuildTarget.iOS) || EzoicAdsSettings.IncludeCmp)
            {
                return;
            }

            if (StandaloneCmpPackageInstalled())
            {
                throw new BuildFailedException(LogPrefix +
                    "\"Include Ezoic CMP\" is off (Project Settings > Ezoic), but the project also has the " +
                    "com.ezoic.cmp-sdk package, which needs the Ezoic CMP. Remove that package, or turn " +
                    "\"Include Ezoic CMP\" back on.");
            }

            Debug.Log(LogPrefix + "\"Include Ezoic CMP\" is off: this " + target +
                      " build leaves the Ezoic CMP out. Set EzoicConfiguration.CmpEnabled = false and pass your CMP's consent to the SDK.");
        }

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            bool includeCmp = EzoicAdsSettings.IncludeCmp;
            string root = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
            if (!includeCmp)
            {
                // EDM's Android Resolver can copy the libraries into the project as .aar files
                // instead of patching Gradle; a Gradle exclude can't remove a copied file.
                string copied = FindCopiedCmpLibrary(root);
                if (copied != null)
                {
                    throw new BuildFailedException(LogPrefix +
                        "\"Include Ezoic CMP\" is off, but the Android project has the CMP library as a file (" +
                        copied + "). Let the External Dependency Manager's Android Resolver patch " +
                        "mainTemplate.gradle (Custom Main Gradle Template on) instead of copying libraries " +
                        "into Assets/Plugins/Android, delete the copied com.ezoic.sdk.ezoic-cmp-sdk .aar, " +
                        "and build again.");
                }
            }

            int changed = 0;
            foreach (string module in new[] { unityLibraryPath, Path.Combine(root, "launcher") })
            {
                changed += ApplyToGradleModule(module, includeCmp) ? 1 : 0;
            }

            if (!includeCmp)
            {
                Debug.Log(LogPrefix + "excluded com.ezoic.sdk:ezoic-cmp-sdk from the Gradle project (" +
                          (changed > 0 ? "updated " + changed + " build file(s)" : "already excluded") + ").");
            }
        }

        /// <summary>An ezoic-cmp-sdk .aar in the generated project, outside Gradle build output.</summary>
        private static string FindCopiedCmpLibrary(string gradleRoot)
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            foreach (string file in Directory.GetFiles(gradleRoot, "*" + GradleCmpExclusion.Module + "*.aar", SearchOption.AllDirectories))
            {
                string relative = file.Substring(gradleRoot.Length);
                if (relative.IndexOf(separator + "build" + separator, StringComparison.Ordinal) < 0)
                {
                    return file;
                }
            }

            return null;
        }

        private static bool ApplyToGradleModule(string modulePath, bool includeCmp)
        {
            string groovy = Path.Combine(modulePath, "build.gradle");
            string kotlin = Path.Combine(modulePath, "build.gradle.kts");
            bool kotlinDsl = !File.Exists(groovy) && File.Exists(kotlin);
            string path = kotlinDsl ? kotlin : groovy;
            if (!File.Exists(path))
            {
                if (!includeCmp)
                {
                    Debug.LogWarning(LogPrefix + "no " + groovy + " to exclude the Ezoic CMP from.");
                }

                return false;
            }

            string before = File.ReadAllText(path);
            string after = GradleCmpExclusion.Apply(before, includeCmp, kotlinDsl);
            if (after == before)
            {
                return false;
            }

            File.WriteAllText(path, after);
            return true;
        }

        [PostProcessBuild(IosPostProcessOrder)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            bool includeCmp = EzoicAdsSettings.IncludeCmp;
            string pbxproj = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxproj))
            {
                if (!includeCmp)
                {
                    Debug.LogWarning(LogPrefix + "no " + pbxproj + "; link the EzoicAdsSDKWithoutCMP Swift package product yourself.");
                }

                return;
            }

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(File.ReadAllText(pbxproj), includeCmp);
            if (result.Error != null)
            {
                if (includeCmp)
                {
                    Debug.LogWarning(LogPrefix + "could not read the Xcode project to check the linked Ezoic product: " + result.Error);
                    return;
                }

                throw new BuildFailedException(LogPrefix + "\"Include Ezoic CMP\" is off, but the Xcode project could not be read (" +
                                               result.Error + "). In Xcode, link the " + XcodeCmpProduct.WithoutCmp +
                                               " product of ezoic-swift-sdk-dist instead of " + XcodeCmpProduct.WithCmp + ".");
            }

            if (result.Switched > 0)
            {
                File.WriteAllText(pbxproj, result.Text);
                Debug.Log(LogPrefix + "the Xcode project now links " +
                          (includeCmp ? XcodeCmpProduct.WithCmp : XcodeCmpProduct.WithoutCmp) +
                          " (\"Include Ezoic CMP\" is " + (includeCmp ? "on" : "off") + ").");
            }

            if (result.Switched > 0 && result.AlreadyWanted > 0)
            {
                Debug.LogWarning(LogPrefix + "the Xcode project lists the " +
                                 (includeCmp ? XcodeCmpProduct.WithCmp : XcodeCmpProduct.WithoutCmp) +
                                 " product more than once. Build with Replace (not Append) after changing \"Include Ezoic CMP\".");
            }

            if (!includeCmp && result.Switched == 0 && result.AlreadyWanted == 0)
            {
                Debug.LogWarning(LogPrefix + "\"Include Ezoic CMP\" is off, but the Xcode project doesn't link the " +
                                 XcodeCmpProduct.WithCmp + " product of ezoic-swift-sdk-dist" +
                                 (result.DistPackageFound ? "" : " (the package isn't in the project: is Swift Package Manager Integration on in EDM's iOS Resolver?)") +
                                 ". If you add the package yourself, add the " + XcodeCmpProduct.WithoutCmp + " product.");
            }
        }

        private static bool StandaloneCmpPackageInstalled()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == StandaloneCmpAssembly)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

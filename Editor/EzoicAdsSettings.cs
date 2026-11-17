using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ezoic.Ads.Editor
{
    /// <summary>
    /// Project settings of the Ezoic Ads package, under <b>Project Settings → Ezoic</b>. They are
    /// stored in <c>ProjectSettings/EzoicAdsSettings.json</c>, so they go into version control with
    /// the rest of the project settings. Build scripts can set them too, before
    /// <c>BuildPipeline.BuildPlayer</c>:
    /// <code>Ezoic.Ads.Editor.EzoicAdsSettings.IncludeCmp = false;</code>
    /// </summary>
    public static class EzoicAdsSettings
    {
        internal const string SettingsPath = "ProjectSettings/EzoicAdsSettings.json";

        [Serializable]
        private sealed class Data
        {
            public bool includeCmp = true;
        }

        /// <summary>
        /// Whether Android and iOS builds include the Ezoic CMP (the built-in consent dialog).
        /// Default <c>true</c>. Turn it off only if you use another CMP: the build then leaves the
        /// CMP's native code out of the app (Android: a Gradle exclude of
        /// <c>com.ezoic.sdk:ezoic-cmp-sdk</c>; iOS: the <c>EzoicAdsSDKWithoutCMP</c> Swift package
        /// product instead of <c>EzoicAdsSDK</c>). Also set <c>CmpEnabled = false</c> in your
        /// <c>EzoicConfiguration</c>. Don't turn it off in a project that also
        /// has the <c>com.ezoic.cmp-sdk</c> package: that package needs the CMP.
        /// </summary>
        public static bool IncludeCmp
        {
            get { return Load().includeCmp; }
            set
            {
                Data data = Load();
                if (data.includeCmp == value && File.Exists(SettingsPath))
                {
                    return;
                }

                data.includeCmp = value;
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(data, true) + "\n");
            }
        }

        private static Data Load()
        {
            var data = new Data();
            if (!File.Exists(SettingsPath))
            {
                return data;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(SettingsPath), data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Ezoic Ads: could not read " + SettingsPath + " (" + e.Message +
                                 "); using the default settings.");
            }

            return data;
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Ezoic", SettingsScope.Project)
            {
                label = "Ezoic",
                keywords = new[] { "Ezoic", "CMP", "consent", "GDPR", "TCF" },
                guiHandler = _ =>
                {
                    EditorGUILayout.Space();
                    bool include = EditorGUILayout.Toggle(
                        new GUIContent("Include Ezoic CMP",
                            "Builds the Ezoic consent dialog (CMP) into Android and iOS apps. Turn off only if you use another CMP."),
                        IncludeCmp);
                    if (include != IncludeCmp)
                    {
                        IncludeCmp = include;
                    }

                    EditorGUILayout.HelpBox(include
                            ? "The Ezoic CMP is built into Android and iOS apps. It shows the consent dialog " +
                              "while EzoicConfiguration.CmpEnabled is true (the default)."
                            : "Android and iOS builds leave the Ezoic CMP out: Gradle excludes " +
                              "com.ezoic.sdk:ezoic-cmp-sdk, and Xcode links the EzoicAdsSDKWithoutCMP Swift package " +
                              "product instead of EzoicAdsSDK. Also set EzoicConfiguration.CmpEnabled = false and pass " +
                              "your CMP's consent with SetGDPRConsent / SetGPPConsent. EzoicAds.IsBuiltInCmpIncluded " +
                              "reads false in these apps. Don't use this with the com.ezoic.cmp-sdk package. " +
                              "After changing it, build iOS with Replace, not Append.",
                        MessageType.Info);
                },
            };
        }
    }
}

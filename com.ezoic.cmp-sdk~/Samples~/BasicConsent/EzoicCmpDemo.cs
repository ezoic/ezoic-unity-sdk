// EzoicCmpDemo
// -----------------------------------------------------------------------------
// A single-file demo of the Ezoic CMP Unity package (com.ezoic.cmp-sdk), for apps
// that don't use the Ezoic Ads package. It initializes the CMP, asks for consent,
// opens the privacy settings and resets consent, logging every result to an
// on-screen console.
//
// The UI is drawn with Unity IMGUI (OnGUI / GUILayout) only: no scene assets, no
// prefabs, and no dependency on the UnityEngine.UI package.
//
// How to use:
//   1. Create an empty scene and add an empty GameObject.
//   2. Attach this script to it and set your Ezoic "Domain" in the Inspector.
//   3. Build to an Android or iOS device (the editor runs the CMP as a safe no-op
//      stub) and tap the on-screen buttons.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Ezoic.Cmp;
using UnityEngine;

namespace Ezoic.Cmp.Samples
{
    /// <summary>
    /// Drives an IMGUI control panel for <see cref="EzoicCmp"/> and writes every consent
    /// result to an on-screen scrolling log.
    /// </summary>
    public sealed class EzoicCmpDemo : MonoBehaviour
    {
        [Header("Ezoic configuration")]
        [Tooltip("Your Ezoic-registered domain, e.g. \"example.com\".")]
        [SerializeField] private string domain = "example.com";

        [Tooltip("Initialize automatically in Start (recommended for real apps).")]
        [SerializeField] private bool initializeOnStart = true;

        // Keep at most this many status lines on screen.
        private const int MaxLogLines = 15;

        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;
        private bool _initialized;
        private bool _disposed;

        private void Start()
        {
            Log("Ready. Set your domain, then tap Initialize.");
            if (initializeOnStart)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            _disposed = true;
        }

        private void OnGUI()
        {
            // Scale the IMGUI layer for high-DPI mobile screens; 160 dpi is "1x", and 2x is
            // the fallback when the dpi is unavailable (0 on some platforms).
            float scale = Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 160f) : 2f;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(10f, 10f, Screen.width / scale - 20f, Screen.height / scale - 20f));

            GUILayout.Label("Ezoic CMP — Basic Consent");
            GUILayout.Label("Consent required: " + Describe(EzoicCmp.IsConsentRequired)
                + "   |   Privacy options required: " + Describe(EzoicCmp.IsPrivacyOptionsRequired));

            GUILayout.BeginHorizontal();
            GUI.enabled = !_initialized;
            if (GUILayout.Button("Initialize"))
            {
                Initialize();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Ask if required"))
            {
                AskIfRequired();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            // In a real app, show this entry point while IsPrivacyOptionsRequired is true (GDPR
            // regions and covered US states). The demo keeps it enabled everywhere.
            if (GUILayout.Button("Privacy settings"))
            {
                Log("PresentConsentSettings()…");
                EzoicCmp.PresentConsentSettings(result => Log("Consent settings: " + result));
            }
            if (GUILayout.Button("Reset consent"))
            {
                Log("ResetConsent().");
                EzoicCmp.ResetConsent();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Status log");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void Initialize()
        {
            _initialized = true;
            Log("Initialize(\"" + domain + "\").");
            EzoicCmp.Initialize(domain);

            // Waits for the CMP's settings to load and shows the dialog only where it is
            // required (GDPR regions). In covered US states the CMP shows its notice by itself.
            AskIfRequired();
        }

        private void AskIfRequired()
        {
            Log("PresentConsentIfRequired()…");
            EzoicCmp.PresentConsentIfRequired(result => Log("Consent: " + result));
        }

        private void Log(string message)
        {
            if (_disposed)
            {
                return;
            }

            _log.Add(message);
            if (_log.Count > MaxLogLines)
            {
                _log.RemoveRange(0, _log.Count - MaxLogLines);
            }

            _logScroll = new Vector2(0f, float.MaxValue);
            Debug.Log("[EzoicCmpDemo] " + message);
        }

        private static string Describe(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "unknown";
        }
    }
}

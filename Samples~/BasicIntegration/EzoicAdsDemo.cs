// EzoicAdsDemo
// -----------------------------------------------------------------------------
// A single-file, self-contained demo of the Ezoic Ads Unity SDK. It exercises
// every public surface of the SDK — initialization, banners, interstitials, and
// rewarded ads — and logs every ad event to an on-screen console so you can watch
// the full lifecycle on a device.
//
// The UI is drawn with Unity IMGUI (OnGUI / GUILayout) only: no scene assets, no
// prefabs, and no dependency on the UnityEngine.UI package. That keeps the sample
// buildable in a headless / package-import context.
//
// How to use:
//   1. Create an empty scene and add an empty GameObject.
//   2. Attach this script to it.
//   3. In the Inspector, set your Ezoic "Domain" and the three ad unit ids
//      (Banner / Interstitial / Rewarded).
//   4. Build to an Android or iOS device (ads only serve on device — the editor
//      runs the SDK as a safe no-op stub) and tap the on-screen buttons.
//
// Recommended button order: Initialize first, then load/show any ad type.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Ezoic.Ads;
using UnityEngine;

namespace Ezoic.Ads.Samples
{
    /// <summary>
    /// Drives an IMGUI control panel that initializes the Ezoic Ads SDK and loads,
    /// shows, hides, and destroys banner, interstitial, and rewarded ads. Every
    /// public ad event is subscribed and written to an on-screen scrolling log.
    /// </summary>
    public sealed class EzoicAdsDemo : MonoBehaviour
    {
        [Header("Ezoic configuration")]
        [Tooltip("Your Ezoic-registered domain, e.g. \"example.com\".")]
        [SerializeField] private string domain = "example.com";

        [Tooltip("Ezoic ad unit id used for the banner ad.")]
        [SerializeField] private int bannerAdUnitId = 12345;

        [Tooltip("Ezoic ad unit id used for the interstitial ad.")]
        [SerializeField] private int interstitialAdUnitId = 23456;

        [Tooltip("Ezoic ad unit id used for the rewarded ad.")]
        [SerializeField] private int rewardedAdUnitId = 34567;

        // Keep at most this many status lines on screen.
        private const int MaxLogLines = 15;

        private readonly List<string> _log = new List<string>();
        private Vector2 _logScroll;

        private EzoicBannerAd _banner;
        private EzoicInterstitialAd _interstitial;
        private EzoicRewardedAd _rewarded;

        private bool _initializing;
        private bool _interstitialLoading;
        private bool _rewardedLoading;
        private bool _disposed;

        private void Start()
        {
            Log("Ready. Set your domain + ad unit ids, then tap Initialize.");
            Log("SDK version: " + Describe(EzoicAds.Version));
        }

        private void OnDestroy()
        {
            _disposed = true;

            // Destroying an ad stops native callbacks and releases the native peer,
            // which also ends the managed event subscriptions held by that instance.
            DestroyBanner();
            DestroyInterstitial();
            DestroyRewarded();
        }

        private void OnGUI()
        {
            // Scale the whole IMGUI layer for high-DPI mobile screens so buttons stay
            // tappable. 160 dpi is the baseline "1x" density; fall back to 2x when the
            // reported dpi is unavailable (0 on some platforms).
            float scale = Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 160f) : 2f;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float scaledWidth = Screen.width / scale;
            float scaledHeight = Screen.height / scale;

            GUILayout.BeginArea(new Rect(10f, 10f, scaledWidth - 20f, scaledHeight - 20f));

            GUILayout.Label("Ezoic Ads — Basic Integration");
            GUILayout.Label("Initialized: " + EzoicAds.IsInitialized + "   |   SDK: " + Describe(EzoicAds.Version));

            DrawInitializeSection();
            DrawBannerSection();
            DrawInterstitialSection();
            DrawRewardedSection();
            DrawLogSection();

            GUILayout.EndArea();

            GUI.matrix = previousMatrix;
        }

        private void DrawInitializeSection()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Domain: " + domain);
            GUI.enabled = !_initializing && !EzoicAds.IsInitialized;
            if (GUILayout.Button(_initializing ? "Initializing…" : "Initialize"))
            {
                Initialize();
            }
            GUI.enabled = true;
        }

        private void DrawBannerSection()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Banner (unit " + bannerAdUnitId + ")");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load"))
            {
                LoadBanner();
            }
            if (GUILayout.Button("Show"))
            {
                ShowBanner();
            }
            if (GUILayout.Button("Hide"))
            {
                HideBanner();
            }
            if (GUILayout.Button("Destroy"))
            {
                DestroyBanner();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInterstitialSection()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Interstitial (unit " + interstitialAdUnitId + ")");
            GUILayout.BeginHorizontal();
            GUI.enabled = !_interstitialLoading;
            if (GUILayout.Button(_interstitialLoading ? "Loading…" : "Load"))
            {
                LoadInterstitial();
            }
            GUI.enabled = _interstitial != null && _interstitial.IsLoaded;
            if (GUILayout.Button("Show"))
            {
                ShowInterstitial();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawRewardedSection()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Rewarded (unit " + rewardedAdUnitId + ")");
            GUILayout.BeginHorizontal();
            GUI.enabled = !_rewardedLoading;
            if (GUILayout.Button(_rewardedLoading ? "Loading…" : "Load"))
            {
                LoadRewarded();
            }
            GUI.enabled = _rewarded != null && _rewarded.IsLoaded;
            if (GUILayout.Button("Show"))
            {
                ShowRewarded();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawLogSection()
        {
            GUILayout.Space(6f);
            GUILayout.Label("Status log");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }
            GUILayout.EndScrollView();
        }

        // --- Initialize ------------------------------------------------------

        private void Initialize()
        {
            _initializing = true;
            Log("Initialize(\"" + domain + "\")…");
            EzoicAds.Initialize(domain, (success, error) =>
            {
                _initializing = false;
                if (_disposed)
                {
                    return;
                }

                if (success)
                {
                    Log("Initialize: success. SDK version " + Describe(EzoicAds.Version));
                }
                else
                {
                    Log("Initialize: FAILED — " + Describe(error));
                }
            });
        }

        // --- Banner ----------------------------------------------------------

        private void LoadBanner()
        {
            if (_banner == null)
            {
                // Adaptive banner (null size) anchored to the bottom of the screen.
                _banner = new EzoicBannerAd(bannerAdUnitId, BannerPosition.Bottom);
                _banner.OnLoaded += OnBannerLoaded;
                _banner.OnLoadFailed += OnBannerLoadFailed;
                _banner.OnClicked += OnBannerClicked;
                _banner.OnImpression += OnBannerImpression;
                _banner.OnSizeChanged += OnBannerSizeChanged;
                Log("Banner: created (bottom, adaptive).");
            }

            Log("Banner: Load()…");
            _banner.Load();
        }

        private void ShowBanner()
        {
            if (_banner == null)
            {
                Log("Banner: Show ignored — load a banner first.");
                return;
            }

            Log("Banner: Show().");
            _banner.Show();
        }

        private void HideBanner()
        {
            if (_banner == null)
            {
                Log("Banner: Hide ignored — load a banner first.");
                return;
            }

            Log("Banner: Hide().");
            _banner.Hide();
        }

        private void DestroyBanner()
        {
            if (_banner == null)
            {
                return;
            }

            _banner.OnLoaded -= OnBannerLoaded;
            _banner.OnLoadFailed -= OnBannerLoadFailed;
            _banner.OnClicked -= OnBannerClicked;
            _banner.OnImpression -= OnBannerImpression;
            _banner.OnSizeChanged -= OnBannerSizeChanged;
            _banner.Destroy();
            _banner = null;
            Log("Banner: Destroy().");
        }

        private void OnBannerLoaded()
        {
            Log("Banner event: OnLoaded — showing.");
            if (_banner != null)
            {
                _banner.Show();
            }
        }

        private void OnBannerLoadFailed(string error)
        {
            Log("Banner event: OnLoadFailed — " + Describe(error));
        }

        private void OnBannerClicked()
        {
            Log("Banner event: OnClicked.");
        }

        private void OnBannerImpression()
        {
            Log("Banner event: OnImpression.");
        }

        private void OnBannerSizeChanged(int width, int height)
        {
            Log("Banner event: OnSizeChanged — " + width + "x" + height + ".");
        }

        // --- Interstitial ----------------------------------------------------

        private void LoadInterstitial()
        {
            if (_interstitialLoading)
            {
                return;
            }

            // A new load replaces any previously loaded instance.
            DestroyInterstitial();

            _interstitialLoading = true;
            Log("Interstitial: Load()…");
            EzoicInterstitialAd.Load(
                interstitialAdUnitId,
                onLoaded: ad =>
                {
                    _interstitialLoading = false;
                    if (_disposed)
                    {
                        ad.Destroy();
                        return;
                    }

                    _interstitial = ad;
                    ad.OnShown += OnInterstitialShown;
                    ad.OnFailedToShow += OnInterstitialFailedToShow;
                    ad.OnDismissed += OnInterstitialDismissed;
                    ad.OnImpression += OnInterstitialImpression;
                    ad.OnClicked += OnInterstitialClicked;
                    Log("Interstitial event: onLoaded (IsLoaded=" + ad.IsLoaded + ").");
                },
                onFailed: error =>
                {
                    _interstitialLoading = false;
                    if (_disposed)
                    {
                        return;
                    }

                    Log("Interstitial event: onFailed — " + Describe(error));
                });
        }

        private void ShowInterstitial()
        {
            if (_interstitial == null || !_interstitial.IsLoaded)
            {
                Log("Interstitial: Show ignored — load one first.");
                return;
            }

            Log("Interstitial: Show().");
            _interstitial.Show();
        }

        private void DestroyInterstitial()
        {
            if (_interstitial == null)
            {
                return;
            }

            _interstitial.OnShown -= OnInterstitialShown;
            _interstitial.OnFailedToShow -= OnInterstitialFailedToShow;
            _interstitial.OnDismissed -= OnInterstitialDismissed;
            _interstitial.OnImpression -= OnInterstitialImpression;
            _interstitial.OnClicked -= OnInterstitialClicked;
            _interstitial.Destroy();
            _interstitial = null;
        }

        private void OnInterstitialShown()
        {
            Log("Interstitial event: OnShown.");
        }

        private void OnInterstitialFailedToShow(string error)
        {
            Log("Interstitial event: OnFailedToShow — " + Describe(error));
        }

        private void OnInterstitialDismissed()
        {
            Log("Interstitial event: OnDismissed.");
        }

        private void OnInterstitialImpression()
        {
            Log("Interstitial event: OnImpression.");
        }

        private void OnInterstitialClicked()
        {
            Log("Interstitial event: OnClicked.");
        }

        // --- Rewarded --------------------------------------------------------

        private void LoadRewarded()
        {
            if (_rewardedLoading)
            {
                return;
            }

            // A new load replaces any previously loaded instance.
            DestroyRewarded();

            _rewardedLoading = true;
            Log("Rewarded: Load()…");
            EzoicRewardedAd.Load(
                rewardedAdUnitId,
                onLoaded: ad =>
                {
                    _rewardedLoading = false;
                    if (_disposed)
                    {
                        ad.Destroy();
                        return;
                    }

                    _rewarded = ad;
                    ad.OnUserEarnedReward += OnRewardedUserEarnedReward;
                    ad.OnShown += OnRewardedShown;
                    ad.OnFailedToShow += OnRewardedFailedToShow;
                    ad.OnDismissed += OnRewardedDismissed;
                    ad.OnImpression += OnRewardedImpression;
                    ad.OnClicked += OnRewardedClicked;
                    Log("Rewarded event: onLoaded (IsLoaded=" + ad.IsLoaded + ").");
                },
                onFailed: error =>
                {
                    _rewardedLoading = false;
                    if (_disposed)
                    {
                        return;
                    }

                    Log("Rewarded event: onFailed — " + Describe(error));
                });
        }

        private void ShowRewarded()
        {
            if (_rewarded == null || !_rewarded.IsLoaded)
            {
                Log("Rewarded: Show ignored — load one first.");
                return;
            }

            Log("Rewarded: Show().");
            _rewarded.Show();
        }

        private void DestroyRewarded()
        {
            if (_rewarded == null)
            {
                return;
            }

            _rewarded.OnUserEarnedReward -= OnRewardedUserEarnedReward;
            _rewarded.OnShown -= OnRewardedShown;
            _rewarded.OnFailedToShow -= OnRewardedFailedToShow;
            _rewarded.OnDismissed -= OnRewardedDismissed;
            _rewarded.OnImpression -= OnRewardedImpression;
            _rewarded.OnClicked -= OnRewardedClicked;
            _rewarded.Destroy();
            _rewarded = null;
        }

        private void OnRewardedUserEarnedReward(string type, int amount)
        {
            Log("Rewarded event: OnUserEarnedReward — " + amount + " " + Describe(type) + ".");
        }

        private void OnRewardedShown()
        {
            Log("Rewarded event: OnShown.");
        }

        private void OnRewardedFailedToShow(string error)
        {
            Log("Rewarded event: OnFailedToShow — " + Describe(error));
        }

        private void OnRewardedDismissed()
        {
            Log("Rewarded event: OnDismissed.");
        }

        private void OnRewardedImpression()
        {
            Log("Rewarded event: OnImpression.");
        }

        private void OnRewardedClicked()
        {
            Log("Rewarded event: OnClicked.");
        }

        // --- Logging ---------------------------------------------------------

        private void Log(string message)
        {
            _log.Add(message);
            if (_log.Count > MaxLogLines)
            {
                _log.RemoveRange(0, _log.Count - MaxLogLines);
            }

            // Auto-scroll to the newest line.
            _logScroll = new Vector2(0f, float.MaxValue);
            Debug.Log("[EzoicAdsDemo] " + message);
        }

        private static string Describe(string value)
        {
            return string.IsNullOrEmpty(value) ? "(none)" : value;
        }
    }
}

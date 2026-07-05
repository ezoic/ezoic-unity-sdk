namespace Ezoic.Ads
{
    /// <summary>
    /// Screen position for an <see cref="EzoicBannerAd"/>. The banner is anchored to the
    /// corresponding edge/corner of the screen using the platform's gravity rules.
    /// </summary>
    public enum BannerPosition
    {
        /// <summary>Top edge, horizontally centered.</summary>
        Top,

        /// <summary>Bottom edge, horizontally centered.</summary>
        Bottom,

        /// <summary>Top-left corner.</summary>
        TopLeft,

        /// <summary>Top-right corner.</summary>
        TopRight,

        /// <summary>Bottom-left corner.</summary>
        BottomLeft,

        /// <summary>Bottom-right corner.</summary>
        BottomRight,

        /// <summary>Centered on screen.</summary>
        Center
    }
}

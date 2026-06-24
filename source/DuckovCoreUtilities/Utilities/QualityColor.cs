using UnityEngine;

namespace SlimeNull.DuckovInterop.Utilities
{
    public static class QualityColor
    {
        private static readonly Color[] _qualityColors = new Color[7]
        {
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
            new Color(0.6f, 0.9f, 0.6f, 0.24f),
            new Color(0.6f, 0.8f, 1f, 0.3f),
            new Color(1f, 0.5f, 1f, 0.4f),
            new Color(1f, 0.75f, 0.2f, 0.6f),
            new Color(1f, 0.3f, 0.3f, 0.4f)
        };

        public static Color Get(int quality)
        {
            if (quality < 0)
            {
                quality = 0;
            }

            if (quality >= _qualityColors.Length)
            {
                quality = _qualityColors.Length - 1;
            }

            return _qualityColors[quality];
        }
    }
}

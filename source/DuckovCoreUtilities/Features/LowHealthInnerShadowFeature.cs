using SlimeNull.DuckovCoreUtilities.Infrastructure;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class LowHealthInnerShadowFeature : FeatureBase
    {
        private const int GradientSize = 128;

        private Texture2D? _leftGradient;
        private Texture2D? _rightGradient;
        private Texture2D? _topGradient;
        private Texture2D? _bottomGradient;

        public override string Name => "Low health inner shadow";

        public Color ShadowColor { get; set; } = new Color(1f, 0f, 0f, 0.5f);
        public float ShadowDistance { get; set; } = 150f;
        public float HealthThresholdUpper { get; set; } = 0.6f;
        public float HealthThresholdLower { get; set; } = 0.2f;

        protected override void OnEnable()
        {
            EnsureTextures();
        }

        protected override void OnDisable()
        {
            DestroyTexture(ref _leftGradient);
            DestroyTexture(ref _rightGradient);
            DestroyTexture(ref _topGradient);
            DestroyTexture(ref _bottomGradient);
        }

        public override void OnGUI()
        {
            var alpha = GetShadowAlpha();
            if (alpha <= 0f)
            {
                return;
            }

            EnsureTextures();

            var distance = Mathf.Max(0f, ShadowDistance);
            if (distance <= 0f)
            {
                return;
            }

            var previousColor = GUI.color;
            var color = ShadowColor;
            color.a = alpha;
            GUI.color = color;

            GUI.DrawTexture(new Rect(0f, 0f, distance, Screen.height), _leftGradient);
            GUI.DrawTexture(new Rect(Screen.width - distance, 0f, distance, Screen.height), _rightGradient);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, distance), _topGradient);
            GUI.DrawTexture(new Rect(0f, Screen.height - distance, Screen.width, distance), _bottomGradient);

            GUI.color = previousColor;
        }

        private float GetShadowAlpha()
        {
            var thresholdUpper = Mathf.Clamp01(HealthThresholdUpper);
            var thresholdLower = Mathf.Clamp01(HealthThresholdLower);
            if (thresholdUpper <= 0f)
            {
                return 0f;
            }

            var health = LevelManager.Instance?.MainCharacter?.Health;
            if (health == null ||
                health.MaxHealth <= 0f)
            {
                return 0f;
            }

            var healthRatio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
            if (healthRatio >= thresholdUpper)
            {
                return 0f;
            }

            var danger = Mathf.InverseLerp(thresholdUpper, thresholdLower, healthRatio);
            return ShadowColor.a * danger;
        }

        private void EnsureTextures()
        {
            _leftGradient ??= CreateHorizontalGradient(reverse: false);
            _rightGradient ??= CreateHorizontalGradient(reverse: true);
            _topGradient ??= CreateVerticalGradient(reverse: true);
            _bottomGradient ??= CreateVerticalGradient(reverse: false);
        }

        private static Texture2D CreateHorizontalGradient(bool reverse)
        {
            var texture = new Texture2D(GradientSize, 1, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (var x = 0; x < GradientSize; x++)
            {
                var t = x / (float)(GradientSize - 1);
                var alpha = reverse ? t : 1f - t;
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateVerticalGradient(bool reverse)
        {
            var texture = new Texture2D(1, GradientSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (var y = 0; y < GradientSize; y++)
            {
                var t = y / (float)(GradientSize - 1);
                var alpha = reverse ? t : 1f - t;
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            return texture;
        }

        private static void DestroyTexture(ref Texture2D? texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }
    }
}

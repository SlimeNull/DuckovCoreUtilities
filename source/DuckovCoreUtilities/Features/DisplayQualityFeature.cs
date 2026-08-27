using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VLB;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class DisplayQualityFeature : ItemDecorateFeature
    {
        private const int QualityCount = 7;

        private DecorateMode _mode = DecorateMode.Border;
        private readonly Color[] _qualityColors =
        {
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 0f),
            new Color(0.6f, 0.9f, 0.6f, 0.24f),
            new Color(0.6f, 0.8f, 1f, 0.3f),
            new Color(1f, 0.5f, 1f, 0.4f),
            new Color(1f, 0.75f, 0.2f, 0.6f),
            new Color(1f, 0.3f, 0.3f, 0.4f),
        };

        public override string Name => "Display quality";

        private AccessTools.FieldRef<ItemDisplay, GameObject> backgroundRingOfItemDisplay =
            AccessTools.FieldRefAccess<ItemDisplay, GameObject>("backgroundRing");

        public enum DecorateMode
        {
            [InspectorName("@SettingsText/QualityBorder")]
            Border,
            [InspectorName("@SettingsText/QualityCornerBadge")]
            Corner,
            [InspectorName("@SettingsText/QualityBackground")]
            Background
        }

        public DecorateMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                RefreshExistingIndicators();
            }
        }

        public void SetQualityColors(
            Color quality0,
            Color quality1,
            Color quality2,
            Color quality3,
            Color quality4,
            Color quality5,
            Color quality6)
        {
            _qualityColors[0] = quality0;
            _qualityColors[1] = quality1;
            _qualityColors[2] = quality2;
            _qualityColors[3] = quality3;
            _qualityColors[4] = quality4;
            _qualityColors[5] = quality5;
            _qualityColors[6] = quality6;
            RefreshExistingIndicators();
        }

        public void RefreshExistingIndicators()
        {
            foreach (var component in Resources.FindObjectsOfTypeAll<QualityDisplayComponent>())
            {
                if (component != null)
                {
                    component.Mode = Mode;
                    component.Refresh(force: true);
                }
            }
        }

        private Color GetQualityColor(int quality)
        {
            return _qualityColors[Mathf.Clamp(quality, 0, QualityCount - 1)];
        }

        protected override void DecorateItemDisplay(ItemDisplay itemDisplay)
        {
            var child = new GameObject("QualityDisplay");
            var rectTransform = child.AddComponent<RectTransform>();
            var layoutElement = child.AddComponent<LayoutElement>();
            var display = child.AddComponent<CustomQualityIndicator>();
            var uniformModifier = child.AddComponent<UniformModifier>();

            rectTransform.SetParent(itemDisplay.transform, false);
            rectTransform.SetAsFirstSibling();

            rectTransform.localPosition = default;
            rectTransform.localRotation = default;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(.5f, .5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            uniformModifier.Radius = 15;

            var backgroundRing = backgroundRingOfItemDisplay.Invoke(itemDisplay).GetComponent<Graphic>();
            var qualityDisplayComponent = itemDisplay.gameObject.GetOrAddComponent<QualityDisplayComponent>();
            qualityDisplayComponent.Mode = Mode;
            qualityDisplayComponent.Initialize(this, itemDisplay, backgroundRing, display);
        }

        private class QualityDisplayComponent : MonoBehaviour
        {
            private DisplayQualityFeature? _feature;
            private ItemDisplay? _target;
            private Graphic? _originBackgroundRing;
            private CustomQualityIndicator? _customQualityIndicator;

            private Color _originColorOfBackgroundRing;
            private Item? _lastItem;
            private bool? _lastInspected;

            public DecorateMode Mode { get; set; }

            public void Initialize(
                DisplayQualityFeature feature,
                ItemDisplay target,
                Graphic originBackgroundRing,
                CustomQualityIndicator customQualityIndicator)
            {
                _feature = feature;
                _target = target;
                _originBackgroundRing = originBackgroundRing;
                _customQualityIndicator = customQualityIndicator;

                if (originBackgroundRing is not null)
                {
                    _originColorOfBackgroundRing = originBackgroundRing.color;
                }

                Refresh();
            }

            public void Refresh(bool force = false)
            {
                if (_target is not null &&
                    (force || _target.Target != _lastItem || _target.Target?.Inspected != _lastInspected))
                {
                    if (_feature is not null &&
                        _target.Target is { } item &&
                        item.StackCount > 0 &&
                        item.Inspected)
                    {
                        SetColor(_feature.GetQualityColor(item.Quality));
                    }
                    else
                    {
                        SetColor(Color.clear);
                    }

                    _lastItem = _target.Target;
                    _lastInspected = _target.Target?.Inspected;
                }
            }

            private void SetColor(Color color)
            {
                if (Mode == DecorateMode.Background ||
                    Mode == DecorateMode.Corner)
                {
                    if (_customQualityIndicator is not null)
                    {
                        _customQualityIndicator.Mode = Mode;
                        _customQualityIndicator.color = color;
                        _customQualityIndicator.SetAllDirty();
                    }

                    if (_originBackgroundRing is not null)
                    {
                        _originBackgroundRing.color = _originColorOfBackgroundRing;
                    }
                }
                else if (Mode == DecorateMode.Border)
                {
                    if (_customQualityIndicator is not null)
                    {
                        _customQualityIndicator.color = Color.clear;
                    }

                    if (_originBackgroundRing is not null)
                    {
                        if (color.a == 0)
                        {
                            color = _originColorOfBackgroundRing;
                        }

                        _originBackgroundRing.color = color;
                    }
                }
            }
        }

        [HarmonyPatchCategory("ItemDecorateFeature")]
        [HarmonyPatch(typeof(ItemDisplay), "Refresh")]
        private static class ItemDisplayRefreshPatch
        {
            private static void Postfix(ItemDisplay __instance)
            {
                __instance.GetComponent<QualityDisplayComponent>()?.Refresh();
            }
        }

        private class CustomQualityIndicator : Graphic
        {
            private const float CornerSize = 0.3f;
            private const float Radius = 15f;
            private const int ArcSegments = 8;

            public DecorateMode Mode { get; set; }

            public CustomQualityIndicator()
            {
                color = Color.clear;
                raycastTarget = false;
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();

                if (Mode == DecorateMode.Corner)
                {
                    var rect = rectTransform.rect;

                    var width = rect.width;
                    var height = rect.height;

                    if (width <= 0f || height <= 0f)
                        return;

                    var radius = Mathf.Min(Radius, width * CornerSize, height * CornerSize);

                    var xRight = 1f;
                    var yTop = 1f;
                    var xInner = 1f - CornerSize;
                    var yInner = 1f - CornerSize;

                    var rx = radius / width;
                    var ry = radius / height;

                    var points = new List<Vector2>();

                    // Right edge lower point: (1, 0.7)
                    points.Add(ToLocal(new Vector2(xRight, yInner), rect));

                    // Rounded outer corner arc, replacing the hard point (1, 1).
                    // Arc goes from right edge to top edge around top-right corner.
                    for (var i = 0; i <= ArcSegments; i++)
                    {
                        var t = i / (float)ArcSegments;
                        var angle = Mathf.Lerp(0f, 90f, t) * Mathf.Deg2Rad;

                        var x = 1f - rx + Mathf.Cos(angle) * rx;
                        var y = 1f - ry + Mathf.Sin(angle) * ry;

                        points.Add(ToLocal(new Vector2(x, y), rect));
                    }

                    // Top edge left point: (0.7, 1)
                    points.Add(ToLocal(new Vector2(xInner, yTop), rect));

                    for (var i = 0; i < points.Count; i++)
                        vh.AddVert(points[i], color, Vector2.zero);

                    // Fan triangulation. Shape is convex enough for this.
                    for (var i = 1; i < points.Count - 1; i++)
                        vh.AddTriangle(0, i, i + 1);
                }
                else if (Mode == DecorateMode.Background)
                {
                    var rect = rectTransform.rect;
                    var width = rect.width;
                    var height = rect.height;

                    if (width <= 0f || height <= 0f)
                        return;

                    var radius = Mathf.Min(Radius, width * 0.5f, height * 0.5f);

                    var centerIndex = 0;
                    vh.AddVert(rect.center, color, Vector2.zero);

                    var points = new List<Vector2>();

                    if (radius <= 0f)
                    {
                        points.Add(new Vector2(rect.xMax, rect.yMin));
                        points.Add(new Vector2(rect.xMax, rect.yMax));
                        points.Add(new Vector2(rect.xMin, rect.yMax));
                        points.Add(new Vector2(rect.xMin, rect.yMin));
                    }
                    else
                    {
                        AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f, 0f);
                        AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
                        AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
                        AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
                    }

                    for (var i = 0; i < points.Count; i++)
                        vh.AddVert(points[i], color, Vector2.zero);

                    for (var i = 0; i < points.Count; i++)
                    {
                        var current = i + 1;
                        var next = i == points.Count - 1 ? 1 : i + 2;

                        vh.AddTriangle(centerIndex, current, next);
                    }
                }
            }


            private static void AddArc(
                List<Vector2> points,
                Vector2 center,
                float radius,
                float startAngle,
                float endAngle)
            {
                for (var i = 0; i <= ArcSegments; i++)
                {
                    var t = i / (float)ArcSegments;
                    var angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;

                    points.Add(new Vector2(
                        center.x + Mathf.Cos(angle) * radius,
                        center.y + Mathf.Sin(angle) * radius
                    ));
                }
            }

            private static Vector2 ToLocal(Vector2 normalized, Rect rect)
            {
                return new Vector2(
                    rect.xMin + normalized.x * rect.width,
                    rect.yMin + normalized.y * rect.height
                );
            }
        }
    }
}

using Duckov.UI;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Features.Abstraction;
using SlimeNull.DuckovCoreUtilities.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class DisplayQualityFeature : ItemDecorateFeature
    {
        public override string Name => "Display quality";

        protected override void DecorateItemDisplay(ItemDisplay itemDisplay)
        {
            var child = new GameObject("QualityDisplay");
            var rectTransform = child.AddComponent<RectTransform>();
            var layoutElement = child.AddComponent<LayoutElement>();
            var display = child.AddComponent<QualityDisplayComponent>();
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

            display.Initialize(itemDisplay);

            Debug.Log($"Display quality feature applied to {itemDisplay.name}");
        }

        private class QualityDisplayComponent : Graphic
        {
            private const float CornerSize = 0.3f;
            private const float Radius = 15f;
            private const int ArcSegments = 8;

            private ItemDisplay? _target;
            private Item? _lastItem;

            public void Initialize(ItemDisplay target)
            {
                _target = target;
            }

            public QualityDisplayComponent()
            {
                color = Color.clear;
                raycastTarget = false;
            }

            void LateUpdate()
            {
                if (_target is not null &&
                    _target.Target != _lastItem)
                {
                    if (_target.Target is { } item &&
                        item.StackCount > 0 &&
                        item.Inspected)
                    {
                        color = QualityColor.Get(item.Quality);
                    }
                    else
                    {
                        color = Color.clear;
                    }

                    _lastItem = _target.Target;
                    Debug.Log($"Quality display updated for {_lastItem?.DisplayName}, quality: {_lastItem?.Quality}");
                }
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();

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

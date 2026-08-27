using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class GrenadeRadiusFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(GrenadeRadiusFeature);

        private static GrenadeRadiusFeature? _active;

        public override string Name => "Grenade radius display";

        public Color RadiusColor { get; set; } = new Color(1f, 0.25f, 0.25f, 0.35f);
        public Color ProgressColor { get; set; } = new Color(1f, 0.9f, 0.6f, 0.5f);
        public Color SmokeTimerColor { get; set; } = Color.white;
        public bool ShowFuseProgress { get; set; } = true;
        public bool ShowSmokeTimer { get; set; } = true;

        public void RefreshExistingIndicators()
        {
            foreach (var indicator in Resources.FindObjectsOfTypeAll<GrenadeRangeIndicator>())
            {
                indicator?.UpdateAppearance(RadiusColor, ProgressColor);
            }
            foreach (var indicator in Resources.FindObjectsOfTypeAll<SmokeLifetimeIndicator>())
            {
                indicator?.UpdateAppearance(ShowSmokeTimer, SmokeTimerColor);
            }
        }

        protected override void OnEnable()
        {
            _active = this;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            _active = null;

            foreach (var indicator in Resources.FindObjectsOfTypeAll<GrenadeRangeIndicator>())
            {
                if (indicator != null)
                {
                    UnityEngine.Object.Destroy(indicator);
                }
            }
            foreach (var indicator in Resources.FindObjectsOfTypeAll<SmokeLifetimeIndicator>())
            {
                if (indicator != null)
                {
                    UnityEngine.Object.Destroy(indicator);
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(Grenade), "Launch")]
        private static class GrenadeLaunchPatch
        {
            private static void Postfix(Grenade __instance, CharacterMainControl fromCharacter)
            {
                var feature = _active;
                if (feature == null || __instance == null || fromCharacter == null)
                {
                    return;
                }

                var indicator = __instance.GetComponent<GrenadeRangeIndicator>() ??
                    __instance.gameObject.AddComponent<GrenadeRangeIndicator>();
                indicator.Setup(__instance, feature);
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(FowSmoke), "Start")]
        private static class SmokeStartPatch
        {
            private static void Postfix(FowSmoke __instance)
            {
                var feature = _active;
                if (feature == null || !feature.ShowSmokeTimer || __instance == null)
                {
                    return;
                }

                var indicator = __instance.GetComponent<SmokeLifetimeIndicator>() ??
                    __instance.gameObject.AddComponent<SmokeLifetimeIndicator>();
                indicator.Setup(__instance.lifeTime, __instance.startTime, feature.SmokeTimerColor);
            }
        }

        private sealed class GrenadeRangeIndicator : MonoBehaviour
        {
            private const int Segments = 64;
            private const float YOffset = 0.08f;

            private Grenade? _grenade;
            private GameObject? _root;
            private Mesh? _baseMesh;
            private Mesh? _progressMesh;
            private Material? _baseMaterial;
            private Material? _progressMaterial;
            private float _radius;
            private float _duration;
            private float _startTime;
            private bool _timerStarted;
            private bool _showProgress;

            public void Setup(Grenade grenade, GrenadeRadiusFeature feature)
            {
                CleanupVisuals();
                _grenade = grenade;
                _radius = Mathf.Max(0.01f, grenade.damageRange);
                _duration = Mathf.Max(0.01f, grenade.delayTime);
                _showProgress = feature.ShowFuseProgress;

                _root = new GameObject("DCU_GrenadeRadius");
                _root.transform.rotation = Quaternion.identity;
                CreateDisc("Radius", feature.RadiusColor, 3000, out _baseMesh, out _baseMaterial);
                WorldDisc.Build(_baseMesh!, _radius, Segments, 1f);

                if (_showProgress)
                {
                    CreateDisc("FuseProgress", feature.ProgressColor, 3001, out _progressMesh, out _progressMaterial);
                    WorldDisc.Build(_progressMesh!, 0f, Segments, 1f);
                }

                grenade.onExplodeEvent.RemoveListener(OnExploded);
                grenade.onExplodeEvent.AddListener(OnExploded);

                if (!grenade.delayFromCollide)
                {
                    StartTimer();
                }
                UpdatePosition();
            }

            public void UpdateAppearance(Color radiusColor, Color progressColor)
            {
                WorldDisc.SetColor(_baseMaterial, radiusColor);
                WorldDisc.SetColor(_progressMaterial, progressColor);
            }

            private void CreateDisc(string name, Color color, int renderQueue, out Mesh mesh, out Material material)
            {
                var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(_root!.transform, false);

                mesh = new Mesh { name = $"DCU_{name}Mesh" };
                mesh.MarkDynamic();
                child.GetComponent<MeshFilter>().sharedMesh = mesh;

                material = WorldDisc.CreateMaterial(color, renderQueue);
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            private void OnCollisionEnter(Collision _)
            {
                if (_grenade != null && _grenade.delayFromCollide && !_timerStarted)
                {
                    StartTimer();
                }
            }

            private void StartTimer()
            {
                _startTime = Time.time;
                _timerStarted = true;
            }

            private void LateUpdate()
            {
                if (_grenade == null || _root == null)
                {
                    return;
                }

                UpdatePosition();
                if (_showProgress && _progressMesh != null && _timerStarted)
                {
                    var progress = Mathf.Clamp01((Time.time - _startTime) / _duration);
                    WorldDisc.Build(_progressMesh, _radius * progress, Segments, 1f);
                }
            }

            private void UpdatePosition()
            {
                if (_grenade == null || _root == null)
                {
                    return;
                }

                var position = _grenade.transform.position;
                position.y += YOffset;
                _root.transform.SetPositionAndRotation(position, Quaternion.identity);
            }

            private void OnExploded()
            {
                Destroy(this);
            }

            private void OnDestroy()
            {
                if (_grenade != null && _grenade.onExplodeEvent != null)
                {
                    _grenade.onExplodeEvent.RemoveListener(OnExploded);
                }
                CleanupVisuals();
            }

            private void CleanupVisuals()
            {
                if (_root != null)
                {
                    Destroy(_root);
                    _root = null;
                }
                if (_baseMesh != null)
                {
                    Destroy(_baseMesh);
                    _baseMesh = null;
                }
                if (_progressMesh != null)
                {
                    Destroy(_progressMesh);
                    _progressMesh = null;
                }
                if (_baseMaterial != null)
                {
                    Destroy(_baseMaterial);
                    _baseMaterial = null;
                }
                if (_progressMaterial != null)
                {
                    Destroy(_progressMaterial);
                    _progressMaterial = null;
                }
            }
        }

        private sealed class SmokeLifetimeIndicator : MonoBehaviour
        {
            private const int Segments = 40;

            private GameObject? _root;
            private Mesh? _backgroundMesh;
            private Mesh? _progressMesh;
            private Material? _backgroundMaterial;
            private Material? _progressMaterial;
            private float _startAt;
            private float _endAt;

            public void Setup(float lifetime, float startDelay, Color color)
            {
                CleanupVisuals();
                _startAt = Time.time + Mathf.Max(0f, startDelay);
                _endAt = _startAt + Mathf.Max(0.01f, lifetime);

                _root = new GameObject("DCU_SmokeLifetime");
                _root.transform.SetParent(transform, false);
                _root.transform.localPosition = new Vector3(0f, 1.5f, 0f);

                CreateDisc("Background", Color.black, 3002, out _backgroundMesh, out _backgroundMaterial);
                WorldDisc.Build(_backgroundMesh!, 0.33f, Segments, 1f, vertical: true);
                CreateDisc("Remaining", color, 3003, out _progressMesh, out _progressMaterial);
                WorldDisc.Build(_progressMesh!, 0.3f, Segments, 1f, vertical: true);
            }

            public void UpdateAppearance(bool visible, Color color)
            {
                if (_root != null)
                {
                    _root.SetActive(visible);
                }
                WorldDisc.SetColor(_progressMaterial, color);
            }

            private void CreateDisc(string name, Color color, int renderQueue, out Mesh mesh, out Material material)
            {
                var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(_root!.transform, false);

                mesh = new Mesh { name = $"DCU_Smoke{name}Mesh" };
                mesh.MarkDynamic();
                child.GetComponent<MeshFilter>().sharedMesh = mesh;

                material = WorldDisc.CreateMaterial(color, renderQueue);
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            private void LateUpdate()
            {
                if (_root == null || _progressMesh == null)
                {
                    return;
                }

                var camera = Camera.main;
                if (camera != null)
                {
                    _root.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
                }

                if (Time.time < _startAt)
                {
                    return;
                }

                var remaining = 1f - Mathf.InverseLerp(_startAt, _endAt, Time.time);
                WorldDisc.Build(_progressMesh, 0.3f, Segments, remaining, vertical: true);
                if (remaining <= 0f)
                {
                    _root.SetActive(false);
                }
            }

            private void OnDestroy()
            {
                CleanupVisuals();
            }

            private void CleanupVisuals()
            {
                if (_root != null)
                {
                    Destroy(_root);
                    _root = null;
                }
                if (_backgroundMesh != null)
                {
                    Destroy(_backgroundMesh);
                    _backgroundMesh = null;
                }
                if (_progressMesh != null)
                {
                    Destroy(_progressMesh);
                    _progressMesh = null;
                }
                if (_backgroundMaterial != null)
                {
                    Destroy(_backgroundMaterial);
                    _backgroundMaterial = null;
                }
                if (_progressMaterial != null)
                {
                    Destroy(_progressMaterial);
                    _progressMaterial = null;
                }
            }
        }

        private static class WorldDisc
        {
            public static Material CreateMaterial(Color color, int renderQueue)
            {
                var shader = FindShader();
                if (shader == null)
                {
                    throw new InvalidOperationException("No compatible transparent shader was found.");
                }

                var material = new Material(shader) { renderQueue = renderQueue };
                SetIfPresent(material, "_Color", color);
                SetIfPresent(material, "_Surface", 1f);
                SetIfPresent(material, "_ZWrite", 0f);
                SetIfPresent(material, "_Cull", 0f);
                SetIfPresent(material, "_SrcBlend", 5f);
                SetIfPresent(material, "_DstBlend", 10f);
                return material;
            }

            public static void Build(Mesh mesh, float radius, int segments, float ratio, bool vertical = false)
            {
                ratio = Mathf.Clamp01(ratio);
                var vertices = new Vector3[segments + 2];
                var triangles = new int[segments * 3];
                var angle = Mathf.PI * 2f * ratio;

                vertices[0] = Vector3.zero;
                for (var i = 0; i <= segments; i++)
                {
                    var current = angle * i / segments + (vertical ? Mathf.PI / 2f : 0f);
                    var x = radius * Mathf.Cos(current);
                    var other = radius * Mathf.Sin(current);
                    vertices[i + 1] = vertical ? new Vector3(x, other, 0f) : new Vector3(x, 0f, other);
                }
                for (var i = 0; i < segments; i++)
                {
                    var offset = i * 3;
                    triangles[offset] = 0;
                    triangles[offset + 1] = i + 1;
                    triangles[offset + 2] = i + 2;
                }

                mesh.Clear();
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
            }

            public static void SetColor(Material? material, Color color)
            {
                if (material != null && material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", color);
                }
            }

            private static Shader? FindShader()
            {
                foreach (var name in new[]
                {
                    "Sprites/Default",
                    "Unlit/Transparent",
                    "Unlit/Color",
                    "Universal Render Pipeline/Unlit",
                    "UI/Default",
                    "Hidden/Internal-Colored"
                })
                {
                    var shader = Shader.Find(name);
                    if (shader != null)
                    {
                        return shader;
                    }
                }
                return null;
            }

            private static void SetIfPresent(Material material, string property, float value)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, value);
                }
            }

            private static void SetIfPresent(Material material, string property, Color value)
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, value);
                }
            }
        }
    }
}

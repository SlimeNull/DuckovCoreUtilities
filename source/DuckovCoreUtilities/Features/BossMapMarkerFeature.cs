using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.UI;
using Duckov.Utilities;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class BossMapMarkerFeature : FeatureBase
    {
        public enum TrackingMode
        {
            [InspectorName("@SettingsText/TrackingStatic")]
            Static,

            [InspectorName("@SettingsText/TrackingDynamic")]
            Dynamic,
        }

        private const string HarmonyCategory = nameof(BossMapMarkerFeature);
        private const int BossMarkerIconIndex = 3;

        private readonly Dictionary<CharacterMainControl, BossMarker> _markers =
            new Dictionary<CharacterMainControl, BossMarker>();
        private readonly List<CharacterMainControl?> _staleCharacters = new List<CharacterMainControl?>();

        private TrackingMode _mode = TrackingMode.Static;
        private bool _showNames = true;
        private Color _markerColor = new Color(1f, 0.3f, 0.3f, 1f);
        private bool _wasMapOpen;

        private static BossMapMarkerFeature? Current { get; set; }

        public override string Name => "Boss map markers";

        public TrackingMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                if (_mode == TrackingMode.Static)
                {
                    RestoreSpawnPositions();
                }
            }
        }

        public bool ShowNames
        {
            get => _showNames;
            set
            {
                if (_showNames == value)
                {
                    return;
                }

                _showNames = value;
                RefreshMarkerVisuals();
            }
        }

        public Color MarkerColor
        {
            get => _markerColor;
            set
            {
                if (_markerColor == value)
                {
                    return;
                }

                _markerColor = value;
                RefreshMarkerVisuals();
            }
        }

        protected override void OnEnable()
        {
            Current = this;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            Health.OnDead += OnHealthDead;
            Context.Harmony.PatchCategory(HarmonyCategory);
            TrackExistingBosses();
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            Health.OnDead -= OnHealthDead;
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            ClearMarkers();
            _wasMapOpen = false;
        }

        public override void Tick()
        {
            if (_markers.Count == 0 || Mode != TrackingMode.Dynamic)
            {
                _wasMapOpen = false;
                return;
            }

            var mapOpen = IsMapOpen();
            if (mapOpen && !_wasMapOpen)
            {
                PruneDestroyedMarkers();
                RefreshMarkerVisuals();
            }

            if (mapOpen)
            {
                UpdateDynamicPositions();
            }

            _wasMapOpen = mapOpen;
        }

        private void OnAfterLevelInitialized()
        {
            ClearMarkers();
            TrackExistingBosses();
        }

        private void OnHealthDead(Health health, DamageInfo _)
        {
            if (health != null)
            {
                RemoveMarker(health.TryGetCharacter());
            }
        }

        public override void RefreshLocalization()
        {
            RefreshMarkerVisuals();
        }

        private void TrackExistingBosses()
        {
            foreach (var root in UnityEngine.Object.FindObjectsOfType<CharacterSpawnerRoot>(includeInactive: true))
            {
                if (root == null || !IsLoadedSceneObject(root.gameObject) || root.CreatedCharacters == null)
                {
                    continue;
                }

                foreach (var character in root.CreatedCharacters)
                {
                    TryTrack(character);
                }
            }
        }

        private void TryTrack(CharacterMainControl? character)
        {
            try
            {
                TrackCharacter(character);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovCoreUtilities] Could not create BOSS map marker: {ex}");
            }
        }

        private void TrackCharacter(CharacterMainControl? character)
        {
            if (character == null ||
                _markers.ContainsKey(character) ||
                !IsLoadedSceneObject(character.gameObject) ||
                character.Health == null ||
                character.Health.IsDead ||
                !IsBoss(character) ||
                character.GetComponent<SimplePointOfInterest>() != null)
            {
                return;
            }

            var spawnPosition = character.transform.position;
            var markerObject = new GameObject("DCU_BossMarker:" + GetDisplayName(character));
            try
            {
                markerObject.transform.position = spawnPosition;
                if (MultiSceneCore.MainScene.HasValue)
                {
                    MultiSceneCore.MoveToMainScene(markerObject);
                }

                var point = markerObject.AddComponent<SimplePointOfInterest>();
                point.ShadowColor = Color.clear;
                point.ShadowDistance = 0f;
                point.HideIcon = false;

                var marker = new BossMarker(character, markerObject, point, spawnPosition);
                _markers.Add(character, marker);
                RefreshMarkerVisual(marker);
            }
            catch
            {
                _markers.Remove(character);
                UnityEngine.Object.Destroy(markerObject);
                throw;
            }
        }

        private static bool IsBoss(CharacterMainControl character)
        {
            if (character.isBossCharacter)
            {
                return true;
            }

            var preset = character.characterPreset;
            if (preset == null)
            {
                return false;
            }

            if (preset.isBoss)
            {
                return true;
            }

            var bossIcon = GameplayDataSettings.UIStyle?.BossCharacterIcon;
            return bossIcon != null && preset.GetCharacterIcon() == bossIcon;
        }

        private void UpdateDynamicPositions()
        {
            _staleCharacters.Clear();
            foreach (var pair in _markers)
            {
                var character = pair.Key;
                if (character == null ||
                    character.Health == null ||
                    character.Health.IsDead ||
                    !IsLoadedSceneObject(character.gameObject))
                {
                    _staleCharacters.Add(character);
                    continue;
                }

                pair.Value.MarkerObject.transform.position = character.transform.position;
            }

            RemoveStaleCharacters();
        }

        private void RestoreSpawnPositions()
        {
            foreach (var marker in _markers.Values)
            {
                if (marker.MarkerObject != null)
                {
                    marker.MarkerObject.transform.position = marker.SpawnPosition;
                }
            }
        }

        private void PruneDestroyedMarkers()
        {
            _staleCharacters.Clear();
            foreach (var pair in _markers)
            {
                if (pair.Key == null ||
                    pair.Key.Health == null ||
                    pair.Key.Health.IsDead ||
                    !IsLoadedSceneObject(pair.Key.gameObject))
                {
                    _staleCharacters.Add(pair.Key);
                }
            }

            RemoveStaleCharacters();
        }

        private void RemoveStaleCharacters()
        {
            foreach (var character in _staleCharacters)
            {
                RemoveMarker(character);
            }
            _staleCharacters.Clear();
        }

        private void RemoveMarker(CharacterMainControl? character)
        {
            if (ReferenceEquals(character, null) || !_markers.TryGetValue(character, out var marker))
            {
                return;
            }

            _markers.Remove(character);
            DestroyMarker(marker);
        }

        private void ClearMarkers()
        {
            foreach (var marker in _markers.Values)
            {
                DestroyMarker(marker);
            }

            _markers.Clear();
            _staleCharacters.Clear();
        }

        private static void DestroyMarker(BossMarker marker)
        {
            if (marker.Point != null)
            {
                marker.Point.enabled = false;
            }
            if (marker.MarkerObject != null)
            {
                UnityEngine.Object.Destroy(marker.MarkerObject);
            }
        }

        private void RefreshMarkerVisuals()
        {
            foreach (var marker in _markers.Values)
            {
                RefreshMarkerVisual(marker);
            }
        }

        private void RefreshMarkerVisual(BossMarker marker)
        {
            if (marker.Character == null || marker.Point == null || marker.MarkerObject == null)
            {
                return;
            }

            var displayName = GetDisplayName(marker.Character);
            marker.MarkerObject.name = "DCU_BossMarker:" + displayName;
            marker.Point.Color = MarkerColor;
            marker.Point.Setup(GetBossIcon(), ShowNames ? displayName : string.Empty, followActiveScene: true);
        }

        private static Sprite? GetBossIcon()
        {
            var icons = MapMarkerManager.Icons;
            if (icons != null && icons.Count > BossMarkerIconIndex && icons[BossMarkerIconIndex] != null)
            {
                return icons[BossMarkerIconIndex];
            }

            return GameplayDataSettings.UIStyle?.BossCharacterIcon;
        }

        private static string GetDisplayName(CharacterMainControl character)
        {
            var displayName = character.characterPreset?.DisplayName;
            return string.IsNullOrWhiteSpace(displayName) ? "BOSS" : displayName;
        }

        private static bool IsMapOpen()
        {
            var map = MiniMapView.Instance;
            return map != null && View.ActiveView == map;
        }

        private static bool IsLoadedSceneObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            var scene = gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private sealed class BossMarker
        {
            public BossMarker(
                CharacterMainControl character,
                GameObject markerObject,
                SimplePointOfInterest point,
                Vector3 spawnPosition)
            {
                Character = character;
                MarkerObject = markerObject;
                Point = point;
                SpawnPosition = spawnPosition;
            }

            public CharacterMainControl Character { get; }
            public GameObject MarkerObject { get; }
            public SimplePointOfInterest Point { get; }
            public Vector3 SpawnPosition { get; }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(CharacterSpawnerRoot), nameof(CharacterSpawnerRoot.AddCreatedCharacter))]
        private static class CharacterSpawnedPatch
        {
            private static void Postfix(CharacterMainControl c)
            {
                Current?.TryTrack(c);
            }
        }
    }
}

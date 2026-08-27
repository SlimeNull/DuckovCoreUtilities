using Duckov.Quests.UI;
using HarmonyLib;
using Saves;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class QuestFavoriteFeature : FeatureBase
    {
        public enum MarkerStyle
        {
            Star,
            Heart
        }

        private const string HarmonyCategory = nameof(QuestFavoriteFeature);
        private const string SaveKey = "DuckovCoreUtilities/QuestFavorites";
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(QuestView), "activeEntries");

        private static QuestFavoriteFeature? _active;
        private readonly HashSet<int> _favoriteQuestIds = new HashSet<int>();

        public override string Name => "Quest favorites";

        public MarkerStyle Style { get; set; } = MarkerStyle.Star;
        public Color MarkerColor { get; set; } = new Color(1f, 0.78f, 0.12f, 1f);

        public void RefreshAppearanceAndSort()
        {
            RefreshExistingEntries();
        }

        protected override void OnEnable()
        {
            _active = this;
            Load();
            SavesSystem.OnSetFile += Load;
            SavesSystem.OnCollectSaveData += Save;
            Context.Harmony.PatchCategory(HarmonyCategory);
            RefreshExistingEntries();
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            SavesSystem.OnSetFile -= Load;
            SavesSystem.OnCollectSaveData -= Save;
            Save();
            _active = null;

            foreach (var marker in Resources.FindObjectsOfTypeAll<QuestFavoriteMarker>())
            {
                if (marker != null)
                {
                    Object.Destroy(marker);
                }
            }
        }

        private void Load()
        {
            _favoriteQuestIds.Clear();
            var saved = SavesSystem.Load<List<int>>(SaveKey);
            if (saved != null)
            {
                foreach (var questId in saved)
                {
                    _favoriteQuestIds.Add(questId);
                }
            }

            RefreshExistingEntries();
        }

        private void Save()
        {
            SavesSystem.Save(SaveKey, _favoriteQuestIds.OrderBy(static id => id).ToList());
        }

        private void ToggleFavorite(QuestEntry entry, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right || entry.Target == null)
            {
                return;
            }

            var questId = entry.Target.ID;
            if (!_favoriteQuestIds.Add(questId))
            {
                _favoriteQuestIds.Remove(questId);
            }

            eventData.Use();
            Save();
            RefreshExistingEntries();
        }

        private void EnsureMarker(QuestEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var marker = entry.GetComponent<QuestFavoriteMarker>() ?? entry.gameObject.AddComponent<QuestFavoriteMarker>();
            marker.Configure(this, entry);
            marker.SetAppearance(
                entry.Target != null && _favoriteQuestIds.Contains(entry.Target.ID),
                Style == MarkerStyle.Star ? "★" : "♥",
                MarkerColor);
        }

        private void RefreshExistingEntries()
        {
            foreach (var entry in Resources.FindObjectsOfTypeAll<QuestEntry>())
            {
                if (entry != null && entry.gameObject.scene.IsValid())
                {
                    EnsureMarker(entry);
                }
            }

            var view = QuestView.Instance;
            if (view != null)
            {
                SortFavoritesFirst(view);
            }
        }

        private void SortFavoritesFirst(QuestView view)
        {
            if (!(ActiveEntriesField.GetValue(view) is List<QuestEntry> entries) || entries.Count < 2)
            {
                return;
            }

            var validEntries = entries.Where(static entry => entry != null && entry.Target != null).ToList();
            var siblingIndexes = validEntries.Select(static entry => entry.transform.GetSiblingIndex()).OrderBy(static index => index).ToArray();
            var ordered = validEntries
                .Select((entry, index) => new { Entry = entry, Index = index })
                .OrderByDescending(pair => _favoriteQuestIds.Contains(pair.Entry.Target.ID))
                .ThenBy(static pair => pair.Index)
                .Select(static pair => pair.Entry)
                .ToArray();

            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index].transform.SetSiblingIndex(siblingIndexes[index]);
            }
        }

        private sealed class QuestFavoriteMarker : MonoBehaviour
        {
            private QuestFavoriteFeature? _feature;
            private QuestEntry? _entry;
            private TextMeshProUGUI? _text;

            public void Configure(QuestFavoriteFeature feature, QuestEntry entry)
            {
                if (_entry != null)
                {
                    _entry.onClick -= OnEntryClicked;
                }

                _feature = feature;
                _entry = entry;
                _entry.onClick -= OnEntryClicked;
                _entry.onClick += OnEntryClicked;
                EnsureText();
            }

            public void SetAppearance(bool visible, string symbol, Color color)
            {
                EnsureText();
                if (_text == null)
                {
                    return;
                }

                _text.gameObject.SetActive(visible);
                _text.text = symbol;
                _text.color = color;
            }

            private void EnsureText()
            {
                if (_text != null)
                {
                    return;
                }

                var gameObject = new GameObject("DCU_FavoriteMarker", typeof(RectTransform), typeof(TextMeshProUGUI));
                gameObject.transform.SetParent(transform, false);
                var rect = (RectTransform)gameObject.transform;
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-8f, -5f);
                rect.sizeDelta = new Vector2(32f, 32f);
                rect.SetAsLastSibling();

                _text = gameObject.GetComponent<TextMeshProUGUI>();
                _text.fontSize = 25f;
                _text.alignment = TextAlignmentOptions.Center;
                _text.raycastTarget = false;
            }

            private void OnEntryClicked(QuestEntry entry, PointerEventData eventData)
            {
                _feature?.ToggleFavorite(entry, eventData);
            }

            private void OnDestroy()
            {
                if (_entry != null)
                {
                    _entry.onClick -= OnEntryClicked;
                }
                if (_text != null)
                {
                    Object.Destroy(_text.gameObject);
                    _text = null;
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(QuestEntry), "Setup")]
        private static class QuestEntrySetupPatch
        {
            private static void Postfix(QuestEntry __instance)
            {
                _active?.EnsureMarker(__instance);
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(QuestView), "RefreshEntryList")]
        private static class QuestViewRefreshPatch
        {
            private static void Postfix(QuestView __instance)
            {
                _active?.SortFavoritesFirst(__instance);
            }
        }
    }
}

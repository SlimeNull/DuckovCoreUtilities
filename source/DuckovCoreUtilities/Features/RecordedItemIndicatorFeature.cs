using Duckov.MasterKeys;
using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class RecordedItemIndicatorFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(RecordedItemIndicatorFeature);
        private const string IndicatorName = "DCU_RecordedItemIndicator";

        private static RecordedItemIndicatorFeature? _active;

        public override string Name => "Recorded key and blueprint indicator";

        public Color BackgroundColor { get; set; } = new Color(0.2f, 0.8f, 0.2f, 1f);
        public Color TextColor { get; set; } = Color.white;

        public void RefreshExistingIndicators()
        {
            foreach (var display in Resources.FindObjectsOfTypeAll<ItemDisplay>())
            {
                if (display != null && display.gameObject.scene.IsValid())
                {
                    Refresh(display);
                }
            }
        }

        protected override void OnEnable()
        {
            _active = this;
            MasterKeysManager.OnMasterKeyUnlocked += OnRecordedStateChanged;
            CraftingManager.OnFormulaUnlocked += OnRecordedStateChanged;
            Context.Harmony.PatchCategory(HarmonyCategory);
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            MasterKeysManager.OnMasterKeyUnlocked -= OnRecordedStateChanged;
            CraftingManager.OnFormulaUnlocked -= OnRecordedStateChanged;
            _active = null;

            foreach (var marker in Resources.FindObjectsOfTypeAll<RecordedItemMarker>())
            {
                if (marker != null)
                {
                    Object.Destroy(marker.gameObject);
                }
            }
        }

        private void OnRecordedStateChanged(int _)
        {
            RefreshExistingIndicators();
        }

        private void OnRecordedStateChanged(string _)
        {
            RefreshExistingIndicators();
        }

        private void Refresh(ItemDisplay display)
        {
            if (display == null)
            {
                return;
            }

            var item = display.Target;
            var visible = item != null &&
                !item.NeedInspection &&
                ((IsKey(item) && MasterKeysManager.IsActive(item.TypeID)) ||
                 (IsBlueprint(item) && IsBlueprintRecorded(item)));

            var marker = display.GetComponentInChildren<RecordedItemMarker>(true);
            if (!visible)
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
                return;
            }

            marker ??= CreateMarker(display);
            marker.gameObject.SetActive(true);
            marker.SetColors(BackgroundColor, TextColor);
        }

        private static bool IsKey(Item item)
        {
            return item.Tags != null && item.Tags.Contains("Key");
        }

        private static bool IsBlueprint(Item item)
        {
            return item.Tags != null && item.Tags.Contains("Formula_Blueprint");
        }

        private static bool IsBlueprintRecorded(Item item)
        {
            var formulaId = FormulasRegisterView.GetFormulaID(item);
            return !string.IsNullOrEmpty(formulaId) && CraftingManager.UnlockedFormulaIDs.Contains(formulaId);
        }

        private static RecordedItemMarker CreateMarker(ItemDisplay display)
        {
            var root = new GameObject(IndicatorName, typeof(RectTransform), typeof(RecordedItemMarker));
            root.transform.SetParent(display.transform, false);

            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-5f, -5f);
            rect.sizeDelta = new Vector2(28f, 28f);
            rect.SetAsLastSibling();

            var background = CreateText(root.transform, "Background", "●", 32f);
            var text = CreateText(root.transform, "Text", "✓", 20f);

            var marker = root.GetComponent<RecordedItemMarker>();
            marker.Initialize(background, text);
            return marker;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float fontSize)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);

            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private sealed class RecordedItemMarker : MonoBehaviour
        {
            private TextMeshProUGUI? _background;
            private TextMeshProUGUI? _text;

            public void Initialize(TextMeshProUGUI background, TextMeshProUGUI text)
            {
                _background = background;
                _text = text;
            }

            public void SetColors(Color background, Color text)
            {
                if (_background != null)
                {
                    _background.color = background;
                }
                if (_text != null)
                {
                    _text.color = text;
                }
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(ItemDisplay), "Setup")]
        private static class ItemDisplaySetupPatch
        {
            private static void Postfix(ItemDisplay __instance)
            {
                _active?.Refresh(__instance);
            }
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(ItemDisplay), "Refresh")]
        private static class ItemDisplayRefreshPatch
        {
            private static void Postfix(ItemDisplay __instance)
            {
                _active?.Refresh(__instance);
            }
        }
    }
}

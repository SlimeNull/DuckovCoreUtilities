using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class KillRecordFeature : FeatureBase
    {
        private const string HudCanvasName = "HUDCanvas";
        private const string IndicatorsName = "SimpleIndicators";
        private const string ToggleName = "SimpleIndicatorEntry_Toggle";
        private const string ToggleTitleName = "MapTitle";
        private const string RecordPanelName = "KillRecord";
        private const string RecordTextName = "Text (TMP)";
        private const string BackgroundSpriteName = "procedural_ui_image_default_sprite";
        private const string GameFontName = "ResourceHanRoundedCN-Medium SDF";
        private const float DefaultPanelWidth = 282f;
        private const float HorizontalPadding = 12f;
        private const float VerticalPadding = 8f;
        private const float AttachRetryInterval = 1f;

        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.772549f);

        private readonly List<KillRecord> _records = new List<KillRecord>();

        private GameObject? _panel;
        private GameObject? _toggleParent;
        private TextMeshProUGUI? _recordText;
        private LayoutElement? _panelLayout;
        private float _nextAttachAttempt;

        public override string Name => "Kill record";

        public float RecordDuration { get; set; } = 5f;
        public int MaxRecordCount { get; set; } = 5;
        public string RecordFormat { get; set; } = SettingsText.KillRecordDefaultFormat;

        protected override void OnEnable()
        {
            Health.OnDead += OnHealthDead;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            TryAttachToIndicators();
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            Health.OnDead -= OnHealthDead;
            DestroyPanel();
        }

        public override void Tick()
        {
            if (_panel == null || _toggleParent == null || _recordText == null || _panelLayout == null)
            {
                ClearDestroyedPanelReferences();
                if (Time.unscaledTime >= _nextAttachAttempt)
                {
                    TryAttachToIndicators();
                }
            }

            if (PruneExpiredRecords())
            {
                RefreshPanel();
            }

            UpdateVisibility();
        }

        private void OnAfterLevelInitialized()
        {
            DestroyPanel();
            TryAttachToIndicators();
        }

        private void OnHealthDead(Health health, DamageInfo damageInfo)
        {
            if (!IsPlayerKill(health, damageInfo))
            {
                return;
            }

            var victimName = GetVictimName(health);
            _records.Add(new KillRecord(FormatRecord(victimName), Time.time + Mathf.Max(0.01f, RecordDuration)));

            var maxRecordCount = Mathf.Max(1, MaxRecordCount);
            if (_records.Count > maxRecordCount)
            {
                _records.RemoveRange(0, _records.Count - maxRecordCount);
            }

            TryAttachToIndicators();
            RefreshPanel();
            UpdateVisibility();
        }

        private void TryAttachToIndicators()
        {
            if (_panel != null)
            {
                return;
            }

            _nextAttachAttempt = Time.unscaledTime + AttachRetryInterval;

            var hudCanvas = GameObject.Find(HudCanvasName);
            var indicatorsTransform = hudCanvas != null ? hudCanvas.transform.Find(IndicatorsName) : null;
            var indicatorHud = indicatorsTransform != null ? indicatorsTransform.GetComponent<IndicatorHUD>() : null;
            var toggleParent = indicatorHud != null ? indicatorHud.toggleParent : null;
            var toggle = indicatorsTransform != null ? indicatorsTransform.Find(ToggleName)?.gameObject : null;
            if (indicatorsTransform == null || toggleParent == null || toggle == null)
            {
                return;
            }

            var panel = new GameObject(
                RecordPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(LayoutElement));
            panel.transform.SetParent(indicatorsTransform, false);
            panel.transform.SetSiblingIndex(toggleParent.transform.GetSiblingIndex());

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.localScale = Vector3.one;

            var modifier = panel.AddComponent<UniformModifier>();
            var background = panel.AddComponent<ProceduralImage>();
            modifier.Radius = 8f;
            background.color = BackgroundColor;
            background.raycastTarget = false;
            background.sprite = FindBackgroundSprite(toggle);

            var toggleParentRect = toggleParent.GetComponent<RectTransform>();
            var panelWidth = toggleParentRect != null && toggleParentRect.rect.width > 0f
                ? toggleParentRect.rect.width
                : DefaultPanelWidth;
            _panelLayout = panel.GetComponent<LayoutElement>();
            _panelLayout.preferredWidth = panelWidth;
            _panelLayout.flexibleWidth = 0f;
            _panelLayout.flexibleHeight = 0f;

            var textObject = new GameObject(RecordTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panel.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(HorizontalPadding, VerticalPadding);
            textRect.offsetMax = new Vector2(-HorizontalPadding, -VerticalPadding);

            var titleTemplate = toggle.transform.Find(ToggleTitleName)?.GetComponent<TextMeshProUGUI>();
            _recordText = textObject.GetComponent<TextMeshProUGUI>();
            _recordText.font = FindGameFont() ?? titleTemplate?.font ?? TMP_Settings.defaultFontAsset;
            _recordText.fontSize = titleTemplate != null ? titleTemplate.fontSize : 28f;
            _recordText.fontStyle = titleTemplate != null ? titleTemplate.fontStyle : FontStyles.Normal;
            _recordText.color = Color.white;
            _recordText.alignment = TextAlignmentOptions.TopLeft;
            _recordText.enableWordWrapping = false;
            _recordText.overflowMode = TextOverflowModes.Ellipsis;
            _recordText.raycastTarget = false;

            _panel = panel;
            _toggleParent = toggleParent;
            RefreshPanel();
            UpdateVisibility();
        }

        private void RefreshPanel()
        {
            if (_recordText == null || _panelLayout == null)
            {
                return;
            }

            if (_records.Count == 0)
            {
                _recordText.text = string.Empty;
                _panelLayout.preferredHeight = 0f;
                return;
            }

            var lines = new string[_records.Count];
            for (var i = 0; i < _records.Count; i++)
            {
                lines[i] = _records[i].Text;
            }

            _recordText.text = string.Join("\n", lines);
            var availableWidth = Mathf.Max(1f, _panelLayout.preferredWidth - HorizontalPadding * 2f);
            var preferredTextHeight = _recordText.GetPreferredValues(_recordText.text, availableWidth, 0f).y;
            _panelLayout.preferredHeight = Mathf.Ceil(preferredTextHeight) + VerticalPadding * 2f;
        }

        private void UpdateVisibility()
        {
            if (_panel == null || _toggleParent == null)
            {
                return;
            }

            var shouldShow = _records.Count > 0 && !_toggleParent.activeSelf;
            if (_panel.activeSelf != shouldShow)
            {
                _panel.SetActive(shouldShow);
            }
        }

        private bool PruneExpiredRecords()
        {
            var changed = false;
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _records[i].ExpiresAt)
                {
                    _records.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private string FormatRecord(string victimName)
        {
            try
            {
                return string.Format(RecordFormat, victimName);
            }
            catch (FormatException)
            {
                return victimName;
            }
        }

        private static bool IsPlayerKill(Health? health, DamageInfo damageInfo)
        {
            return health != null &&
                health.team != Teams.player &&
                damageInfo.fromCharacter != null &&
                damageInfo.fromCharacter.IsMainCharacter;
        }

        private static string GetVictimName(Health health)
        {
            var character = health.TryGetCharacter();
            var preset = character != null ? character.characterPreset : null;
            if (preset != null && !string.IsNullOrWhiteSpace(preset.DisplayName))
            {
                return preset.DisplayName;
            }

            if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
            {
                return preset.Name;
            }

            return health.team.ToString();
        }

        private static TMP_FontAsset? FindGameFont()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var font in fonts)
            {
                if (font != null && font.name == GameFontName)
                {
                    return font;
                }
            }

            return null;
        }

        private static Sprite? FindBackgroundSprite(GameObject toggle)
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var sprite in sprites)
            {
                if (sprite != null && sprite.name == BackgroundSpriteName)
                {
                    return sprite;
                }
            }

            return toggle.GetComponent<ProceduralImage>()?.sprite;
        }

        private void ClearDestroyedPanelReferences()
        {
            if (_panel != null && _toggleParent != null && _recordText != null && _panelLayout != null)
            {
                return;
            }

            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }

            _panel = null;
            _toggleParent = null;
            _recordText = null;
            _panelLayout = null;
        }

        private void DestroyPanel()
        {
            _records.Clear();
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }

            _panel = null;
            _toggleParent = null;
            _recordText = null;
            _panelLayout = null;
            _nextAttachAttempt = 0f;
        }

        private sealed class KillRecord
        {
            public KillRecord(string text, float expiresAt)
            {
                Text = text;
                ExpiresAt = expiresAt;
            }

            public string Text { get; }

            public float ExpiresAt { get; }
        }
    }
}

using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class KillRecordFeature : FeatureBase
    {
        private const string SourceOperationName = "Operation";
        private const string HudCanvasName = "HUDCanvas";
        private const string RecordPanelName = "KillRecord";
        private const string RecordLineName = "KillRecordLine";
        private const float LineHeight = 22f;
        private const float LineSpacing = 2f;

        private readonly Queue<GameObject> _records = new Queue<GameObject>();

        private GameObject? _panel;
        private RectTransform? _contentRoot;
        private LayoutElement? _layoutElement;
        private TMP_FontAsset? _textFont;
        private Material? _textFontMaterial;
        private float _textFontSize = 18f;
        private FontStyles _textFontStyle = FontStyles.Normal;
        private Color _textColor = Color.white;
        private bool _hasTextStyle;

        public override string Name => "Kill record";

        public float RecordDuration { get; set; } = 5f;
        public int MaxRecordCount { get; set; } = 5;
        public string RecordFormat { get; set; } = "击杀 {0}";

        protected override void OnEnable()
        {
            Health.OnDead += OnHealthDead;
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            TryCreatePanel();
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            Health.OnDead -= OnHealthDead;
            DestroyPanel();
        }

        private void OnAfterLevelInitialized()
        {
            DestroyPanel();
            TryCreatePanel();
        }

        private void OnHealthDead(Health health, DamageInfo damageInfo)
        {
            if (!IsPlayerKill(health, damageInfo))
            {
                return;
            }

            if (health == null)
            {
                return;
            }

            TryCreatePanel();
            if (_panel == null ||
                _contentRoot == null)
            {
                Debug.LogWarning("[KillRecordFeature] Skip record because panel/content was not created.");
                return;
            }

            TrimToCapacityBeforeAdd();

            var victimName = GetVictimName(health);
            var line = CreateRecordLine(string.Format(RecordFormat, victimName));
            _records.Enqueue(line);
            UpdatePanelHeight();
            _panel.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
            Object.Destroy(line, Mathf.Max(0.01f, RecordDuration));
        }

        public override void Tick()
        {
            if (_records.Count <= 0)
            {
                return;
            }

            var previousCount = _records.Count;
            PruneDestroyedRecords();
            if (_records.Count == previousCount)
            {
                return;
            }

            if (_panel != null &&
                _panel.activeSelf &&
                _records.Count <= 0)
            {
                _panel.SetActive(false);
            }

            if (_records.Count > 0)
            {
                UpdatePanelHeight();
            }
        }

        private static bool IsPlayerKill(Health? health, DamageInfo damageInfo)
        {
            if (health == null ||
                health.team == Teams.player ||
                damageInfo.fromCharacter == null ||
                !damageInfo.fromCharacter.IsMainCharacter)
            {
                return false;
            }

            return true;
        }

        private void TryCreatePanel()
        {
            if (_panel != null)
            {
                return;
            }

            var source = FindOperationPanel();
            var parent = FindVisibleHudParent(source);
            if (source == null ||
                parent == null)
            {
                Debug.LogWarning("[KillRecordFeature] Operation panel or visible HUD parent was not found.");
                return;
            }

            var cloned = Object.Instantiate(source, parent);
            cloned.name = RecordPanelName;
            cloned.SetActive(false);
            _panel = cloned;

            SetupPanelTransform(source.GetComponent<RectTransform>(), cloned.GetComponent<RectTransform>());
            SetupContentRoot(cloned);
        }

        private static GameObject? FindOperationPanel()
        {
            GameObject? fallback = null;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in objects)
            {
                if (obj.name == SourceOperationName &&
                    obj.GetComponent<RectTransform>() != null &&
                    obj.GetComponentInParent<Canvas>() != null)
                {
                    if (fallback == null)
                    {
                        fallback = obj;
                    }

                    if (obj.activeInHierarchy)
                    {
                        return obj;
                    }
                }
            }

            return fallback;
        }

        private static Transform? FindVisibleHudParent(GameObject? source)
        {
            var hudCanvas = GameObject.Find(HudCanvasName);
            if (hudCanvas != null &&
                hudCanvas.activeInHierarchy)
            {
                return hudCanvas.transform;
            }

            if (source != null)
            {
                var sourceCanvas = source.GetComponentInParent<Canvas>();
                if (sourceCanvas != null &&
                    sourceCanvas.gameObject.activeInHierarchy)
                {
                    return sourceCanvas.transform;
                }
            }

            return null;
        }

        private void SetupPanelTransform(RectTransform sourceRect, RectTransform recordRect)
        {
            recordRect.anchorMin = new Vector2(1f, 1f);
            recordRect.anchorMax = new Vector2(1f, 1f);
            recordRect.pivot = new Vector2(1f, 1f);
            recordRect.sizeDelta = new Vector2(Mathf.Max(240f, sourceRect.rect.width > 0f ? sourceRect.rect.width : 320f), 0f);
            recordRect.anchoredPosition = new Vector2(-24f, -180f);
        }

        private void SetupContentRoot(GameObject panel)
        {
            _contentRoot = panel.GetComponent<RectTransform>();
            CaptureTextStyle(panel.GetComponentInChildren<TextMeshProUGUI>(true));

            for (var i = panel.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(panel.transform.GetChild(i).gameObject);
            }

            var layout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = LineSpacing;
            layout.padding = new RectOffset(10, 10, 8, 8);

            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                Object.Destroy(fitter);
            }

            _layoutElement = panel.GetComponent<LayoutElement>() ?? panel.AddComponent<LayoutElement>();
            _layoutElement.preferredHeight = 0f;
            _layoutElement.flexibleHeight = 0f;
        }

        private GameObject CreateRecordLine(string text)
        {
            var line = new GameObject(RecordLineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            line.transform.SetParent(_contentRoot, false);

            var rectTransform = line.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(0f, LineHeight);

            var layoutElement = line.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = LineHeight;
            layoutElement.flexibleHeight = 0f;

            var textComponent = line.GetComponent<TextMeshProUGUI>();
            CopyTextStyle(textComponent);
            textComponent.text = text;

            return line;
        }

        private void CaptureTextStyle(TextMeshProUGUI? template)
        {
            if (template == null)
            {
                return;
            }

            _textFont = template.font;
            _textFontMaterial = template.fontSharedMaterial;
            _textFontSize = template.fontSize;
            _textFontStyle = template.fontStyle;
            _textColor = template.color;
            _hasTextStyle = true;
        }

        private void CopyTextStyle(TextMeshProUGUI target)
        {
            if (_hasTextStyle)
            {
                target.font = _textFont;
                target.fontSharedMaterial = _textFontMaterial;
                target.fontSize = _textFontSize;
                target.fontStyle = _textFontStyle;
                target.color = _textColor;
            }
            else
            {
                target.fontSize = 18f;
                target.color = Color.white;
            }

            target.alignment = TextAlignmentOptions.Left;
            target.enableWordWrapping = false;
            target.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void TrimToCapacityBeforeAdd()
        {
            PruneDestroyedRecords();

            var maxRecordCount = Mathf.Max(1, MaxRecordCount);
            while (_records.Count >= maxRecordCount)
            {
                var oldest = _records.Dequeue();
                if (oldest != null)
                {
                    Object.Destroy(oldest);
                }
            }

            UpdatePanelHeight();
        }

        private void PruneDestroyedRecords()
        {
            while (_records.Count > 0 &&
                _records.Peek() == null)
            {
                _records.Dequeue();
            }
        }

        private void UpdatePanelHeight()
        {
            if (_layoutElement == null)
            {
                return;
            }

            PruneDestroyedRecords();

            if (_records.Count <= 0)
            {
                _layoutElement.preferredHeight = 0f;
                return;
            }

            _layoutElement.preferredHeight = 16f + _records.Count * LineHeight + (_records.Count - 1) * LineSpacing;
        }

        private static string GetVictimName(Health health)
        {
            var character = health.TryGetCharacter();
            var preset = character != null ? character.characterPreset : null;
            if (preset != null &&
                !string.IsNullOrWhiteSpace(preset.DisplayName))
            {
                return preset.DisplayName;
            }

            if (preset != null &&
                !string.IsNullOrWhiteSpace(preset.Name))
            {
                return preset.Name;
            }

            return health.team.ToString();
        }

        private void DestroyPanel()
        {
            _records.Clear();
            _contentRoot = null;
            _layoutElement = null;
            _textFont = null;
            _textFontMaterial = null;
            _hasTextStyle = false;

            if (_panel != null)
            {
                Object.Destroy(_panel);
                _panel = null;
            }
        }
    }
}

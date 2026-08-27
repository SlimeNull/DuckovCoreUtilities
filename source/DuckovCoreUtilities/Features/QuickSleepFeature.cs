using Cysharp.Threading.Tasks;
using Duckov.UI;
using Duckov.UI.DialogueBubbles;
using Duckov.Weathers;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using SlimeNull.DuckovCoreUtilities.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class QuickSleepFeature : FeatureBase
    {
        private const string HarmonyCategory = nameof(QuickSleepFeature);
        private const string ContainerName = "DCU_QuickSleepButtons";
        private const string EscHintShownKey = "DuckovCoreUtilities_QuickSleep_EscHintShown";
        private const int MaximumRainSearchDays = 100;

        private static readonly MethodInfo? SleepMethod =
            AccessTools.Method(typeof(SleepView), "Sleep", new[] { typeof(float) });
        private static readonly FieldInfo? SleepingField = AccessTools.Field(typeof(SleepView), "sleeping");
        private static readonly FieldInfo? SliderField = AccessTools.Field(typeof(SleepView), "slider");

        private readonly List<QuickSleepButtons> _buttonGroups = new List<QuickSleepButtons>();
        private QuickSleepRuntime? _runtime;
        private int _firstHour = 6;
        private int _firstMinute;
        private int _secondHour = 22;
        private int _secondMinute;

        private static QuickSleepFeature? Current { get; set; }

        public override string Name => "Quick sleep";

        public int FirstHour
        {
            get => _firstHour;
            set
            {
                _firstHour = Mathf.Clamp(value, 0, 23);
                RefreshButtons();
            }
        }

        public int FirstMinute
        {
            get => _firstMinute;
            set
            {
                _firstMinute = Mathf.Clamp(value, 0, 59);
                RefreshButtons();
            }
        }

        public int SecondHour
        {
            get => _secondHour;
            set
            {
                _secondHour = Mathf.Clamp(value, 0, 23);
                RefreshButtons();
            }
        }

        public int SecondMinute
        {
            get => _secondMinute;
            set
            {
                _secondMinute = Mathf.Clamp(value, 0, 59);
                RefreshButtons();
            }
        }

        protected override void OnEnable()
        {
            Current = this;
            _runtime = Context.HostObject.GetComponent<QuickSleepRuntime>()
                ?? Context.HostObject.AddComponent<QuickSleepRuntime>();
            _runtime.Initialize(this);
            Context.Harmony.PatchCategory(HarmonyCategory);

            var view = SleepView.Instance;
            if (view != null && view.gameObject.activeInHierarchy)
            {
                TryAttach(view);
            }
        }

        protected override void OnDisable()
        {
            Context.Harmony.UnpatchCategory(HarmonyCategory);
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            _runtime?.ClearOwner(this);
            _runtime = null;
            foreach (var group in _buttonGroups)
            {
                if (group != null)
                {
                    UnityEngine.Object.Destroy(group.gameObject);
                }
            }
            _buttonGroups.Clear();
        }

        public override void RefreshLocalization()
        {
            RefreshButtons();
        }

        private void TryAttach(SleepView view)
        {
            try
            {
                Attach(view);
            }
            catch (Exception ex)
            {
                var partialContainer = view != null
                    ? view.transform.Find("Panel/" + ContainerName)?.GetComponent<QuickSleepButtons>()
                    : null;
                if (partialContainer != null)
                {
                    _buttonGroups.Remove(partialContainer);
                    UnityEngine.Object.Destroy(partialContainer.gameObject);
                }

                Debug.LogError($"[DuckovCoreUtilities] Could not attach quick-sleep buttons: {ex}");
            }
        }

        private void Attach(SleepView view)
        {
            if (view == null)
            {
                return;
            }

            var panel = view.transform.Find("Panel");
            var confirmTransform = panel?.Find("ConfirmButton");
            var confirmButton = confirmTransform?.GetComponent<Button>();
            var confirmRect = confirmTransform as RectTransform;
            if (panel == null || confirmButton == null || confirmRect == null)
            {
                Debug.LogWarning("[DuckovCoreUtilities] Could not attach quick-sleep buttons to SleepView.");
                return;
            }

            var existing = panel.Find(ContainerName)?.GetComponent<QuickSleepButtons>();
            if (existing != null)
            {
                existing.Initialize(this, view);
                if (!_buttonGroups.Contains(existing))
                {
                    _buttonGroups.Add(existing);
                }
                RefreshButtons(existing);
                return;
            }

            var containerObject = new GameObject(
                ContainerName,
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(GridLayoutGroup),
                typeof(QuickSleepButtons));
            containerObject.transform.SetParent(panel, false);
            containerObject.transform.SetSiblingIndex(confirmTransform!.GetSiblingIndex() + 1);

            var buttonWidth = Mathf.Max(160f, GetRectWidth(confirmRect) * 0.7f);
            var buttonHeight = Mathf.Max(64f, GetRectHeight(confirmRect) * 0.9f);
            var spacing = new Vector2(15f, 15f);
            var grid = containerObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(buttonWidth, buttonHeight);
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            var height = buttonHeight * 2f + spacing.y;
            var width = buttonWidth * 3f + spacing.x * 2f;
            var layout = containerObject.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
            layout.preferredWidth = width;

            var containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(width, height);

            var group = containerObject.GetComponent<QuickSleepButtons>();
            group.Initialize(this, view);
            for (var index = 0; index < 6; index++)
            {
                var buttonObject = UnityEngine.Object.Instantiate(confirmButton.gameObject, containerObject.transform);
                buttonObject.name = $"QuickSleepButton_{index}";
                RemoveButtonIcon(buttonObject.transform);

                var button = buttonObject.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                var capturedIndex = index;
                button.onClick.AddListener((UnityAction)(() => OnButtonClicked(group, capturedIndex)));
                group.Buttons.Add(button);
            }

            _buttonGroups.Add(group);
            RefreshButtons(group);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel);
        }

        private static float GetRectWidth(RectTransform rect)
        {
            return rect.rect.width > 1f ? rect.rect.width : Mathf.Abs(rect.sizeDelta.x);
        }

        private static float GetRectHeight(RectTransform rect)
        {
            return rect.rect.height > 1f ? rect.rect.height : Mathf.Abs(rect.sizeDelta.y);
        }

        private static void RemoveButtonIcon(Transform button)
        {
            var icon = button.Find("Content/Layout_Normal/Image");
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            var normalLayout = button.Find("Content/Layout_Normal");
            var layout = normalLayout?.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
            }

            var title = normalLayout?.Find("DifficultyTitle");
            var titleRect = title as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = Vector2.zero;
                titleRect.sizeDelta = new Vector2(150f, 42f);
            }

            var localizer = title?.GetComponent<SodaCraft.Localizations.TextLocalizor>();
            if (localizer != null)
            {
                localizer.enabled = false;
            }
        }

        private void RefreshButtons()
        {
            _buttonGroups.RemoveAll(static group => group == null);
            foreach (var group in _buttonGroups)
            {
                RefreshButtons(group);
            }
        }

        private void RefreshButtons(QuickSleepButtons group)
        {
            if (group == null)
            {
                return;
            }

            var labels = GetButtonLabels();
            for (var i = 0; i < group.Buttons.Count && i < labels.Length; i++)
            {
                var button = group.Buttons[i];
                if (button == null)
                {
                    continue;
                }

                SetButtonText(button.transform, labels[i]);
            }

            UpdateButtonInteractability(group);
        }

        private static void SetButtonText(Transform button, string value)
        {
            var text = button.Find("Content/Layout_Normal/DifficultyTitle")?.GetComponent<TextMeshProUGUI>()
                ?? button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (text == null)
            {
                return;
            }

            text.text = value;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = Vector4.zero;
        }

        private string[] GetButtonLabels()
        {
            var first = $"{FirstHour:00}:{FirstMinute:00}";
            var second = $"{SecondHour:00}:{SecondMinute:00}";
            return new[]
            {
                string.Format(SettingsText.Culture, SettingsText.QuickSleepToTime, first),
                string.Format(SettingsText.Culture, SettingsText.QuickSleepToTime, second),
                SettingsText.QuickSleepUntilRainy,
                SettingsText.QuickSleepToStormI,
                SettingsText.QuickSleepToStormII,
                SettingsText.QuickSleepStormEnd,
            };
        }

        private static void UpdateButtonInteractability(QuickSleepButtons group)
        {
            var weatherManager = WeatherManager.Instance;
            var storm = weatherManager?.Storm;
            var level = storm != null ? storm.GetStormLevel(GameClock.Now) : -1;
            SetInteractable(group, 0, true);
            SetInteractable(group, 1, true);
            SetInteractable(group, 2, weatherManager != null);
            SetInteractable(group, 3, level == 0);
            SetInteractable(group, 4, level >= 0 && level != 2);
            SetInteractable(group, 5, level >= 0);
        }

        private static void SetInteractable(QuickSleepButtons group, int index, bool value)
        {
            if (index >= 0 && index < group.Buttons.Count && group.Buttons[index] != null)
            {
                group.Buttons[index].interactable = value;
            }
        }

        private void OnButtonClicked(QuickSleepButtons group, int index)
        {
            if (group == null || group.View == null)
            {
                return;
            }

            var now = GameClock.Now;
            float minutes;
            switch (index)
            {
                case 0:
                    minutes = CalculateMinutesUntil(now, FirstHour, FirstMinute);
                    break;
                case 1:
                    minutes = CalculateMinutesUntil(now, SecondHour, SecondMinute);
                    break;
                case 2:
                    var wakeTime = GetSelectedWakeTime(group.View);
                    _runtime?.StartRainSearch(group.View, wakeTime.hour, wakeTime.minute);
                    return;
                case 3:
                    minutes = GetStormMinutes(static storm => storm.GetStormETA(GameClock.Now));
                    break;
                case 4:
                    minutes = GetStormMinutes(static storm => storm.GetStormIOverETA(GameClock.Now));
                    break;
                case 5:
                    minutes = GetStormMinutes(static storm => storm.GetStormIIOverETA(GameClock.Now));
                    break;
                default:
                    return;
            }

            if (minutes > 0f)
            {
                InvokeSleep(group.View, minutes);
            }
        }

        private static float GetStormMinutes(Func<Storm, TimeSpan> selector)
        {
            var storm = WeatherManager.Instance?.Storm;
            return storm == null ? 0f : Mathf.Max(0f, (float)selector(storm).TotalMinutes);
        }

        private (int hour, int minute) GetSelectedWakeTime(SleepView view)
        {
            try
            {
                if (SliderField?.GetValue(view) is Slider slider)
                {
                    var target = GameClock.Now.Add(TimeSpan.FromMinutes(slider.value));
                    return (target.Hours, target.Minutes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovCoreUtilities] Could not read SleepView slider: {ex.Message}");
            }

            return (FirstHour, FirstMinute);
        }

        private static float CalculateMinutesUntil(TimeSpan current, int targetHour, int targetMinute)
        {
            var currentMinutes = current.TotalMinutes % 1440d;
            var targetMinutes = targetHour * 60d + targetMinute;
            var difference = targetMinutes - currentMinutes;
            if (difference <= 0d)
            {
                difference += 1440d;
            }

            return (float)difference;
        }

        private static bool InvokeSleep(SleepView view, float minutes)
        {
            if (view == null || SleepMethod == null || minutes <= 0f)
            {
                return false;
            }

            try
            {
                SleepMethod.Invoke(view, new object[] { minutes });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DuckovCoreUtilities] Quick sleep failed: {ex}");
                return false;
            }
        }

        private static bool IsSleeping(SleepView view)
        {
            try
            {
                return view != null && SleepingField?.GetValue(view) is true;
            }
            catch
            {
                return false;
            }
        }

        private void ShowDialogue(string message)
        {
            var character = CharacterMainControl.Main;
            if (character != null)
            {
                DialogueBubblesManager.Show(message, character.transform, 2.5f, false, false, -1f, 3f).Forget();
            }
        }

        private static string GetMessage(MessageKind kind, int count = 0)
        {
            return kind switch
            {
                MessageKind.Hint => SettingsText.QuickSleepCancelHint,
                MessageKind.Cancelled => SettingsText.QuickSleepCancelled,
                MessageKind.Found => string.Format(SettingsText.Culture, SettingsText.QuickSleepFound, count),
                _ => string.Format(SettingsText.Culture, SettingsText.QuickSleepExhausted, count),
            };
        }

        private sealed class QuickSleepButtons : MonoBehaviour
        {
            public readonly List<Button> Buttons = new List<Button>(6);
            public QuickSleepFeature Owner = null!;
            public SleepView View = null!;

            public void Initialize(QuickSleepFeature owner, SleepView view)
            {
                Owner = owner;
                View = view;
            }
        }

        private sealed class QuickSleepRuntime : MonoBehaviour
        {
            private QuickSleepFeature? _owner;
            private Coroutine? _rainSearch;
            private bool _cancelRequested;

            public void Initialize(QuickSleepFeature owner)
            {
                _owner = owner;
                enabled = false;
            }

            public void ClearOwner(QuickSleepFeature owner)
            {
                if (!ReferenceEquals(_owner, owner))
                {
                    return;
                }

                if (_rainSearch != null)
                {
                    StopCoroutine(_rainSearch);
                    _rainSearch = null;
                }
                _owner = null;
                enabled = false;
            }

            public void StartRainSearch(SleepView view, int wakeHour, int wakeMinute)
            {
                if (_owner == null)
                {
                    return;
                }

                if (_rainSearch != null)
                {
                    StopCoroutine(_rainSearch);
                }
                enabled = true;
                _rainSearch = StartCoroutine(SearchForRain(view, wakeHour, wakeMinute));
            }

            private void Update()
            {
                if (_rainSearch != null && Input.GetKeyDown(KeyCode.Escape))
                {
                    _cancelRequested = true;
                }
            }

            private IEnumerator SearchForRain(SleepView view, int wakeHour, int wakeMinute)
            {
                _cancelRequested = false;
                var owner = _owner;
                if (owner == null || view == null)
                {
                    _rainSearch = null;
                    yield break;
                }

                if (PlayerPrefs.GetInt(EscHintShownKey, 0) != 1)
                {
                    owner.ShowDialogue(GetMessage(MessageKind.Hint));
                    PlayerPrefs.SetInt(EscHintShownKey, 1);
                    PlayerPrefs.Save();
                }

                for (var day = 0; day <= MaximumRainSearchDays; day++)
                {
                    if (_cancelRequested)
                    {
                        owner.ShowDialogue(GetMessage(MessageKind.Cancelled));
                        break;
                    }

                    if (WeatherManager.GetWeather(GameClock.Now) == Weather.Rainy)
                    {
                        owner.ShowDialogue(GetMessage(MessageKind.Found, day));
                        break;
                    }

                    if (day >= MaximumRainSearchDays)
                    {
                        owner.ShowDialogue(GetMessage(MessageKind.Exhausted, MaximumRainSearchDays));
                        break;
                    }

                    var minutes = CalculateMinutesUntil(GameClock.Now, wakeHour, wakeMinute);
                    if (!InvokeSleep(view!, minutes))
                    {
                        break;
                    }

                    yield return null;
                    var timeout = Time.realtimeSinceStartup + 10f;
                    while (view != null && IsSleeping(view) && Time.realtimeSinceStartup < timeout)
                    {
                        yield return null;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                }

                _rainSearch = null;
                enabled = false;
            }
        }

        private enum MessageKind
        {
            Hint,
            Cancelled,
            Found,
            Exhausted,
        }

        [HarmonyPatchCategory(HarmonyCategory)]
        [HarmonyPatch(typeof(SleepView), "OnEnable")]
        private static class SleepViewEnablePatch
        {
            private static void Postfix(SleepView __instance)
            {
                Current?.TryAttach(__instance);
            }
        }
    }
}

using Duckov.Options.UI;
using HarmonyLib;
using SlimeNull.DuckovModSettings.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SlimeNull.DuckovModSettings.UI
{
    internal sealed class SettingsPanelInjector : IDisposable
    {
        private static readonly FieldInfo? TabButtonsField = typeof(OptionsPanel).GetField("tabButtons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TabField = typeof(OptionsPanel_TabButton).GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, SettingsPage> PagesByPanel = new Dictionary<int, SettingsPage>();

        private readonly SettingsCatalog _catalog;
        private readonly Action _onPageClosing;
        private readonly List<Attachment> _attachments = new List<Attachment>();

        public SettingsPanelInjector(SettingsCatalog catalog, Action onPageClosing)
        {
            _catalog = catalog;
            _onPageClosing = onPageClosing;
        }

        public void Refresh()
        {
            for (var i = _attachments.Count - 1; i >= 0; i--)
            {
                if (_attachments[i].Panel == null)
                {
                    PagesByPanel.Remove(_attachments[i].PanelId);
                    _attachments.RemoveAt(i);
                }
            }

            foreach (var panel in Resources.FindObjectsOfTypeAll<OptionsPanel>())
            {
                if (panel == null || !panel.gameObject.scene.IsValid() || PagesByPanel.ContainsKey(panel.GetInstanceID()))
                {
                    continue;
                }

                TryAttach(panel);
            }
        }

        public void Dispose()
        {
            foreach (var attachment in _attachments)
            {
                PagesByPanel.Remove(attachment.PanelId);
                if (attachment.Button != null)
                {
                    UnityEngine.Object.Destroy(attachment.Button.gameObject);
                }
                if (attachment.Page != null)
                {
                    UnityEngine.Object.Destroy(attachment.Page.gameObject);
                }
            }
            _attachments.Clear();
        }

        internal static void NotifyPanelClosing(OptionsPanel panel)
        {
            if (panel != null && PagesByPanel.TryGetValue(panel.GetInstanceID(), out var page) && page != null && page.gameObject.activeSelf)
            {
                page.CommitPendingChanges();
            }
        }

        private void TryAttach(OptionsPanel panel)
        {
            if (TabButtonsField?.GetValue(panel) is not List<OptionsPanel_TabButton> buttons || TabField == null)
            {
                Debug.LogWarning("[DuckovModSettings] OptionsPanel fields could not be resolved.");
                return;
            }

            var templateButton = buttons.FirstOrDefault(button => button != null);
            if (templateButton == null || TabField.GetValue(templateButton) is not GameObject templateTab || templateTab == null)
            {
                return;
            }

            var buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, templateButton.transform.parent);
            buttonObject.name = "DuckovModSettings_TabButton";
            var button = buttonObject.GetComponent<OptionsPanel_TabButton>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                return;
            }

            RemoveLocalizers(buttonObject);
            var tabLabel = buttonObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (tabLabel != null)
            {
                tabLabel.text = "模组";
            }

            var pageObject = new GameObject("DuckovModSettings_Content", typeof(RectTransform));
            var pageRect = (RectTransform)pageObject.transform;
            pageRect.SetParent(templateTab.transform.parent, false);
            CopyRect(templateTab.GetComponent<RectTransform>(), pageRect);
            pageObject.SetActive(false);

            TabField.SetValue(button, pageObject);
            button.onClicked = (clicked, eventData) =>
            {
                eventData?.Use();
                panel.SetSelection(clicked);
            };
            buttons.Add(button);

            var page = pageObject.AddComponent<SettingsPage>();
            page.Initialize(_catalog, _onPageClosing, tabLabel?.font);
            PagesByPanel[panel.GetInstanceID()] = page;
            _attachments.Add(new Attachment(panel, button, page));

            var selection = panel.GetSelection();
            if (selection != null)
            {
                panel.SetSelection(selection);
            }
        }

        private static void RemoveLocalizers(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (behaviour != null && behaviour.GetType().Name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.Destroy(behaviour);
                }
            }
        }

        private static void CopyRect(RectTransform? source, RectTransform destination)
        {
            if (source == null)
            {
                destination.anchorMin = Vector2.zero;
                destination.anchorMax = Vector2.one;
                destination.offsetMin = Vector2.zero;
                destination.offsetMax = Vector2.zero;
                return;
            }

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localScale = source.localScale;
        }

        private sealed class Attachment
        {
            public Attachment(OptionsPanel panel, OptionsPanel_TabButton button, SettingsPage page)
            {
                Panel = panel;
                PanelId = panel.GetInstanceID();
                Button = button;
                Page = page;
            }

            public OptionsPanel Panel { get; }
            public int PanelId { get; }
            public OptionsPanel_TabButton Button { get; }
            public SettingsPage Page { get; }
        }
    }

    [HarmonyPatch(typeof(UIPanel), nameof(UIPanel.Close))]
    internal static class OptionsPanelClosePatch
    {
        private static void Prefix(UIPanel __instance)
        {
            if (__instance is OptionsPanel optionsPanel)
            {
                SettingsPanelInjector.NotifyPanelClosing(optionsPanel);
            }
        }
    }
}

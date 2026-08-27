using Duckov.Options.UI;
using HarmonyLib;
using SlimeNull.DuckovModSettings.Core;
using SlimeNull.DuckovModSettings.Localization;
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
        private const string TabButtonName = "DuckovModSettings_TabButton";
        private static readonly FieldInfo? TabButtonsField = typeof(OptionsPanel).GetField("tabButtons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TabField = typeof(OptionsPanel_TabButton).GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? SelectedIndicatorField = typeof(OptionsPanel_TabButton).GetField("selectedIndicator", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, Attachment> AttachmentsByPanel = new Dictionary<int, Attachment>();

        private readonly SettingsCatalog _catalog;
        private readonly Action<SettingsPage> _onPageOpening;
        private readonly Action<SettingsPage> _onPageClosing;
        private readonly List<Attachment> _attachments = new List<Attachment>();

        public SettingsPanelInjector(
            SettingsCatalog catalog,
            Action<SettingsPage> onPageOpening,
            Action<SettingsPage> onPageClosing)
        {
            _catalog = catalog;
            _onPageOpening = onPageOpening;
            _onPageClosing = onPageClosing;
        }

        public void AttachExistingPanels()
        {
            for (var i = _attachments.Count - 1; i >= 0; i--)
            {
                if (_attachments[i].Panel == null || _attachments[i].Button == null || _attachments[i].Page == null)
                {
                    AttachmentsByPanel.Remove(_attachments[i].PanelId);
                    _attachments.RemoveAt(i);
                }
            }

            foreach (var panel in Resources.FindObjectsOfTypeAll<OptionsPanel>())
            {
                if (panel == null || !panel.gameObject.scene.IsValid() || !panel.isActiveAndEnabled || panel.GetSelection() == null)
                {
                    continue;
                }

                TryAttach(panel);
            }
        }

        public void Attach(OptionsPanel panel)
        {
            if (panel != null && panel.gameObject.scene.IsValid() && panel.GetSelection() != null)
            {
                TryAttach(panel);
            }
        }

        public void Dispose()
        {
            foreach (var attachment in _attachments)
            {
                Detach(attachment);
            }
            _attachments.Clear();
        }

        public void RefreshLocalization()
        {
            foreach (var attachment in _attachments)
            {
                if (attachment.Button != null)
                {
                    var label = attachment.Button.GetComponentInChildren<TMP_Text>(includeInactive: true);
                    if (label != null)
                    {
                        label.text = SettingsText.Get("ModSettings");
                    }
                }
                if (attachment.Page != null)
                {
                    attachment.Page.RefreshLocalization();
                }
            }
        }

        internal static void NotifyPanelOpened(OptionsPanel panel)
        {
            if (TryGetAttachment(panel, out var attachment) && panel.GetSelection() == attachment.Button)
            {
                attachment.Page.NotifyMenuOpened();
            }
        }

        internal static void NotifySelectionChanged(OptionsPanel panel, OptionsPanel_TabButton selection)
        {
            if (!TryGetAttachment(panel, out var attachment))
            {
                return;
            }

            if (selection == attachment.Button)
            {
                attachment.Page.NotifyMenuOpened();
            }
            else
            {
                attachment.Page.NotifyMenuClosed();
            }
        }

        internal static void NotifyPanelClosing(OptionsPanel panel)
        {
            if (TryGetAttachment(panel, out var attachment))
            {
                attachment.Page.NotifyMenuClosed();
            }
        }

        private void TryAttach(OptionsPanel panel)
        {
            var panelId = panel.GetInstanceID();
            if (AttachmentsByPanel.TryGetValue(panelId, out var current))
            {
                if (current.Owner == this && current.Panel != null && current.Button != null && current.Page != null)
                {
                    return;
                }
                Detach(current);
            }

            if (TabButtonsField?.GetValue(panel) is not List<OptionsPanel_TabButton> buttons || TabField == null)
            {
                Debug.LogWarning("[DuckovModSettings] OptionsPanel fields could not be resolved.");
                return;
            }

            RemoveStaleButtons(buttons);

            var templateButton = buttons.FirstOrDefault(button => button != null);
            if (templateButton == null || TabField.GetValue(templateButton) is not GameObject templateTab || templateTab == null)
            {
                return;
            }

            GameObject? buttonObject = null;
            GameObject? pageObject = null;
            OptionsPanel_TabButton? button = null;
            try
            {
                buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, templateButton.transform.parent);
                buttonObject.name = TabButtonName;
                button = buttonObject.GetComponent<OptionsPanel_TabButton>();
                if (button == null || SelectedIndicatorField?.GetValue(button) is not GameObject selectedIndicator || selectedIndicator == null)
                {
                    throw new InvalidOperationException("OptionsPanel tab template is incomplete.");
                }
                selectedIndicator.SetActive(false);

                RemoveLocalizers(buttonObject);
                var tabLabel = buttonObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (tabLabel != null)
                {
                    tabLabel.text = SettingsText.Get("ModSettings");
                }

                pageObject = new GameObject("DuckovModSettings_Content", typeof(RectTransform));
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

                var page = pageObject.AddComponent<SettingsPage>();
                page.Initialize(
                    _catalog,
                    _onPageOpening,
                    _onPageClosing,
                    tabLabel?.font,
                    templateTab.GetComponent<RectTransform>());
                buttons.Add(button);
                var attachment = new Attachment(this, panel, button, page);
                AttachmentsByPanel[panelId] = attachment;
                _attachments.Add(attachment);
            }
            catch (Exception ex)
            {
                if (button != null)
                {
                    buttons.Remove(button);
                }
                if (pageObject != null)
                {
                    UnityEngine.Object.Destroy(pageObject);
                }
                if (buttonObject != null)
                {
                    UnityEngine.Object.Destroy(buttonObject);
                }
                Debug.LogError($"[DuckovModSettings] Could not create OptionsPanel tab: {ex}");
            }
        }

        private static bool TryGetAttachment(OptionsPanel panel, out Attachment attachment)
        {
            if (panel != null && AttachmentsByPanel.TryGetValue(panel.GetInstanceID(), out attachment!))
            {
                if (attachment.Panel != null && attachment.Button != null && attachment.Page != null)
                {
                    return true;
                }
                AttachmentsByPanel.Remove(panel.GetInstanceID());
            }

            attachment = null!;
            return false;
        }

        private static void RemoveStaleButtons(List<OptionsPanel_TabButton> buttons)
        {
            for (var i = buttons.Count - 1; i >= 0; i--)
            {
                var button = buttons[i];
                if (button != null && !string.Equals(button.gameObject.name, TabButtonName, StringComparison.Ordinal))
                {
                    continue;
                }

                buttons.RemoveAt(i);
                if (button != null)
                {
                    if (TabField?.GetValue(button) is GameObject stalePage && stalePage != null)
                    {
                        UnityEngine.Object.Destroy(stalePage);
                    }
                    UnityEngine.Object.Destroy(button.gameObject);
                }
            }
        }

        private static void Detach(Attachment attachment)
        {
            AttachmentsByPanel.Remove(attachment.PanelId);
            if (attachment.Page != null)
            {
                attachment.Page.NotifyMenuClosed();
            }

            if (attachment.Panel != null && TabButtonsField?.GetValue(attachment.Panel) is List<OptionsPanel_TabButton> buttons)
            {
                for (var i = buttons.Count - 1; i >= 0; i--)
                {
                    if (buttons[i] == null || buttons[i] == attachment.Button)
                    {
                        buttons.RemoveAt(i);
                    }
                }

                if (attachment.Panel.GetSelection() == attachment.Button)
                {
                    var replacement = buttons.FirstOrDefault(button => button != null);
                    if (replacement != null)
                    {
                        attachment.Panel.SetSelection(replacement);
                    }
                }
            }

            if (attachment.Button != null)
            {
                UnityEngine.Object.Destroy(attachment.Button.gameObject);
            }
            if (attachment.Page != null)
            {
                UnityEngine.Object.Destroy(attachment.Page.gameObject);
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
            public Attachment(SettingsPanelInjector owner, OptionsPanel panel, OptionsPanel_TabButton button, SettingsPage page)
            {
                Owner = owner;
                Panel = panel;
                PanelId = panel.GetInstanceID();
                Button = button;
                Page = page;
            }

            public SettingsPanelInjector Owner { get; }
            public OptionsPanel Panel { get; }
            public int PanelId { get; }
            public OptionsPanel_TabButton Button { get; }
            public SettingsPage Page { get; }
        }
    }

    [HarmonyPatch(typeof(OptionsPanel), "Start")]
    internal static class OptionsPanelStartPatch
    {
        private static void Postfix(OptionsPanel __instance)
        {
            ModBehaviour.Instance?.AttachOptionsPanel(__instance);
        }
    }

    [HarmonyPatch(typeof(OptionsPanel), "OnOpen")]
    internal static class OptionsPanelOpenPatch
    {
        private static void Postfix(OptionsPanel __instance)
        {
            ModBehaviour.Instance?.AttachOptionsPanel(__instance);
            SettingsPanelInjector.NotifyPanelOpened(__instance);
        }
    }

    [HarmonyPatch(typeof(OptionsPanel), nameof(OptionsPanel.SetSelection))]
    internal static class OptionsPanelSelectionPatch
    {
        private static void Postfix(OptionsPanel __instance, OptionsPanel_TabButton selection)
        {
            SettingsPanelInjector.NotifySelectionChanged(__instance, selection);
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

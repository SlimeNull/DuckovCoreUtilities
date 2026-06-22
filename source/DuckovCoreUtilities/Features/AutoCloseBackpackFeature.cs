using Duckov.UI;
using HarmonyLib;
using SlimeNull.DuckovCoreUtilities.Collections;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal class AutoCloseBackpackFeature : FeatureBase
    {
        private static WeakReference<AutoCloseBackpackFeature>? _instance;

        private const string PatchCatagory = nameof(AutoCloseBackpackFeature);

        private CharacterMainControl? _attachedCharacter;

        public override string Name => "Auto close backpack";

        public bool WhenMove { get; set; }
        public bool WhenHurt { get; set; }

        public AutoCloseBackpackFeature()
        {
            _instance = new WeakReference<AutoCloseBackpackFeature>(this);
        }

        protected override void OnEnable()
        {
            LevelManager.OnControllingCharacterChanged += OnControllingCharacterChanged;
            Context.Harmony.PatchCategory(PatchCatagory);

            var levelManager = LevelManager.Instance;
            if (levelManager != null &&
                levelManager.MainCharacter != null)
            {
                AttachToCharacter(levelManager.MainCharacter);
            }
        }

        protected override void OnDisable()
        {
            DetachFromCharacter();
            Context.Harmony.UnpatchCategory(PatchCatagory);
            LevelManager.OnControllingCharacterChanged -= OnControllingCharacterChanged;
        }

        private void OnControllingCharacterChanged(CharacterMainControl control)
        {
            AttachToCharacter(control);
        }

        private void AttachToCharacter(CharacterMainControl control)
        {
            DetachFromCharacter();
            if (control is null)
            {
                return;
            }

            control.Health.OnHurtEvent.AddListener(OnHurt);
            _attachedCharacter = control;
        }

        private void DetachFromCharacter()
        {
            if (_attachedCharacter is not null)
            {
                _attachedCharacter.Health.OnHurtEvent.RemoveListener(OnHurt);
            }
        }

        private void OnHurt(DamageInfo arg0)
        {
            if (WhenHurt)
            {
                CloseLootView();
            }
        }

        private static void CloseLootView()
        {
            View.ActiveView?.Close();
        }

        [HarmonyPatchCategory(PatchCatagory)]
        [HarmonyPatch(typeof(CharacterInputControl), nameof(CharacterInputControl.OnPlayerMoveInput))]
        private class CharacterInputControlPatch
        {
            private static void Prefix(InputAction.CallbackContext context)
            {
                if (context.performed &&
                    _instance is not null &&
                    _instance.TryGetTarget(out var target))
                {
                    if (target.WhenMove)
                    {
                        CloseLootView();
                    }
                }
            }
        }
    }
}

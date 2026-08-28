using Duckov;
using Duckov.Rules;
using HarmonyLib;
using ItemStatsSystem;
using Saves;
using SlimeNull.DuckovCustomDeath.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeNull.DuckovCustomDeath.Gameplay
{
    [HarmonyPatch(typeof(LevelManager), "CharacterDieTask")]
    internal static class CharacterDieTaskPatch
    {
        [HarmonyPrefix]
        private static void Prefix(LevelManager __instance)
        {
            DeathInventoryController.Prepare(__instance.MainCharacter);
        }

        [HarmonyPostfix]
        private static void Postfix(LevelManager __instance)
        {
            // Normally restored by the SaveMainCharacter prefix before the character is saved.
            DeathInventoryController.RestoreFor(__instance.MainCharacter);
        }
    }

    [HarmonyPatch(typeof(DeadBodyManager), "RecordDeath")]
    internal static class RecordDeathPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CharacterMainControl mainCharacter)
        {
            if (CustomDeathOptions.TombRetention == TombRetentionMode.DoNotKeep)
            {
                return true;
            }

            return !DeathInventoryController.ShouldSuppressDeathRecord(mainCharacter);
        }
    }

    [HarmonyPatch(typeof(DeadBodyManager), "AppendDeathInfo")]
    internal static class AppendDeathInfoPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(List<DeadBodyManager.DeathInfo> ___deaths)
        {
            if (CustomDeathOptions.TombRetention != TombRetentionMode.DoNotKeep)
            {
                return true;
            }

            ___deaths.Clear();
            SavesSystem.Save("DeathList", ___deaths);
            return false;
        }
    }

    [HarmonyPatch(typeof(InteractableLootbox), nameof(InteractableLootbox.CreateFromItem))]
    internal static class CreateDeathTombPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Item item,
            ref bool filterDontDropOnDead,
            ref InteractableLootbox? __result)
        {
            if (!DeathInventoryController.ShouldSuppressTomb(item))
            {
                if (DeathInventoryController.ShouldForceAllDrops(item))
                {
                    filterDontDropOnDead = false;
                }

                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(LevelManager), "SaveMainCharacter")]
    internal static class SaveMainCharacterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(LevelManager __instance)
        {
            DeathInventoryController.RestoreBeforeSave(__instance.MainCharacter);
        }
    }

    [HarmonyPatch(typeof(CharacterMainControl), nameof(CharacterMainControl.DestroyAllItem))]
    internal static class DestroyAllItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CharacterMainControl __instance)
        {
            DeathInventoryController.MarkDeathItemsProcessed(__instance);
        }
    }

    [HarmonyPatch(typeof(CharacterMainControl), nameof(CharacterMainControl.DropAllItems))]
    internal static class DropAllItemsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CharacterMainControl __instance)
        {
            var characterItem = __instance.CharacterItem;
            if (!DeathInventoryController.ShouldForceAllDrops(characterItem))
            {
                return true;
            }

            var items = new List<Item>();
            if (characterItem.Inventory != null)
            {
                foreach (var item in characterItem.Inventory)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            if (characterItem.Slots != null)
            {
                foreach (var slot in characterItem.Slots)
                {
                    if (slot?.Content != null)
                    {
                        items.Add(slot.Content);
                    }
                }
            }

            foreach (var item in items)
            {
                item.Drop(__instance.transform.position, createRigidbody: true, Vector3.forward, 360f);
            }

            return false;
        }

        [HarmonyPostfix]
        private static void Postfix(CharacterMainControl __instance)
        {
            DeathInventoryController.MarkDeathItemsProcessed(__instance);
        }
    }

    [HarmonyPatch(typeof(Ruleset), nameof(Ruleset.SaveDeadbodyCount), MethodType.Getter)]
    internal static class SaveDeadbodyCountPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            switch (CustomDeathOptions.TombRetention)
            {
                case TombRetentionMode.KeepTwo:
                    __result = 2;
                    break;
                case TombRetentionMode.KeepThree:
                    __result = 3;
                    break;
                case TombRetentionMode.KeepAll:
                    __result = int.MaxValue;
                    break;
            }
        }
    }
}

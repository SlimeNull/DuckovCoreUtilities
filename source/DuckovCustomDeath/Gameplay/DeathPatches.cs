using Duckov.Rules;
using HarmonyLib;
using SlimeNull.DuckovCustomDeath.Configuration;

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

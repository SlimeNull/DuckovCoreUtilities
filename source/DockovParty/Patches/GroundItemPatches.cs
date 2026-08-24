using HarmonyLib;
using ItemStatsSystem;
using SlimeNull.DockovParty.Game;
using UnityEngine;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(ItemExtensions), nameof(ItemExtensions.Drop), new[]
    {
        typeof(Item),
        typeof(Vector3),
        typeof(bool),
        typeof(Vector3),
        typeof(float),
    })]
    internal static class GroundDropPatch
    {
        private static void Prefix(Item item, out bool __state)
        {
            var main = CharacterMainControl.Main;
            var mainItem = main?.CharacterItem;
            __state = item != null && mainItem != null && item.GetRoot() == mainItem &&
                main != null && !main.Health.IsDead;
        }

        private static void Postfix(Item item, DuckovItemAgent __result, bool __state)
        {
            var ground = PartyRuntime.Instance?.GroundItems;
            if (ground == null || ground.ApplyingSpawn || __result == null)
            {
                return;
            }

            if (PartyRuntime.Instance?.IsHost == true)
            {
                ground.RegisterHostDrop(__result);
            }
            else if (__state)
            {
                ground.ReportClientDrop(item, __result);
            }
        }
    }

    [HarmonyPatch(typeof(CharacterMainControl), nameof(CharacterMainControl.PickupItem))]
    internal static class GroundPickupPatch
    {
        private static bool Prefix(CharacterMainControl __instance, Item item, ref bool __result)
        {
            var ground = PartyRuntime.Instance?.GroundItems;
            if (ground == null || !ground.TryRequestClientPickup(__instance, item, out var result))
            {
                return true;
            }

            __result = result;
            return false;
        }
    }
}

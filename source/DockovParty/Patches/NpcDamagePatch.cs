using HarmonyLib;
using SlimeNull.DockovParty.Game;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    [HarmonyPriority(Priority.High)]
    internal static class NpcDamagePatch
    {
        private static bool Prefix(DamageReceiver __instance, DamageInfo damageInfo, ref bool __result)
        {
            var replicator = PartyRuntime.Instance?.Npcs;
            if (replicator == null ||
                !replicator.TryForwardClientDamage(__instance, damageInfo, out var result))
            {
                return true;
            }

            __result = result;
            return false;
        }
    }
}

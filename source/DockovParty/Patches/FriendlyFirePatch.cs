using HarmonyLib;
using SlimeNull.DockovParty.Game;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(DamageReceiver), nameof(DamageReceiver.Hurt))]
    internal static class FriendlyFirePatch
    {
        private static bool Prefix(DamageReceiver __instance, DamageInfo damageInfo, ref bool __result)
        {
            var runtime = PartyRuntime.Instance;
            if (runtime == null || !runtime.Connected || damageInfo.fromCharacter == null ||
                damageInfo.fromCharacter.Team != Teams.player || __instance.Team != Teams.player)
            {
                return true;
            }

            var target = __instance.health?.TryGetCharacter();
            if (target == null ||
                (target != CharacterMainControl.Main && target.GetComponent<RemotePlayerReplica>() == null))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}

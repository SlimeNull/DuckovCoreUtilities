using Cysharp.Threading.Tasks;
using Duckov.UI;
using HarmonyLib;
using SlimeNull.DockovParty.Game;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(ClosureView), nameof(ClosureView.ShowAndReturnTask), new[]
    {
        typeof(DamageInfo),
        typeof(float),
    })]
    internal static class DeathClosurePatch
    {
        private static bool Prefix(DamageInfo dmgInfo, ref UniTask __result)
        {
            var spectator = PartyRuntime.Instance?.Spectator;
            if (spectator == null || !spectator.TryInterceptDeathClosure(dmgInfo, out var replacement))
            {
                return true;
            }

            __result = replacement;
            return false;
        }
    }
}

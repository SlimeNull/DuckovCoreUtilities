using Cysharp.Threading.Tasks;
using Duckov.Scenes;
using Eflatun.SceneReference;
using HarmonyLib;
using ItemStatsSystem;
using Saves;
using SlimeNull.DockovParty.Game;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadScene), new[]
    {
        typeof(SceneReference),
        typeof(SceneReference),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(MultiSceneLocation),
        typeof(bool),
        typeof(bool),
    })]
    internal static class CoordinatedSceneLoadPatch
    {
        private static bool Prefix(
            SceneLoader __instance,
            SceneReference sceneReference,
            SceneReference overrideCurtainScene,
            bool clickToConinue,
            bool notifyEvacuation,
            bool doCircleFade,
            bool useLocation,
            MultiSceneLocation location,
            bool saveToFile,
            bool hideTips,
            ref UniTask __result)
        {
            var coordinator = PartyRuntime.Instance?.Scenes;
            if (coordinator == null)
            {
                return true;
            }

            if (!coordinator.TryIntercept(
                    __instance,
                    sceneReference,
                    overrideCurtainScene,
                    clickToConinue,
                    notifyEvacuation,
                    doCircleFade,
                    useLocation,
                    location,
                    saveToFile,
                    hideTips,
                    out var replacement))
            {
                return true;
            }

            __result = replacement;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemSavesUtilities), nameof(ItemSavesUtilities.LoadItem), typeof(string))]
    internal static class ClientCharacterLoadPatch
    {
        private static bool Prefix(string key, ref UniTask<Item> __result)
        {
            var runtime = PartyRuntime.Instance;
            if (runtime == null || !runtime.ShouldLoadAssignedClientCharacter(key))
            {
                return true;
            }

            __result = runtime.InstantiateAssignedClientCharacterAsync();
            return false;
        }
    }

    [HarmonyPatch(typeof(SavesSystem), nameof(SavesSystem.SaveFile))]
    internal static class ClientSaveFilePatch
    {
        private static bool Prefix()
        {
            var runtime = PartyRuntime.Instance;
            return runtime == null || !runtime.SuppressClientSave;
        }
    }
}

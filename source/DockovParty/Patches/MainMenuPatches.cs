using Duckov.UI.MainMenu;
using HarmonyLib;
using SlimeNull.DockovParty.Game;

namespace SlimeNull.DockovParty.Patches
{
    [HarmonyPatch(typeof(ContinueButton), "OnButtonClicked")]
    internal static class ContinueButtonPatch
    {
        private static void Prefix()
        {
            PartyRuntime.Instance?.BeginHostFromContinue();
        }
    }
}

using HarmonyLib;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities.Infrastructure
{
    public sealed class FeatureContext
    {
        public FeatureContext(GameObject hostObject, Harmony harmony)
        {
            HostObject = hostObject;
            Harmony = harmony;
        }

        public GameObject HostObject { get; }
        public Harmony Harmony { get; }
    }
}

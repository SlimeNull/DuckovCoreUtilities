using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class NetworkContainer : MonoBehaviour
    {
        public string ContainerId { get; private set; } = string.Empty;

        public void Initialize(string containerId)
        {
            ContainerId = containerId;
        }
    }
}

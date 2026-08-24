using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class NetworkGroundItem : MonoBehaviour
    {
        public string GroundId { get; private set; } = string.Empty;
        public InteractablePickup? Pickup { get; private set; }

        public void Initialize(string groundId, InteractablePickup pickup)
        {
            GroundId = groundId;
            Pickup = pickup;
        }
    }
}

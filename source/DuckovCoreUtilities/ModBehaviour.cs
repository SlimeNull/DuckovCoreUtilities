using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using UnityEngine;

namespace SlimeNull.DuckovCoreUtilities
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private FeatureHost? _features;

        protected override void OnAfterSetup()
        {
            if (_features != null)
            {
                return;
            }

            Debug.Log("loading DCU");

            _features = new FeatureHost(gameObject);
            _features.Register(new DisplayPriceFeature());
            _features.Register(new DisplayStorageCount());
            _features.Register(new DisplayQualityFeature());
            _features.Register(new LootboxOutlineFeature());
            _features.Register(new InventorySortButtonsFeature());
            _features.Register(new AutoCloseBackpackFeature());
            _features.Register(new AutoFadeHudWhenAimingFeature());
            _features.Register(new BulletCountCrosshairColorFeature());
            _features.Register(new LowHealthInnerShadowFeature());
            _features.Register(new HierarchyInspectorMcpFeature());
            _features.EnableAll();

            Debug.Log("loaded DCU");
        }

        protected override void OnBeforeDeactivate()
        {
            Debug.Log("deactivating DCU");
            _features?.DisableAll();
            _features = null;
        }

        private void Update()
        {
            _features?.Tick();
        }

        private void OnGUI()
        {
            _features?.OnGUI();
        }
    }
}

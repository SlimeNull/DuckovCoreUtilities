using SlimeNull.DuckovCoreUtilities.Features;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System.Threading.Tasks;
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
            _features.Register(new DisplayPriceFeature());                     // 显示价值
            _features.Register(new DisplayStorageCount());                     // 显示仓库物品数量
            _features.Register(new DisplayQualityFeature());                   // 显示物品品质
            _features.Register(new LootboxOutlineFeature());                   // 显示战利品箱轮廓
            _features.Register(new InventorySortButtonsFeature());             // 显示仓库排序按钮
            _features.Register(new AutoCloseBackpackFeature());                // 自动关闭背包
            _features.Register(new AutoFadeHudWhenAimingFeature());            // 瞄准时 HUD 淡出
            _features.Register(new BulletCountCrosshairColorFeature());        // 弹药数量准心颜色
            _features.Register(new LowHealthInnerShadowFeature());             // 低血量内阴影
            _features.Register(new KillRecordFeature());                       // 击杀记录
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

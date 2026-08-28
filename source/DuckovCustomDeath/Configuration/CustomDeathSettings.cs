using System;
using UnityEngine;

namespace SlimeNull.DuckovCustomDeath.Configuration
{
    internal enum DeathDropMode
    {
        [InspectorName("@SettingsText/DropModeNormal")]
        Normal,

        [InspectorName("@SettingsText/DropModeLowQualityBackpack")]
        LowQualityBackpackOnly,

        [InspectorName("@SettingsText/DropModeBackpack")]
        BackpackOnly,

        [InspectorName("@SettingsText/DropModeNone")]
        None,
    }

    internal enum TombRetentionMode
    {
        [InspectorName("@SettingsText/TombRetentionNormal")]
        Normal,

        [InspectorName("@SettingsText/TombRetentionTwo")]
        KeepTwo,

        [InspectorName("@SettingsText/TombRetentionThree")]
        KeepThree,

        [InspectorName("@SettingsText/TombRetentionUnlimited")]
        KeepAll,
    }

    internal static class CustomDeathOptions
    {
        public static DeathDropMode DropMode { get; private set; } = DeathDropMode.Normal;

        public static TombRetentionMode TombRetention { get; private set; } = TombRetentionMode.Normal;

        public static void Apply(DeathDropMode dropMode, TombRetentionMode tombRetention)
        {
            DropMode = Enum.IsDefined(typeof(DeathDropMode), dropMode)
                ? dropMode
                : DeathDropMode.Normal;
            TombRetention = Enum.IsDefined(typeof(TombRetentionMode), tombRetention)
                ? tombRetention
                : TombRetentionMode.Normal;
        }

        public static void Reset()
        {
            Apply(DeathDropMode.Normal, TombRetentionMode.Normal);
        }
    }

    internal sealed class CustomDeathSettings : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("@SettingsText/DeathDropMode")]
        [Tooltip("@SettingsText/DeathDropModeTooltip")]
        private DeathDropMode deathDropMode = DeathDropMode.Normal;

        [SerializeField]
        [InspectorName("@SettingsText/TombRetentionMode")]
        [Tooltip("@SettingsText/TombRetentionModeTooltip")]
        private TombRetentionMode tombRetentionMode = TombRetentionMode.Normal;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void DuckovModSettingsUpdated()
        {
            Apply();
        }

        private void Apply()
        {
            CustomDeathOptions.Apply(deathDropMode, tombRetentionMode);
        }
    }
}

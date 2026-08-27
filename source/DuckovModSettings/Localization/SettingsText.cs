using SlimeNull.DuckovModSettings.Core;
using System.Globalization;
using System.Resources;

namespace SlimeNull.DuckovModSettings.Localization
{
    internal sealed class SettingsText
    {
        private static ResourceManager? _resourceManager;

        private SettingsText()
        {
        }

        public static ResourceManager ResourceManager =>
            _resourceManager ??= new ResourceManager(typeof(SettingsText));

        public static CultureInfo? Culture { get; set; }

        public static string Get(string key)
        {
            return LocalizedText.Resolve("@SettingsText/" + key, typeof(SettingsText).Assembly);
        }
    }
}

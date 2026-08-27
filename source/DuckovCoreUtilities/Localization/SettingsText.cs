using System.Globalization;
using System.Resources;

namespace SlimeNull.DuckovCoreUtilities.Localization
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
    }
}

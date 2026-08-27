using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using UnityEngine;

namespace SlimeNull.Mods.Localization
{
    internal static class ModLanguage
    {
        public static CultureInfo GetCulture(SystemLanguage language)
        {
            var cultureName = language switch
            {
                SystemLanguage.Afrikaans => "af",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Basque => "eu",
                SystemLanguage.Belarusian => "be",
                SystemLanguage.Bulgarian => "bg",
                SystemLanguage.Catalan => "ca",
                SystemLanguage.Chinese => "zh-Hans",
                SystemLanguage.ChineseSimplified => "zh-Hans",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Danish => "da",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.English => "en",
                SystemLanguage.Estonian => "et",
                SystemLanguage.Faroese => "fo",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hebrew => "he",
                SystemLanguage.Hindi => "hi",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Icelandic => "is",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Italian => "it",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Latvian => "lv",
                SystemLanguage.Lithuanian => "lt",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Russian => "ru",
                SystemLanguage.SerboCroatian => "sr-Latn",
                SystemLanguage.Slovak => "sk",
                SystemLanguage.Slovenian => "sl",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Thai => "th",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Vietnamese => "vi",
                _ => string.Empty,
            };

            if (cultureName.Length == 0)
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        public static void PrepareResourceManager(
            ResourceManager resourceManager,
            Assembly mainAssembly,
            CultureInfo culture)
        {
            LoadSatelliteAssembly(mainAssembly, culture);
            resourceManager.ReleaseAllResources();
        }

        private static void LoadSatelliteAssembly(Assembly mainAssembly, CultureInfo culture)
        {
            if (culture.Equals(CultureInfo.InvariantCulture))
            {
                return;
            }

            var mainAssemblyName = mainAssembly.GetName().Name;
            if (string.IsNullOrEmpty(mainAssemblyName))
            {
                return;
            }

            var satelliteAssemblyName = mainAssemblyName + ".resources";
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var loadedName = loadedAssembly.GetName();
                    if (string.Equals(loadedName.Name, satelliteAssemblyName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(loadedName.CultureName, culture.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                catch
                {
                }
            }

            string location;
            try
            {
                location = mainAssembly.Location;
            }
            catch
            {
                return;
            }

            var assemblyDirectory = Path.GetDirectoryName(location);
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                return;
            }

            var satellitePath = Path.Combine(
                assemblyDirectory,
                culture.Name,
                satelliteAssemblyName + ".dll");
            if (!File.Exists(satellitePath))
            {
                return;
            }

            try
            {
                Assembly.LoadFrom(satellitePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModLocalization] Could not load '{satellitePath}': {ex.Message}");
            }
        }
    }
}

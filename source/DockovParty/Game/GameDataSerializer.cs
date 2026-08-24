using ItemStatsSystem.Data;
using Newtonsoft.Json;

namespace SlimeNull.DockovParty.Game
{
    internal static class GameDataSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static string SerializeItem(ItemTreeData? data)
        {
            return data == null ? string.Empty : JsonConvert.SerializeObject(data, Settings);
        }

        public static ItemTreeData? DeserializeItem(string? json)
        {
            return string.IsNullOrWhiteSpace(json) ?
                null : JsonConvert.DeserializeObject<ItemTreeData>(json, Settings);
        }

        public static string SerializeInventory(InventoryData? data)
        {
            return data == null ? string.Empty : JsonConvert.SerializeObject(data, Settings);
        }

        public static InventoryData? DeserializeInventory(string? json)
        {
            return string.IsNullOrWhiteSpace(json) ?
                null : JsonConvert.DeserializeObject<InventoryData>(json, Settings);
        }
    }
}

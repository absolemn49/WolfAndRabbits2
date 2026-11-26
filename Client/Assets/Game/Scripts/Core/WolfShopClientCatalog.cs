using System;
using UnityEngine;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Core
{
    [Serializable]
    internal class WolfShopClientConfigJson
    {
        public int maxSlotItems = 6;
        public WolfShopClientEntryJson[] items = null;
    }

    [Serializable]
    internal class WolfShopClientEntryJson
    {
        public string id = "";
        public string nameKey = "";
        public string name = "";
        public string icon = "";
        public string itemType = "slot";
        public string statType = "attack";
        public float statValue = 0f;
        public int cost = 0;
        public string visualTag = "";
    }

    /// <summary>
    /// Loads client-side wolf_shop.json (Resources/Config/wolf_shop.json) and resolves item icons
    /// (custom PNG in Resources/Icons/<icon> if provided, otherwise procedural icon fallback).
    /// Server-sent catalog in MatchStartMsg takes authoritative precedence during live matches.
    /// </summary>
    public static class WolfShopClientCatalog
    {
        private static ShopItemDto[] _localItems;
        private static int _localMaxSlots = 6;
        private static bool _loaded;

        public static int LocalMaxSlots
        {
            get { EnsureLoaded(); return _localMaxSlots; }
        }

        public static ShopItemDto[] LocalItems
        {
            get { EnsureLoaded(); return _localItems; }
        }

        public static ShopItemDto[] ResolveCatalog(ShopItemDto[] serverItems, out int maxSlots, int serverMaxSlots)
        {
            EnsureLoaded();
            maxSlots = serverMaxSlots > 0 ? serverMaxSlots : _localMaxSlots;
            if (serverItems != null && serverItems.Length > 0)
                return serverItems;
            return _localItems;
        }

        public static ShopItemDto FindById(ShopItemDto[] catalog, string itemId)
        {
            if (catalog == null || string.IsNullOrEmpty(itemId)) return null;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i] != null && string.Equals(catalog[i].itemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return catalog[i];
            }
            return null;
        }

        public static Sprite GetItemIcon(string iconKey)
        {
            if (!string.IsNullOrWhiteSpace(iconKey))
            {
                // Allows designers to drop custom PNG icons into Resources/Icons/<iconKey>.png
                Sprite custom = Resources.Load<Sprite>("Icons/" + iconKey);
                if (custom != null) return custom;
            }
            return ProceduralTextureFactory.GetIcon(string.IsNullOrWhiteSpace(iconKey) ? "shop" : iconKey);
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _localItems = new ShopItemDto[0];

            TextAsset asset = Resources.Load<TextAsset>("Config/wolf_shop");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return;

            try
            {
                WolfShopClientConfigJson cfg = JsonUtility.FromJson<WolfShopClientConfigJson>(asset.text);
                if (cfg == null) return;
                _localMaxSlots = Mathf.Max(1, cfg.maxSlotItems);
                if (cfg.items != null)
                {
                    _localItems = new ShopItemDto[cfg.items.Length];
                    for (int i = 0; i < cfg.items.Length; i++)
                    {
                        WolfShopClientEntryJson src = cfg.items[i];
                        _localItems[i] = new ShopItemDto
                        {
                            itemId = src.id,
                            nameKey = src.nameKey,
                            name = src.name,
                            icon = src.icon,
                            itemType = src.itemType,
                            statType = src.statType,
                            statValue = src.statValue,
                            cost = src.cost,
                            visualTag = src.visualTag
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[WolfShopClientCatalog] Failed to parse Resources/Config/wolf_shop.json: " + ex.Message);
            }
        }
    }
}

using System.Text.Json;
using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public static class WolfItemTypes
{
    public const string Slot = "slot";
    public const string Fruit = "fruit";
}

public static class WolfStatTypes
{
    public const string Attack = "attack";
    public const string AttackSpeedPercent = "attack_speed_percent";
    public const string MoveSpeedPercent = "move_speed_percent";
    public const string Armor = "armor";
    public const string MaxHp = "max_hp";
}

public sealed class WolfShopConfigFile
{
    public int maxSlotItems { get; set; } = 6;
    public List<WolfShopItemSpec> items { get; set; } = new();
}

public sealed class WolfShopItemSpec
{
    public string id { get; set; } = string.Empty;
    public string nameKey { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string icon { get; set; } = string.Empty;
    public string itemType { get; set; } = WolfItemTypes.Slot;
    public string statType { get; set; } = WolfStatTypes.Attack;
    public float statValue { get; set; }
    public int cost { get; set; }
    public string visualTag { get; set; } = string.Empty;
}

public static class WolfShopCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly object Sync = new();
    private static WolfShopConfigFile? _cached;

    public static WolfShopConfigFile Current
    {
        get
        {
            lock (Sync)
            {
                _cached ??= LoadFromDisk();
                return _cached;
            }
        }
    }

    public static int MaxSlotItems => Math.Max(1, Current.maxSlotItems);

    public static void Reload()
    {
        lock (Sync)
        {
            _cached = LoadFromDisk();
        }
    }

    public static WolfShopItemSpec? GetItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        foreach (var item in Current.items)
        {
            if (string.Equals(item.id, itemId, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    public static List<ShopItemDto> ToDtos() =>
        Current.items.Select(i => new ShopItemDto
        {
            itemId = i.id,
            nameKey = i.nameKey,
            name = i.name,
            icon = i.icon,
            itemType = i.itemType,
            statType = i.statType,
            statValue = i.statValue,
            cost = i.cost,
            visualTag = i.visualTag
        }).ToList();

    private static WolfShopConfigFile LoadFromDisk()
    {
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "wolf_shop.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Server", "Config", "wolf_shop.json"),
            Path.Combine(AppContext.BaseDirectory, "Config", "wolf_shop.json")
        };

        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                string json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<WolfShopConfigFile>(json, JsonOptions);
                if (parsed != null && parsed.items.Count > 0)
                    return parsed;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WolfShopCatalog] Failed to parse {path}: {ex.Message}");
            }
        }

        // Built-in fallback matching wolf_shop.json defaults
        return new WolfShopConfigFile
        {
            maxSlotItems = 6,
            items = new List<WolfShopItemSpec>
            {
                new() { id = "iron_claws", nameKey = "shop.item.iron_claws", icon = "iron_claws", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.Attack, statValue = 15, cost = 70, visualTag = "weapon_1" },
                new() { id = "siege_hammer", nameKey = "shop.item.siege_hammer", icon = "siege_hammer", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.Attack, statValue = 35, cost = 150, visualTag = "weapon_2" },
                new() { id = "fury_gloves", nameKey = "shop.item.fury_gloves", icon = "fury_gloves", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.AttackSpeedPercent, statValue = 25, cost = 90 },
                new() { id = "swift_boots", nameKey = "shop.item.swift_boots", icon = "swift_boots", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.MoveSpeedPercent, statValue = 18, cost = 85, visualTag = "stamina_1" },
                new() { id = "wind_cloak", nameKey = "shop.item.wind_cloak", icon = "wind_cloak", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.MoveSpeedPercent, statValue = 30, cost = 160, visualTag = "stamina_2" },
                new() { id = "leather_armor", nameKey = "shop.item.leather_armor", icon = "leather_armor", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.Armor, statValue = 4, cost = 80, visualTag = "armor_1" },
                new() { id = "iron_armor", nameKey = "shop.item.iron_armor", icon = "iron_armor", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.Armor, statValue = 8, cost = 160, visualTag = "armor_2" },
                new() { id = "vitality_ring", nameKey = "shop.item.vitality_ring", icon = "vitality_ring", itemType = WolfItemTypes.Slot, statType = WolfStatTypes.MaxHp, statValue = 120, cost = 110 },
                new() { id = "fruit_attack", nameKey = "shop.item.fruit_attack", icon = "fruit_attack", itemType = WolfItemTypes.Fruit, statType = WolfStatTypes.Attack, statValue = 6, cost = 55 },
                new() { id = "fruit_attack_speed", nameKey = "shop.item.fruit_attack_speed", icon = "fruit_attack_speed", itemType = WolfItemTypes.Fruit, statType = WolfStatTypes.AttackSpeedPercent, statValue = 12, cost = 60 },
                new() { id = "fruit_move_speed", nameKey = "shop.item.fruit_move_speed", icon = "fruit_move_speed", itemType = WolfItemTypes.Fruit, statType = WolfStatTypes.MoveSpeedPercent, statValue = 8, cost = 60 },
                new() { id = "fruit_armor", nameKey = "shop.item.fruit_armor", icon = "fruit_armor", itemType = WolfItemTypes.Fruit, statType = WolfStatTypes.Armor, statValue = 2, cost = 65 },
                new() { id = "fruit_hp", nameKey = "shop.item.fruit_hp", icon = "fruit_hp", itemType = WolfItemTypes.Fruit, statType = WolfStatTypes.MaxHp, statValue = 45, cost = 50 }
            }
        };
    }
}

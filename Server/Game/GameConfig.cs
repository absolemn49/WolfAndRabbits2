using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public static class BuildingTypes
{
    public const string Gate = "gate";
    public const string GoldMine = "gold_mine";
    public const string Cannon = "cannon";
}

public static class Roles
{
    public const string Wolf = "Wolf";
    public const string Rabbit = "Rabbit";
}

public sealed record BuildingTierSpec(
    string BuildingType,
    string DisplayName,
    int Level,
    int Cost,
    int MaxHp,
    int Armor,
    float GoldPerSec,
    int Damage,
    float Range,
    float AttackInterval);

public static class GameConfig
{
    public const int MaxPlayersPerRoom = 4;
    public const int MinPlayersToStart = 2;

    public const int TickRateHz = 20;
    public const float TickDeltaSec = 1f / TickRateHz;
    public const int SnapshotEveryNTicks = 2; // 10 Hz

    public const float PrepDurationSec = 30f;
    public const float HuntDurationSec = 360f; // 6 minutes

    public const int MapWidth = 44;
    public const int MapHeight = 44;

    public const float PlayerRadius = 0.34f;
    public const float WolfBaseSpeed = 4.45f;
    public const float RabbitBaseSpeed = 4.10f;

    public const int WolfBaseMaxHp = 260;
    public const int RabbitBaseMaxHp = 100;

    public const int RabbitStartingGold = 200;
    public const int WolfStartingGold = 200;
    public const float RabbitPassiveGoldPerSec = 1f;
    public const float WolfPassiveGoldPerSec = 2f;

    public const int WolfBaseUnitDamage = 35;
    public const int WolfBaseBuildingDamage = 35;
    public const float WolfAttackRange = 1.65f;
    public const float WolfAttackCooldownSec = 0.85f;
    public const int WolfHpPerBonusGold = 8;

    public const float ShopInteractRadius = 3.5f;
    public const float VisionRadius = 5.5f;
    public const float CannonProjectileSpeed = 11.5f;

    public static readonly IReadOnlyList<BuildingTierSpec> BuildingTiers = new List<BuildingTierSpec>
    {
        // Gate (Cổng)
        new(BuildingTypes.Gate, "Cổng gỗ", 1, 50, 300, 0, 0f, 0, 0f, 0f),
        new(BuildingTypes.Gate, "Cổng sắt", 2, 90, 550, 5, 0f, 0, 0f, 0f),
        new(BuildingTypes.Gate, "Cổng thần thánh", 3, 150, 900, 10, 0f, 0, 0f, 0f),

        // Gold Mine (Nhà sản xuất tiền)
        new(BuildingTypes.GoldMine, "Mỏ vàng I", 1, 60, 180, 0, 2f, 0, 0f, 0f),
        new(BuildingTypes.GoldMine, "Mỏ vàng II", 2, 110, 300, 0, 4f, 0, 0f, 0f),
        new(BuildingTypes.GoldMine, "Kho báu III", 3, 180, 450, 0, 7f, 0, 0f, 0f),

        // Cannon (Pháo)
        new(BuildingTypes.Cannon, "Pháo I", 1, 90, 240, 0, 0f, 12, 4.0f, 1.0f),
        new(BuildingTypes.Cannon, "Pháo đôi II", 2, 130, 380, 0, 0f, 20, 4.5f, 1.0f),
        new(BuildingTypes.Cannon, "Pháo ma pháp III", 3, 210, 560, 0, 0f, 32, 5.0f, 0.87f),
    };

    public static BuildingTierSpec? GetBuildingSpec(string buildingType, int level)
    {
        foreach (var spec in BuildingTiers)
        {
            if (spec.BuildingType == buildingType && spec.Level == level)
                return spec;
        }
        return null;
    }

    public static List<BuildingTierDto> ToBuildingTierDtos() =>
        BuildingTiers.Select(b => new BuildingTierDto
        {
            buildingType = b.BuildingType,
            displayName = b.DisplayName,
            level = b.Level,
            cost = b.Cost,
            maxHp = b.MaxHp,
            armor = b.Armor,
            goldPerSec = b.GoldPerSec,
            damage = b.Damage,
            range = b.Range,
            attackInterval = b.AttackInterval
        }).ToList();
}

namespace WolfAndRabbits.Server.Game;

public enum MatchPhase
{
    Prep,
    Hunt,
    Ended
}

public sealed class PlayerEntity
{
    public string PlayerId { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Rabbit;
    public int ColorIndex { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public float InputDirX { get; set; }
    public float InputDirY { get; set; }
    public float FacingX { get; set; }
    public float FacingY { get; set; } = -1f;
    public bool Moving { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public bool Alive { get; set; } = true;
    public bool Spawned { get; set; } = true;

    public int Gold { get; set; }
    public float GoldFraction { get; set; }
    public int DamageGoldRemainder { get; set; }

    // Wolf 3D visual tiers (derived automatically from equipped items' visualTag / speed)
    public int WeaponTier { get; set; }
    public int ArmorTier { get; set; }
    public int StaminaTier { get; set; }

    // Wolf 5 stats bonuses (summed from slot equipment + permanent fruits)
    public int BonusAttack { get; set; }
    public float BonusAttackSpeedPercent { get; set; }
    public float BonusMoveSpeedPercent { get; set; }
    public int BonusArmor { get; set; }
    public int BonusMaxHp { get; set; }

    // Wolf inventory: limited slot items + count of permanent fruits eaten
    public List<string> SlotItemIds { get; } = new();
    public int FruitsEaten { get; set; }

    public float AttackCooldownRemaining { get; set; }
    public bool NearShop { get; set; }

    // Match statistics
    public int TotalGoldEarned { get; set; }
    public int BuildingsBuilt { get; set; }
    public int BuildingsDestroyed { get; set; }
    public int TotalDamageDealt { get; set; }

    public float GetMoveSpeed()
    {
        if (Role == Roles.Wolf)
        {
            float mult = 1f + MathF.Max(0f, BonusMoveSpeedPercent) / 100f;
            return GameConfig.WolfBaseSpeed * mult;
        }
        return GameConfig.RabbitBaseSpeed;
    }

    public int GetWolfUnitDamage() => GameConfig.WolfBaseUnitDamage + BonusAttack;

    public int GetWolfBuildingDamage() => GameConfig.WolfBaseBuildingDamage + BonusAttack;

    public float GetWolfAttackCooldown()
    {
        float speedMult = 1f + MathF.Max(0f, BonusAttackSpeedPercent) / 100f;
        return MathF.Max(0.25f, GameConfig.WolfAttackCooldownSec / speedMult);
    }

    public int GetWolfArmor() => BonusArmor;
}

public sealed class BuildingEntity
{
    public int BuildingId { get; init; }
    public string OwnerId { get; init; } = string.Empty;
    public string BuildingType { get; init; } = string.Empty;
    public int Level { get; set; } = 1;
    public int TileX { get; init; }
    public int TileY { get; init; }
    public int BaseId { get; init; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Armor { get; set; }
    public float FireCooldownRemaining { get; set; }
    public float AimAngle { get; set; }

    public float CenterX => TileX + 0.5f;
    public float CenterY => TileY + 0.5f;
}

public sealed class ProjectileEntity
{
    public int ProjectileId { get; init; }
    public int SourceBuildingId { get; init; }
    public string OwnerId { get; init; } = string.Empty;
    public string TargetPlayerId { get; init; } = string.Empty;
    public int Level { get; init; }
    public int Damage { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float Speed { get; init; }
}

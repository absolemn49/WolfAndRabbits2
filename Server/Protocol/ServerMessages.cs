namespace WolfAndRabbits.Server.Protocol;

public sealed class WelcomeMessage
{
    public string type { get; set; } = "welcome";
    public string clientId { get; set; } = string.Empty;
    public string serverVersion { get; set; } = "1.0.0";
}

public sealed class ErrorMessage
{
    public string type { get; set; } = "error";
    public string code { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string requestId { get; set; } = string.Empty;
}

public sealed class RoomSummaryDto
{
    public string roomCode { get; set; } = string.Empty;
    public string hostName { get; set; } = string.Empty;
    public int playerCount { get; set; }
    public int maxPlayers { get; set; }
    public bool hasPassword { get; set; }
}

public sealed class RoomListMessage
{
    public string type { get; set; } = "room_list";
    public List<RoomSummaryDto> rooms { get; set; } = new();
}

public sealed class RoomPlayerDto
{
    public string playerId { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public bool isHost { get; set; }
    public string role { get; set; } = "Rabbit";
    public int colorIndex { get; set; }
}

public sealed class RoomStateMessage
{
    public string type { get; set; } = "room_state";
    public string roomCode { get; set; } = string.Empty;
    public string hostId { get; set; } = string.Empty;
    public bool hasPassword { get; set; }
    public string status { get; set; } = "Waiting";
    public int maxPlayers { get; set; }
    public List<RoomPlayerDto> players { get; set; } = new();
}

public sealed class BaseSummaryDto
{
    public int baseId { get; set; }
    public string name { get; set; } = string.Empty;
    public int entranceX { get; set; }
    public int entranceY { get; set; }
    public int entranceDirX { get; set; }
    public int entranceDirY { get; set; }
    public float centerX { get; set; }
    public float centerY { get; set; }
}

public sealed class ShopItemDto
{
    public string itemId { get; set; } = string.Empty;
    public string nameKey { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string icon { get; set; } = string.Empty;
    public string itemType { get; set; } = "slot";
    public string statType { get; set; } = "attack";
    public float statValue { get; set; }
    public int cost { get; set; }
    public string visualTag { get; set; } = string.Empty;
}

public sealed class BuildingTierDto
{
    public string buildingType { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public int level { get; set; }
    public int cost { get; set; }
    public int maxHp { get; set; }
    public int armor { get; set; }
    public float goldPerSec { get; set; }
    public int damage { get; set; }
    public float range { get; set; }
    public float attackInterval { get; set; }
}

public sealed class MatchStartMessage
{
    public string type { get; set; } = "match_start";
    public string roomCode { get; set; } = string.Empty;
    public string yourPlayerId { get; set; } = string.Empty;
    public string yourRole { get; set; } = string.Empty;
    public int mapWidth { get; set; }
    public int mapHeight { get; set; }
    public int shopX { get; set; }
    public int shopY { get; set; }
    public float shopInteractRadius { get; set; }
    public float visionRadius { get; set; }
    public float wolfAttackRange { get; set; }
    public int wolfMaxSlots { get; set; } = 6;
    public int[] tiles { get; set; } = Array.Empty<int>();
    public int[] tileBaseIds { get; set; } = Array.Empty<int>();
    public List<BaseSummaryDto> bases { get; set; } = new();
    public List<ShopItemDto> shopItems { get; set; } = new();
    public List<BuildingTierDto> buildingTiers { get; set; } = new();
}

public sealed class PlayerSnapshotDto
{
    public string playerId { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string role { get; set; } = string.Empty;
    public float x { get; set; }
    public float y { get; set; }
    public float facingX { get; set; }
    public float facingY { get; set; }
    public bool moving { get; set; }
    public int hp { get; set; }
    public int maxHp { get; set; }
    public bool alive { get; set; }
    public bool spawned { get; set; } = true;
    public int gold { get; set; }
    public float goldPerSec { get; set; }
    public int weaponTier { get; set; }
    public int armorTier { get; set; }
    public int staminaTier { get; set; }
    public int attackDamage { get; set; }
    public float attackSpeedPercent { get; set; }
    public float moveSpeedPercent { get; set; }
    public int armor { get; set; }
    public int fruitsEaten { get; set; }
    public List<string> slotItems { get; set; } = new();
    public float attackCooldown { get; set; }
    public bool nearShop { get; set; }
    public int colorIndex { get; set; }
}

public sealed class BuildingSnapshotDto
{
    public int buildingId { get; set; }
    public string ownerId { get; set; } = string.Empty;
    public string buildingType { get; set; } = string.Empty;
    public int level { get; set; }
    public int tileX { get; set; }
    public int tileY { get; set; }
    public int baseId { get; set; }
    public int hp { get; set; }
    public int maxHp { get; set; }
    public int armor { get; set; }
    public float aimAngle { get; set; }
}

public sealed class ProjectileSnapshotDto
{
    public int projectileId { get; set; }
    public float x { get; set; }
    public float y { get; set; }
    public float targetX { get; set; }
    public float targetY { get; set; }
    public int level { get; set; }
}

public sealed class GameEventDto
{
    public string eventType { get; set; } = string.Empty;
    public string actorId { get; set; } = string.Empty;
    public string targetId { get; set; } = string.Empty;
    public float x { get; set; }
    public float y { get; set; }
    public int value { get; set; }
    public string extra { get; set; } = string.Empty;
}

public sealed class StateSnapshotMessage
{
    public string type { get; set; } = "state_snapshot";
    public int tick { get; set; }
    public string phase { get; set; } = "Prep";
    public float phaseTimeRemaining { get; set; }
    public int aliveRabbits { get; set; }
    public int totalRabbits { get; set; }
    public List<PlayerSnapshotDto> players { get; set; } = new();
    public List<BuildingSnapshotDto> buildings { get; set; } = new();
    public List<ProjectileSnapshotDto> projectiles { get; set; } = new();
    public List<GameEventDto> events { get; set; } = new();
}

public sealed class PlayerMatchStatDto
{
    public string playerId { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string role { get; set; } = string.Empty;
    public bool alive { get; set; }
    public int goldEarned { get; set; }
    public int buildingsBuilt { get; set; }
    public int buildingsDestroyed { get; set; }
    public int damageDealt { get; set; }
}

public sealed class MatchEndMessage
{
    public string type { get; set; } = "match_end";
    public string winner { get; set; } = string.Empty;
    public string reason { get; set; } = string.Empty;
    public float durationSec { get; set; }
    public List<PlayerMatchStatDto> stats { get; set; } = new();
}

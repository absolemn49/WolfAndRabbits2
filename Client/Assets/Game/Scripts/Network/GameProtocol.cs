using System;

namespace WolfAndRabbits.Client.Protocol
{
    public enum TileType
    {
        Grass = 0,
        Tree = 1,
        BaseFloor = 2,
        BaseEntrance = 3,
        CenterPlaza = 4,
        CenterShop = 5
    }

    [Serializable]
    public class MessageTypePeek
    {
        public string type;
    }

    // ---- Client -> Server ----

    [Serializable]
    public class SimpleRequestMsg
    {
        public string type;
        public string requestId;
    }

    [Serializable]
    public class CreateRoomMsg
    {
        public string type = "create_room";
        public string playerName;
        public string password;
    }

    [Serializable]
    public class JoinRoomMsg
    {
        public string type = "join_room";
        public string roomCode;
        public string playerName;
        public string password;
        public string requestId;
    }

    [Serializable]
    public class MoveInputMsg
    {
        public string type = "move_input";
        public float dirX;
        public float dirY;
    }

    [Serializable]
    public class BuildRequestMsg
    {
        public string type = "build_request";
        public string requestId;
        public string buildingType;
        public int tileX;
        public int tileY;
    }

    [Serializable]
    public class UpgradeRequestMsg
    {
        public string type = "upgrade_request";
        public string requestId;
        public int buildingId;
    }

    [Serializable]
    public class BuyItemMsg
    {
        public string type = "buy_item";
        public string requestId;
        public string itemId;
    }

    // ---- Server -> Client ----

    [Serializable]
    public class WelcomeMsg
    {
        public string type;
        public string clientId;
        public string serverVersion;
    }

    [Serializable]
    public class ErrorMsg
    {
        public string type;
        public string code;
        public string message;
        public string requestId;
    }

    [Serializable]
    public class RoomSummaryDto
    {
        public string roomCode;
        public string hostName;
        public int playerCount;
        public int maxPlayers;
        public bool hasPassword;
    }

    [Serializable]
    public class RoomListMsg
    {
        public string type;
        public RoomSummaryDto[] rooms;
    }

    [Serializable]
    public class RoomPlayerDto
    {
        public string playerId;
        public string name;
        public bool isHost;
        public string role;
        public int colorIndex;
    }

    [Serializable]
    public class RoomStateMsg
    {
        public string type;
        public string roomCode;
        public string hostId;
        public bool hasPassword;
        public string status;
        public int maxPlayers;
        public RoomPlayerDto[] players;
    }

    [Serializable]
    public class BaseSummaryDto
    {
        public int baseId;
        public string name;
        public int entranceX;
        public int entranceY;
        public int entranceDirX;
        public int entranceDirY;
        public float centerX;
        public float centerY;
    }

    [Serializable]
    public class ShopItemDto
    {
        public string itemId;
        public string nameKey;
        public string name;
        public string icon;
        public string itemType;   // "slot" | "fruit"
        public string statType;   // "attack" | "attack_speed_percent" | "move_speed_percent" | "armor" | "max_hp"
        public float statValue;
        public int cost;
        public string visualTag;
    }

    [Serializable]
    public class BuildingTierDto
    {
        public string buildingType;
        public string displayName;
        public int level;
        public int cost;
        public int maxHp;
        public int armor;
        public float goldPerSec;
        public int damage;
        public float range;
        public float attackInterval;
    }

    [Serializable]
    public class MatchStartMsg
    {
        public string type;
        public string roomCode;
        public string yourPlayerId;
        public string yourRole;
        public int mapWidth;
        public int mapHeight;
        public int shopX;
        public int shopY;
        public float shopInteractRadius;
        public float visionRadius;
        public float wolfAttackRange;
        public int wolfMaxSlots;
        public int[] tiles;
        public int[] tileBaseIds;
        public BaseSummaryDto[] bases;
        public ShopItemDto[] shopItems;
        public BuildingTierDto[] buildingTiers;
    }

    [Serializable]
    public class PlayerSnapshotDto
    {
        public string playerId;
        public string name;
        public string role;
        public float x;
        public float y;
        public float facingX;
        public float facingY;
        public bool moving;
        public int hp;
        public int maxHp;
        public bool alive;
        public bool spawned = true;
        public int gold;
        public float goldPerSec;
        public int weaponTier;
        public int armorTier;
        public int staminaTier;
        public int attackDamage;
        public float attackSpeedPercent;
        public float moveSpeedPercent;
        public int armor;
        public int fruitsEaten;
        public string[] slotItems;
        public float attackCooldown;
        public bool nearShop;
        public int colorIndex;
    }

    [Serializable]
    public class BuildingSnapshotDto
    {
        public int buildingId;
        public string ownerId;
        public string buildingType;
        public int level;
        public int tileX;
        public int tileY;
        public int baseId;
        public int hp;
        public int maxHp;
        public int armor;
        public float aimAngle;
    }

    [Serializable]
    public class ProjectileSnapshotDto
    {
        public int projectileId;
        public float x;
        public float y;
        public float targetX;
        public float targetY;
        public int level;
    }

    [Serializable]
    public class GameEventDto
    {
        public string eventType;
        public string actorId;
        public string targetId;
        public float x;
        public float y;
        public int value;
        public string extra;
    }

    [Serializable]
    public class StateSnapshotMsg
    {
        public string type;
        public int tick;
        public string phase;
        public float phaseTimeRemaining;
        public int aliveRabbits;
        public int totalRabbits;
        public PlayerSnapshotDto[] players;
        public BuildingSnapshotDto[] buildings;
        public ProjectileSnapshotDto[] projectiles;
        public GameEventDto[] events;
    }

    [Serializable]
    public class PlayerMatchStatDto
    {
        public string playerId;
        public string name;
        public string role;
        public bool alive;
        public int goldEarned;
        public int buildingsBuilt;
        public int buildingsDestroyed;
        public int damageDealt;
    }

    [Serializable]
    public class MatchEndMsg
    {
        public string type;
        public string winner;
        public string reason;
        public float durationSec;
        public PlayerMatchStatDto[] stats;
    }
}

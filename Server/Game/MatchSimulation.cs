using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public sealed class ActionResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static ActionResult Ok() => new() { Success = true };
    public static ActionResult Fail(string code, string message) => new() { Success = false, Code = code, Message = message };
}

public sealed class MatchSimulation
{
    public MapDefinition Map { get; }
    public int Tick { get; private set; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Prep;
    public float PhaseTimeRemaining { get; private set; }
    public float ElapsedSec { get; private set; }

    public string? Winner { get; private set; }
    public string? WinReason { get; private set; }

    private readonly Dictionary<string, PlayerEntity> _players = new();
    private readonly List<PlayerEntity> _playerOrder = new();
    private readonly Dictionary<int, BuildingEntity> _buildings = new();
    private readonly Dictionary<(int x, int y), BuildingEntity> _buildingsByTile = new();
    private readonly List<ProjectileEntity> _projectiles = new();
    private readonly List<GameEventDto> _pendingEvents = new();
    private readonly HashSet<string> _processedRequestIds = new();

    private int _nextBuildingId = 1;
    private int _nextProjectileId = 1;

    public MatchSimulation(MapDefinition map, IReadOnlyList<(string playerId, string name, string role, int colorIndex)> roster,
        float? prepDurationOverride = null, float? huntDurationOverride = null)
    {
        Map = map;
        Phase = MatchPhase.Prep;
        PhaseTimeRemaining = prepDurationOverride ?? GameConfig.PrepDurationSec;
        _huntDurationSec = huntDurationOverride ?? GameConfig.HuntDurationSec;

        int rabbitIndex = 0;
        foreach (var member in roster)
        {
            bool isWolf = member.role == Roles.Wolf;
            (float sx, float sy) spawn = isWolf
                ? map.WolfSpawn
                : map.RabbitSpawns[rabbitIndex % map.RabbitSpawns.Count];

            if (!isWolf) rabbitIndex++;

            var entity = new PlayerEntity
            {
                PlayerId = member.playerId,
                Name = member.name,
                Role = member.role,
                ColorIndex = member.colorIndex,
                X = spawn.sx,
                Y = spawn.sy,
                FacingX = 0f,
                FacingY = -1f,
                Hp = isWolf ? GameConfig.WolfBaseMaxHp : GameConfig.RabbitBaseMaxHp,
                MaxHp = isWolf ? GameConfig.WolfBaseMaxHp : GameConfig.RabbitBaseMaxHp,
                Alive = true,
                Spawned = !isWolf, // Wolf spawns after the 30s Prep phase ends
                Gold = isWolf ? GameConfig.WolfStartingGold : GameConfig.RabbitStartingGold,
                NearShop = false
            };

            _players[entity.PlayerId] = entity;
            _playerOrder.Add(entity);
        }
    }

    private readonly float _huntDurationSec;

    public IReadOnlyCollection<PlayerEntity> Players => _playerOrder;
    public IReadOnlyCollection<BuildingEntity> Buildings => _buildings.Values;

    public PlayerEntity? GetPlayer(string playerId) =>
        _players.TryGetValue(playerId, out var p) ? p : null;

    public void SetMoveInput(string playerId, float dirX, float dirY)
    {
        if (!_players.TryGetValue(playerId, out var p) || !p.Alive || !p.Spawned || Phase == MatchPhase.Ended)
            return;

        if (float.IsNaN(dirX) || float.IsNaN(dirY) || float.IsInfinity(dirX) || float.IsInfinity(dirY))
            return;

        float lenSq = dirX * dirX + dirY * dirY;
        if (lenSq > 1f)
        {
            float len = MathF.Sqrt(lenSq);
            dirX /= len;
            dirY /= len;
        }
        else if (lenSq < 0.01f)
        {
            dirX = 0f;
            dirY = 0f;
        }

        p.InputDirX = dirX;
        p.InputDirY = dirY;
    }

    public void OnPlayerDisconnected(string playerId)
    {
        if (!_players.TryGetValue(playerId, out var p) || !p.Alive || Phase == MatchPhase.Ended)
            return;

        p.Alive = false;
        p.Hp = 0;
        p.InputDirX = 0f;
        p.InputDirY = 0f;
        EmitEvent("player_died", playerId, playerId, p.X, p.Y, 0, "disconnected");
        CheckWinConditions();
    }

    public ActionResult TryBuild(string playerId, string? requestId, string? buildingType, int tileX, int tileY)
    {
        if (!string.IsNullOrWhiteSpace(requestId) && _processedRequestIds.Contains(requestId))
            return ActionResult.Ok();

        if (Phase == MatchPhase.Ended)
            return ActionResult.Fail("match_ended", "Trận đấu đã kết thúc.");

        if (!_players.TryGetValue(playerId, out var player) || !player.Alive)
            return ActionResult.Fail("invalid_player", "Không thể xây lúc này.");

        if (player.Role != Roles.Rabbit)
            return ActionResult.Fail("wrong_role", "Chỉ thỏ mới có thể xây công trình.");

        if (string.IsNullOrWhiteSpace(buildingType))
            return ActionResult.Fail("invalid_building", "Loại công trình không hợp lệ.");

        var spec = GameConfig.GetBuildingSpec(buildingType, 1);
        if (spec == null)
            return ActionResult.Fail("invalid_building", "Loại công trình không tồn tại.");

        if (!Map.InBounds(tileX, tileY))
            return ActionResult.Fail("out_of_bounds", "Ô nằm ngoài bản đồ.");

        int px = (int)MathF.Floor(player.X);
        int py = (int)MathF.Floor(player.Y);
        if (Math.Abs(tileX - px) > 1 || Math.Abs(tileY - py) > 1)
            return ActionResult.Fail("out_of_range", "Chỉ được xây trong vùng 3×3 quanh vị trí đứng.");

        var tile = Map.GetTile(tileX, tileY);
        if (buildingType == BuildingTypes.Gate)
        {
            if (tile != TileType.BaseEntrance)
                return ActionResult.Fail("invalid_tile", "Cổng chỉ được đặt tại ô lối vào căn cứ.");
        }
        else
        {
            if (tile != TileType.BaseFloor)
                return ActionResult.Fail("invalid_tile", "Công trình này chỉ được đặt trên sàn bên trong căn cứ.");
        }

        if (_buildingsByTile.ContainsKey((tileX, tileY)))
            return ActionResult.Fail("tile_occupied", "Ô này đã có công trình.");

        // Check no alive, spawned character occupies the target tile
        foreach (var other in _playerOrder)
        {
            if (!other.Alive || !other.Spawned) continue;
            int ox = (int)MathF.Floor(other.X);
            int oy = (int)MathF.Floor(other.Y);
            if ((ox == tileX && oy == tileY) || CircleIntersectsTile(other.X, other.Y, GameConfig.PlayerRadius * 0.85f, tileX, tileY))
                return ActionResult.Fail("character_blocking", "Có nhân vật đang đứng trên ô này.");
        }

        if (player.Gold < spec.Cost)
            return ActionResult.Fail("not_enough_gold", $"Cần {spec.Cost} vàng để xây {spec.DisplayName}.");

        if (!string.IsNullOrWhiteSpace(requestId))
            _processedRequestIds.Add(requestId);

        player.Gold -= spec.Cost;
        player.BuildingsBuilt++;

        var building = new BuildingEntity
        {
            BuildingId = _nextBuildingId++,
            OwnerId = player.PlayerId,
            BuildingType = spec.BuildingType,
            Level = 1,
            TileX = tileX,
            TileY = tileY,
            BaseId = Map.GetBaseId(tileX, tileY),
            Hp = spec.MaxHp,
            MaxHp = spec.MaxHp,
            Armor = spec.Armor,
            FireCooldownRemaining = 0.25f,
            AimAngle = 0f
        };

        _buildings[building.BuildingId] = building;
        _buildingsByTile[(tileX, tileY)] = building;

        EmitEvent("building_placed", player.PlayerId, building.BuildingId.ToString(),
            building.CenterX, building.CenterY, 1, building.BuildingType);

        return ActionResult.Ok();
    }

    public ActionResult TryUpgrade(string playerId, string? requestId, int buildingId)
    {
        if (!string.IsNullOrWhiteSpace(requestId) && _processedRequestIds.Contains(requestId))
            return ActionResult.Ok();

        if (Phase == MatchPhase.Ended)
            return ActionResult.Fail("match_ended", "Trận đấu đã kết thúc.");

        if (!_players.TryGetValue(playerId, out var player) || !player.Alive)
            return ActionResult.Fail("invalid_player", "Không thể nâng cấp lúc này.");

        if (player.Role != Roles.Rabbit)
            return ActionResult.Fail("wrong_role", "Chỉ thỏ mới có thể nâng cấp công trình.");

        if (!_buildings.TryGetValue(buildingId, out var building))
            return ActionResult.Fail("not_found", "Không tìm thấy công trình.");

        if (building.OwnerId != player.PlayerId)
            return ActionResult.Fail("not_owner", "Bạn chỉ có thể nâng cấp công trình do mình xây.");

        int px = (int)MathF.Floor(player.X);
        int py = (int)MathF.Floor(player.Y);
        if (Math.Abs(building.TileX - px) > 1 || Math.Abs(building.TileY - py) > 1)
            return ActionResult.Fail("out_of_range", "Hãy đứng gần công trình (vùng 3×3) để nâng cấp.");

        if (building.Level >= 3)
            return ActionResult.Fail("max_level", "Công trình đã đạt cấp tối đa.");

        var nextSpec = GameConfig.GetBuildingSpec(building.BuildingType, building.Level + 1);
        if (nextSpec == null)
            return ActionResult.Fail("max_level", "Không có cấp tiếp theo.");

        if (player.Gold < nextSpec.Cost)
            return ActionResult.Fail("not_enough_gold", $"Cần {nextSpec.Cost} vàng để nâng cấp lên {nextSpec.DisplayName}.");

        if (!string.IsNullOrWhiteSpace(requestId))
            _processedRequestIds.Add(requestId);

        player.Gold -= nextSpec.Cost;
        int hpGain = nextSpec.MaxHp - building.MaxHp;
        building.Level = nextSpec.Level;
        building.MaxHp = nextSpec.MaxHp;
        building.Hp = Math.Min(building.MaxHp, building.Hp + Math.Max(0, hpGain));
        building.Armor = nextSpec.Armor;

        EmitEvent("building_upgraded", player.PlayerId, building.BuildingId.ToString(),
            building.CenterX, building.CenterY, building.Level, building.BuildingType);

        return ActionResult.Ok();
    }

    public ActionResult TryWolfAttack(string playerId, string? requestId)
    {
        if (!string.IsNullOrWhiteSpace(requestId) && _processedRequestIds.Contains(requestId))
            return ActionResult.Ok();

        if (Phase == MatchPhase.Ended)
            return ActionResult.Fail("match_ended", "Trận đấu đã kết thúc.");

        if (!_players.TryGetValue(playerId, out var wolf) || !wolf.Alive)
            return ActionResult.Fail("invalid_player", "Không thể tấn công lúc này.");

        if (wolf.Role != Roles.Wolf)
            return ActionResult.Fail("wrong_role", "Chỉ sói mới có thể tấn công.");

        if (!wolf.Spawned)
            return ActionResult.Fail("wolf_not_spawned", "Sói chưa xuất hiện! Hãy chờ hết 30 giây chuẩn bị.");

        if (wolf.AttackCooldownRemaining > 0f)
            return ActionResult.Fail("cooldown", "Đang hồi chiêu.");

        if (!string.IsNullOrWhiteSpace(requestId))
            _processedRequestIds.Add(requestId);

        wolf.AttackCooldownRemaining = wolf.GetWolfAttackCooldown();
        EmitEvent("attack_swing", wolf.PlayerId, string.Empty, wolf.X, wolf.Y, wolf.WeaponTier, Phase.ToString());

        // No damage dealt during Prep phase
        if (Phase == MatchPhase.Prep)
            return ActionResult.Ok();

        // Find nearest unobstructed target (Rabbit or Building) within WolfAttackRange (1.65 tiles)
        PlayerEntity? bestRabbit = null;
        BuildingEntity? bestBuilding = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in _playerOrder)
        {
            if (!candidate.Alive || candidate.Role != Roles.Rabbit) continue;
            float dist = Distance(wolf.X, wolf.Y, candidate.X, candidate.Y);
            if (dist > GameConfig.WolfAttackRange) continue;
            if (!HasClearMeleeLine(wolf.X, wolf.Y, candidate.X, candidate.Y, ignoreBuildingId: -1)) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                bestRabbit = candidate;
                bestBuilding = null;
            }
        }

        foreach (var b in _buildings.Values)
        {
            if (b.Hp <= 0) continue;
            float dist = Distance(wolf.X, wolf.Y, b.CenterX, b.CenterY);
            if (dist > GameConfig.WolfAttackRange) continue;
            if (!HasClearMeleeLine(wolf.X, wolf.Y, b.CenterX, b.CenterY, ignoreBuildingId: b.BuildingId)) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                bestBuilding = b;
                bestRabbit = null;
            }
        }

        if (bestRabbit != null)
        {
            FaceTowards(wolf, bestRabbit.X, bestRabbit.Y);
            int rawDamage = wolf.GetWolfUnitDamage();
            int actual = Math.Min(bestRabbit.Hp, Math.Max(1, rawDamage));
            bestRabbit.Hp -= actual;
            wolf.TotalDamageDealt += actual;

            EmitEvent("damage", wolf.PlayerId, bestRabbit.PlayerId, bestRabbit.X, bestRabbit.Y, actual, "rabbit");

            if (bestRabbit.Hp <= 0)
            {
                bestRabbit.Hp = 0;
                bestRabbit.Alive = false;
                EmitEvent("player_died", wolf.PlayerId, bestRabbit.PlayerId, bestRabbit.X, bestRabbit.Y, 0, "slain");
                CheckWinConditions();
            }
        }
        else if (bestBuilding != null)
        {
            FaceTowards(wolf, bestBuilding.CenterX, bestBuilding.CenterY);
            int rawDamage = wolf.GetWolfBuildingDamage();
            int effective = Math.Max(1, rawDamage - bestBuilding.Armor);
            int actual = Math.Min(bestBuilding.Hp, effective);

            bestBuilding.Hp -= actual;
            wolf.TotalDamageDealt += actual;

            EmitEvent("damage", wolf.PlayerId, bestBuilding.BuildingId.ToString(),
                bestBuilding.CenterX, bestBuilding.CenterY, actual, bestBuilding.BuildingType);

            // Wolf earns 1 gold per 8 actual HP dealt to buildings, accumulating remainder
            if (actual > 0)
            {
                wolf.DamageGoldRemainder += actual;
                int bonusGold = wolf.DamageGoldRemainder / GameConfig.WolfHpPerBonusGold;
                wolf.DamageGoldRemainder %= GameConfig.WolfHpPerBonusGold;
                if (bonusGold > 0)
                {
                    wolf.Gold += bonusGold;
                    wolf.TotalGoldEarned += bonusGold;
                    EmitEvent("gold_earned", wolf.PlayerId, string.Empty,
                        bestBuilding.CenterX, bestBuilding.CenterY, bonusGold, "siege");
                }
            }

            if (bestBuilding.Hp <= 0)
            {
                _buildings.Remove(bestBuilding.BuildingId);
                _buildingsByTile.Remove((bestBuilding.TileX, bestBuilding.TileY));
                wolf.BuildingsDestroyed++;
                EmitEvent("building_destroyed", wolf.PlayerId, bestBuilding.BuildingId.ToString(),
                    bestBuilding.CenterX, bestBuilding.CenterY, bestBuilding.Level, bestBuilding.BuildingType);
            }
        }

        return ActionResult.Ok();
    }

    public ActionResult TryBuyShopItem(string playerId, string? requestId, string? itemId)
    {
        if (!string.IsNullOrWhiteSpace(requestId) && _processedRequestIds.Contains(requestId))
            return ActionResult.Ok();

        if (Phase == MatchPhase.Ended)
            return ActionResult.Fail("match_ended", "Trận đấu đã kết thúc.");

        if (!_players.TryGetValue(playerId, out var wolf) || !wolf.Alive)
            return ActionResult.Fail("invalid_player", "Không thể mua trang bị lúc này.");

        if (wolf.Role != Roles.Wolf)
            return ActionResult.Fail("wrong_role", "Chỉ sói mới có thể mua trang bị.");

        if (!wolf.Spawned)
            return ActionResult.Fail("wolf_not_spawned", "Sói chưa xuất hiện! Hãy chờ hết 30 giây chuẩn bị.");

        if (!IsNearShop(wolf.X, wolf.Y))
            return ActionResult.Fail("too_far_from_shop", "Hãy đứng gần Nhà trang bị ở giữa bản đồ.");

        if (string.IsNullOrWhiteSpace(itemId))
            return ActionResult.Fail("invalid_item", "Vật phẩm không hợp lệ.");

        var item = WolfShopCatalog.GetItem(itemId);
        if (item == null)
            return ActionResult.Fail("invalid_item", "Không tìm thấy vật phẩm.");

        bool isSlotItem = string.Equals(item.itemType, WolfItemTypes.Slot, StringComparison.OrdinalIgnoreCase);
        bool isFruitItem = string.Equals(item.itemType, WolfItemTypes.Fruit, StringComparison.OrdinalIgnoreCase);
        if (!isSlotItem && !isFruitItem)
            return ActionResult.Fail("invalid_item", "Loại vật phẩm không hợp lệ.");

        if (isSlotItem && wolf.SlotItemIds.Count >= WolfShopCatalog.MaxSlotItems)
            return ActionResult.Fail("inventory_full", $"Túi đồ đã đầy ({WolfShopCatalog.MaxSlotItems}/{WolfShopCatalog.MaxSlotItems} ô).");

        if (wolf.Gold < item.cost)
            return ActionResult.Fail("not_enough_gold", $"Cần {item.cost} vàng.");

        if (!string.IsNullOrWhiteSpace(requestId))
            _processedRequestIds.Add(requestId);

        wolf.Gold -= item.cost;

        if (isSlotItem)
            wolf.SlotItemIds.Add(item.id);
        else
            wolf.FruitsEaten++;

        ApplyWolfStatBonus(wolf, item);
        ApplyWolfVisualTag(wolf, item);

        EmitEvent("item_bought", wolf.PlayerId, item.id, wolf.X, wolf.Y, (int)MathF.Round(item.statValue), item.itemType);
        return ActionResult.Ok();
    }

    private static void ApplyWolfStatBonus(PlayerEntity wolf, WolfShopItemSpec item)
    {
        switch (item.statType.ToLowerInvariant())
        {
            case WolfStatTypes.Attack:
                wolf.BonusAttack += (int)MathF.Round(item.statValue);
                break;
            case WolfStatTypes.AttackSpeedPercent:
                wolf.BonusAttackSpeedPercent += item.statValue;
                break;
            case WolfStatTypes.MoveSpeedPercent:
                wolf.BonusMoveSpeedPercent += item.statValue;
                break;
            case WolfStatTypes.Armor:
                wolf.BonusArmor += (int)MathF.Round(item.statValue);
                break;
            case WolfStatTypes.MaxHp:
                int hpGain = (int)MathF.Round(item.statValue);
                wolf.BonusMaxHp += hpGain;
                wolf.MaxHp = GameConfig.WolfBaseMaxHp + wolf.BonusMaxHp;
                wolf.Hp = Math.Min(wolf.MaxHp, wolf.Hp + Math.Max(0, hpGain));
                break;
        }
    }

    private static void ApplyWolfVisualTag(PlayerEntity wolf, WolfShopItemSpec item)
    {
        switch ((item.visualTag ?? string.Empty).ToLowerInvariant())
        {
            case "weapon_1": wolf.WeaponTier = Math.Max(wolf.WeaponTier, 1); break;
            case "weapon_2": wolf.WeaponTier = Math.Max(wolf.WeaponTier, 2); break;
            case "armor_1": wolf.ArmorTier = Math.Max(wolf.ArmorTier, 1); break;
            case "armor_2": wolf.ArmorTier = Math.Max(wolf.ArmorTier, 2); break;
            case "stamina_1": wolf.StaminaTier = Math.Max(wolf.StaminaTier, 1); break;
            case "stamina_2": wolf.StaminaTier = Math.Max(wolf.StaminaTier, 2); break;
            case "stamina_3": wolf.StaminaTier = Math.Max(wolf.StaminaTier, 3); break;
        }

        // Also elevate floating speed emblem tier naturally from total move speed bonus
        if (wolf.BonusMoveSpeedPercent >= 36f) wolf.StaminaTier = Math.Max(wolf.StaminaTier, 3);
        else if (wolf.BonusMoveSpeedPercent >= 24f) wolf.StaminaTier = Math.Max(wolf.StaminaTier, 2);
        else if (wolf.BonusMoveSpeedPercent >= 12f) wolf.StaminaTier = Math.Max(wolf.StaminaTier, 1);
    }

    public void Step(float dt)
    {
        if (Phase == MatchPhase.Ended) return;

        Tick++;
        ElapsedSec += dt;

        // 1. Update Phase Timer
        PhaseTimeRemaining -= dt;
        if (Phase == MatchPhase.Prep && PhaseTimeRemaining <= 0.0001f)
        {
            Phase = MatchPhase.Hunt;
            PhaseTimeRemaining = _huntDurationSec;

            // Spawn Wolf right when the 30s Prep phase finishes!
            foreach (var p in _playerOrder)
            {
                if (p.Alive && !p.Spawned)
                {
                    p.Spawned = true;
                    p.GoldFraction = 0f;
                    p.NearShop = p.Role == Roles.Wolf && IsNearShop(p.X, p.Y);
                    EmitEvent("wolf_spawned", p.PlayerId, string.Empty, p.X, p.Y, p.Gold, "Hunt");
                }
            }

            EmitEvent("phase_changed", string.Empty, string.Empty, Map.ShopX + 0.5f, Map.ShopY + 0.5f, 0, "Hunt");
        }
        else if (Phase == MatchPhase.Hunt && PhaseTimeRemaining <= 0.0001f)
        {
            PhaseTimeRemaining = 0f;
            CheckWinConditions();
            if (Phase == MatchPhase.Ended) return;
        }

        // 2. Economy (passive + Gold Mines)
        StepEconomy(dt);

        // 3. Player Movement & Cooldowns
        StepPlayers(dt);

        // 4. Cannons & Projectiles (active during Hunt)
        if (Phase == MatchPhase.Hunt)
        {
            StepCannons(dt);
            StepProjectiles(dt);
        }

        // 5. Win Check
        CheckWinConditions();
    }

    private void StepEconomy(float dt)
    {
        var mineBonusByOwner = new Dictionary<string, float>();
        foreach (var b in _buildings.Values)
        {
            if (b.Hp <= 0 || b.BuildingType != BuildingTypes.GoldMine) continue;
            var spec = GameConfig.GetBuildingSpec(BuildingTypes.GoldMine, b.Level);
            if (spec == null) continue;
            mineBonusByOwner[b.OwnerId] = mineBonusByOwner.GetValueOrDefault(b.OwnerId) + spec.GoldPerSec;
        }

        foreach (var p in _playerOrder)
        {
            if (!p.Alive || !p.Spawned) continue;
            float rate = p.Role == Roles.Wolf
                ? GameConfig.WolfPassiveGoldPerSec
                : GameConfig.RabbitPassiveGoldPerSec + mineBonusByOwner.GetValueOrDefault(p.PlayerId);

            p.GoldFraction += rate * dt;
            if (p.GoldFraction >= 1f)
            {
                int whole = (int)MathF.Floor(p.GoldFraction);
                p.GoldFraction -= whole;
                p.Gold += whole;
                p.TotalGoldEarned += whole;
            }
        }
    }

    private void StepPlayers(float dt)
    {
        foreach (var p in _playerOrder)
        {
            if (!p.Alive || !p.Spawned)
            {
                p.Moving = false;
                p.InputDirX = 0f;
                p.InputDirY = 0f;
                p.NearShop = false;
                continue;
            }

            if (p.AttackCooldownRemaining > 0f)
                p.AttackCooldownRemaining = MathF.Max(0f, p.AttackCooldownRemaining - dt);

            float dx = p.InputDirX;
            float dy = p.InputDirY;
            bool moving = (dx * dx + dy * dy) > 0.001f;
            p.Moving = moving;

            if (moving)
            {
                float speed = p.GetMoveSpeed();
                float nx = p.X + dx * speed * dt;
                float ny = p.Y + dy * speed * dt;

                ResolveMovement(p, nx, ny);

                float flen = MathF.Sqrt(dx * dx + dy * dy);
                if (flen > 0.001f)
                {
                    p.FacingX = dx / flen;
                    p.FacingY = dy / flen;
                }
            }

            p.NearShop = p.Role == Roles.Wolf && IsNearShop(p.X, p.Y);
        }
    }

    private void ResolveMovement(PlayerEntity player, float targetX, float targetY)
    {
        const float r = GameConfig.PlayerRadius;
        float x = Math.Clamp(targetX, 1f + r, Map.Width - 1f - r);
        float y = Math.Clamp(targetY, 1f + r, Map.Height - 1f - r);

        // Iterative circle vs solid-tile AABB resolution for silky sliding around corners & 1-tile gates
        for (int iter = 0; iter < 3; iter++)
        {
            int minTx = Math.Max(0, (int)MathF.Floor(x - r) - 1);
            int maxTx = Math.Min(Map.Width - 1, (int)MathF.Floor(x + r) + 1);
            int minTy = Math.Max(0, (int)MathF.Floor(y - r) - 1);
            int maxTy = Math.Min(Map.Height - 1, (int)MathF.Floor(y + r) + 1);

            for (int ty = minTy; ty <= maxTy; ty++)
            {
                for (int tx = minTx; tx <= maxTx; tx++)
                {
                    if (!IsTileSolidForPlayer(player, tx, ty)) continue;

                    float closestX = Math.Clamp(x, tx, tx + 1f);
                    float closestY = Math.Clamp(y, ty, ty + 1f);
                    float diffX = x - closestX;
                    float diffY = y - closestY;
                    float distSq = diffX * diffX + diffY * diffY;

                    if (distSq < r * r)
                    {
                        if (distSq > 1e-6f)
                        {
                            float dist = MathF.Sqrt(distSq);
                            float push = r - dist;
                            x += (diffX / dist) * push;
                            y += (diffY / dist) * push;
                        }
                        else
                        {
                            // Center inside tile fallback: push out along nearest edge
                            float leftPen = x - tx;
                            float rightPen = (tx + 1f) - x;
                            float botPen = y - ty;
                            float topPen = (ty + 1f) - y;
                            float minPen = MathF.Min(MathF.Min(leftPen, rightPen), MathF.Min(botPen, topPen));
                            if (minPen == leftPen) x = tx - r;
                            else if (minPen == rightPen) x = tx + 1f + r;
                            else if (minPen == botPen) y = ty - r;
                            else y = ty + 1f + r;
                        }
                    }
                }
            }
        }

        player.X = x;
        player.Y = y;
    }

    private bool IsTileSolidForPlayer(PlayerEntity player, int tx, int ty)
    {
        if (Map.IsSolidStaticTile(tx, ty))
            return true;

        if (_buildingsByTile.TryGetValue((tx, ty), out var b) && b.Hp > 0)
        {
            // Gate blocks Wolf while HP > 0, allows Rabbits through!
            if (b.BuildingType == BuildingTypes.Gate)
                return player.Role == Roles.Wolf;

            return true;
        }

        return false;
    }

    private void StepCannons(float dt)
    {
        var wolf = _playerOrder.FirstOrDefault(p => p.Role == Roles.Wolf && p.Alive && p.Spawned);
        if (wolf == null) return;

        foreach (var b in _buildings.Values)
        {
            if (b.Hp <= 0 || b.BuildingType != BuildingTypes.Cannon) continue;

            if (b.FireCooldownRemaining > 0f)
                b.FireCooldownRemaining = MathF.Max(0f, b.FireCooldownRemaining - dt);

            var spec = GameConfig.GetBuildingSpec(BuildingTypes.Cannon, b.Level);
            if (spec == null) continue;

            float dx = wolf.X - b.CenterX;
            float dy = wolf.Y - b.CenterY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > spec.Range) continue;

            if (!HasClearTreeLine(b.CenterX, b.CenterY, wolf.X, wolf.Y))
                continue;

            b.AimAngle = MathF.Atan2(dy, dx);

            if (b.FireCooldownRemaining <= 0f)
            {
                b.FireCooldownRemaining = spec.AttackInterval;
                var proj = new ProjectileEntity
                {
                    ProjectileId = _nextProjectileId++,
                    SourceBuildingId = b.BuildingId,
                    OwnerId = b.OwnerId,
                    TargetPlayerId = wolf.PlayerId,
                    Level = b.Level,
                    Damage = spec.Damage,
                    X = b.CenterX,
                    Y = b.CenterY,
                    TargetX = wolf.X,
                    TargetY = wolf.Y,
                    Speed = GameConfig.CannonProjectileSpeed
                };
                _projectiles.Add(proj);

                EmitEvent("cannon_shot", b.BuildingId.ToString(), wolf.PlayerId,
                    b.CenterX, b.CenterY, b.Level, $"{wolf.X:F2},{wolf.Y:F2}");
            }
        }
    }

    private void StepProjectiles(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var proj = _projectiles[i];
            if (!_players.TryGetValue(proj.TargetPlayerId, out var wolf) || !wolf.Alive)
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            proj.TargetX = wolf.X;
            proj.TargetY = wolf.Y;

            float dx = proj.TargetX - proj.X;
            float dy = proj.TargetY - proj.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float step = proj.Speed * dt;

            if (dist <= step + 0.35f)
            {
                // Final tree check before impact
                if (!HasClearTreeLine(proj.X, proj.Y, wolf.X, wolf.Y))
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                int effective = Math.Max(1, proj.Damage - wolf.GetWolfArmor());
                int actual = Math.Min(wolf.Hp, effective);
                wolf.Hp -= actual;

                if (_players.TryGetValue(proj.OwnerId, out var owner))
                    owner.TotalDamageDealt += actual;

                EmitEvent("cannon_hit", proj.SourceBuildingId.ToString(), wolf.PlayerId,
                    wolf.X, wolf.Y, actual, proj.Level.ToString());
                EmitEvent("damage", proj.OwnerId, wolf.PlayerId, wolf.X, wolf.Y, actual, "wolf");

                _projectiles.RemoveAt(i);

                if (wolf.Hp <= 0)
                {
                    wolf.Hp = 0;
                    wolf.Alive = false;
                    EmitEvent("player_died", proj.OwnerId, wolf.PlayerId, wolf.X, wolf.Y, 0, "cannon");
                    CheckWinConditions();
                    if (Phase == MatchPhase.Ended) return;
                }
            }
            else
            {
                float nextX = proj.X + (dx / dist) * step;
                float nextY = proj.Y + (dy / dist) * step;

                // Tree intercepts cannonball in flight
                if (Map.BlocksVisionOrProjectile((int)MathF.Floor(nextX), (int)MathF.Floor(nextY)))
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                proj.X = nextX;
                proj.Y = nextY;
            }
        }
    }

    private void CheckWinConditions()
    {
        if (Phase == MatchPhase.Ended) return;

        var wolf = _playerOrder.FirstOrDefault(p => p.Role == Roles.Wolf);
        int aliveRabbits = _playerOrder.Count(p => p.Role == Roles.Rabbit && p.Alive);

        if (aliveRabbits == 0)
        {
            EndMatch(Roles.Wolf, "Sói đã hạ gục toàn bộ thỏ!");
            return;
        }

        if (wolf == null || !wolf.Alive)
        {
            EndMatch("Rabbits", "Sói đã bị hạ gục! Phe thỏ chiến thắng!");
            return;
        }

        if (Phase == MatchPhase.Hunt && PhaseTimeRemaining <= 0f)
        {
            EndMatch("Rabbits", "Hết thời gian săn! Phe thỏ sống sót chiến thắng!");
        }
    }

    private void EndMatch(string winner, string reason)
    {
        Phase = MatchPhase.Ended;
        Winner = winner;
        WinReason = reason;
    }

    public StateSnapshotMessage BuildSnapshotAndClearEvents()
    {
        var mineBonusByOwner = new Dictionary<string, float>();
        foreach (var b in _buildings.Values)
        {
            if (b.Hp <= 0 || b.BuildingType != BuildingTypes.GoldMine) continue;
            var spec = GameConfig.GetBuildingSpec(BuildingTypes.GoldMine, b.Level);
            if (spec != null)
                mineBonusByOwner[b.OwnerId] = mineBonusByOwner.GetValueOrDefault(b.OwnerId) + spec.GoldPerSec;
        }

        int totalRabbits = _playerOrder.Count(p => p.Role == Roles.Rabbit);
        int aliveRabbits = _playerOrder.Count(p => p.Role == Roles.Rabbit && p.Alive);

        var snap = new StateSnapshotMessage
        {
            tick = Tick,
            phase = Phase.ToString(),
            phaseTimeRemaining = MathF.Max(0f, PhaseTimeRemaining),
            aliveRabbits = aliveRabbits,
            totalRabbits = totalRabbits,
            players = _playerOrder.Select(p => new PlayerSnapshotDto
            {
                playerId = p.PlayerId,
                name = p.Name,
                role = p.Role,
                x = p.X,
                y = p.Y,
                facingX = p.FacingX,
                facingY = p.FacingY,
                moving = p.Moving,
                hp = p.Hp,
                maxHp = p.MaxHp,
                alive = p.Alive,
                spawned = p.Spawned,
                gold = p.Gold,
                goldPerSec = (!p.Alive || !p.Spawned)
                    ? 0f
                    : (p.Role == Roles.Wolf
                        ? GameConfig.WolfPassiveGoldPerSec
                        : GameConfig.RabbitPassiveGoldPerSec + mineBonusByOwner.GetValueOrDefault(p.PlayerId)),
                weaponTier = p.WeaponTier,
                armorTier = p.ArmorTier,
                staminaTier = p.StaminaTier,
                attackDamage = p.Role == Roles.Wolf ? p.GetWolfUnitDamage() : 0,
                attackSpeedPercent = p.BonusAttackSpeedPercent,
                moveSpeedPercent = p.BonusMoveSpeedPercent,
                armor = p.GetWolfArmor(),
                fruitsEaten = p.FruitsEaten,
                slotItems = new List<string>(p.SlotItemIds),
                attackCooldown = p.AttackCooldownRemaining,
                nearShop = p.NearShop,
                colorIndex = p.ColorIndex
            }).ToList(),
            buildings = _buildings.Values.Select(b => new BuildingSnapshotDto
            {
                buildingId = b.BuildingId,
                ownerId = b.OwnerId,
                buildingType = b.BuildingType,
                level = b.Level,
                tileX = b.TileX,
                tileY = b.TileY,
                baseId = b.BaseId,
                hp = b.Hp,
                maxHp = b.MaxHp,
                armor = b.Armor,
                aimAngle = b.AimAngle
            }).ToList(),
            projectiles = _projectiles.Select(pr => new ProjectileSnapshotDto
            {
                projectileId = pr.ProjectileId,
                x = pr.X,
                y = pr.Y,
                targetX = pr.TargetX,
                targetY = pr.TargetY,
                level = pr.Level
            }).ToList(),
            events = new List<GameEventDto>(_pendingEvents)
        };

        _pendingEvents.Clear();
        return snap;
    }

    public MatchEndMessage BuildMatchEndMessage()
    {
        return new MatchEndMessage
        {
            winner = Winner ?? "Rabbits",
            reason = WinReason ?? "Trận đấu kết thúc.",
            durationSec = ElapsedSec,
            stats = _playerOrder.Select(p => new PlayerMatchStatDto
            {
                playerId = p.PlayerId,
                name = p.Name,
                role = p.Role,
                alive = p.Alive,
                goldEarned = p.TotalGoldEarned,
                buildingsBuilt = p.BuildingsBuilt,
                buildingsDestroyed = p.BuildingsDestroyed,
                damageDealt = p.TotalDamageDealt
            }).ToList()
        };
    }

    // Raycast helper: trees block cannon shots
    public bool HasClearTreeLine(float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.05f) return true;

        int steps = Math.Max(4, (int)MathF.Ceiling(dist * 6f));
        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            float sx = x0 + dx * t;
            float sy = y0 + dy * t;
            int tx = (int)MathF.Floor(sx);
            int ty = (int)MathF.Floor(sy);
            if (Map.BlocksVisionOrProjectile(tx, ty))
                return false;
        }
        return true;
    }

    // Raycast helper: trees and OTHER alive buildings block Wolf melee attacks
    public bool HasClearMeleeLine(float x0, float y0, float x1, float y1, int ignoreBuildingId)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 0.05f) return true;

        int steps = Math.Max(6, (int)MathF.Ceiling(dist * 8f));
        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            float sx = x0 + dx * t;
            float sy = y0 + dy * t;
            int tx = (int)MathF.Floor(sx);
            int ty = (int)MathF.Floor(sy);
            if (Map.BlocksVisionOrProjectile(tx, ty))
                return false;

            if (_buildingsByTile.TryGetValue((tx, ty), out var blocking) &&
                blocking.Hp > 0 &&
                blocking.BuildingId != ignoreBuildingId)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsNearShop(float x, float y) =>
        Distance(x, y, Map.ShopX + 0.5f, Map.ShopY + 0.5f) <= GameConfig.ShopInteractRadius;

    private static float Distance(float x0, float y0, float x1, float y1)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static bool CircleIntersectsTile(float cx, float cy, float radius, int tx, int ty)
    {
        float closestX = Math.Clamp(cx, tx, tx + 1f);
        float closestY = Math.Clamp(cy, ty, ty + 1f);
        float dx = cx - closestX;
        float dy = cy - closestY;
        return (dx * dx + dy * dy) < (radius * radius);
    }

    private static void FaceTowards(PlayerEntity p, float tx, float ty)
    {
        float dx = tx - p.X;
        float dy = ty - p.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len > 0.001f)
        {
            p.FacingX = dx / len;
            p.FacingY = dy / len;
        }
    }

    private void EmitEvent(string eventType, string actorId, string targetId, float x, float y, int value, string extra)
    {
        _pendingEvents.Add(new GameEventDto
        {
            eventType = eventType,
            actorId = actorId,
            targetId = targetId,
            x = x,
            y = y,
            value = value,
            extra = extra
        });
    }
}

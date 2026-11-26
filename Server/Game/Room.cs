using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public enum RoomStatus
{
    Waiting,
    InMatch
}

public sealed class RoomMember
{
    public ClientConnection Connection { get; init; } = null!;
    public string Name { get; set; } = string.Empty;
    public int ColorIndex { get; set; }
}

public sealed class Room
{
    public string RoomCode { get; }
    public string Password { get; }
    public bool HasPassword => !string.IsNullOrEmpty(Password);
    public RoomStatus Status { get; private set; } = RoomStatus.Waiting;

    private readonly object _sync = new();
    private readonly List<RoomMember> _members = new();
    private string _hostId;

    private MatchSimulation? _match;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private int _ticksSinceSnapshot;
    private bool _matchEndBroadcasted;

    public Room(string roomCode, string password, ClientConnection host, string hostName)
    {
        RoomCode = roomCode;
        Password = password;
        _hostId = host.ClientId;
        _members.Add(new RoomMember
        {
            Connection = host,
            Name = hostName,
            ColorIndex = 0
        });
        host.CurrentRoomCode = roomCode;
    }

    public RoomSummaryDto? GetLobbySummary()
    {
        lock (_sync)
        {
            if (Status != RoomStatus.Waiting) return null;
            if (_members.Count == 0 || _members.Count >= GameConfig.MaxPlayersPerRoom) return null;

            var host = _members.FirstOrDefault(m => m.Connection.ClientId == _hostId) ?? _members[0];
            return new RoomSummaryDto
            {
                roomCode = RoomCode,
                hostName = host.Name,
                playerCount = _members.Count,
                maxPlayers = GameConfig.MaxPlayersPerRoom,
                hasPassword = HasPassword
            };
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_sync)
            {
                return _members.Count == 0;
            }
        }
    }

    public ActionResult TryAddMember(ClientConnection client, string playerName, string? password)
    {
        lock (_sync)
        {
            if (Status != RoomStatus.Waiting)
                return ActionResult.Fail("room_started", "Phòng đã bắt đầu trận đấu.");

            if (_members.Count >= GameConfig.MaxPlayersPerRoom)
                return ActionResult.Fail("room_full", "Phòng đã đủ 4 người.");

            if (HasPassword && !string.Equals(Password, password ?? string.Empty, StringComparison.Ordinal))
                return ActionResult.Fail("wrong_password", "Mật khẩu phòng không đúng.");

            if (_members.Any(m => m.Connection.ClientId == client.ClientId))
                return ActionResult.Ok();

            int nextColor = PickNextColorIndex();
            _members.Add(new RoomMember
            {
                Connection = client,
                Name = playerName,
                ColorIndex = nextColor
            });
            client.CurrentRoomCode = RoomCode;

            BroadcastRoomStateLocked();
            return ActionResult.Ok();
        }
    }

    public bool RemoveMember(string clientId)
    {
        CancellationTokenSource? ctsToCancel = null;
        bool becameEmpty;

        lock (_sync)
        {
            int idx = _members.FindIndex(m => m.Connection.ClientId == clientId);
            if (idx < 0) return _members.Count == 0;

            var removed = _members[idx];
            removed.Connection.CurrentRoomCode = null;
            _members.RemoveAt(idx);

            if (Status == RoomStatus.InMatch && _match != null)
            {
                _match.OnPlayerDisconnected(clientId);
                if (_match.Phase == MatchPhase.Ended && !_matchEndBroadcasted)
                {
                    BroadcastMatchEndLocked();
                    ctsToCancel = _loopCts;
                    _loopCts = null;
                }
            }

            if (_members.Count > 0 && _hostId == clientId)
            {
                _hostId = _members[0].Connection.ClientId;
            }

            becameEmpty = _members.Count == 0;
            if (becameEmpty)
            {
                ctsToCancel = _loopCts;
                _loopCts = null;
            }
            else if (Status == RoomStatus.Waiting)
            {
                BroadcastRoomStateLocked();
            }
        }

        ctsToCancel?.Cancel();
        return becameEmpty;
    }

    public ActionResult TryStartMatch(string requesterId, float? prepOverride = null, float? huntOverride = null)
    {
        lock (_sync)
        {
            if (Status != RoomStatus.Waiting)
                return ActionResult.Fail("already_started", "Trận đấu đang diễn ra.");

            if (requesterId != _hostId)
                return ActionResult.Fail("not_host", "Chỉ chủ phòng mới có thể bắt đầu trận.");

            if (_members.Count < GameConfig.MinPlayersToStart)
                return ActionResult.Fail("not_enough_players", "Cần ít nhất 2 người chơi để bắt đầu.");

            var map = MapDefinition.CreateDefaultForestMap();
            var roster = _members.Select(m => (
                playerId: m.Connection.ClientId,
                name: m.Name,
                role: m.Connection.ClientId == _hostId ? Roles.Wolf : Roles.Rabbit,
                colorIndex: m.ColorIndex
            )).ToList();

            _match = new MatchSimulation(map, roster, prepOverride, huntOverride);
            Status = RoomStatus.InMatch;
            _ticksSinceSnapshot = 0;
            _matchEndBroadcasted = false;

            var exportedTiles = map.ExportTilesAsIntArray();
            var exportedBases = map.ExportBases();
            var shopDtos = WolfShopCatalog.ToDtos();
            var tierDtos = GameConfig.ToBuildingTierDtos();

            foreach (var member in _members)
            {
                string role = member.Connection.ClientId == _hostId ? Roles.Wolf : Roles.Rabbit;
                member.Connection.Send(new MatchStartMessage
                {
                    roomCode = RoomCode,
                    yourPlayerId = member.Connection.ClientId,
                    yourRole = role,
                    mapWidth = map.Width,
                    mapHeight = map.Height,
                    shopX = map.ShopX,
                    shopY = map.ShopY,
                    shopInteractRadius = GameConfig.ShopInteractRadius,
                    visionRadius = GameConfig.VisionRadius,
                    wolfAttackRange = GameConfig.WolfAttackRange,
                    wolfMaxSlots = WolfShopCatalog.MaxSlotItems,
                    tiles = exportedTiles,
                    tileBaseIds = map.TileBaseIds,
                    bases = exportedBases,
                    shopItems = shopDtos,
                    buildingTiers = tierDtos
                });
            }

            // Send initial snapshot immediately so clients place entities on frame 0
            BroadcastSnapshotLocked();

            _loopCts = new CancellationTokenSource();
            var token = _loopCts.Token;
            _loopTask = Task.Run(() => RunMatchLoopAsync(token), token);

            return ActionResult.Ok();
        }
    }

    public ActionResult TryReturnToWaitingRoom(string requesterId)
    {
        CancellationTokenSource? ctsToCancel = null;
        lock (_sync)
        {
            if (Status == RoomStatus.Waiting)
            {
                BroadcastRoomStateLocked();
                return ActionResult.Ok();
            }

            if (_match != null && _match.Phase != MatchPhase.Ended)
                return ActionResult.Fail("match_running", "Trận đấu chưa kết thúc.");

            ctsToCancel = _loopCts;
            _loopCts = null;
            _match = null;
            Status = RoomStatus.Waiting;
            BroadcastRoomStateLocked();
        }
        ctsToCancel?.Cancel();
        return ActionResult.Ok();
    }

    public void HandleMoveInput(string clientId, float dirX, float dirY)
    {
        lock (_sync)
        {
            _match?.SetMoveInput(clientId, dirX, dirY);
        }
    }

    public ActionResult HandleBuild(string clientId, string? requestId, string? buildingType, int tileX, int tileY)
    {
        lock (_sync)
        {
            if (_match == null || Status != RoomStatus.InMatch)
                return ActionResult.Fail("no_match", "Chưa vào trận đấu.");
            return _match.TryBuild(clientId, requestId, buildingType, tileX, tileY);
        }
    }

    public ActionResult HandleUpgrade(string clientId, string? requestId, int buildingId)
    {
        lock (_sync)
        {
            if (_match == null || Status != RoomStatus.InMatch)
                return ActionResult.Fail("no_match", "Chưa vào trận đấu.");
            return _match.TryUpgrade(clientId, requestId, buildingId);
        }
    }

    public ActionResult HandleAttack(string clientId, string? requestId)
    {
        lock (_sync)
        {
            if (_match == null || Status != RoomStatus.InMatch)
                return ActionResult.Fail("no_match", "Chưa vào trận đấu.");
            var res = _match.TryWolfAttack(clientId, requestId);
            if (_match.Phase == MatchPhase.Ended && !_matchEndBroadcasted)
            {
                BroadcastSnapshotLocked();
                BroadcastMatchEndLocked();
            }
            return res;
        }
    }

    public ActionResult HandleBuyItem(string clientId, string? requestId, string? itemId)
    {
        lock (_sync)
        {
            if (_match == null || Status != RoomStatus.InMatch)
                return ActionResult.Fail("no_match", "Chưa vào trận đấu.");
            return _match.TryBuyShopItem(clientId, requestId, itemId);
        }
    }

    public void SendCurrentRoomState(ClientConnection client)
    {
        lock (_sync)
        {
            client.Send(BuildRoomStateMessageLocked());
        }
    }

    private async Task RunMatchLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / GameConfig.TickRateHz));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                bool shouldStop = false;
                lock (_sync)
                {
                    if (_match == null || Status != RoomStatus.InMatch)
                    {
                        shouldStop = true;
                    }
                    else
                    {
                        _match.Step(GameConfig.TickDeltaSec);
                        _ticksSinceSnapshot++;

                        if (_match.Phase == MatchPhase.Ended)
                        {
                            BroadcastSnapshotLocked();
                            if (!_matchEndBroadcasted)
                                BroadcastMatchEndLocked();
                            shouldStop = true;
                        }
                        else if (_ticksSinceSnapshot >= GameConfig.SnapshotEveryNTicks)
                        {
                            _ticksSinceSnapshot = 0;
                            BroadcastSnapshotLocked();
                        }
                    }
                }

                if (shouldStop) break;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void BroadcastSnapshotLocked()
    {
        if (_match == null) return;
        var snap = _match.BuildSnapshotAndClearEvents();
        string json = ClientConnection.Serialize(snap);
        foreach (var member in _members)
        {
            member.Connection.SendRawJson(json);
        }
    }

    private void BroadcastMatchEndLocked()
    {
        if (_match == null) return;
        _matchEndBroadcasted = true;
        var endMsg = _match.BuildMatchEndMessage();
        string json = ClientConnection.Serialize(endMsg);
        foreach (var member in _members)
        {
            member.Connection.SendRawJson(json);
        }
    }

    private void BroadcastRoomStateLocked()
    {
        var msg = BuildRoomStateMessageLocked();
        string json = ClientConnection.Serialize(msg);
        foreach (var member in _members)
        {
            member.Connection.SendRawJson(json);
        }
    }

    private RoomStateMessage BuildRoomStateMessageLocked()
    {
        return new RoomStateMessage
        {
            roomCode = RoomCode,
            hostId = _hostId,
            hasPassword = HasPassword,
            status = Status.ToString(),
            maxPlayers = GameConfig.MaxPlayersPerRoom,
            players = _members.Select(m => new RoomPlayerDto
            {
                playerId = m.Connection.ClientId,
                name = m.Name,
                isHost = m.Connection.ClientId == _hostId,
                role = m.Connection.ClientId == _hostId ? Roles.Wolf : Roles.Rabbit,
                colorIndex = m.ColorIndex
            }).ToList()
        };
    }

    private int PickNextColorIndex()
    {
        for (int c = 0; c < GameConfig.MaxPlayersPerRoom; c++)
        {
            if (_members.All(m => m.ColorIndex != c))
                return c;
        }
        return _members.Count;
    }
}

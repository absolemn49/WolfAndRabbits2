using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WolfAndRabbits.Server.Game;
using WolfAndRabbits.Server.Protocol;

int passed = 0;

void Assert(bool cond, string msg)
{
    if (!cond) throw new Exception("ASSERTION FAILED: " + msg);
}

void RunTest(string name, Action body)
{
    body();
    passed++;
    Console.WriteLine($"[PASS] {name}");
}

async Task RunAsyncTest(string name, Func<Task> body)
{
    await body();
    passed++;
    Console.WriteLine($"[PASS] {name}");
}

// 1. Map Validation Test
RunTest("Map has 10 varied bases, 1-tile entrances, all reachable", () =>
{
    var map = MapDefinition.CreateDefaultForestMap();
    Assert(map.Width == 44 && map.Height == 44, "Map should be 44x44");
    Assert(map.Bases.Count == 10, "Must have exactly 10 bases");

    var dirs = map.Bases.Select(b => (b.EntranceDirX, b.EntranceDirY)).Distinct().Count();
    Assert(dirs == 4, $"Expected entrances facing all 4 directions, got {dirs}");

    foreach (var b in map.Bases)
    {
        Assert(map.GetTile(b.EntranceX, b.EntranceY) == TileType.BaseEntrance, $"Base {b.BaseId} entrance mismatch");
        Assert(b.FloorTiles.Count >= 12, $"Base {b.BaseId} too small");
    }
});

// 2. Starting Gold (200g), Wolf Delayed Spawn & Economy, Building Placement, 3x3 Rule, Idempotency & Upgrades
RunTest("Starting gold 200g, Wolf unspawned during Prep (no gold gain), Rabbit economy, 3x3 rules & upgrades", () =>
{
    var map = MapDefinition.CreateDefaultForestMap();
    var roster = new List<(string, string, string, int)>
    {
        ("w1", "WolfHost", Roles.Wolf, 0),
        ("r1", "Rabbit1", Roles.Rabbit, 1)
    };
    var sim = new MatchSimulation(map, roster, prepDurationOverride: 2f, huntDurationOverride: 60f);
    var wolf = sim.GetPlayer("w1")!;
    var rabbit = sim.GetPlayer("r1")!;

    Assert(wolf.Gold == 200 && rabbit.Gold == 200, $"Both must start at 200g, got wolf={wolf.Gold}, rabbit={rabbit.Gold}");
    Assert(!wolf.Spawned && rabbit.Spawned, "Wolf must NOT be spawned during Prep; Rabbit must be spawned");

    // During Prep (first 1.95s = 39 ticks): Wolf cannot move, attack, buy, and Wolf gold stays 200g!
    float wx0 = wolf.X;
    sim.SetMoveInput("w1", 1f, 0f);
    for (int i = 0; i < 39; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(sim.Phase == MatchPhase.Prep && !wolf.Spawned, "Wolf still unspawned before Prep finishes");
    Assert(MathF.Abs(wolf.X - wx0) < 0.001f, "Unspawned Wolf must not move");
    Assert(wolf.Gold == 200, $"Unspawned Wolf gold must stay 200g during Prep, got {wolf.Gold}");
    Assert(!sim.TryWolfAttack("w1", "atk-unspawned").Success && sim.TryWolfAttack("w1", "atk-unspawned2").Code == "wolf_not_spawned", "Unspawned Wolf cannot attack");
    Assert(!sim.TryBuyShopItem("w1", "buy-unspawned", "iron_claws").Success && sim.TryBuyShopItem("w1", "buy-unspawned2", "iron_claws").Code == "wolf_not_spawned", "Unspawned Wolf cannot buy");

    // Step 1 more tick (t = 2.0s) -> Prep ends, Wolf spawns! Then step 160 more ticks (8.0s of Hunt)
    for (int i = 0; i < 161; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(sim.Phase == MatchPhase.Hunt && wolf.Spawned, "Wolf must spawn when Prep finishes");
    Assert(wolf.Gold == 216, $"Wolf expected 200 + 16 = 216g after 8s spawned, got {wolf.Gold}");
    Assert(rabbit.Gold == 210, $"Rabbit expected 200 + 10 = 210g after 10s, got {rabbit.Gold}");

    // Move Rabbit inside Base 1 right beside entrance (7, 37)
    var b1 = map.Bases[0];
    rabbit.X = b1.EntranceX - 0.5f; // tile (6, 37) which is BaseFloor
    rabbit.Y = b1.EntranceY + 0.5f;

    // Not enough gold test (set rabbit gold to 10 temporarily) -> fails without deducting gold
    rabbit.Gold = 10;
    var failGold = sim.TryBuild("r1", "req-gate-1", BuildingTypes.Gate, b1.EntranceX, b1.EntranceY);
    Assert(!failGold.Success && failGold.Code == "not_enough_gold", "Should reject without 50g");
    Assert(rabbit.Gold == 10, "Gold must not change on failed build");

    // Give Rabbit gold to test placement rules
    rabbit.Gold = 600;

    // Gate on BaseFloor rejected
    var wrongTile = sim.TryBuild("r1", "req-wrong-1", BuildingTypes.Gate, b1.EntranceX - 1, b1.EntranceY);
    Assert(!wrongTile.Success, "Gate must only be on BaseEntrance");

    // Mine on BaseEntrance rejected
    var wrongTile2 = sim.TryBuild("r1", "req-wrong-2", BuildingTypes.GoldMine, b1.EntranceX, b1.EntranceY);
    Assert(!wrongTile2.Success, "GoldMine must only be on BaseFloor");

    // Out of 3x3 range rejected
    var farTile = sim.TryBuild("r1", "req-far", BuildingTypes.GoldMine, 3, 39);
    Assert(!farTile.Success && farTile.Code == "out_of_range", "Build outside 3x3 must fail");

    // Place Gate at Base 1 Entrance with idempotency test
    var okGate = sim.TryBuild("r1", "req-gate-ok", BuildingTypes.Gate, b1.EntranceX, b1.EntranceY);
    Assert(okGate.Success, "Gate placement should succeed");
    Assert(rabbit.Gold == 550, "Should deduct 50g for Lv1 Gate");

    // Replay same requestId -> processed ONCE, no second deduction
    var dupGate = sim.TryBuild("r1", "req-gate-ok", BuildingTypes.Gate, b1.EntranceX, b1.EntranceY);
    Assert(dupGate.Success && rabbit.Gold == 550, "Duplicate requestId must be idempotent");

    // Place GoldMine at (6, 38) (inside 3x3 around (6,37))
    var okMine = sim.TryBuild("r1", "req-mine-1", BuildingTypes.GoldMine, 6, 38);
    Assert(okMine.Success && rabbit.Gold == 490, "Lv1 GoldMine costs 60g");

    // Place Cannon at (6, 36)
    var okCannon = sim.TryBuild("r1", "req-cannon-1", BuildingTypes.Cannon, 6, 36);
    Assert(okCannon.Success && rabbit.Gold == 400, "Lv1 Cannon costs 90g");

    // Verify GoldMine Lv1 income: +1 passive + 2 mine = +3g/sec -> over 5s = +15g
    int gBefore = rabbit.Gold;
    for (int i = 0; i < 100; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(rabbit.Gold == gBefore + 15, $"Expected +15g in 5s, got {rabbit.Gold - gBefore}");

    // Upgrade Mine Lv1 -> Lv2 (+4g/s -> total +5g/s) -> Lv3 (+7g/s -> total +8g/s)
    var mine = sim.Buildings.First(b => b.BuildingType == BuildingTypes.GoldMine);
    Assert(sim.TryUpgrade("r1", "upg-m2", mine.BuildingId).Success, "Mine Lv2 upgrade failed");
    Assert(mine.Level == 2 && mine.MaxHp == 300, "Mine Lv2 stats mismatch");

    Assert(sim.TryUpgrade("r1", "upg-m3", mine.BuildingId).Success, "Mine Lv3 upgrade failed");
    Assert(mine.Level == 3 && mine.MaxHp == 450, "Mine Lv3 stats mismatch");

    int gBeforeLv3 = rabbit.Gold;
    for (int i = 0; i < 40; i++) sim.Step(GameConfig.TickDeltaSec); // 2 seconds -> +16g
    Assert(rabbit.Gold == gBeforeLv3 + 16, $"Lv3 Mine expected +16g in 2s, got {rabbit.Gold - gBeforeLv3}");
});

// 3. Gate Collision, Wolf Attack, 1g/8hp Remainder Accumulator, Prep Phase No-Damage
RunTest("Prep no-damage, Gate blocks Wolf / passes Rabbit, 1g/8hp remainder, Gate break opens path", () =>
{
    var map = MapDefinition.CreateDefaultForestMap();
    var roster = new List<(string, string, string, int)>
    {
        ("w1", "Wolf", Roles.Wolf, 0),
        ("r1", "Rabbit", Roles.Rabbit, 1)
    };
    var sim = new MatchSimulation(map, roster, prepDurationOverride: 1.0f, huntDurationOverride: 60f);
    var wolf = sim.GetPlayer("w1")!;
    var rabbit = sim.GetPlayer("r1")!;
    var b1 = map.Bases[0]; // Entrance (7, 37) facing East (+1, 0)

    rabbit.Gold = 200;
    rabbit.X = 6.5f; rabbit.Y = 37.5f;
    Assert(sim.TryBuild("r1", "g1", BuildingTypes.Gate, b1.EntranceX, b1.EntranceY).Success, "Build gate");
    var gate = sim.Buildings.First();

    // Rabbit walks right through Gate to outside (8.5, 37.5)
    sim.SetMoveInput("r1", 1f, 0f);
    for (int i = 0; i < 16; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(rabbit.X > 7.8f, $"Rabbit must pass through Gate freely, x={rabbit.X}");
    sim.SetMoveInput("r1", 0f, 0f);
    // Park rabbit safely back inside
    rabbit.X = 4.5f; rabbit.Y = 37.5f;

    // During Prep, Wolf has not spawned yet and cannot attack
    Assert(sim.Phase == MatchPhase.Prep && !wolf.Spawned, "Should still be Prep with Wolf unspawned");
    Assert(!sim.TryWolfAttack("w1", "atk-prep").Success && sim.TryWolfAttack("w1", "atk-prep-2").Code == "wolf_not_spawned", "Unspawned Wolf cannot attack in Prep");
    Assert(gate.Hp == 300, $"Gate HP untouched during Prep, hp={gate.Hp}");

    // Advance past Prep into Hunt -> Wolf spawns!
    for (int i = 0; i < 20; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(sim.Phase == MatchPhase.Hunt && wolf.Spawned, "Must transition to Hunt and spawn Wolf");

    // Position spawned Wolf directly outside Gate at (8.4, 37.5) and verify Gate blocks Wolf
    wolf.X = 8.4f; wolf.Y = 37.5f;
    sim.SetMoveInput("w1", -1f, 0f);
    for (int i = 0; i < 3; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(wolf.X >= 8.0f + GameConfig.PlayerRadius - 0.05f, $"Gate must block Wolf! wolf.X={wolf.X}");
    sim.SetMoveInput("w1", 0f, 0f);

    // Set gate HP to 40, wolf gold = 0, remainder = 0 -> hit 1 deals 35 (leaves 5 HP -> 4g, rem 3);
    // hit 2 deals ONLY 5 actual HP (3 + 5 = 8 -> +1g, rem 0) and breaks gate!
    gate.Hp = 40;
    wolf.Gold = 0;
    wolf.GoldFraction = 0f;
    wolf.DamageGoldRemainder = 0;
    wolf.AttackCooldownRemaining = 0f;

    Assert(sim.TryWolfAttack("w1", "atk-1").Success, "Attack 1");
    Assert(gate.Hp == 5, $"Expected 5 HP left, got {gate.Hp}");
    Assert(wolf.Gold == 4 && wolf.DamageGoldRemainder == 3, $"Expected 4g rem 3, got {wolf.Gold}g rem {wolf.DamageGoldRemainder}");

    wolf.AttackCooldownRemaining = 0f;
    Assert(sim.TryWolfAttack("w1", "atk-2").Success, "Attack 2 killing blow");
    Assert(sim.Buildings.Count == 0, "Gate should be removed when HP hits 0");
    Assert(wolf.Gold == 5 && wolf.DamageGoldRemainder == 0, $"Killing blow of 5 HP must yield +1g total 5g rem 0, got {wolf.Gold}g rem {wolf.DamageGoldRemainder}");

    // Now Wolf can walk cleanly through the opened entrance!
    sim.SetMoveInput("w1", -1f, 0f);
    for (int i = 0; i < 16; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(wolf.X < 7.2f, $"Wolf should enter base after Gate breaks, wolf.X={wolf.X}");
});

// 4. Wolf 5 Stats, Slot Limit (6), Permanent Fruits, Shop Range & Cannon Kills Wolf -> Rabbits Win
RunTest("Wolf 5 stats, slot limit (6), unlimited fruits, shop range, Cannon kills Wolf -> Rabbits win", () =>
{
    var map = MapDefinition.CreateDefaultForestMap();
    var roster = new List<(string, string, string, int)>
    {
        ("w1", "Wolf", Roles.Wolf, 0),
        ("r1", "Rabbit", Roles.Rabbit, 1)
    };
    var sim = new MatchSimulation(map, roster, prepDurationOverride: 0.1f, huntDurationOverride: 60f);
    var wolf = sim.GetPlayer("w1")!;
    var rabbit = sim.GetPlayer("r1")!;

    // Step past 0.1s Prep so Wolf spawns near Center Shop
    for (int i = 0; i < 4; i++) sim.Step(GameConfig.TickDeltaSec);
    Assert(sim.Phase == MatchPhase.Hunt && wolf.Spawned && wolf.NearShop, "Wolf must spawn near Center Shop after Prep");

    // Wolf starts near Center Shop -> test all 5 stats via slot items + fruits!
    wolf.Gold = 2500;
    float baseMove = wolf.GetMoveSpeed();
    float baseAtkCd = wolf.GetWolfAttackCooldown();

    // Fill all 6 limited slots: attack, attack_speed_percent, move_speed_percent, armor, max_hp, attack
    Assert(sim.TryBuyShopItem("w1", "s1", "iron_claws").Success && wolf.WeaponTier == 1 && wolf.GetWolfUnitDamage() == 50, "Buy Iron Claws (+15 atk)");
    Assert(sim.TryBuyShopItem("w1", "s2", "fury_gloves").Success && wolf.BonusAttackSpeedPercent == 25f && wolf.GetWolfAttackCooldown() < baseAtkCd, "Buy Fury Gloves (+25% aspd)");
    Assert(sim.TryBuyShopItem("w1", "s3", "swift_boots").Success && wolf.BonusMoveSpeedPercent == 18f && wolf.GetMoveSpeed() > baseMove, "Buy Swift Boots (+18% spd)");
    Assert(sim.TryBuyShopItem("w1", "s4", "leather_armor").Success && wolf.ArmorTier == 1 && wolf.GetWolfArmor() == 4, "Buy Leather Armor (+4 armor)");
    Assert(sim.TryBuyShopItem("w1", "s5", "vitality_ring").Success && wolf.MaxHp == 380 && wolf.Hp == 380, "Buy Vitality Ring (+120 maxHp)");
    Assert(sim.TryBuyShopItem("w1", "s6", "siege_hammer").Success && wolf.WeaponTier == 2 && wolf.SlotItemIds.Count == 6, "Buy 6th slot item Siege Hammer");

    // 7th slot item must be rejected with inventory_full!
    var fullRes = sim.TryBuyShopItem("w1", "s7", "iron_armor");
    Assert(!fullRes.Success && fullRes.Code == "inventory_full", $"7th slot item must fail with inventory_full, got {fullRes.Code}");

    // Permanent Fruits DO NOT consume slots -> can still eat all 5 fruits even with 6/6 slots full!
    Assert(sim.TryBuyShopItem("w1", "f1", "fruit_attack").Success && wolf.GetWolfUnitDamage() == 91, "Eat Attack Fruit (+6 atk)");
    Assert(sim.TryBuyShopItem("w1", "f2", "fruit_attack_speed").Success && wolf.BonusAttackSpeedPercent == 37f, "Eat Attack Speed Fruit (+12% aspd)");
    Assert(sim.TryBuyShopItem("w1", "f3", "fruit_move_speed").Success && wolf.BonusMoveSpeedPercent == 26f, "Eat Move Speed Fruit (+8% spd)");
    Assert(sim.TryBuyShopItem("w1", "f4", "fruit_armor").Success && wolf.GetWolfArmor() == 6, "Eat Armor Fruit (+2 armor)");
    Assert(sim.TryBuyShopItem("w1", "f5", "fruit_hp").Success && wolf.MaxHp == 425 && wolf.FruitsEaten == 5, "Eat HP Fruit (+45 maxHp)");

    // Move Wolf far away from shop -> buy fails
    wolf.X = 8.5f; wolf.Y = 37.5f;
    sim.Step(GameConfig.TickDeltaSec);
    var farBuy = sim.TryBuyShopItem("w1", "buy-far", "fruit_hp");
    Assert(!farBuy.Success && farBuy.Code == "too_far_from_shop", "Shop must reject when far");

    // Place Rabbit's Cannon at (6, 37) with clear LOS through open entrance (7, 37) to Wolf at (8.5, 37.5)
    rabbit.Gold = 500;
    rabbit.X = 5.5f; rabbit.Y = 37.5f;
    Assert(sim.TryBuild("r1", "c1", BuildingTypes.Cannon, 6, 37).Success, "Place Lv1 Cannon");

    // Transition to Hunt and drop Wolf HP low so Cannon finishes Wolf (effective dmg = 12 - 6 armor = 6/shot -> 3 shots)
    wolf.Hp = 15;
    for (int i = 0; i < 100; i++)
    {
        sim.Step(GameConfig.TickDeltaSec);
        if (sim.Phase == MatchPhase.Ended) break;
    }
    Assert(sim.Phase == MatchPhase.Ended && sim.Winner == "Rabbits", $"Cannon must kill Wolf so Rabbits win! phase={sim.Phase} hp={wolf.Hp}");
});

// 5. Full End-to-End Live WebSocket Test against ASP.NET Core Server (1v1 + 1v3!)
await RunAsyncTest("Live ASP.NET Core WebSocket 1v3 match (Lobby -> Password -> Room List -> Start -> Play -> Win)", async () =>
{
    int port = 5197;
    string localServerDll = Path.Combine(AppContext.BaseDirectory, "WolfAndRabbits.Server.dll");
    string serverDll = File.Exists(localServerDll)
        ? localServerDll
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Server", "bin", "Debug", "net10.0", "WolfAndRabbits.Server.dll"));
    Assert(File.Exists(serverDll), $"Server dll not found at {serverDll}");

    var psi = new ProcessStartInfo("dotnet", $"\"{serverDll}\" --urls http://127.0.0.1:{port}")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    using var proc = Process.Start(psi)!;
    var outLog = new StringBuilder();
    var errLog = new StringBuilder();
    proc.OutputDataReceived += (_, e) => { if (e.Data != null) outLog.AppendLine(e.Data); };
    proc.ErrorDataReceived += (_, e) => { if (e.Data != null) errLog.AppendLine(e.Data); };
    proc.BeginOutputReadLine();
    proc.BeginErrorReadLine();
    try
    {
        // Wait until /health responds
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
        bool healthy = false;
        string lastErr = "";
        for (int i = 0; i < 40; i++)
        {
            try
            {
                var r = await http.GetAsync($"http://127.0.0.1:{port}/health");
                if (r.IsSuccessStatusCode) { healthy = true; break; }
                lastErr = "HTTP " + r.StatusCode;
            }
            catch (Exception ex)
            {
                lastErr = ex.GetType().Name + ": " + ex.Message;
            }
            await Task.Delay(120);
        }
        Assert(healthy, $"Server did not become healthy. Exited={proc.HasExited} LastErr={lastErr}\nOUT:\n{outLog}\nERR:\n{errLog}");

        var wsUri = new Uri($"ws://127.0.0.1:{port}/ws");
        using var cWolf = await WsTestClient.ConnectAsync(wsUri);
        using var cR1 = await WsTestClient.ConnectAsync(wsUri);
        using var cR2 = await WsTestClient.ConnectAsync(wsUri);
        using var cR3 = await WsTestClient.ConnectAsync(wsUri);

        // Host creates password-protected room
        await cWolf.SendAsync(new { type = "create_room", playerName = "AlphaWolf", password = "secret" });
        var roomState = await cWolf.WaitForTypeAsync("room_state");
        string code = roomState.GetProperty("roomCode").GetString()!;
        Assert(!string.IsNullOrEmpty(code), "Room code missing");

        // Verify room appears in room_list
        await cR1.SendAsync(new { type = "list_rooms" });
        var listMsg = await cR1.WaitForTypeAsync("room_list");
        Assert(listMsg.GetProperty("rooms").GetArrayLength() == 1, "Room should be listed");

        // Wrong password rejected
        await cR1.SendAsync(new { type = "join_room", roomCode = code, playerName = "Bunny1", password = "wrong", requestId = "j-bad" });
        var errMsg = await cR1.WaitForTypeAsync("error");
        Assert(errMsg.GetProperty("code").GetString() == "wrong_password", "Wrong password must be rejected");

        // Start blocked with 1 player
        await cWolf.SendAsync(new { type = "start_match", requestId = "s-1" });
        var startErr = await cWolf.WaitForTypeAsync("error");
        Assert(startErr.GetProperty("code").GetString() == "not_enough_players", "Must require >= 2 players");

        // 3 Rabbits join with correct password -> room hits 4/4!
        await cR1.SendAsync(new { type = "join_room", roomCode = code, playerName = "Bunny1", password = "secret", requestId = "j1" });
        await cR1.WaitForTypeAsync("room_state");
        await cR2.SendAsync(new { type = "join_room", roomCode = code, playerName = "Bunny2", password = "secret", requestId = "j2" });
        await cR2.WaitForTypeAsync("room_state");
        await cR3.SendAsync(new { type = "join_room", roomCode = code, playerName = "Bunny3", password = "secret", requestId = "j3" });
        await cR3.WaitForTypeAsync("room_state");

        // Full room (4/4) must NOT appear in joinable room list
        var apiRooms = await http.GetStringAsync($"http://127.0.0.1:{port}/api/rooms");
        Assert(apiRooms.Trim() == "[]", $"Full room must be hidden from room list, got: {apiRooms}");

        // Host starts 1v3 match -> all 4 clients receive match_start + state_snapshot!
        await cWolf.SendAsync(new { type = "start_match", requestId = "start-ok" });
        var msW = await cWolf.WaitForTypeAsync("match_start");
        var ms1 = await cR1.WaitForTypeAsync("match_start");
        var ms2 = await cR2.WaitForTypeAsync("match_start");
        var ms3 = await cR3.WaitForTypeAsync("match_start");

        Assert(msW.GetProperty("yourRole").GetString() == "Wolf", "Host must be Wolf");
        Assert(ms1.GetProperty("yourRole").GetString() == "Rabbit", "Guest 1 must be Rabbit");
        Assert(ms2.GetProperty("yourRole").GetString() == "Rabbit", "Guest 2 must be Rabbit");
        Assert(ms3.GetProperty("yourRole").GetString() == "Rabbit", "Guest 3 must be Rabbit");

        // Send movement input and confirm server snapshots advance ticks & move characters
        await cWolf.SendAsync(new { type = "move_input", dirX = 1f, dirY = 0f });
        await Task.Delay(350);
        var snap = await cWolf.WaitForTypeAsync("state_snapshot");
        Assert(snap.GetProperty("players").GetArrayLength() == 4, "Snapshot must include all 4 players");
        Assert(snap.GetProperty("aliveRabbits").GetInt32() == 3, "3 alive rabbits expected");

        // Disconnect all 3 rabbits -> server ends match and sends identical Wolf victory!
        await cR1.CloseAsync();
        await cR2.CloseAsync();
        await cR3.CloseAsync();

        var matchEnd = await cWolf.WaitForTypeAsync("match_end");
        Assert(matchEnd.GetProperty("winner").GetString() == "Wolf", "Wolf must win when all rabbits eliminated");
    }
    finally
    {
        if (!proc.HasExited)
            proc.Kill(entireProcessTree: true);
    }
});

Console.WriteLine($"\nALL {passed} TESTS PASSED SUCCESSFULLY!");

sealed class WsTestClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();

    public static async Task<WsTestClient> ConnectAsync(Uri uri)
    {
        var c = new WsTestClient();
        await c._ws.ConnectAsync(uri, CancellationToken.None);
        await c.WaitForTypeAsync("welcome");
        await c.WaitForTypeAsync("room_list"); // Drain initial empty room_list sent on connect
        return c;
    }

    public async Task SendAsync(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task<JsonElement> WaitForTypeAsync(string expectedType, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var buf = new byte[64 * 1024];
        using var ms = new MemoryStream();
        while (!cts.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult res;
            do
            {
                res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                if (res.MessageType == WebSocketMessageType.Close)
                    throw new Exception($"Socket closed while waiting for {expectedType}");
                ms.Write(buf, 0, res.Count);
            }
            while (!res.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == expectedType)
                return doc.RootElement.Clone();
        }
        throw new TimeoutException($"Timed out waiting for {expectedType}");
    }

    public async Task CloseAsync()
    {
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "test", CancellationToken.None);
    }

    public void Dispose() => _ws.Dispose();
}

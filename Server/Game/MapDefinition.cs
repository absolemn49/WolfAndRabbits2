using WolfAndRabbits.Server.Protocol;

namespace WolfAndRabbits.Server.Game;

public enum TileType
{
    Grass = 0,
    Tree = 1,
    BaseFloor = 2,
    BaseEntrance = 3,
    CenterPlaza = 4,
    CenterShop = 5
}

public sealed class BaseInfo
{
    public int BaseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int EntranceX { get; init; }
    public int EntranceY { get; init; }
    public int EntranceDirX { get; init; }
    public int EntranceDirY { get; init; }
    public float CenterX { get; init; }
    public float CenterY { get; init; }
    public HashSet<(int x, int y)> FloorTiles { get; init; } = new();
}

public sealed class MapDefinition
{
    public int Width { get; }
    public int Height { get; }
    public TileType[] Tiles { get; }
    public int[] TileBaseIds { get; }
    public int ShopX { get; }
    public int ShopY { get; }
    public (float x, float y) WolfSpawn { get; }
    public IReadOnlyList<(float x, float y)> RabbitSpawns { get; }
    public IReadOnlyList<BaseInfo> Bases { get; }

    private MapDefinition(
        int width,
        int height,
        TileType[] tiles,
        int[] tileBaseIds,
        int shopX,
        int shopY,
        (float x, float y) wolfSpawn,
        List<(float x, float y)> rabbitSpawns,
        List<BaseInfo> bases)
    {
        Width = width;
        Height = height;
        Tiles = tiles;
        TileBaseIds = tileBaseIds;
        ShopX = shopX;
        ShopY = shopY;
        WolfSpawn = wolfSpawn;
        RabbitSpawns = rabbitSpawns;
        Bases = bases;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public TileType GetTile(int x, int y)
    {
        if (!InBounds(x, y)) return TileType.Tree;
        return Tiles[y * Width + x];
    }

    public int GetBaseId(int x, int y)
    {
        if (!InBounds(x, y)) return 0;
        return TileBaseIds[y * Width + x];
    }

    public bool IsSolidStaticTile(int x, int y)
    {
        var t = GetTile(x, y);
        return t == TileType.Tree || t == TileType.CenterShop;
    }

    public bool BlocksVisionOrProjectile(int x, int y)
    {
        return GetTile(x, y) == TileType.Tree;
    }

    public int[] ExportTilesAsIntArray()
    {
        var arr = new int[Tiles.Length];
        for (int i = 0; i < Tiles.Length; i++)
            arr[i] = (int)Tiles[i];
        return arr;
    }

    public List<BaseSummaryDto> ExportBases() =>
        Bases.Select(b => new BaseSummaryDto
        {
            baseId = b.BaseId,
            name = b.Name,
            entranceX = b.EntranceX,
            entranceY = b.EntranceY,
            entranceDirX = b.EntranceDirX,
            entranceDirY = b.EntranceDirY,
            centerX = b.CenterX,
            centerY = b.CenterY
        }).ToList();

    public static MapDefinition CreateDefaultForestMap()
    {
        const int W = GameConfig.MapWidth;
        const int H = GameConfig.MapHeight;
        var tiles = new TileType[W * H];
        var baseIds = new int[W * H];

        void SetTile(int x, int y, TileType t, int bId = 0)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            tiles[y * W + x] = t;
            baseIds[y * W + x] = bId;
        }

        // 1. Solid outer forest border
        for (int x = 0; x < W; x++)
        {
            SetTile(x, 0, TileType.Tree);
            SetTile(x, H - 1, TileType.Tree);
        }
        for (int y = 0; y < H; y++)
        {
            SetTile(0, y, TileType.Tree);
            SetTile(W - 1, y, TileType.Tree);
        }

        // 2. Center plaza & Equipment Shop
        int shopX = 22;
        int shopY = 22;
        for (int y = 18; y <= 25; y++)
        {
            for (int x = 18; x <= 25; x++)
            {
                SetTile(x, y, TileType.CenterPlaza);
            }
        }
        SetTile(shopX, shopY, TileType.CenterShop);

        // 3. Build 10 varied bases surrounded by trees with strictly 1 entrance tile each
        var bases = new List<BaseInfo>();

        void AddBase(int id, string name, IEnumerable<(int x, int y)> floorCells, (int x, int y) entrance, (int dx, int dy) dir)
        {
            var floorSet = new HashSet<(int x, int y)>(floorCells);
            foreach (var (fx, fy) in floorSet)
            {
                SetTile(fx, fy, TileType.BaseFloor, id);
            }

            // Wrap all 8-neighbors around the floor with trees
            foreach (var (fx, fy) in floorSet)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int nx = fx + ox;
                        int ny = fy + oy;
                        if (!floorSet.Contains((nx, ny)))
                        {
                            SetTile(nx, ny, TileType.Tree, 0);
                        }
                    }
                }
            }

            // Carve the single 1-tile entrance
            SetTile(entrance.x, entrance.y, TileType.BaseEntrance, id);

            float cx = (float)floorSet.Average(c => c.x + 0.5);
            float cy = (float)floorSet.Average(c => c.y + 0.5);
            bases.Add(new BaseInfo
            {
                BaseId = id,
                Name = name,
                EntranceX = entrance.x,
                EntranceY = entrance.y,
                EntranceDirX = dir.dx,
                EntranceDirY = dir.dy,
                CenterX = cx,
                CenterY = cy,
                FloorTiles = floorSet
            });
        }

        IEnumerable<(int x, int y)> Rect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    yield return (x, y);
        }

        // Base 1: NW Square Keep (Entrance East)
        AddBase(1, "Trại Tây Bắc", Rect(3, 36, 6, 39), (7, 37), (1, 0));

        // Base 2: North Inverted-L Sanctuary (Entrance South)
        AddBase(2, "Pháo Đài Bắc",
            Rect(15, 37, 20, 39).Concat(Rect(18, 35, 20, 36)),
            (19, 34), (0, -1));

        // Base 3: NE Tall Hall (Entrance West)
        AddBase(3, "Rừng Sâu Đông Bắc", Rect(36, 34, 39, 39), (35, 36), (-1, 0));

        // Base 4: North-East Stepped Courtyard (Entrance South)
        AddBase(4, "Đồi Đá Bắc",
            Rect(26, 34, 29, 37).Concat(Rect(27, 38, 28, 38)),
            (27, 33), (0, -1));

        // Base 5: West L-Sanctuary (Entrance East)
        AddBase(5, "Thung Lũng Tây",
            Rect(3, 20, 6, 24).Concat(Rect(7, 20, 8, 22)),
            (9, 21), (1, 0));

        // Base 6: East T-Bastion (Entrance West)
        AddBase(6, "Thành Cổ Đông",
            Rect(36, 19, 39, 24).Concat(Rect(34, 21, 35, 23)),
            (33, 22), (-1, 0));

        // Base 7: SW Wide Courtyard (Entrance North)
        AddBase(7, "Bãi Cỏ Tây Nam", Rect(3, 4, 8, 7), (6, 8), (0, 1));

        // Base 8: South-West L-Grove (Entrance East)
        AddBase(8, "Lùm Cây Nam",
            Rect(13, 4, 16, 7).Concat(Rect(15, 8, 16, 9)),
            (17, 6), (1, 0));

        // Base 9: South-East Rectangular Den (Entrance North)
        AddBase(9, "Hang Thỏ Nam", Rect(24, 4, 28, 7), (26, 8), (0, 1));

        // Base 10: SE Inverted-L Fortress (Entrance West)
        AddBase(10, "Khu Rừng Đông Nam",
            Rect(35, 4, 39, 7).Concat(Rect(37, 8, 39, 10)),
            (34, 6), (-1, 0));

        var wolfSpawn = (22.5f, 19.5f);
        var rabbitSpawns = new List<(float x, float y)>
        {
            (10.5f, 30.5f),
            (32.5f, 29.5f),
            (10.5f, 13.5f),
            (31.5f, 13.5f)
        };

        // 4. Scattered aesthetic & tactical forest tree clusters in open corridors
        var clusters = new (int x, int y)[]
        {
            // North-West corridor cover
            (11, 36), (11, 37), (12, 37),
            (5, 30), (6, 30), (5, 31),
            (14, 29), (15, 29), (15, 30),
            // North-East corridor cover
            (23, 30), (24, 30), (23, 29),
            (32, 36), (32, 37),
            (29, 28), (30, 28), (30, 29),
            // West & East flanks
            (13, 23), (14, 23), (14, 24),
            (13, 18), (13, 19),
            (29, 23), (29, 24), (28, 24),
            (29, 18), (30, 18),
            // South-West corridor cover
            (5, 14), (6, 14), (6, 15),
            (14, 14), (15, 14), (15, 13),
            (20, 11), (20, 12), (21, 11),
            // South-East corridor cover
            (27, 13), (28, 13), (28, 14),
            (37, 14), (38, 14), (38, 15),
            (31, 9), (31, 10),
            // Edge accents
            (11, 41), (23, 41), (33, 41),
            (1, 15), (1, 29), (42, 15), (42, 29),
            (20, 2), (31, 2)
        };

        foreach (var (tx, ty) in clusters)
        {
            if (tx <= 0 || ty <= 0 || tx >= W - 1 || ty >= H - 1) continue;
            if (tiles[ty * W + tx] != TileType.Grass) continue;

            // Keep clear of spawn points and base entrances
            bool tooClose = false;
            foreach (var b in bases)
            {
                if (Math.Abs(tx - b.EntranceX) <= 2 && Math.Abs(ty - b.EntranceY) <= 2)
                {
                    tooClose = true;
                    break;
                }
            }
            foreach (var sp in rabbitSpawns)
            {
                if (Math.Abs(tx + 0.5f - sp.x) < 2.2f && Math.Abs(ty + 0.5f - sp.y) < 2.2f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose)
                SetTile(tx, ty, TileType.Tree);
        }

        var map = new MapDefinition(W, H, tiles, baseIds, shopX, shopY, wolfSpawn, rabbitSpawns, bases);
        map.Validate();
        return map;
    }

    private void Validate()
    {
        if (Bases.Count != 10)
            throw new InvalidOperationException($"Expected 10 bases, got {Bases.Count}.");

        // Verify each base has strictly 1 entrance opening to outside and no leaks
        foreach (var b in Bases)
        {
            int entranceOpenings = 0;
            foreach (var (fx, fy) in b.FloorTiles)
            {
                var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
                foreach (var (dx, dy) in dirs)
                {
                    int nx = fx + dx;
                    int ny = fy + dy;
                    var nt = GetTile(nx, ny);
                    if (nt == TileType.BaseFloor && GetBaseId(nx, ny) == b.BaseId) continue;
                    if (nt == TileType.Tree) continue;
                    if (nt == TileType.BaseEntrance && nx == b.EntranceX && ny == b.EntranceY)
                    {
                        entranceOpenings++;
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Base {b.BaseId} has invalid leak at ({nx},{ny}) of type {nt}.");
                }
            }
            if (entranceOpenings == 0)
                throw new InvalidOperationException($"Base {b.BaseId} entrance is not adjacent to floor.");
        }

        // BFS from Wolf spawn to verify all base entrances & rabbit spawns are reachable
        var visited = new bool[Width * Height];
        var q = new Queue<(int x, int y)>();
        int startX = (int)MathF.Floor(WolfSpawn.x);
        int startY = (int)MathF.Floor(WolfSpawn.y);
        visited[startY * Width + startX] = true;
        q.Enqueue((startX, startY));

        var cardinals = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            foreach (var (dx, dy) in cardinals)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!InBounds(nx, ny) || visited[ny * Width + nx]) continue;
                if (IsSolidStaticTile(nx, ny)) continue;
                visited[ny * Width + nx] = true;
                q.Enqueue((nx, ny));
            }
        }

        foreach (var b in Bases)
        {
            if (!visited[b.EntranceY * Width + b.EntranceX])
                throw new InvalidOperationException($"Base {b.BaseId} entrance ({b.EntranceX},{b.EntranceY}) is unreachable!");
        }
        foreach (var sp in RabbitSpawns)
        {
            int sx = (int)MathF.Floor(sp.x);
            int sy = (int)MathF.Floor(sp.y);
            if (!visited[sy * Width + sx])
                throw new InvalidOperationException($"Rabbit spawn ({sx},{sy}) is unreachable!");
        }
    }
}

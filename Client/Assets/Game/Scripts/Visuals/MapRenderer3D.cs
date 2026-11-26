using System.Collections.Generic;
using UnityEngine;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Visuals
{
    public class MapRenderer3D : MonoBehaviour
    {
        private GameObject _mapRoot;
        private Transform _shopCrystal;
        private Transform _shopRuneCircle;
        private Light _shopForgeLight;
        private float _shopBaseLightIntensity = 3.2f;

        private readonly List<GameObject> _highlightPool = new List<GameObject>();
        private readonly List<Vector2Int> _activeHighlightTiles = new List<Vector2Int>();
        private Material _validBuildHighlightMat;
        private Material _upgradeHighlightMat;
        private Material _entranceHighlightMat;

        private int _width;
        private int _height;
        private int[] _tiles;

        public void BuildMap(MatchStartMsg startMsg)
        {
            ClearMap();

            _width = startMsg.mapWidth;
            _height = startMsg.mapHeight;
            _tiles = startMsg.tiles;

            _mapRoot = new GameObject("Map3D_Root");
            _mapRoot.transform.SetParent(transform, false);

            BuildStylizedTerrainMesh(startMsg);
            BuildBatchedForestTrees(startMsg);
            BuildForestDecorations(startMsg);
            BuildCenterEquipmentShop(startMsg.shopX + 0.5f, startMsg.shopY + 0.5f, startMsg.shopInteractRadius);
            BuildBaseEntranceMarkers(startMsg.bases);
            BuildAmbientFireflies();
            PrepareHighlightMaterials();
        }

        public void BuildLobbyPreview()
        {
            const int pw = 44;
            const int ph = 44;
            int[] previewTiles = new int[pw * ph];
            for (int y = 0; y < ph; y++)
            {
                for (int x = 0; x < pw; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(22.5f, 22.5f));
                    if (x == 22 && y == 22)
                        previewTiles[y * pw + x] = (int)TileType.CenterShop;
                    else if (d < 4.5f)
                        previewTiles[y * pw + x] = (int)TileType.CenterPlaza;
                    else if (d > 8.5f && d < 14.5f && ((x * 7 + y * 13) % 3 != 0))
                        previewTiles[y * pw + x] = (int)TileType.Tree;
                    else
                        previewTiles[y * pw + x] = (int)TileType.Grass;
                }
            }

            MatchStartMsg fake = new MatchStartMsg
            {
                mapWidth = pw,
                mapHeight = ph,
                shopX = 22,
                shopY = 22,
                shopInteractRadius = 3.5f,
                tiles = previewTiles,
                tileBaseIds = new int[pw * ph],
                bases = new BaseSummaryDto[0]
            };

            BuildMap(fake);
        }

        public void ClearMap()
        {
            _shopCrystal = null;
            _shopRuneCircle = null;
            _shopForgeLight = null;
            _highlightPool.Clear();
            _activeHighlightTiles.Clear();

            if (_mapRoot != null)
            {
                Destroy(_mapRoot);
                _mapRoot = null;
            }
        }

        private void Update()
        {
            float t = Time.time;
            if (_shopCrystal != null)
            {
                _shopCrystal.Rotate(Vector3.up, 42f * Time.deltaTime, Space.World);
                Vector3 p = _shopCrystal.localPosition;
                p.y = 2.65f + Mathf.Sin(t * 2.4f) * 0.16f;
                _shopCrystal.localPosition = p;
            }

            if (_shopRuneCircle != null)
            {
                _shopRuneCircle.Rotate(Vector3.up, 18f * Time.deltaTime, Space.World);
            }

            if (_shopForgeLight != null)
            {
                _shopForgeLight.intensity = _shopBaseLightIntensity + Mathf.Sin(t * 7.3f) * 0.35f + Mathf.Cos(t * 11.7f) * 0.2f;
            }
        }

        public void SetBuildHighlights(List<Vector2Int> tiles, string mode)
        {
            _activeHighlightTiles.Clear();
            for (int i = 0; i < _highlightPool.Count; i++)
                _highlightPool[i].SetActive(false);

            if (tiles == null || tiles.Count == 0) return;

            Material mat = mode == "upgrade"
                ? _upgradeHighlightMat
                : (mode == "gate" ? _entranceHighlightMat : _validBuildHighlightMat);

            for (int i = 0; i < tiles.Count; i++)
            {
                GameObject quad = GetOrCreateHighlightQuad(i);
                quad.SetActive(true);
                quad.transform.position = new Vector3(tiles[i].x + 0.5f, 0.06f, tiles[i].y + 0.5f);
                MeshRenderer mr = quad.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
                _activeHighlightTiles.Add(tiles[i]);
            }
        }

        public bool IsHighlightedTile(int tx, int ty)
        {
            for (int i = 0; i < _activeHighlightTiles.Count; i++)
            {
                if (_activeHighlightTiles[i].x == tx && _activeHighlightTiles[i].y == ty)
                    return true;
            }
            return false;
        }

        private GameObject GetOrCreateHighlightQuad(int index)
        {
            while (_highlightPool.Count <= index)
            {
                GameObject q = StylizedMeshFactory.CreateMeshPart(
                    "BuildHighlight_" + _highlightPool.Count,
                    StylizedMeshFactory.GroundQuadMesh,
                    _mapRoot != null ? _mapRoot.transform : transform,
                    Vector3.zero,
                    new Vector3(0.94f, 1f, 0.94f),
                    _validBuildHighlightMat,
                    Vector3.zero,
                    castShadows: false);
                q.SetActive(false);
                _highlightPool.Add(q);
            }
            return _highlightPool[index];
        }

        private void PrepareHighlightMaterials()
        {
            _validBuildHighlightMat = StylizedMeshFactory.CreateGlowMaterial(
                new Color(0.28f, 1.35f, 0.58f, 0.85f), ProceduralTextureFactory.TileFrameTexture, 4.5f, 0.18f);
            _entranceHighlightMat = StylizedMeshFactory.CreateGlowMaterial(
                new Color(0.35f, 1.15f, 1.60f, 0.90f), ProceduralTextureFactory.TileFrameTexture, 5.0f, 0.22f);
            _upgradeHighlightMat = StylizedMeshFactory.CreateGlowMaterial(
                new Color(1.65f, 1.25f, 0.28f, 0.92f), ProceduralTextureFactory.TileFrameTexture, 5.5f, 0.25f);
        }

        private void BuildStylizedTerrainMesh(MatchStartMsg startMsg)
        {
            // Subdivided 2x2 per tile terrain with painterly Warcraft 3 vertex color palette
            int sub = 2;
            int vxCount = _width * sub + 1;
            int vzCount = _height * sub + 1;

            Vector3[] verts = new Vector3[vxCount * vzCount];
            Vector2[] uvs = new Vector2[vxCount * vzCount];
            Color[] colors = new Color[vxCount * vzCount];
            int[] tris = new int[_width * sub * _height * sub * 6];

            Color grassLush = new Color(0.24f, 0.58f, 0.26f);
            Color grassSunlit = new Color(0.38f, 0.68f, 0.25f);
            Color grassDeep = new Color(0.16f, 0.41f, 0.22f);
            Color baseFloorCol = new Color(0.66f, 0.57f, 0.40f);
            Color baseEntranceCol = new Color(0.84f, 0.72f, 0.44f);
            Color plazaCol = new Color(0.58f, 0.59f, 0.62f);
            Color shopCoreCol = new Color(0.78f, 0.70f, 0.45f);

            for (int iz = 0; iz < vzCount; iz++)
            {
                for (int ix = 0; ix < vxCount; ix++)
                {
                    float wx = ix / (float)sub;
                    float wz = iz / (float)sub;
                    int idx = iz * vxCount + ix;

                    // Sample surrounding tiles for smooth natural color transitions
                    Color colSum = Color.black;
                    int samples = 0;
                    bool nearTree = false;
                    for (int oz = -1; oz <= 0; oz++)
                    {
                        for (int ox = -1; ox <= 0; ox++)
                        {
                            int tx = Mathf.Clamp(Mathf.FloorToInt(wx) + ox, 0, _width - 1);
                            int ty = Mathf.Clamp(Mathf.FloorToInt(wz) + oz, 0, _height - 1);
                            TileType tt = (TileType)_tiles[ty * _width + tx];
                            float n = Mathf.PerlinNoise(wx * 0.17f + 3.1f, wz * 0.17f + 5.7f);
                            Color tileCol;
                            switch (tt)
                            {
                                case TileType.BaseFloor:
                                    tileCol = Color.Lerp(baseFloorCol * 0.90f, baseFloorCol * 1.08f, n);
                                    break;
                                case TileType.BaseEntrance:
                                    tileCol = baseEntranceCol;
                                    break;
                                case TileType.CenterPlaza:
                                    float distCenter = Vector2.Distance(new Vector2(wx, wz), new Vector2(startMsg.shopX + 0.5f, startMsg.shopY + 0.5f));
                                    tileCol = Color.Lerp(shopCoreCol, plazaCol, Mathf.Clamp01(distCenter / 4.2f));
                                    break;
                                case TileType.CenterShop:
                                    tileCol = shopCoreCol;
                                    break;
                                case TileType.Tree:
                                    nearTree = true;
                                    tileCol = Color.Lerp(grassDeep, grassLush * 0.85f, n);
                                    break;
                                default:
                                    tileCol = Color.Lerp(grassLush, grassSunlit, n);
                                    break;
                            }
                            colSum += tileCol;
                            samples++;
                        }
                    }

                    // Gentle scenic relief under forest mounds while keeping playable lanes level
                    float y = nearTree ? (0.04f + Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f) * 0.10f) : 0f;

                    verts[idx] = new Vector3(wx, y, wz);
                    uvs[idx] = new Vector2(wx * 0.35f, wz * 0.35f);
                    colors[idx] = colSum / Mathf.Max(1, samples);
                }
            }

            int ti = 0;
            for (int iz = 0; iz < vzCount - 1; iz++)
            {
                for (int ix = 0; ix < vxCount - 1; ix++)
                {
                    int bl = iz * vxCount + ix;
                    int br = bl + 1;
                    int tl = bl + vxCount;
                    int tr = tl + 1;

                    tris[ti++] = bl; tris[ti++] = tl; tris[ti++] = tr;
                    tris[ti++] = bl; tris[ti++] = tr; tris[ti++] = br;
                }
            }

            Mesh mesh = new Mesh { name = "ForestTerrainMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject terrainGo = new GameObject("ForestTerrain");
            terrainGo.transform.SetParent(_mapRoot.transform, false);
            terrainGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = StylizedMeshFactory.CreateStylizedMaterial(
                Color.white,
                ProceduralTextureFactory.GrassTexture,
                smoothness: 0.20f,
                rimStrength: 0.08f,
                rimColor: new Color(0.8f, 0.95f, 0.7f, 1f),
                useVertexColor: true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Add subtle outer dark-forest border skirt so camera edges never show void
            GameObject skirt = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skirt.name = "OuterForestSkirt";
            skirt.transform.SetParent(_mapRoot.transform, false);
            skirt.transform.position = new Vector3(_width * 0.5f, -0.08f, _height * 0.5f);
            skirt.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            skirt.transform.localScale = new Vector3(_width + 50f, _height + 50f, 1f);
            Destroy(skirt.GetComponent<Collider>());
            skirt.GetComponent<Renderer>().sharedMaterial = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.08f, 0.16f, 0.12f), ProceduralTextureFactory.GrassTexture, 0.1f, 0.05f, fogInfluence: 0.6f);
        }

        private void BuildBatchedForestTrees(MatchStartMsg startMsg)
        {
            // Combine all stylized low-poly trunks & multi-tier swaying crowns for crisp 60fps on WebGL/Mobile!
            GameObject tempCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh cylMesh = tempCyl.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCyl);

            Mesh coneMesh = StylizedMeshFactory.Cone6Mesh;

            List<CombineInstance> trunkCombines = new List<CombineInstance>();
            List<CombineInstance> crownBrightCombines = new List<CombineInstance>();
            List<CombineInstance> crownEmeraldCombines = new List<CombineInstance>();
            List<CombineInstance> crownTealCombines = new List<CombineInstance>();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_tiles[y * _width + x] != (int)TileType.Tree) continue;

                    int hash = ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                    float r1 = (hash % 1000) / 1000f;
                    float r2 = ((hash / 1000) % 1000) / 1000f;
                    float r3 = ((hash / 1000000) % 1000) / 1000f;

                    float scale = 0.86f + r1 * 0.34f;
                    float yaw = r2 * 360f;
                    float jx = (r1 - 0.5f) * 0.16f;
                    float jz = (r2 - 0.5f) * 0.16f;
                    Vector3 basePos = new Vector3(x + 0.5f + jx, 0f, y + 0.5f + jz);

                    // Gnarled trunk
                    trunkCombines.Add(new CombineInstance
                    {
                        mesh = cylMesh,
                        transform = Matrix4x4.TRS(
                            basePos + new Vector3(0f, 0.42f * scale, 0f),
                            Quaternion.Euler((r3 - 0.5f) * 6f, yaw, (r1 - 0.5f) * 6f),
                            new Vector3(0.24f * scale, 0.46f * scale, 0.24f * scale))
                    });

                    // Choose lush WC3 foliage palette bucket
                    List<CombineInstance> targetCrown = r3 < 0.33f
                        ? crownBrightCombines
                        : (r3 < 0.68f ? crownEmeraldCombines : crownTealCombines);

                    // 3 stacked faceted Warcraft pine/cypress tiers
                    targetCrown.Add(new CombineInstance
                    {
                        mesh = coneMesh,
                        transform = Matrix4x4.TRS(
                            basePos + new Vector3(0f, 0.55f * scale, 0f),
                            Quaternion.Euler(0f, yaw, 0f),
                            new Vector3(1.18f * scale, 1.05f * scale, 1.18f * scale))
                    });
                    targetCrown.Add(new CombineInstance
                    {
                        mesh = coneMesh,
                        transform = Matrix4x4.TRS(
                            basePos + new Vector3(0f, 1.18f * scale, 0f),
                            Quaternion.Euler(0f, yaw + 25f, 0f),
                            new Vector3(0.92f * scale, 0.96f * scale, 0.92f * scale))
                    });
                    targetCrown.Add(new CombineInstance
                    {
                        mesh = coneMesh,
                        transform = Matrix4x4.TRS(
                            basePos + new Vector3(0f, 1.78f * scale, 0f),
                            Quaternion.Euler(0f, yaw + 55f, 0f),
                            new Vector3(0.62f * scale, 0.85f * scale, 0.62f * scale))
                    });
                }
            }

            Material trunkMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.42f, 0.27f, 0.16f), ProceduralTextureFactory.WoodTexture,
                smoothness: 0.18f, rimStrength: 0.18f, rimColor: new Color(0.75f, 0.55f, 0.35f));

            Material crownBrightMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.22f, 0.64f, 0.26f), ProceduralTextureFactory.GrassTexture,
                smoothness: 0.30f, rimStrength: 0.45f, rimColor: new Color(0.78f, 1.0f, 0.45f), windSway: 0.08f);

            Material crownEmeraldMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.14f, 0.50f, 0.28f), ProceduralTextureFactory.GrassTexture,
                smoothness: 0.28f, rimStrength: 0.42f, rimColor: new Color(0.55f, 0.96f, 0.65f), windSway: 0.08f);

            Material crownTealMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.11f, 0.42f, 0.36f), ProceduralTextureFactory.GrassTexture,
                smoothness: 0.34f, rimStrength: 0.48f, rimColor: new Color(0.45f, 0.90f, 0.92f), windSway: 0.08f);

            CreateCombinedBatch("ForestTrunks", trunkCombines, trunkMat);
            CreateCombinedBatch("ForestCrowns_Sunlit", crownBrightCombines, crownBrightMat);
            CreateCombinedBatch("ForestCrowns_Emerald", crownEmeraldCombines, crownEmeraldMat);
            CreateCombinedBatch("ForestCrowns_MysticTeal", crownTealCombines, crownTealMat);
        }

        private void BuildForestDecorations(MatchStartMsg startMsg)
        {
            // Sparkling enchanted blue mushrooms & golden forest flowers on non-blocking meadow edges
            Mesh crystalMesh = StylizedMeshFactory.OctaCrystalMesh;
            List<CombineInstance> mushroomCombines = new List<CombineInstance>();
            List<CombineInstance> flowerCombines = new List<CombineInstance>();

            for (int y = 1; y < _height - 1; y++)
            {
                for (int x = 1; x < _width - 1; x++)
                {
                    if (_tiles[y * _width + x] != (int)TileType.Grass) continue;
                    int h = ((x * 2654435761u) ^ (y * 2246822519u)).GetHashCode() & 0x7fffffff;
                    if (h % 13 == 0)
                    {
                        bool nearTree = _tiles[y * _width + (x + 1)] == (int)TileType.Tree ||
                                        _tiles[y * _width + (x - 1)] == (int)TileType.Tree ||
                                        _tiles[(y + 1) * _width + x] == (int)TileType.Tree ||
                                        _tiles[(y - 1) * _width + x] == (int)TileType.Tree;
                        float ox = ((h % 100) / 100f - 0.5f) * 0.6f;
                        float oz = (((h / 100) % 100) / 100f - 0.5f) * 0.6f;
                        Vector3 pos = new Vector3(x + 0.5f + ox, 0.09f, y + 0.5f + oz);

                        if (nearTree)
                        {
                            mushroomCombines.Add(new CombineInstance
                            {
                                mesh = crystalMesh,
                                transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, h % 360, 0f), new Vector3(0.24f, 0.22f, 0.24f))
                            });
                        }
                        else
                        {
                            flowerCombines.Add(new CombineInstance
                            {
                                mesh = crystalMesh,
                                transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, h % 360, 0f), new Vector3(0.20f, 0.14f, 0.20f))
                            });
                        }
                    }
                }
            }

            Material glowMushroomMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.30f, 0.88f, 1.0f),
                smoothness: 0.7f,
                rimStrength: 0.8f,
                rimColor: new Color(0.5f, 0.95f, 1f),
                emission: new Color(0.22f, 0.95f, 1.45f) * 1.15f);

            Material wildflowerMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(1.0f, 0.85f, 0.28f),
                smoothness: 0.5f,
                rimStrength: 0.6f,
                rimColor: new Color(1f, 0.95f, 0.55f),
                emission: new Color(0.85f, 0.62f, 0.12f) * 0.7f);

            CreateCombinedBatch("GlowingMushrooms", mushroomCombines, glowMushroomMat, castShadows: false);
            CreateCombinedBatch("Wildflowers", flowerCombines, wildflowerMat, castShadows: false);
        }

        private void BuildCenterEquipmentShop(float cx, float cz, float interactRadius)
        {
            GameObject shopRoot = new GameObject("CenterEquipmentShop");
            shopRoot.transform.SetParent(_mapRoot.transform, false);
            shopRoot.transform.position = new Vector3(cx, 0f, cz);

            Material stoneMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.62f, 0.60f, 0.58f), ProceduralTextureFactory.StoneTexture, 0.35f, 0.35f);
            Material woodMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.52f, 0.31f, 0.16f), ProceduralTextureFactory.WoodTexture, 0.25f, 0.25f);
            Material roofMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.76f, 0.18f, 0.16f), ProceduralTextureFactory.WoodTexture, 0.45f, 0.55f,
                rimColor: new Color(1f, 0.82f, 0.38f));
            Material goldTrimMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.98f, 0.78f, 0.22f), null, 0.82f, 0.65f,
                rimColor: new Color(1f, 0.95f, 0.6f), emission: new Color(0.6f, 0.42f, 0.06f));
            Material forgeEmberMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(1f, 0.45f, 0.1f), null, 0.6f, 0.9f,
                emission: new Color(2.4f, 0.85f, 0.15f));
            Material crystalMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.35f, 0.92f, 1.0f), null, 0.95f, 0.9f,
                rimColor: Color.white, emission: new Color(0.35f, 1.45f, 2.2f));

            // Stepped octagonal stone pedestal
            StylizedMeshFactory.CreatePart("PedestalBase", PrimitiveType.Cylinder, shopRoot.transform,
                new Vector3(0f, 0.12f, 0f), new Vector3(1.45f, 0.12f, 1.45f), stoneMat);
            StylizedMeshFactory.CreatePart("GoldRing", PrimitiveType.Cylinder, shopRoot.transform,
                new Vector3(0f, 0.22f, 0f), new Vector3(1.18f, 0.05f, 1.18f), goldTrimMat);

            // Four corner timber pillars supporting Warcraft pavilion roof
            Vector3[] pillars =
            {
                new Vector3(-0.38f, 0.72f, -0.38f),
                new Vector3(0.38f, 0.72f, -0.38f),
                new Vector3(-0.38f, 0.72f, 0.38f),
                new Vector3(0.38f, 0.72f, 0.38f)
            };
            for (int i = 0; i < pillars.Length; i++)
            {
                StylizedMeshFactory.CreatePart("Pillar_" + i, PrimitiveType.Cube, shopRoot.transform,
                    pillars[i], new Vector3(0.13f, 1.0f, 0.13f), woodMat);
            }

            // Glowing anvil & forge hearth inside
            StylizedMeshFactory.CreatePart("ForgeHearth", PrimitiveType.Cube, shopRoot.transform,
                new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.42f, 0.55f), stoneMat);
            StylizedMeshFactory.CreatePart("EmberCore", PrimitiveType.Sphere, shopRoot.transform,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.38f, 0.22f, 0.38f), forgeEmberMat, castShadows: false);

            // Stylized peaked crimson fantasy roof + golden spire trim
            StylizedMeshFactory.CreateMeshPart("Roof", StylizedMeshFactory.Cone6Mesh, shopRoot.transform,
                new Vector3(0f, 1.16f, 0f), new Vector3(1.75f, 1.05f, 1.75f), roofMat);
            StylizedMeshFactory.CreatePart("RoofCrown", PrimitiveType.Cylinder, shopRoot.transform,
                new Vector3(0f, 2.15f, 0f), new Vector3(0.24f, 0.12f, 0.24f), goldTrimMat);

            // Floating rotating arcane crystal above the forge!
            GameObject crystal = StylizedMeshFactory.CreateMeshPart("FloatingCrystal", StylizedMeshFactory.OctaCrystalMesh,
                shopRoot.transform, new Vector3(0f, 2.65f, 0f), new Vector3(0.62f, 0.78f, 0.62f), crystalMat, castShadows: false);
            _shopCrystal = crystal.transform;

            // Rotating glowing arcane shop boundary ring on plaza floor
            Material runeMat = StylizedMeshFactory.CreateGlowMaterial(
                new Color(1.45f, 1.08f, 0.38f, 0.55f), ProceduralTextureFactory.RuneRingTexture, 2.0f, 0.12f);
            GameObject runeRing = StylizedMeshFactory.CreateMeshPart("ShopRuneCircle", StylizedMeshFactory.GroundQuadMesh,
                shopRoot.transform, new Vector3(0f, 0.04f, 0f),
                new Vector3(interactRadius * 1.95f, 1f, interactRadius * 1.95f), runeMat, castShadows: false);
            _shopRuneCircle = runeRing.transform;

            // Warm flickering forge Point Light (illuminates the center plaza in URP Forward+!)
            GameObject lightGo = new GameObject("ForgePointLight");
            lightGo.transform.SetParent(shopRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            _shopForgeLight = lightGo.AddComponent<Light>();
            _shopForgeLight.type = LightType.Point;
            _shopForgeLight.color = new Color(1.0f, 0.72f, 0.32f);
            _shopForgeLight.range = 10.5f;
            _shopForgeLight.intensity = _shopBaseLightIntensity;
            _shopForgeLight.shadows = LightShadows.None;
        }

        private void BuildBaseEntranceMarkers(BaseSummaryDto[] bases)
        {
            if (bases == null) return;

            Material stoneMarkerMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.55f, 0.56f, 0.60f), ProceduralTextureFactory.StoneTexture, 0.4f, 0.35f);
            Material lanternGlowMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(1.0f, 0.88f, 0.45f), null, 0.8f, 0.9f,
                emission: new Color(1.7f, 1.25f, 0.35f));

            foreach (BaseSummaryDto b in bases)
            {
                // Place twin small glowing stone boundary beacons on either side of the entrance
                Vector3 center = new Vector3(b.entranceX + 0.5f, 0f, b.entranceY + 0.5f);
                Vector3 perp = new Vector3(-b.entranceDirY, 0f, b.entranceDirX);
                Vector3 outDir = new Vector3(b.entranceDirX, 0f, b.entranceDirY);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 postPos = center + perp * (s * 0.46f) + outDir * 0.42f;
                    GameObject post = new GameObject("BaseBeacon_" + b.baseId + "_" + s);
                    post.transform.SetParent(_mapRoot.transform, false);
                    post.transform.position = postPos;

                    StylizedMeshFactory.CreatePart("Pillar", PrimitiveType.Cube, post.transform,
                        new Vector3(0f, 0.24f, 0f), new Vector3(0.16f, 0.48f, 0.16f), stoneMarkerMat);
                    StylizedMeshFactory.CreateMeshPart("Gem", StylizedMeshFactory.OctaCrystalMesh, post.transform,
                        new Vector3(0f, 0.56f, 0f), new Vector3(0.22f, 0.26f, 0.22f), lanternGlowMat, castShadows: false);
                }
            }
        }

        private void BuildAmbientFireflies()
        {
            // Floating golden fairy fireflies across the enchanted Warcraft forest!
            GameObject psGo = new GameObject("EnchantedFireflies");
            psGo.transform.SetParent(_mapRoot.transform, false);
            psGo.transform.position = new Vector3(_width * 0.5f, 1.4f, _height * 0.5f);

            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.48f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.maxParticles = 140;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.94f, 0.48f, 0.95f),
                new Color(0.55f, 1.0f, 0.82f, 0.95f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 20f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_width - 4f, 2.2f, _height - 4f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.25f), new GradientAlphaKey(0.85f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ParticleSystemRenderer psr = psGo.GetComponent<ParticleSystemRenderer>();
            psr.material = StylizedMeshFactory.CreateGlowMaterial(
                new Color(1.9f, 1.65f, 0.7f, 1f), ProceduralTextureFactory.SoftGlowTexture);
        }

        private void CreateCombinedBatch(string name, List<CombineInstance> combines, Material mat, bool castShadows = true)
        {
            if (combines.Count == 0) return;

            Mesh combined = new Mesh { name = name };
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true);
            combined.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(_mapRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

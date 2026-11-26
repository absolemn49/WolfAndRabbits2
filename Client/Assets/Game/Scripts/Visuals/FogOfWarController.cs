using System.Collections.Generic;
using UnityEngine;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Visuals
{
    public class FogOfWarController : MonoBehaviour
    {
        private static readonly int FogTexProp = Shader.PropertyToID("_WRFogTex");
        private static readonly int MapBoundsProp = Shader.PropertyToID("_WRMapBounds");
        private static readonly int FogEnabledProp = Shader.PropertyToID("_WRFogEnabled");
        private static readonly int FogTintProp = Shader.PropertyToID("_WRFogTint");

        private int _width;
        private int _height;
        private int[] _tiles;
        private int[] _tileBaseIds;
        private BaseSummaryDto[] _bases;
        private float _visionRadius = 5.5f;

        private bool[] _visibleNow;
        private float[] _smoothVis;
        private Color32[] _pixels;
        private Texture2D _fogTexture;

        private readonly Dictionary<int, List<int>> _baseTileIndices = new Dictionary<int, List<int>>();
        private float _recalcTimer;
        private bool _initialized;

        public Texture2D FogTexture => _fogTexture;

        public void Initialize(MatchStartMsg startMsg)
        {
            _width = startMsg.mapWidth;
            _height = startMsg.mapHeight;
            _tiles = startMsg.tiles;
            _tileBaseIds = startMsg.tileBaseIds;
            _bases = startMsg.bases ?? new BaseSummaryDto[0];
            _visionRadius = startMsg.visionRadius > 0.5f ? startMsg.visionRadius : 5.5f;

            int count = _width * _height;
            _visibleNow = new bool[count];
            _smoothVis = new float[count];
            _pixels = new Color32[count];

            _baseTileIndices.Clear();
            for (int i = 0; i < count; i++)
            {
                int bId = _tileBaseIds != null && i < _tileBaseIds.Length ? _tileBaseIds[i] : 0;
                if (bId > 0)
                {
                    if (!_baseTileIndices.TryGetValue(bId, out List<int> list))
                    {
                        list = new List<int>();
                        _baseTileIndices[bId] = list;
                    }
                    list.Add(i);
                }
            }

            if (_fogTexture != null)
                Destroy(_fogTexture);

            _fogTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _fogTexture.wrapMode = TextureWrapMode.Clamp;
            _fogTexture.filterMode = FilterMode.Bilinear;

            Shader.SetGlobalTexture(FogTexProp, _fogTexture);
            Shader.SetGlobalVector(MapBoundsProp, new Vector4(0f, 0f, _width, _height));
            Shader.SetGlobalColor(FogTintProp, new Color(0.62f, 0.66f, 0.72f, 1f));
            Shader.SetGlobalFloat(FogEnabledProp, 1f);

            _initialized = true;
            _recalcTimer = 0f;
        }

        public void DisableFog()
        {
            _initialized = false;
            Shader.SetGlobalFloat(FogEnabledProp, 0f);
        }

        public void UpdateVision(float viewerX, float viewerY, bool viewerAlive, float dt, bool viewerSpawned = true)
        {
            if (!_initialized) return;

            _recalcTimer -= dt;
            if (_recalcTimer <= 0f)
            {
                _recalcTimer = 0.05f; // 20 Hz recalculation
                RecalculateVisibility(viewerX, viewerY, viewerAlive, viewerSpawned);
            }

            int count = _width * _height;
            float blendSpeed = 8.5f * dt;
            for (int i = 0; i < count; i++)
            {
                float target = _visibleNow[i] ? 1f : 0f;
                _smoothVis[i] = Mathf.MoveTowards(_smoothVis[i], target, blendSpeed);
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(_smoothVis[i] * 255f), 0, 255);
                _pixels[i] = new Color32(v, v, v, 255);
            }

            _fogTexture.SetPixels32(_pixels);
            _fogTexture.Apply(false);
        }

        public bool IsTileVisible(int x, int y)
        {
            if (!_initialized) return true;
            if (x < 0 || y < 0 || x >= _width || y >= _height) return false;
            return _visibleNow[y * _width + x];
        }

        public bool IsWorldPosVisible(float x, float y)
        {
            return IsTileVisible(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
        }

        private void RecalculateVisibility(float vx, float vy, bool viewerAlive, bool viewerSpawned)
        {
            int count = _width * _height;
            for (int i = 0; i < count; i++)
                _visibleNow[i] = false;

            if (!viewerAlive)
            {
                // Spectator mode when dead: reveal whole map gently
                for (int i = 0; i < count; i++)
                    _visibleNow[i] = true;
                return;
            }

            // Before Wolf spawns (first 30s Prep phase), keep map shrouded in Fog of War
            if (!viewerSpawned)
                return;

            int minX = Mathf.Max(0, Mathf.FloorToInt(vx - _visionRadius - 1f));
            int maxX = Mathf.Min(_width - 1, Mathf.CeilToInt(vx + _visionRadius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(vy - _visionRadius - 1f));
            int maxY = Mathf.Min(_height - 1, Mathf.CeilToInt(vy + _visionRadius + 1f));

            float rSq = _visionRadius * _visionRadius;

            for (int ty = minY; ty <= maxY; ty++)
            {
                for (int tx = minX; tx <= maxX; tx++)
                {
                    float cx = tx + 0.5f;
                    float cy = ty + 0.5f;
                    float dx = cx - vx;
                    float dy = cy - vy;
                    if (dx * dx + dy * dy > rSq) continue;

                    if (HasLineOfSight(vx, vy, cx, cy, tx, ty))
                        _visibleNow[ty * _width + tx] = true;
                }
            }

            // Ensure viewer's own tile is always visible
            int vtx = Mathf.Clamp(Mathf.FloorToInt(vx), 0, _width - 1);
            int vty = Mathf.Clamp(Mathf.FloorToInt(vy), 0, _height - 1);
            _visibleNow[vty * _width + vtx] = true;

            // Special Base Entrance Vision Rule:
            // Standing on a base entrance OR directly in front of it reveals the entire interior of that base!
            for (int i = 0; i < _bases.Length; i++)
            {
                BaseSummaryDto b = _bases[i];
                int frontX = b.entranceX + b.entranceDirX;
                int frontY = b.entranceY + b.entranceDirY;

                bool onEntrance = (vtx == b.entranceX && vty == b.entranceY);
                bool inFrontOfEntrance = (vtx == frontX && vty == frontY);
                float distToEntrance = Vector2.Distance(new Vector2(vx, vy), new Vector2(b.entranceX + 0.5f, b.entranceY + 0.5f));
                bool nearEntranceClear = distToEntrance <= 1.35f && HasLineOfSight(vx, vy, b.entranceX + 0.5f, b.entranceY + 0.5f, b.entranceX, b.entranceY);

                if (onEntrance || inFrontOfEntrance || nearEntranceClear)
                {
                    RevealWholeBase(b.baseId);
                }
            }
        }

        private void RevealWholeBase(int baseId)
        {
            if (!_baseTileIndices.TryGetValue(baseId, out List<int> cells)) return;
            for (int i = 0; i < cells.Count; i++)
            {
                int idx = cells[i];
                _visibleNow[idx] = true;

                // Also softly illuminate immediate tree wall edge framing the revealed base
                int cx = idx % _width;
                int cy = idx / _width;
                if (cx > 0) _visibleNow[cy * _width + (cx - 1)] = true;
                if (cx < _width - 1) _visibleNow[cy * _width + (cx + 1)] = true;
                if (cy > 0) _visibleNow[(cy - 1) * _width + cx] = true;
                if (cy < _height - 1) _visibleNow[(cy + 1) * _width + cx] = true;
            }
        }

        private bool HasLineOfSight(float x0, float y0, float x1, float y1, int targetTx, int targetTy)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < 0.1f) return true;

            int steps = Mathf.Max(4, Mathf.CeilToInt(dist * 4f));
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                int sx = Mathf.FloorToInt(x0 + dx * t);
                int sy = Mathf.FloorToInt(y0 + dy * t);
                if (sx == targetTx && sy == targetTy)
                    return true;
                if (sx < 0 || sy < 0 || sx >= _width || sy >= _height)
                    return false;
                if (_tiles[sy * _width + sx] == (int)TileType.Tree)
                    return false;
            }
            return true;
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(FogEnabledProp, 0f);
            if (_fogTexture != null)
                Destroy(_fogTexture);
        }
    }
}

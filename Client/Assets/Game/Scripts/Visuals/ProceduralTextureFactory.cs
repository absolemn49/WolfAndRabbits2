using System.Collections.Generic;
using UnityEngine;

namespace WolfAndRabbits.Client.Visuals
{
    public static class ProceduralTextureFactory
    {
        private static Texture2D _grassTex;
        private static Texture2D _stoneTex;
        private static Texture2D _woodTex;
        private static Texture2D _runeRingTex;
        private static Texture2D _selectionRingTex;
        private static Texture2D _softGlowTex;
        private static Texture2D _tileFrameTex;

        private static Sprite _circleSprite;
        private static Sprite _panelSprite;
        private static readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

        public static Texture2D GrassTexture => _grassTex != null ? _grassTex : (_grassTex = BuildGrassTexture(128));
        public static Texture2D StoneTexture => _stoneTex != null ? _stoneTex : (_stoneTex = BuildStoneTexture(128));
        public static Texture2D WoodTexture => _woodTex != null ? _woodTex : (_woodTex = BuildWoodTexture(128));
        public static Texture2D RuneRingTexture => _runeRingTex != null ? _runeRingTex : (_runeRingTex = BuildRuneRingTexture(256));
        public static Texture2D SelectionRingTexture => _selectionRingTex != null ? _selectionRingTex : (_selectionRingTex = BuildSelectionRingTexture(128));
        public static Texture2D SoftGlowTexture => _softGlowTex != null ? _softGlowTex : (_softGlowTex = BuildSoftGlowTexture(64));
        public static Texture2D TileFrameTexture => _tileFrameTex != null ? _tileFrameTex : (_tileFrameTex = BuildTileFrameTexture(64));

        public static Sprite CircleSprite => _circleSprite != null ? _circleSprite : (_circleSprite = ToSprite(BuildCircleTexture(96)));
        public static Sprite PanelSprite => _panelSprite != null ? _panelSprite : (_panelSprite = ToSprite(BuildPanelTexture(64), new Vector4(10, 10, 10, 10)));

        public static Sprite GetIcon(string key)
        {
            if (_iconCache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = BuildIconTexture(key, 64);
            Sprite sp = ToSprite(tex);
            _iconCache[key] = sp;
            return sp;
        }

        private static Sprite ToSprite(Texture2D tex, Vector4 border = default)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Texture2D BuildGrassTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float n1 = Mathf.PerlinNoise(u * 8f + 1.3f, v * 8f + 2.7f);
                    float n2 = Mathf.PerlinNoise(u * 22f + 7.1f, v * 22f + 4.3f);
                    float blade = Mathf.PerlinNoise(u * 48f, v * 16f);
                    float val = 0.80f + n1 * 0.16f + n2 * 0.08f + (blade - 0.5f) * 0.09f;
                    cols[y * size + x] = new Color(val * 0.96f, val * 1.03f, val * 0.90f, 1f);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildStoneTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int row = y / 16;
                    int bx = (x + (row % 2) * 16) % 32;
                    int by = y % 16;
                    bool mortar = bx <= 1 || by <= 1;
                    float noise = Mathf.PerlinNoise(x * 0.12f + 3f, y * 0.12f + 9f);
                    float shade = mortar ? 0.58f : (0.82f + noise * 0.22f);
                    cols[y * size + x] = new Color(shade, shade * 0.98f, shade * 0.93f, 1f);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildWoodTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.Sin((x * 0.45f) + Mathf.PerlinNoise(x * 0.08f, y * 0.03f) * 6f);
                    bool plankGap = (x % 24) == 0;
                    float v = plankGap ? 0.52f : (0.84f + grain * 0.10f);
                    cols[y * size + x] = new Color(v, v * 0.94f, v * 0.85f, 1f);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D BuildRuneRingTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);

                    float outerRing = Mathf.Exp(-Mathf.Pow((r - 0.86f) * 26f, 2f));
                    float innerRing = Mathf.Exp(-Mathf.Pow((r - 0.64f) * 26f, 2f));
                    float ticks = (r > 0.65f && r < 0.85f)
                        ? Mathf.SmoothStep(0.35f, 0.85f, Mathf.Abs(Mathf.Sin(ang * 12f))) * 0.7f
                        : 0f;
                    float star = (r < 0.62f)
                        ? Mathf.Exp(-Mathf.Pow((r - (0.38f + 0.15f * Mathf.Cos(ang * 6f))) * 22f, 2f)) * 0.65f
                        : 0f;
                    float softAura = Mathf.Clamp01(1f - r) * 0.14f;

                    float a = Mathf.Clamp01(outerRing + innerRing * 0.9f + ticks + star + softAura);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildSelectionRingTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];

            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.80f) * 16f, 2f));
                    float glow = Mathf.Exp(-Mathf.Pow((r - 0.78f) * 6f, 2f)) * 0.35f;
                    float a = Mathf.Clamp01(ring + glow);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildSoftGlowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildTileFrameTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float a = edge < 4 ? 0.95f : (edge < 8 ? 0.50f : 0.22f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildCircleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half + 0.5f) / half;
                    float dy = (y - half + 0.5f) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((0.97f - r) * 28f);
                    float rim = Mathf.Exp(-Mathf.Pow((r - 0.90f) * 20f, 2f)) * 0.25f;
                    float shade = Mathf.Clamp01(0.92f + (dy * 0.08f) + rim);
                    cols[y * size + x] = new Color(shade, shade, shade, a);
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildPanelTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    bool outerCorner = (x < 3 || x >= size - 3) && (y < 3 || y >= size - 3);
                    if (outerCorner)
                    {
                        cols[y * size + x] = Color.clear;
                    }
                    else if (edge < 3)
                    {
                        cols[y * size + x] = new Color(0.92f, 0.76f, 0.36f, 1f); // Warcraft gold trim
                    }
                    else if (edge == 3)
                    {
                        cols[y * size + x] = new Color(0.35f, 0.26f, 0.12f, 1f);
                    }
                    else
                    {
                        float grad = Mathf.Lerp(0.86f, 1.0f, y / (float)size);
                        cols[y * size + x] = new Color(grad, grad, grad, 1f);
                    }
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildIconTexture(string key, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.clear;

            void Plot(int px, int py, Color c)
            {
                if (px < 0 || py < 0 || px >= size || py >= size) return;
                Color bg = cols[py * size + px];
                cols[py * size + px] = Color.Lerp(bg, new Color(c.r, c.g, c.b, 1f), c.a);
                cols[py * size + px].a = Mathf.Max(bg.a, c.a);
            }

            void FillRect(int x0, int y0, int w, int h, Color c)
            {
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++)
                        Plot(x, y, c);
            }

            void FillCircle(float cx, float cy, float rad, Color c)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d <= rad)
                        {
                            float aa = Mathf.Clamp01((rad - d) * 1.5f);
                            Plot(x, y, new Color(c.r, c.g, c.b, c.a * aa));
                        }
                    }
                }
            }

            void DrawLine(float x0, float y0, float x1, float y1, float thick, Color c)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    FillCircle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thick, c);
                }
            }

            Color gold = new Color(1f, 0.84f, 0.22f, 1f);
            Color goldDark = new Color(0.76f, 0.52f, 0.08f, 1f);
            Color silver = new Color(0.86f, 0.92f, 0.98f, 1f);
            Color crimson = new Color(0.95f, 0.24f, 0.22f, 1f);
            Color emerald = new Color(0.28f, 0.90f, 0.45f, 1f);
            Color cyan = new Color(0.32f, 0.86f, 1f, 1f);
            Color wood = new Color(0.66f, 0.42f, 0.20f, 1f);

            switch (key)
            {
                case "coin":
                    FillCircle(32, 32, 24, goldDark);
                    FillCircle(32, 32, 20, gold);
                    FillCircle(32, 32, 14, new Color(1f, 0.93f, 0.48f, 1f));
                    FillRect(29, 20, 6, 24, goldDark);
                    FillRect(26, 29, 12, 6, goldDark);
                    break;

                case "build":
                    // Stylized Warcraft Hammer
                    DrawLine(16, 16, 44, 44, 4.2f, wood);
                    DrawLine(34, 52, 52, 34, 7.5f, silver);
                    DrawLine(36, 50, 50, 36, 4.5f, gold);
                    break;

                case "attack":
                    // Three fierce glowing claw slashes
                    DrawLine(14, 50, 44, 16, 3.6f, crimson);
                    DrawLine(22, 54, 52, 22, 3.8f, new Color(1f, 0.6f, 0.35f, 1f));
                    DrawLine(12, 38, 36, 12, 3.0f, crimson);
                    break;

                case "upgrade":
                    // Glowing upward arrow
                    FillRect(26, 14, 12, 22, emerald);
                    DrawLine(14, 34, 32, 52, 4.8f, emerald);
                    DrawLine(50, 34, 32, 52, 4.8f, emerald);
                    FillRect(28, 16, 8, 30, new Color(0.75f, 1f, 0.8f, 1f));
                    break;

                case "shop":
                    // Golden Anvil / Forge icon
                    FillRect(16, 14, 32, 8, silver);
                    FillRect(24, 22, 16, 12, silver);
                    FillRect(12, 34, 40, 12, gold);
                    DrawLine(10, 40, 20, 46, 4f, gold);
                    FillCircle(32, 52, 5, cyan);
                    break;

                case "follow":
                    // Target crosshair
                    FillCircle(32, 32, 22, new Color(1f, 0.9f, 0.45f, 1f));
                    FillCircle(32, 32, 17, new Color(0.12f, 0.16f, 0.22f, 1f));
                    FillCircle(32, 32, 6, gold);
                    FillRect(30, 4, 4, 14, gold);
                    FillRect(30, 46, 4, 14, gold);
                    FillRect(4, 30, 14, 4, gold);
                    FillRect(46, 30, 14, 4, gold);
                    break;

                case "lock":
                    FillCircle(32, 40, 13, gold);
                    FillCircle(32, 40, 8, new Color(0.12f, 0.14f, 0.18f, 1f));
                    FillRect(15, 12, 34, 26, gold);
                    FillCircle(32, 25, 4, new Color(0.2f, 0.14f, 0.04f, 1f));
                    break;

                case "gate":
                    FillRect(10, 10, 10, 42, wood);
                    FillRect(44, 10, 10, 42, wood);
                    FillRect(16, 14, 32, 32, new Color(0.55f, 0.36f, 0.18f, 1f));
                    FillRect(14, 22, 36, 5, silver);
                    FillRect(14, 36, 36, 5, silver);
                    FillRect(30, 14, 4, 32, new Color(0.28f, 0.16f, 0.06f, 1f));
                    break;

                case "gold_mine":
                    FillRect(14, 12, 36, 12, new Color(0.45f, 0.42f, 0.40f, 1f));
                    FillCircle(24, 28, 10, gold);
                    FillCircle(38, 28, 10, gold);
                    FillCircle(32, 38, 11, new Color(1f, 0.94f, 0.42f, 1f));
                    DrawLine(12, 20, 32, 54, 4f, wood);
                    DrawLine(52, 20, 32, 54, 4f, wood);
                    break;

                case "cannon":
                    FillCircle(24, 24, 14, new Color(0.32f, 0.36f, 0.42f, 1f));
                    DrawLine(22, 26, 50, 46, 7f, new Color(0.22f, 0.26f, 0.32f, 1f));
                    DrawLine(24, 28, 48, 45, 3.5f, silver);
                    FillCircle(52, 48, 6, new Color(1f, 0.55f, 0.15f, 1f));
                    FillRect(12, 10, 28, 10, wood);
                    break;

                case "settings":
                    // Crisp 8-tooth metallic-gold gear icon
                    for (int t = 0; t < 8; t++)
                    {
                        float ang = t * (Mathf.PI * 2f / 8f);
                        float tx = 32f + Mathf.Cos(ang) * 22f;
                        float ty = 32f + Mathf.Sin(ang) * 22f;
                        DrawLine(32f, 32f, tx, ty, 5.2f, goldDark);
                        DrawLine(32f, 32f, 32f + Mathf.Cos(ang) * 21f, 32f + Mathf.Sin(ang) * 21f, 3.6f, gold);
                    }
                    FillCircle(32, 32, 18.5f, goldDark);
                    FillCircle(32, 32, 16.0f, gold);
                    FillCircle(32, 32, 11.5f, new Color(1f, 0.92f, 0.50f, 1f));
                    FillCircle(32, 32, 8.2f, new Color(0.11f, 0.15f, 0.22f, 1f));
                    break;

                case "iron_claws":
                    // Three gleaming iron claw blades on dark hilt
                    DrawLine(14, 14, 24, 24, 5.5f, goldDark);
                    DrawLine(16, 48, 42, 14, 3.6f, silver);
                    DrawLine(24, 54, 50, 20, 4.0f, cyan);
                    DrawLine(34, 52, 54, 30, 3.2f, silver);
                    FillCircle(20, 20, 6.5f, gold);
                    break;

                case "siege_hammer":
                    // Heavy Warcraft siege warhammer with golden rune band
                    DrawLine(12, 12, 42, 42, 4.5f, wood);
                    DrawLine(30, 52, 54, 28, 9.5f, new Color(0.55f, 0.60f, 0.68f, 1f));
                    DrawLine(32, 50, 52, 30, 6.5f, silver);
                    DrawLine(38, 44, 46, 36, 4.5f, gold);
                    break;

                case "fury_gloves":
                    // Crimson berserker gauntlet with lightning accent
                    FillCircle(30, 26, 15, new Color(0.68f, 0.14f, 0.14f, 1f));
                    FillRect(18, 12, 24, 16, crimson);
                    FillRect(16, 34, 8, 16, crimson);
                    FillRect(26, 36, 8, 17, crimson);
                    FillRect(36, 34, 8, 15, crimson);
                    DrawLine(34, 16, 48, 34, 3.0f, gold);
                    DrawLine(48, 34, 38, 34, 2.5f, gold);
                    DrawLine(38, 34, 52, 52, 3.0f, new Color(1f, 0.96f, 0.55f, 1f));
                    break;

                case "swift_boots":
                    // Winged swift boots
                    FillRect(14, 14, 34, 12, new Color(0.20f, 0.48f, 0.82f, 1f));
                    FillRect(16, 22, 18, 24, cyan);
                    FillRect(14, 42, 22, 6, gold);
                    DrawLine(34, 30, 52, 44, 3.4f, Color.white);
                    DrawLine(34, 24, 54, 34, 3.0f, gold);
                    break;

                case "wind_cloak":
                    // Flowing wind cloak with teal breeze arcs
                    DrawLine(32, 52, 14, 14, 7.5f, new Color(0.14f, 0.58f, 0.68f, 1f));
                    DrawLine(32, 52, 50, 14, 7.5f, new Color(0.18f, 0.72f, 0.80f, 1f));
                    FillRect(20, 16, 24, 28, new Color(0.16f, 0.66f, 0.75f, 1f));
                    FillCircle(32, 50, 7, gold);
                    DrawLine(10, 30, 32, 36, 2.4f, Color.white);
                    DrawLine(28, 22, 54, 28, 2.4f, new Color(0.75f, 0.98f, 1f, 1f));
                    break;

                case "leather_armor":
                    // Studded leather breastplate
                    FillRect(16, 14, 32, 34, wood);
                    FillCircle(16, 44, 9, new Color(0.50f, 0.30f, 0.14f, 1f));
                    FillCircle(48, 44, 9, new Color(0.50f, 0.30f, 0.14f, 1f));
                    FillRect(22, 22, 20, 18, new Color(0.76f, 0.50f, 0.24f, 1f));
                    FillCircle(32, 34, 4.5f, gold);
                    break;

                case "iron_armor":
                    // Steel plate cuirass with glowing cyan rune core
                    FillRect(14, 12, 36, 36, new Color(0.52f, 0.58f, 0.66f, 1f));
                    FillCircle(14, 44, 10, silver);
                    FillCircle(50, 44, 10, silver);
                    FillRect(20, 18, 24, 26, silver);
                    FillCircle(32, 32, 7.5f, cyan);
                    FillCircle(32, 32, 4.0f, Color.white);
                    break;

                case "vitality_ring":
                    // Golden ring with ruby vitality gem
                    FillCircle(32, 28, 19, goldDark);
                    FillCircle(32, 28, 16, gold);
                    FillCircle(32, 28, 10.5f, new Color(0.10f, 0.13f, 0.18f, 1f));
                    FillCircle(32, 48, 9.5f, crimson);
                    FillCircle(30, 50, 4.0f, new Color(1f, 0.72f, 0.72f, 1f));
                    break;

                case "fruit_attack":
                    // Crimson Power Apple
                    FillCircle(27, 26, 15, new Color(0.78f, 0.12f, 0.14f, 1f));
                    FillCircle(37, 26, 15, crimson);
                    FillCircle(25, 32, 5.5f, new Color(1f, 0.65f, 0.58f, 1f));
                    DrawLine(32, 38, 34, 52, 2.8f, wood);
                    DrawLine(34, 46, 46, 52, 4.2f, emerald);
                    break;

                case "fruit_attack_speed":
                    // Golden-Orange Lightning Fruit
                    FillCircle(32, 28, 18, new Color(0.95f, 0.54f, 0.10f, 1f));
                    FillCircle(32, 30, 14, new Color(1f, 0.78f, 0.20f, 1f));
                    DrawLine(34, 16, 26, 30, 2.8f, Color.white);
                    DrawLine(26, 30, 36, 30, 2.5f, Color.white);
                    DrawLine(36, 30, 28, 42, 2.8f, Color.white);
                    DrawLine(32, 45, 44, 54, 4.0f, emerald);
                    break;

                case "fruit_move_speed":
                    // Wind-Berry (Cyan-Emerald)
                    FillCircle(32, 27, 17, new Color(0.14f, 0.74f, 0.88f, 1f));
                    FillCircle(32, 34, 12, cyan);
                    FillCircle(27, 36, 4.5f, Color.white);
                    DrawLine(32, 44, 30, 54, 2.6f, wood);
                    DrawLine(32, 48, 46, 54, 4.0f, emerald);
                    break;

                case "fruit_armor":
                    // Iron-Shell Fruit
                    FillCircle(32, 28, 19, new Color(0.42f, 0.48f, 0.56f, 1f));
                    FillCircle(32, 28, 15, silver);
                    FillCircle(32, 28, 9, new Color(0.55f, 0.66f, 0.78f, 1f));
                    DrawLine(22, 28, 42, 28, 2.5f, new Color(0.30f, 0.36f, 0.44f, 1f));
                    DrawLine(32, 45, 44, 54, 4.0f, gold);
                    break;

                case "fruit_hp":
                    // Longevity Peach (Pink-Gold)
                    FillCircle(32, 26, 18, new Color(0.96f, 0.42f, 0.56f, 1f));
                    FillCircle(32, 34, 13, new Color(1f, 0.68f, 0.74f, 1f));
                    FillCircle(26, 33, 5.0f, new Color(1f, 0.92f, 0.86f, 1f));
                    DrawLine(32, 44, 20, 52, 4.2f, emerald);
                    DrawLine(32, 44, 46, 52, 4.2f, emerald);
                    break;

                default:
                    FillCircle(32, 32, 16, new Color(0.75f, 0.80f, 0.88f, 0.65f));
                    break;
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Visuals
{
    public class VfxManager3D : MonoBehaviour
    {
        private class TimedEffect
        {
            public GameObject Go;
            public float Age;
            public float Lifetime;
            public Vector3 Velocity;
            public float StartScale;
            public float EndScale;
            public bool Billboard;
            public Renderer[] Renderers;
            public Text TextLabel;
        }

        private readonly Dictionary<int, GameObject> _projectileObjects = new Dictionary<int, GameObject>();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();
        private readonly HashSet<int> _seenProjIds = new HashSet<int>();

        private Material _slashMat;
        private Material _sparkGoldMat;
        private Material _sparkFireMat;
        private Material _sparkCyanMat;
        private Material _cannonBallMat;
        private Camera _cam;

        private void Awake()
        {
            _slashMat = StylizedMeshFactory.CreateGlowMaterial(new Color(2.4f, 0.35f, 0.22f, 0.92f), null);
            _sparkGoldMat = StylizedMeshFactory.CreateGlowMaterial(new Color(2.2f, 1.75f, 0.35f, 1f), ProceduralTextureFactory.SoftGlowTexture);
            _sparkFireMat = StylizedMeshFactory.CreateGlowMaterial(new Color(2.5f, 0.85f, 0.20f, 1f), ProceduralTextureFactory.SoftGlowTexture);
            _sparkCyanMat = StylizedMeshFactory.CreateGlowMaterial(new Color(0.45f, 1.85f, 2.5f, 1f), ProceduralTextureFactory.SoftGlowTexture);
            _cannonBallMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(1.0f, 0.55f, 0.15f), null, 0.95f, 0.9f,
                emission: new Color(2.8f, 1.15f, 0.22f), fogInfluence: 0.3f);
        }

        public void SyncProjectiles(ProjectileSnapshotDto[] projectiles, FogOfWarController fog)
        {
            _seenProjIds.Clear();

            if (projectiles != null)
            {
                for (int i = 0; i < projectiles.Length; i++)
                {
                    ProjectileSnapshotDto p = projectiles[i];
                    _seenProjIds.Add(p.projectileId);

                    if (!_projectileObjects.TryGetValue(p.projectileId, out GameObject go))
                    {
                        go = CreateCannonballVisual(p.level);
                        go.transform.position = new Vector3(p.x, 0.85f, p.y);
                        _projectileObjects[p.projectileId] = go;
                    }

                    Vector3 targetPos = new Vector3(p.x, 0.82f, p.y);
                    go.transform.position = Vector3.Lerp(go.transform.position, targetPos, 0.65f);

                    bool visible = fog == null || fog.IsWorldPosVisible(p.x, p.y);
                    if (go.activeSelf != visible)
                        go.SetActive(visible);
                }
            }

            List<int> toRemove = null;
            foreach (var kv in _projectileObjects)
            {
                if (!_seenProjIds.Contains(kv.Key))
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(kv.Key);
                }
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int id = toRemove[i];
                    if (_projectileObjects[id] != null)
                        Destroy(_projectileObjects[id]);
                    _projectileObjects.Remove(id);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var kv in _projectileObjects)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            _projectileObjects.Clear();

            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Go != null) Destroy(_effects[i].Go);
            }
            _effects.Clear();
        }

        public void SpawnSlashArc(Vector3 worldPos, Quaternion facing, int weaponTier)
        {
            GameObject go = StylizedMeshFactory.CreateMeshPart(
                "SlashVfx", StylizedMeshFactory.SlashArcMesh, transform,
                worldPos + new Vector3(0f, 0.62f, 0f), Vector3.one * (0.95f + weaponTier * 0.18f),
                new Material(_slashMat), Vector3.zero, castShadows: false);
            go.transform.rotation = facing;
            if (weaponTier >= 2)
                go.GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(2.6f, 1.5f, 0.25f, 0.95f));

            _effects.Add(new TimedEffect
            {
                Go = go,
                Lifetime = 0.24f,
                StartScale = 0.75f + weaponTier * 0.15f,
                EndScale = 1.35f + weaponTier * 0.2f,
                Renderers = go.GetComponentsInChildren<Renderer>()
            });
        }

        public void SpawnBurst(Vector3 worldPos, Color hdrColor, int count = 10, float radius = 0.8f)
        {
            Material mat = new Material(_sparkGoldMat);
            mat.SetColor("_BaseColor", hdrColor);

            for (int i = 0; i < count; i++)
            {
                float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float spd = Random.Range(1.2f, 3.4f) * radius;
                GameObject spark = StylizedMeshFactory.CreateMeshPart(
                    "Spark", StylizedMeshFactory.OctaCrystalMesh, transform,
                    worldPos + new Vector3(0f, 0.45f, 0f), Vector3.one * 0.22f, mat, castShadows: false);

                _effects.Add(new TimedEffect
                {
                    Go = spark,
                    Lifetime = Random.Range(0.32f, 0.58f),
                    Velocity = new Vector3(Mathf.Cos(ang) * spd, Random.Range(1.5f, 3.6f), Mathf.Sin(ang) * spd),
                    StartScale = Random.Range(0.18f, 0.30f),
                    EndScale = 0.02f,
                    Renderers = new[] { spark.GetComponent<Renderer>() }
                });
            }
        }

        public void SpawnRingPulse(Vector3 worldPos, Color hdrColor, float maxScale = 2.0f)
        {
            Material mat = StylizedMeshFactory.CreateGlowMaterial(hdrColor, ProceduralTextureFactory.SelectionRingTexture);
            GameObject ring = StylizedMeshFactory.CreateMeshPart(
                "RingPulse", StylizedMeshFactory.GroundQuadMesh, transform,
                worldPos + new Vector3(0f, 0.08f, 0f), Vector3.one * 0.4f, mat, castShadows: false);

            _effects.Add(new TimedEffect
            {
                Go = ring,
                Lifetime = 0.45f,
                StartScale = 0.45f,
                EndScale = maxScale,
                Renderers = new[] { ring.GetComponent<Renderer>() }
            });
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color)
        {
            GameObject go = new GameObject("FloatText_" + text);
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos + new Vector3(Random.Range(-0.2f, 0.2f), 1.35f, Random.Range(-0.2f, 0.2f));

            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            RectTransform rt = c.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180f, 44f);
            rt.localScale = Vector3.one * 0.012f;

            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = WorldHealthBar3D.GetSafeFont();
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.8f, -1.8f);

            _effects.Add(new TimedEffect
            {
                Go = go,
                Lifetime = 0.95f,
                Velocity = new Vector3(0f, 1.35f, 0f),
                StartScale = 0.011f,
                EndScale = 0.014f,
                Billboard = true,
                TextLabel = txt
            });
        }

        private GameObject CreateCannonballVisual(int level)
        {
            GameObject root = new GameObject("Cannonball_L" + level);
            root.transform.SetParent(transform, false);

            Material mat = new Material(_cannonBallMat);
            if (level == 3)
                mat.SetColor("_EmissionColor", new Color(0.55f, 1.95f, 2.8f));

            float sz = 0.24f + level * 0.04f;
            StylizedMeshFactory.CreatePart("Core", PrimitiveType.Sphere, root.transform,
                Vector3.zero, Vector3.one * sz, mat, castShadows: false);

            TrailRenderer tr = root.AddComponent<TrailRenderer>();
            tr.time = 0.22f;
            tr.startWidth = sz * 0.95f;
            tr.endWidth = 0.02f;
            tr.material = level == 3 ? _sparkCyanMat : _sparkFireMat;
            return root;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cam == null) _cam = Camera.main;

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                TimedEffect fx = _effects[i];
                fx.Age += dt;
                if (fx.Age >= fx.Lifetime || fx.Go == null)
                {
                    if (fx.Go != null) Destroy(fx.Go);
                    _effects.RemoveAt(i);
                    continue;
                }

                float t = fx.Age / fx.Lifetime;
                if (fx.Velocity.sqrMagnitude > 0.001f)
                {
                    fx.Go.transform.position += fx.Velocity * dt;
                    if (!fx.Billboard)
                        fx.Velocity.y -= 7.5f * dt; // Gravity on spark debris
                }

                float sc = Mathf.Lerp(fx.StartScale, fx.EndScale, t);
                fx.Go.transform.localScale = Vector3.one * sc;

                if (fx.Billboard && _cam != null)
                    fx.Go.transform.rotation = _cam.transform.rotation;

                if (fx.TextLabel != null)
                {
                    Color c = fx.TextLabel.color;
                    c.a = 1f - Mathf.Pow(t, 1.8f);
                    fx.TextLabel.color = c;
                }
                else if (fx.Renderers != null)
                {
                    float alpha = 1f - t;
                    for (int r = 0; r < fx.Renderers.Length; r++)
                    {
                        if (fx.Renderers[r] == null) continue;
                        Material m = fx.Renderers[r].material;
                        if (m.HasProperty("_BaseColor"))
                        {
                            Color c = m.GetColor("_BaseColor");
                            c.a = alpha;
                            m.SetColor("_BaseColor", c);
                        }
                    }
                }
            }
        }
    }
}

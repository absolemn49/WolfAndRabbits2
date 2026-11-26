using UnityEngine;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Visuals
{
    /// <summary>
    /// Stylized 3D building renderer supporting 3 distinct visual tiers for Gate, Gold Mine, and Cannon.
    /// </summary>
    public class BuildingVisual3D : MonoBehaviour
    {
        public int BuildingId { get; private set; }
        public string BuildingType { get; private set; }
        public int Level { get; private set; }
        public int TileX { get; private set; }
        public int TileY { get; private set; }

        private GameObject _modelRoot;
        private Transform _turretHead;
        private Transform _spinAccessory;
        private WorldHealthBar3D _hpBar;

        private float _popScale = 0.01f;
        private float _recoil;
        private float _hitShake;
        private float _targetAimAngle;

        public static BuildingVisual3D Create(BuildingSnapshotDto dto, float yawDegrees = 0f, Transform parent = null)
        {
            GameObject go = new GameObject("Building_" + dto.buildingType + "_L" + dto.level + "_" + dto.buildingId);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(dto.tileX + 0.5f, 0f, dto.tileY + 0.5f);
            go.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            BuildingVisual3D vis = go.AddComponent<BuildingVisual3D>();
            vis.BuildingId = dto.buildingId;
            vis.BuildingType = dto.buildingType;
            vis.TileX = dto.tileX;
            vis.TileY = dto.tileY;
            vis.RebuildTierModel(dto.level);
            vis.UpdateState(dto);
            return vis;
        }

        public void UpdateState(BuildingSnapshotDto dto)
        {
            if (dto.level != Level)
            {
                RebuildTierModel(dto.level);
                _popScale = 0.55f;
            }

            _targetAimAngle = dto.aimAngle;

            if (_hpBar != null)
            {
                _hpBar.SetProgress(dto.hp, dto.maxHp, GetBuildingTitle(BuildingType, Level));
                _hpBar.SetVisible(dto.hp < dto.maxHp || Level >= 2);
            }
        }

        public void TriggerHitEffect() => _hitShake = 0.18f;
        public void TriggerCannonRecoil() => _recoil = 0.24f;

        private void RebuildTierModel(int level)
        {
            Level = level;
            _turretHead = null;
            _spinAccessory = null;

            if (_modelRoot != null)
                Destroy(_modelRoot);

            _modelRoot = new GameObject("TierModel_L" + level);
            _modelRoot.transform.SetParent(transform, false);

            switch (BuildingType)
            {
                case GameConstants.BuildingGate: BuildGateTier(level); break;
                case GameConstants.BuildingGoldMine: BuildGoldMineTier(level); break;
                case GameConstants.BuildingCannon: BuildCannonTier(level); break;
            }

            if (_hpBar == null)
            {
                Color barCol = BuildingType == GameConstants.BuildingGate
                    ? new Color(0.28f, 0.78f, 1.0f)
                    : (BuildingType == GameConstants.BuildingGoldMine ? new Color(1.0f, 0.84f, 0.22f) : new Color(1.0f, 0.45f, 0.22f));
                _hpBar = WorldHealthBar3D.Attach(transform, new Vector3(0f, 1.72f, 0f), 1.2f, barCol, GetBuildingTitle(BuildingType, level));
            }
        }

        private void BuildGateTier(int level)
        {
            Material wood = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.56f, 0.36f, 0.18f), ProceduralTextureFactory.WoodTexture, 0.28f, 0.32f);
            Material stone = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.54f, 0.57f, 0.63f), ProceduralTextureFactory.StoneTexture, 0.38f, 0.38f);
            Material iron = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.32f, 0.36f, 0.44f), null, 0.78f, 0.55f);
            Material gold = StylizedMeshFactory.CreateStylizedMaterial(new Color(1.0f, 0.82f, 0.24f), null, 0.90f, 0.80f, emission: new Color(0.85f, 0.58f, 0.10f));
            Material runeGlow = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.35f, 0.88f, 1.0f), null, 0.95f, 0.9f, emission: new Color(0.45f, 1.45f, 2.2f));

            Material postMat = level == 1 ? wood : (level == 2 ? stone : gold);
            Material doorMat = level == 1 ? wood : (level == 2 ? iron : stone);

            float h = 0.86f + (level - 1) * 0.22f;
            StylizedMeshFactory.CreatePart("PostL", PrimitiveType.Cube, _modelRoot.transform, new Vector3(-0.40f, h * 0.5f, 0f), new Vector3(0.20f, h, 0.30f), postMat);
            StylizedMeshFactory.CreatePart("PostR", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0.40f, h * 0.5f, 0f), new Vector3(0.20f, h, 0.30f), postMat);
            StylizedMeshFactory.CreatePart("DoorPanel", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, h * 0.44f, 0f), new Vector3(0.68f, h * 0.82f, 0.20f), doorMat);
            StylizedMeshFactory.CreatePart("CrossBar", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, h * 0.52f, 0f), new Vector3(0.82f, 0.12f, 0.26f), level == 3 ? gold : iron);

            if (level >= 2)
            {
                StylizedMeshFactory.CreatePart("ArchBeam", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, h + 0.06f, 0f), new Vector3(1.02f, 0.18f, 0.34f), level == 3 ? gold : stone);
                StylizedMeshFactory.CreateMeshPart("TorchL", StylizedMeshFactory.OctaCrystalMesh, _modelRoot.transform, new Vector3(-0.40f, h + 0.24f, 0f), new Vector3(0.22f, 0.26f, 0.22f), runeGlow, castShadows: false);
                StylizedMeshFactory.CreateMeshPart("TorchR", StylizedMeshFactory.OctaCrystalMesh, _modelRoot.transform, new Vector3(0.40f, h + 0.24f, 0f), new Vector3(0.22f, 0.26f, 0.22f), runeGlow, castShadows: false);
            }

            if (level >= 3)
            {
                _spinAccessory = StylizedMeshFactory.CreateMeshPart("ArcaneKeystone", StylizedMeshFactory.OctaCrystalMesh,
                    _modelRoot.transform, new Vector3(0f, h + 0.42f, 0f), new Vector3(0.36f, 0.44f, 0.36f), runeGlow, castShadows: false).transform;

                StylizedMeshFactory.CreateMeshPart("ShieldAura", StylizedMeshFactory.GroundQuadMesh, _modelRoot.transform,
                    new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 1f, 1.2f),
                    StylizedMeshFactory.CreateGlowMaterial(new Color(0.45f, 1.2f, 1.8f, 0.55f), ProceduralTextureFactory.RuneRingTexture, 3.0f, 0.15f), castShadows: false);
            }
        }

        private void BuildGoldMineTier(int level)
        {
            Material wood = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.54f, 0.34f, 0.16f), ProceduralTextureFactory.WoodTexture, 0.25f, 0.25f);
            Material stone = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.46f, 0.45f, 0.48f), ProceduralTextureFactory.StoneTexture, 0.35f, 0.35f);
            Material goldOre = StylizedMeshFactory.CreateStylizedMaterial(new Color(1.0f, 0.84f, 0.20f), null, 0.92f, 0.85f,
                rimColor: Color.white, emission: new Color(1.1f, 0.78f, 0.10f) * (0.7f + level * 0.35f));

            StylizedMeshFactory.CreatePart("Plinth", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.86f, 0.28f, 0.86f), level == 1 ? wood : stone);
            StylizedMeshFactory.CreatePart("GoldPileMain", PrimitiveType.Sphere, _modelRoot.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.56f, 0.38f, 0.56f), goldOre);
            StylizedMeshFactory.CreatePart("GoldNugget1", PrimitiveType.Sphere, _modelRoot.transform, new Vector3(-0.22f, 0.32f, 0.18f), new Vector3(0.24f, 0.22f, 0.24f), goldOre);
            StylizedMeshFactory.CreatePart("GoldNugget2", PrimitiveType.Sphere, _modelRoot.transform, new Vector3(0.24f, 0.32f, -0.14f), new Vector3(0.26f, 0.24f, 0.26f), goldOre);

            if (level == 1)
            {
                StylizedMeshFactory.CreatePart("BeamL", PrimitiveType.Cube, _modelRoot.transform, new Vector3(-0.34f, 0.54f, 0f), new Vector3(0.11f, 0.62f, 0.11f), wood);
                StylizedMeshFactory.CreatePart("BeamR", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0.34f, 0.54f, 0f), new Vector3(0.11f, 0.62f, 0.11f), wood);
                StylizedMeshFactory.CreatePart("TopBeam", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, 0.84f, 0f), new Vector3(0.84f, 0.12f, 0.16f), wood);
            }
            else if (level == 2)
            {
                StylizedMeshFactory.CreatePart("RefineryWall", PrimitiveType.Cube, _modelRoot.transform, new Vector3(0f, 0.50f, -0.22f), new Vector3(0.76f, 0.56f, 0.32f), stone);
                _spinAccessory = StylizedMeshFactory.CreatePart("SpinningWheel", PrimitiveType.Cylinder, _modelRoot.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.46f, 0.08f, 0.46f), goldOre).transform;
            }
            else
            {
                StylizedMeshFactory.CreateMeshPart("VaultRoof", StylizedMeshFactory.Cone6Mesh, _modelRoot.transform, new Vector3(0f, 0.46f, 0f), new Vector3(0.95f, 0.52f, 0.95f), stone);
                _spinAccessory = StylizedMeshFactory.CreateMeshPart("GoldCrystal", StylizedMeshFactory.OctaCrystalMesh,
                    _modelRoot.transform, new Vector3(0f, 1.32f, 0f), new Vector3(0.50f, 0.66f, 0.50f), goldOre, castShadows: false).transform;

                GameObject lightGo = new GameObject("GoldGlowLight");
                lightGo.transform.SetParent(_modelRoot.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.82f, 0.28f);
                l.range = 4.5f;
                l.intensity = 2.0f;
            }
        }

        private void BuildCannonTier(int level)
        {
            Material wood = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.52f, 0.34f, 0.18f), ProceduralTextureFactory.WoodTexture, 0.26f, 0.28f);
            Material stone = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.48f, 0.50f, 0.55f), ProceduralTextureFactory.StoneTexture, 0.40f, 0.42f);
            Material iron = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.24f, 0.27f, 0.32f), null, 0.80f, 0.55f);
            Material bronze = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.86f, 0.60f, 0.24f), null, 0.86f, 0.72f);
            Material arcaneCore = StylizedMeshFactory.CreateStylizedMaterial(new Color(1.0f, 0.48f, 0.15f), null, 0.95f, 0.9f,
                emission: level == 3 ? new Color(2.4f, 0.95f, 0.25f) : new Color(1.4f, 0.45f, 0.12f));

            float baseH = 0.52f + (level - 1) * 0.20f;
            StylizedMeshFactory.CreatePart("TowerBase", PrimitiveType.Cylinder, _modelRoot.transform,
                new Vector3(0f, baseH * 0.5f, 0f), new Vector3(0.76f, baseH * 0.5f, 0.76f), level == 1 ? wood : stone);

            GameObject head = new GameObject("TurretHead");
            head.transform.SetParent(_modelRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, baseH + 0.18f, 0f);
            _turretHead = head.transform;

            StylizedMeshFactory.CreatePart("TurretBody", PrimitiveType.Sphere, _turretHead, Vector3.zero, new Vector3(0.46f, 0.38f, 0.46f), level == 3 ? bronze : iron);

            if (level == 1)
            {
                StylizedMeshFactory.CreatePart("Barrel", PrimitiveType.Cylinder, _turretHead, new Vector3(0f, 0.04f, 0.36f), new Vector3(0.18f, 0.34f, 0.18f), iron, new Vector3(90f, 0f, 0f));
            }
            else if (level == 2)
            {
                StylizedMeshFactory.CreatePart("BarrelL", PrimitiveType.Cylinder, _turretHead, new Vector3(-0.12f, 0.05f, 0.40f), new Vector3(0.15f, 0.38f, 0.15f), bronze, new Vector3(90f, 0f, 0f));
                StylizedMeshFactory.CreatePart("BarrelR", PrimitiveType.Cylinder, _turretHead, new Vector3(0.12f, 0.05f, 0.40f), new Vector3(0.15f, 0.38f, 0.15f), bronze, new Vector3(90f, 0f, 0f));
                StylizedMeshFactory.CreatePart("EmberCap", PrimitiveType.Sphere, _turretHead, new Vector3(0f, 0.20f, -0.04f), new Vector3(0.20f, 0.16f, 0.20f), arcaneCore, castShadows: false);
            }
            else
            {
                StylizedMeshFactory.CreatePart("ArcaneBarrel", PrimitiveType.Cylinder, _turretHead, new Vector3(0f, 0.06f, 0.46f), new Vector3(0.24f, 0.44f, 0.24f), bronze, new Vector3(90f, 0f, 0f));
                StylizedMeshFactory.CreatePart("PlasmaMuzzle", PrimitiveType.Sphere, _turretHead, new Vector3(0f, 0.06f, 0.88f), new Vector3(0.22f, 0.22f, 0.22f), arcaneCore, castShadows: false);
                _spinAccessory = StylizedMeshFactory.CreateMeshPart("ArcaneCrown", StylizedMeshFactory.OctaCrystalMesh,
                    _modelRoot.transform, new Vector3(0f, baseH + 0.72f, 0f), new Vector3(0.36f, 0.42f, 0.36f), arcaneCore, castShadows: false).transform;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_popScale < 1f)
            {
                _popScale = Mathf.MoveTowards(_popScale, 1f, dt * 4.8f);
                float ease = 1f + Mathf.Sin(_popScale * Mathf.PI) * 0.15f;
                float s = Mathf.Min(1.12f, _popScale * ease);
                if (_modelRoot != null) _modelRoot.transform.localScale = Vector3.one * s;
            }
            else if (_modelRoot != null)
            {
                _modelRoot.transform.localScale = Vector3.one;
            }

            if (_hitShake > 0f && _modelRoot != null)
            {
                _hitShake -= dt;
                _modelRoot.transform.localPosition = new Vector3(
                    Mathf.Sin(Time.time * 75f) * 0.07f * (_hitShake / 0.18f),
                    0f,
                    Mathf.Cos(Time.time * 68f) * 0.07f * (_hitShake / 0.18f));
            }
            else if (_modelRoot != null)
            {
                _modelRoot.transform.localPosition = Vector3.zero;
            }

            if (_turretHead != null)
            {
                float unityYaw = 90f - (_targetAimAngle * Mathf.Rad2Deg);
                _turretHead.rotation = Quaternion.Slerp(_turretHead.rotation, Quaternion.Euler(0f, unityYaw, 0f), Mathf.Clamp01(dt * 12f));

                if (_recoil > 0f)
                {
                    _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 1.4f);
                    Vector3 lp = _turretHead.localPosition;
                    lp.x = 0f; lp.z = 0f;
                    _turretHead.localPosition = lp - _turretHead.forward * _recoil;
                }
            }

            if (_spinAccessory != null)
                _spinAccessory.Rotate(Vector3.up, 72f * dt, Space.World);
        }

        private static string GetBuildingTitle(string type, int level)
        {
            string stars = new string('★', Mathf.Clamp(level, 1, 3));
            return LocalizationManager.GetBuildingName(type) + " " + stars;
        }
    }
}

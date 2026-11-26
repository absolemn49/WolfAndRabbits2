using UnityEngine;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Visuals
{
    /// <summary>
    /// Stylized 3D character renderer & animator for both Wolf (with visible 3D gear) and Rabbits.
    /// Smoothly interpolates 10Hz server snapshots at full frame rate.
    /// </summary>
    public class CharacterVisual3D : MonoBehaviour
    {
        public string PlayerId { get; private set; }
        public bool IsWolf { get; private set; }

        private Transform _bodyPivot;
        private Transform[] _limbs;
        private Transform _earLeft;
        private Transform _earRight;
        private Transform _tail;

        // Visible 3D Wolf equipment attachments
        private GameObject _clawsObj;
        private GameObject _hammerObj;
        private GameObject _leatherArmorObj;
        private GameObject _ironArmorObj;
        private GameObject _staminaBadgeRoot;
        private Renderer _staminaBadgeRenderer;

        private WorldHealthBar3D _hpBar;

        private Vector3 _targetPos;
        private Quaternion _targetRot = Quaternion.identity;
        private bool _isMoving;
        private float _animTime;
        private float _attackLungeTimer;
        private float _hitFlashTimer;
        private int _currentWeaponTier = -1;
        private int _currentArmorTier = -1;
        private int _currentStaminaTier = -1;

        private static readonly Color[] RabbitAccentColors =
        {
            new Color(0.24f, 0.86f, 0.48f),
            new Color(0.28f, 0.72f, 1.00f),
            new Color(1.00f, 0.42f, 0.72f),
            new Color(1.00f, 0.78f, 0.25f)
        };

        public static CharacterVisual3D Create(PlayerSnapshotDto dto, bool isLocal, Transform parent = null)
        {
            GameObject root = new GameObject("Char_" + dto.role + "_" + dto.name);
            if (parent != null) root.transform.SetParent(parent, false);

            CharacterVisual3D vis = root.AddComponent<CharacterVisual3D>();
            vis.PlayerId = dto.playerId;
            vis.IsWolf = dto.role == GameConstants.RoleWolf;
            vis._targetPos = new Vector3(dto.x, 0f, dto.y);
            root.transform.position = vis._targetPos;

            if (vis.IsWolf)
                vis.BuildWolfModel(dto, isLocal);
            else
                vis.BuildRabbitModel(dto, isLocal);

            vis.ApplySnapshot(dto, immediate: true);
            return vis;
        }

        public void ApplySnapshot(PlayerSnapshotDto dto, bool immediate = false)
        {
            _targetPos = new Vector3(dto.x, 0f, dto.y);
            if (immediate)
                transform.position = _targetPos;

            Vector3 look = new Vector3(dto.facingX, 0f, dto.facingY);
            if (look.sqrMagnitude > 0.001f)
            {
                _targetRot = Quaternion.LookRotation(look.normalized, Vector3.up);
                if (immediate)
                    transform.rotation = _targetRot;
            }

            _isMoving = dto.moving && dto.alive;
            _hpBar?.SetProgress(dto.hp, dto.maxHp);

            if (IsWolf)
                SyncWolfEquipment(dto.weaponTier, dto.armorTier, dto.staminaTier);
        }

        public void TriggerAttackAnim() => _attackLungeTimer = 0.26f;
        public void TriggerHitFlash() => _hitFlashTimer = 0.15f;

        public void SetRenderedVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private void BuildWolfModel(PlayerSnapshotDto dto, bool isLocal)
        {
            Color ringColor = isLocal ? new Color(1.6f, 0.35f, 0.22f, 0.9f) : new Color(1.3f, 0.2f, 0.2f, 0.7f);
            StylizedMeshFactory.CreateMeshPart("SelectionRing", StylizedMeshFactory.GroundQuadMesh, transform,
                new Vector3(0f, 0.05f, 0f), new Vector3(1.18f, 1f, 1.18f),
                StylizedMeshFactory.CreateGlowMaterial(ringColor, ProceduralTextureFactory.SelectionRingTexture, 3.0f, 0.12f),
                castShadows: false);

            _bodyPivot = new GameObject("BodyPivot").transform;
            _bodyPivot.SetParent(transform, false);

            Material furDark = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.24f, 0.26f, 0.31f), null, 0.32f, 0.48f, rimColor: new Color(0.95f, 0.48f, 0.32f));
            Material furMane = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.15f, 0.16f, 0.20f), null, 0.25f, 0.52f, rimColor: new Color(0.85f, 0.55f, 0.45f));
            Material furBelly = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.50f, 0.52f, 0.56f), null, 0.22f, 0.25f);
            Material eyeGlow = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(1f, 0.3f, 0.1f), null, 0.9f, 0.9f, emission: new Color(2.8f, 0.65f, 0.15f));

            StylizedMeshFactory.CreatePart("Torso", PrimitiveType.Cube, _bodyPivot,
                new Vector3(0f, 0.50f, -0.02f), new Vector3(0.46f, 0.36f, 0.78f), furDark);
            StylizedMeshFactory.CreatePart("ChestMane", PrimitiveType.Sphere, _bodyPivot,
                new Vector3(0f, 0.57f, 0.22f), new Vector3(0.56f, 0.46f, 0.48f), furMane);
            StylizedMeshFactory.CreatePart("Belly", PrimitiveType.Cube, _bodyPivot,
                new Vector3(0f, 0.37f, -0.02f), new Vector3(0.36f, 0.16f, 0.62f), furBelly);

            Transform head = new GameObject("Head").transform;
            head.SetParent(_bodyPivot, false);
            head.localPosition = new Vector3(0f, 0.72f, 0.44f);
            StylizedMeshFactory.CreatePart("Skull", PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.36f, 0.32f, 0.34f), furDark);
            StylizedMeshFactory.CreatePart("Snout", PrimitiveType.Cube, head, new Vector3(0f, -0.05f, 0.24f), new Vector3(0.20f, 0.16f, 0.26f), furBelly);
            StylizedMeshFactory.CreatePart("Nose", PrimitiveType.Sphere, head, new Vector3(0f, 0.0f, 0.37f), new Vector3(0.09f, 0.08f, 0.08f), furMane);
            StylizedMeshFactory.CreatePart("EyeL", PrimitiveType.Sphere, head, new Vector3(-0.11f, 0.06f, 0.15f), new Vector3(0.08f, 0.07f, 0.06f), eyeGlow, castShadows: false);
            StylizedMeshFactory.CreatePart("EyeR", PrimitiveType.Sphere, head, new Vector3(0.11f, 0.06f, 0.15f), new Vector3(0.08f, 0.07f, 0.06f), eyeGlow, castShadows: false);
            StylizedMeshFactory.CreateMeshPart("EarL", StylizedMeshFactory.Cone6Mesh, head, new Vector3(-0.12f, 0.15f, -0.02f), new Vector3(0.16f, 0.22f, 0.16f), furMane, new Vector3(-10f, 0f, 12f));
            StylizedMeshFactory.CreateMeshPart("EarR", StylizedMeshFactory.Cone6Mesh, head, new Vector3(0.12f, 0.15f, -0.02f), new Vector3(0.16f, 0.22f, 0.16f), furMane, new Vector3(-10f, 0f, -12f));

            _tail = StylizedMeshFactory.CreatePart("Tail", PrimitiveType.Cube, _bodyPivot,
                new Vector3(0f, 0.56f, -0.52f), new Vector3(0.16f, 0.16f, 0.44f), furMane, new Vector3(-25f, 0f, 0f)).transform;

            _limbs = new Transform[4];
            Vector3[] legPos =
            {
                new Vector3(-0.18f, 0.20f, 0.24f), new Vector3(0.18f, 0.20f, 0.24f),
                new Vector3(-0.18f, 0.20f, -0.26f), new Vector3(0.18f, 0.20f, -0.26f)
            };
            for (int i = 0; i < 4; i++)
                _limbs[i] = StylizedMeshFactory.CreatePart("Leg_" + i, PrimitiveType.Cube, _bodyPivot, legPos[i], new Vector3(0.14f, 0.40f, 0.14f), furDark).transform;

            BuildWolfEquipmentAttachments();

            _hpBar = WorldHealthBar3D.Attach(transform, new Vector3(0f, 1.82f, 0f), 1.35f,
                new Color(0.92f, 0.22f, 0.22f, 1f), LocalizationManager.Get("role.wolf.upper") + " - " + dto.name);
        }

        private void BuildWolfEquipmentAttachments()
        {
            // 1. Iron Claws
            _clawsObj = new GameObject("Equip_IronClaws");
            _clawsObj.transform.SetParent(_bodyPivot, false);
            Material clawMat = StylizedMeshFactory.CreateStylizedMaterial(
                new Color(0.86f, 0.94f, 1.0f), null, 0.92f, 0.85f, rimColor: Color.white, emission: new Color(0.45f, 0.85f, 1.25f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int c = -1; c <= 1; c++)
                {
                    StylizedMeshFactory.CreateMeshPart("Claw_" + side + "_" + c, StylizedMeshFactory.Cone6Mesh, _clawsObj.transform,
                        new Vector3(side * 0.22f + c * 0.05f, 0.18f, 0.46f), new Vector3(0.06f, 0.30f, 0.06f),
                        clawMat, new Vector3(78f, side * 12f, 0f));
                }
            }
            _clawsObj.SetActive(false);

            // 2. Siege Hammer
            _hammerObj = new GameObject("Equip_SiegeHammer");
            _hammerObj.transform.SetParent(_bodyPivot, false);
            Material handleMat = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.48f, 0.28f, 0.14f), ProceduralTextureFactory.WoodTexture, 0.3f, 0.3f);
            Material hammerSteel = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.76f, 0.80f, 0.88f), null, 0.88f, 0.75f);
            Material hammerGold = StylizedMeshFactory.CreateStylizedMaterial(new Color(1.0f, 0.80f, 0.24f), null, 0.9f, 0.9f, emission: new Color(1.45f, 0.85f, 0.18f));
            StylizedMeshFactory.CreatePart("HammerShaft", PrimitiveType.Cylinder, _hammerObj.transform, new Vector3(0.36f, 0.62f, 0.18f), new Vector3(0.07f, 0.56f, 0.07f), handleMat, new Vector3(42f, 0f, -16f));
            StylizedMeshFactory.CreatePart("HammerHead", PrimitiveType.Cube, _hammerObj.transform, new Vector3(0.46f, 0.98f, 0.54f), new Vector3(0.34f, 0.32f, 0.50f), hammerSteel, new Vector3(42f, 0f, -16f));
            StylizedMeshFactory.CreatePart("HammerRuneBand", PrimitiveType.Cube, _hammerObj.transform, new Vector3(0.46f, 0.98f, 0.54f), new Vector3(0.38f, 0.12f, 0.54f), hammerGold, new Vector3(42f, 0f, -16f));
            _hammerObj.SetActive(false);

            // 3. Leather Armor
            _leatherArmorObj = new GameObject("Equip_LeatherArmor");
            _leatherArmorObj.transform.SetParent(_bodyPivot, false);
            Material leatherMat = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.58f, 0.34f, 0.16f), ProceduralTextureFactory.WoodTexture, 0.4f, 0.45f, rimColor: new Color(1f, 0.78f, 0.45f));
            Material bronzeMat = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.88f, 0.66f, 0.26f), null, 0.8f, 0.6f);
            StylizedMeshFactory.CreatePart("LeatherSaddle", PrimitiveType.Cube, _leatherArmorObj.transform, new Vector3(0f, 0.64f, -0.02f), new Vector3(0.56f, 0.22f, 0.64f), leatherMat);
            StylizedMeshFactory.CreatePart("LeatherPauldronL", PrimitiveType.Sphere, _leatherArmorObj.transform, new Vector3(-0.30f, 0.64f, 0.22f), new Vector3(0.24f, 0.22f, 0.28f), leatherMat);
            StylizedMeshFactory.CreatePart("LeatherPauldronR", PrimitiveType.Sphere, _leatherArmorObj.transform, new Vector3(0.30f, 0.64f, 0.22f), new Vector3(0.24f, 0.22f, 0.28f), leatherMat);
            StylizedMeshFactory.CreatePart("LeatherStud", PrimitiveType.Sphere, _leatherArmorObj.transform, new Vector3(0f, 0.75f, 0.16f), new Vector3(0.14f, 0.08f, 0.14f), bronzeMat);
            _leatherArmorObj.SetActive(false);

            // 4. Iron Armor
            _ironArmorObj = new GameObject("Equip_IronArmor");
            _ironArmorObj.transform.SetParent(_bodyPivot, false);
            Material ironPlate = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.76f, 0.82f, 0.90f), null, 0.90f, 0.80f, rimColor: new Color(0.6f, 0.88f, 1f));
            Material runeCore = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.3f, 0.88f, 1f), null, 0.95f, 0.9f, emission: new Color(0.4f, 1.45f, 2.2f));
            StylizedMeshFactory.CreatePart("IronCuirass", PrimitiveType.Cube, _ironArmorObj.transform, new Vector3(0f, 0.66f, -0.02f), new Vector3(0.60f, 0.26f, 0.70f), ironPlate);
            StylizedMeshFactory.CreatePart("IronPauldronL", PrimitiveType.Sphere, _ironArmorObj.transform, new Vector3(-0.34f, 0.68f, 0.22f), new Vector3(0.28f, 0.28f, 0.34f), ironPlate);
            StylizedMeshFactory.CreatePart("IronPauldronR", PrimitiveType.Sphere, _ironArmorObj.transform, new Vector3(0.34f, 0.68f, 0.22f), new Vector3(0.28f, 0.28f, 0.34f), ironPlate);
            StylizedMeshFactory.CreateMeshPart("SpikeL", StylizedMeshFactory.Cone6Mesh, _ironArmorObj.transform, new Vector3(-0.36f, 0.82f, 0.22f), new Vector3(0.14f, 0.24f, 0.14f), ironPlate, new Vector3(0f, 0f, 25f));
            StylizedMeshFactory.CreateMeshPart("SpikeR", StylizedMeshFactory.Cone6Mesh, _ironArmorObj.transform, new Vector3(0.36f, 0.82f, 0.22f), new Vector3(0.14f, 0.24f, 0.14f), ironPlate, new Vector3(0f, 0f, -25f));
            StylizedMeshFactory.CreatePart("HelmPlate", PrimitiveType.Cube, _ironArmorObj.transform, new Vector3(0f, 0.89f, 0.45f), new Vector3(0.38f, 0.10f, 0.36f), ironPlate);
            StylizedMeshFactory.CreateMeshPart("ArmorCore", StylizedMeshFactory.OctaCrystalMesh, _ironArmorObj.transform, new Vector3(0f, 0.80f, 0.08f), new Vector3(0.22f, 0.22f, 0.22f), runeCore, castShadows: false);
            _ironArmorObj.SetActive(false);

            // 5. Stamina Crest above head
            _staminaBadgeRoot = new GameObject("Equip_StaminaEmblem");
            _staminaBadgeRoot.transform.SetParent(transform, false);
            _staminaBadgeRoot.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            GameObject gem = StylizedMeshFactory.CreateMeshPart("StaminaGem", StylizedMeshFactory.OctaCrystalMesh,
                _staminaBadgeRoot.transform, Vector3.zero, new Vector3(0.36f, 0.40f, 0.36f),
                StylizedMeshFactory.CreateStylizedMaterial(Color.white, null, 0.95f, 0.9f, emission: Color.yellow), castShadows: false);
            _staminaBadgeRenderer = gem.GetComponent<Renderer>();
            _staminaBadgeRoot.SetActive(false);
        }

        private void BuildRabbitModel(PlayerSnapshotDto dto, bool isLocal)
        {
            Color accent = RabbitAccentColors[Mathf.Abs(dto.colorIndex) % RabbitAccentColors.Length];
            Color ringColor = isLocal ? new Color(0.35f, 1.55f, 0.65f, 0.92f) : new Color(0.32f, 1.05f, 1.35f, 0.72f);

            StylizedMeshFactory.CreateMeshPart("SelectionRing", StylizedMeshFactory.GroundQuadMesh, transform,
                new Vector3(0f, 0.05f, 0f), new Vector3(0.92f, 1f, 0.92f),
                StylizedMeshFactory.CreateGlowMaterial(ringColor, ProceduralTextureFactory.SelectionRingTexture, 2.8f, 0.12f),
                castShadows: false);

            _bodyPivot = new GameObject("BodyPivot").transform;
            _bodyPivot.SetParent(transform, false);

            Material whiteFur = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.96f, 0.95f, 0.92f), null, 0.28f, 0.50f, rimColor: new Color(1f, 0.98f, 0.85f));
            Material pinkMat = StylizedMeshFactory.CreateStylizedMaterial(new Color(1.0f, 0.66f, 0.74f), null, 0.4f, 0.35f);
            Material scarfMat = StylizedMeshFactory.CreateStylizedMaterial(accent, null, 0.45f, 0.65f, rimColor: Color.white, emission: accent * 0.28f);
            Material eyeDark = StylizedMeshFactory.CreateStylizedMaterial(new Color(0.08f, 0.08f, 0.12f), null, 0.9f, 0.5f);

            StylizedMeshFactory.CreatePart("Body", PrimitiveType.Sphere, _bodyPivot, new Vector3(0f, 0.34f, 0f), new Vector3(0.46f, 0.44f, 0.50f), whiteFur);
            StylizedMeshFactory.CreatePart("Scarf", PrimitiveType.Cylinder, _bodyPivot, new Vector3(0f, 0.52f, 0.02f), new Vector3(0.38f, 0.08f, 0.38f), scarfMat);
            StylizedMeshFactory.CreatePart("CapeTail", PrimitiveType.Cube, _bodyPivot, new Vector3(0f, 0.44f, -0.24f), new Vector3(0.24f, 0.22f, 0.08f), scarfMat, new Vector3(22f, 0f, 0f));

            Transform head = new GameObject("Head").transform;
            head.SetParent(_bodyPivot, false);
            head.localPosition = new Vector3(0f, 0.70f, 0.08f);
            StylizedMeshFactory.CreatePart("Skull", PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.38f, 0.35f, 0.38f), whiteFur);
            StylizedMeshFactory.CreatePart("Nose", PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, 0.19f), new Vector3(0.08f, 0.06f, 0.06f), pinkMat);
            StylizedMeshFactory.CreatePart("EyeL", PrimitiveType.Sphere, head, new Vector3(-0.10f, 0.05f, 0.15f), new Vector3(0.07f, 0.08f, 0.06f), eyeDark);
            StylizedMeshFactory.CreatePart("EyeR", PrimitiveType.Sphere, head, new Vector3(0.10f, 0.05f, 0.15f), new Vector3(0.07f, 0.08f, 0.06f), eyeDark);

            _earLeft = StylizedMeshFactory.CreatePart("EarL", PrimitiveType.Capsule, head, new Vector3(-0.10f, 0.34f, -0.02f), new Vector3(0.11f, 0.24f, 0.08f), whiteFur, new Vector3(-8f, 0f, 10f)).transform;
            _earRight = StylizedMeshFactory.CreatePart("EarR", PrimitiveType.Capsule, head, new Vector3(0.10f, 0.34f, -0.02f), new Vector3(0.11f, 0.24f, 0.08f), whiteFur, new Vector3(-8f, 0f, -10f)).transform;

            StylizedMeshFactory.CreatePart("CottonTail", PrimitiveType.Sphere, _bodyPivot, new Vector3(0f, 0.24f, -0.28f), new Vector3(0.20f, 0.20f, 0.20f), whiteFur);
            _limbs = new Transform[2];
            _limbs[0] = StylizedMeshFactory.CreatePart("FootL", PrimitiveType.Sphere, _bodyPivot, new Vector3(-0.14f, 0.09f, 0.08f), new Vector3(0.16f, 0.12f, 0.24f), whiteFur).transform;
            _limbs[1] = StylizedMeshFactory.CreatePart("FootR", PrimitiveType.Sphere, _bodyPivot, new Vector3(0.14f, 0.09f, 0.08f), new Vector3(0.16f, 0.12f, 0.24f), whiteFur).transform;

            _hpBar = WorldHealthBar3D.Attach(transform, new Vector3(0f, 1.48f, 0f), 1.15f, accent, dto.name);
        }

        private void SyncWolfEquipment(int weaponTier, int armorTier, int staminaTier)
        {
            if (_currentWeaponTier != weaponTier)
            {
                _currentWeaponTier = weaponTier;
                if (_clawsObj != null) _clawsObj.SetActive(weaponTier == 1);
                if (_hammerObj != null) _hammerObj.SetActive(weaponTier >= 2);
            }

            if (_currentArmorTier != armorTier)
            {
                _currentArmorTier = armorTier;
                if (_leatherArmorObj != null) _leatherArmorObj.SetActive(armorTier == 1);
                if (_ironArmorObj != null) _ironArmorObj.SetActive(armorTier >= 2);
            }

            if (_currentStaminaTier != staminaTier)
            {
                _currentStaminaTier = staminaTier;
                if (_staminaBadgeRoot != null)
                {
                    _staminaBadgeRoot.SetActive(staminaTier > 0);
                    if (staminaTier > 0 && _staminaBadgeRenderer != null)
                    {
                        Color col = staminaTier == 1
                            ? new Color(1.0f, 0.65f, 0.22f)
                            : (staminaTier == 2 ? new Color(0.28f, 0.92f, 1.0f) : new Color(1.0f, 0.92f, 0.28f));
                        Material m = _staminaBadgeRenderer.material;
                        m.SetColor("_BaseColor", col);
                        if (m.HasProperty("_EmissionColor"))
                            m.SetColor("_EmissionColor", col * (1.2f + staminaTier * 0.45f));
                        _staminaBadgeRoot.transform.localScale = Vector3.one * (0.85f + staminaTier * 0.18f);
                    }
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, _targetPos, Mathf.Clamp01(dt * 14f));
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, Mathf.Clamp01(dt * 15f));

            _animTime += dt * (_isMoving ? (IsWolf ? 11.5f : 13.0f) : 2.2f);

            if (_bodyPivot != null)
            {
                Vector3 localPos = Vector3.zero;
                if (IsWolf)
                {
                    localPos.y = _isMoving ? Mathf.Abs(Mathf.Sin(_animTime)) * 0.08f : Mathf.Sin(_animTime) * 0.02f;
                    if (_attackLungeTimer > 0f)
                    {
                        _attackLungeTimer -= dt;
                        float p = Mathf.Clamp01(_attackLungeTimer / 0.26f);
                        localPos.z += Mathf.Sin(p * Mathf.PI) * 0.42f;
                    }
                    if (_limbs != null && _limbs.Length == 4)
                    {
                        float swing = _isMoving ? Mathf.Sin(_animTime) * 26f : 0f;
                        _limbs[0].localEulerAngles = new Vector3(swing, 0f, 0f);
                        _limbs[1].localEulerAngles = new Vector3(-swing, 0f, 0f);
                        _limbs[2].localEulerAngles = new Vector3(-swing, 0f, 0f);
                        _limbs[3].localEulerAngles = new Vector3(swing, 0f, 0f);
                    }
                    if (_tail != null)
                        _tail.localEulerAngles = new Vector3(-25f, Mathf.Sin(Time.time * 5.5f) * 18f, 0f);
                }
                else
                {
                    localPos.y = _isMoving ? Mathf.Abs(Mathf.Sin(_animTime)) * 0.24f : Mathf.Sin(_animTime) * 0.03f;
                    if (_earLeft != null && _earRight != null)
                    {
                        float earBob = _isMoving ? Mathf.Sin(_animTime * 2f) * 12f : Mathf.Sin(_animTime) * 4f;
                        _earLeft.localEulerAngles = new Vector3(-8f + earBob, 0f, 10f);
                        _earRight.localEulerAngles = new Vector3(-8f - earBob, 0f, -10f);
                    }
                }
                _bodyPivot.localPosition = localPos;

                if (_hitFlashTimer > 0f)
                {
                    _hitFlashTimer -= dt;
                    float punch = 1f + (_hitFlashTimer / 0.15f) * 0.16f;
                    _bodyPivot.localScale = new Vector3(punch, 1f / Mathf.Sqrt(punch), punch);
                }
                else
                {
                    _bodyPivot.localScale = Vector3.one;
                }
            }

            if (_staminaBadgeRoot != null && _staminaBadgeRoot.activeSelf)
            {
                _staminaBadgeRoot.transform.Rotate(Vector3.up, 95f * dt, Space.World);
                Vector3 sp = _staminaBadgeRoot.transform.localPosition;
                sp.y = 1.48f + Mathf.Sin(Time.time * 3.6f) * 0.08f;
                _staminaBadgeRoot.transform.localPosition = sp;
            }
        }
    }
}

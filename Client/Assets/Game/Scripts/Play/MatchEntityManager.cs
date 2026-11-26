using System.Collections.Generic;
using UnityEngine;
using WolfAndRabbits.Client.Audio;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Play
{
    /// <summary>
    /// Synchronizes 3D characters, 3-tier buildings (including Wolf Fog-of-War memory),
    /// projectiles, and match combat/build events to VFX, SFX, and camera shake.
    /// </summary>
    public class MatchEntityManager : MonoBehaviour
    {
        private readonly Dictionary<string, CharacterVisual3D> _characters = new Dictionary<string, CharacterVisual3D>();
        private readonly Dictionary<int, BuildingVisual3D> _buildings = new Dictionary<int, BuildingVisual3D>();
        private readonly Dictionary<Vector2Int, BuildingSnapshotDto> _wolfKnownBuildings = new Dictionary<Vector2Int, BuildingSnapshotDto>();

        private MatchStartMsg _matchStart;
        private string _localClientId;
        private FogOfWarController _fog;
        private VfxManager3D _vfx;
        private WarcraftCameraController _cameraCtrl;
        private ProceduralAudioManager _audio;
        private MatchHudController _hud;

        public void Initialize(
            MatchStartMsg matchStart,
            string localClientId,
            FogOfWarController fog,
            VfxManager3D vfx,
            WarcraftCameraController cameraCtrl,
            ProceduralAudioManager audio,
            MatchHudController hud)
        {
            ClearAll();
            _matchStart = matchStart;
            _localClientId = localClientId;
            _fog = fog;
            _vfx = vfx;
            _cameraCtrl = cameraCtrl;
            _audio = audio;
            _hud = hud;
        }

        public Vector3? GetLocalCharacterWorldPosition(PlayerSnapshotDto fallbackPlayer)
        {
            if (!string.IsNullOrEmpty(_localClientId) && _characters.TryGetValue(_localClientId, out CharacterVisual3D vis) && vis != null)
                return vis.transform.position;

            if (fallbackPlayer != null)
                return new Vector3(fallbackPlayer.x, 0f, fallbackPlayer.y);

            return null;
        }

        public void ApplySnapshot(StateSnapshotMsg snap)
        {
            if (_matchStart == null || snap == null) return;

            SyncPlayers(snap);
            SyncBuildings(snap);
            _vfx?.SyncProjectiles(snap.projectiles, _fog);
            ProcessEvents(snap.events);
        }

        public void ClearAll()
        {
            foreach (var kv in _characters)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _characters.Clear();

            foreach (var kv in _buildings)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            _buildings.Clear();
            _wolfKnownBuildings.Clear();
            _vfx?.ClearAll();
        }

        private void SyncPlayers(StateSnapshotMsg snap)
        {
            if (snap.players == null) return;
            bool localIsWolf = _matchStart.yourRole == GameConstants.RoleWolf;

            for (int i = 0; i < snap.players.Length; i++)
            {
                PlayerSnapshotDto p = snap.players[i];
                if (!_characters.TryGetValue(p.playerId, out CharacterVisual3D vis))
                {
                    vis = CharacterVisual3D.Create(p, p.playerId == _localClientId, transform);
                    _characters[p.playerId] = vis;
                }
                else
                {
                    vis.ApplySnapshot(p);
                }

                // Rabbits always see other Rabbits; unspawned players (Wolf during first 30s) stay hidden
                bool isSelf = p.playerId == _localClientId;
                bool isAllyRabbit = !localIsWolf && p.role == GameConstants.RoleRabbit;
                bool visible = p.alive && p.spawned && (isSelf || isAllyRabbit || _fog.IsWorldPosVisible(p.x, p.y));
                vis.SetRenderedVisible(visible);
            }
        }

        private void SyncBuildings(StateSnapshotMsg snap)
        {
            bool localIsWolf = _matchStart.yourRole == GameConstants.RoleWolf;
            Dictionary<Vector2Int, BuildingSnapshotDto> liveByTile = new Dictionary<Vector2Int, BuildingSnapshotDto>();

            if (snap.buildings != null)
            {
                for (int i = 0; i < snap.buildings.Length; i++)
                {
                    BuildingSnapshotDto b = snap.buildings[i];
                    liveByTile[new Vector2Int(b.tileX, b.tileY)] = b;
                }
            }

            IEnumerable<BuildingSnapshotDto> toRender;
            if (localIsWolf)
            {
                // Wolf Fog Memory: update/clear remembered tiles ONLY while Wolf has vision of them
                foreach (var kv in liveByTile)
                {
                    if (_fog.IsTileVisible(kv.Key.x, kv.Key.y))
                        _wolfKnownBuildings[kv.Key] = kv.Value;
                }

                List<Vector2Int> clearedTiles = null;
                foreach (var kv in _wolfKnownBuildings)
                {
                    if (_fog.IsTileVisible(kv.Key.x, kv.Key.y) && !liveByTile.ContainsKey(kv.Key))
                    {
                        if (clearedTiles == null) clearedTiles = new List<Vector2Int>();
                        clearedTiles.Add(kv.Key);
                    }
                }
                if (clearedTiles != null)
                {
                    for (int i = 0; i < clearedTiles.Count; i++)
                        _wolfKnownBuildings.Remove(clearedTiles[i]);
                }

                toRender = _wolfKnownBuildings.Values;
            }
            else
            {
                toRender = liveByTile.Values;
            }

            HashSet<int> keepIds = new HashSet<int>();
            foreach (BuildingSnapshotDto b in toRender)
            {
                keepIds.Add(b.buildingId);
                if (!_buildings.TryGetValue(b.buildingId, out BuildingVisual3D vis))
                {
                    float yaw = ComputeGateYaw(b);
                    vis = BuildingVisual3D.Create(b, yaw, transform);
                    _buildings[b.buildingId] = vis;
                }
                else
                {
                    vis.UpdateState(b);
                }
            }

            List<int> removeIds = null;
            foreach (var kv in _buildings)
            {
                if (!keepIds.Contains(kv.Key))
                {
                    if (removeIds == null) removeIds = new List<int>();
                    removeIds.Add(kv.Key);
                }
            }
            if (removeIds != null)
            {
                for (int i = 0; i < removeIds.Count; i++)
                {
                    int id = removeIds[i];
                    if (_buildings[id] != null) Destroy(_buildings[id].gameObject);
                    _buildings.Remove(id);
                }
            }
        }

        private float ComputeGateYaw(BuildingSnapshotDto b)
        {
            if (b.buildingType != GameConstants.BuildingGate || _matchStart.bases == null)
                return 0f;

            for (int i = 0; i < _matchStart.bases.Length; i++)
            {
                BaseSummaryDto baseInfo = _matchStart.bases[i];
                if (baseInfo.entranceX == b.tileX && baseInfo.entranceY == b.tileY)
                    return Mathf.Abs(baseInfo.entranceDirX) > 0 ? 90f : 0f;
            }
            return 0f;
        }

        private void ProcessEvents(GameEventDto[] events)
        {
            if (events == null || events.Length == 0) return;

            for (int i = 0; i < events.Length; i++)
            {
                GameEventDto ev = events[i];
                Vector3 pos = new Vector3(ev.x, 0f, ev.y);
                bool visible = _fog.IsWorldPosVisible(ev.x, ev.y) || ev.actorId == _localClientId || ev.targetId == _localClientId;

                switch (ev.eventType)
                {
                    case "wolf_spawned":
                        if (visible)
                        {
                            _vfx?.SpawnRingPulse(pos, new Color(2.4f, 0.35f, 0.22f, 0.95f), 3.4f);
                            _vfx?.SpawnBurst(pos, new Color(2.5f, 0.72f, 0.20f, 1f), 20, 1.35f);
                            _cameraCtrl?.AddShake(0.26f);
                        }
                        if (ev.actorId == _localClientId)
                        {
                            _cameraCtrl?.FocusOn(pos, immediate: false);
                        }
                        break;

                    case "phase_changed":
                        _hud?.ShowToast(LocalizationManager.Get("toast.hunt_start"), 3.5f);
                        _audio?.PlayUpgrade();
                        break;

                    case "attack_swing":
                        if (visible && _characters.TryGetValue(ev.actorId, out CharacterVisual3D wolfVis))
                        {
                            wolfVis.TriggerAttackAnim();
                            _vfx?.SpawnSlashArc(wolfVis.transform.position, wolfVis.transform.rotation, ev.value);
                            _audio?.PlaySlash();
                        }
                        break;

                    case "damage":
                        if (visible)
                        {
                            _vfx?.SpawnFloatingText(pos, "-" + ev.value, new Color(1f, 0.35f, 0.25f));
                            _vfx?.SpawnBurst(pos, new Color(2.4f, 0.55f, 0.2f, 1f), 6, 0.55f);
                            _audio?.PlayHit();

                            if (int.TryParse(ev.targetId, out int bid) && _buildings.TryGetValue(bid, out BuildingVisual3D bVis))
                                bVis.TriggerHitEffect();
                            else if (_characters.TryGetValue(ev.targetId, out CharacterVisual3D cVis))
                                cVis.TriggerHitFlash();

                            if (ev.targetId == _localClientId || ev.actorId == _localClientId)
                                _cameraCtrl?.AddShake(0.14f);
                        }
                        break;

                    case "building_placed":
                        if (visible)
                        {
                            _vfx?.SpawnRingPulse(pos, new Color(0.45f, 1.85f, 0.85f, 0.9f), 1.9f);
                            _vfx?.SpawnBurst(pos, new Color(1.9f, 1.7f, 0.55f, 1f), 10, 0.7f);
                            _audio?.PlayBuild();
                        }
                        break;

                    case "building_upgraded":
                        if (visible)
                        {
                            _vfx?.SpawnRingPulse(pos, new Color(2.2f, 1.75f, 0.35f, 0.95f), 2.3f);
                            _vfx?.SpawnBurst(pos, new Color(2.3f, 1.85f, 0.40f, 1f), 14, 0.95f);
                            _vfx?.SpawnFloatingText(pos, LocalizationManager.Format("vfx.upgraded", ev.value), new Color(1f, 0.88f, 0.30f));
                            _audio?.PlayUpgrade();
                        }
                        break;

                    case "building_destroyed":
                        if (visible)
                        {
                            _vfx?.SpawnBurst(pos, new Color(2.5f, 1.0f, 0.25f, 1f), 16, 1.15f);
                            _vfx?.SpawnRingPulse(pos, new Color(2.2f, 0.45f, 0.18f, 0.9f), 2.5f);
                            _audio?.PlayDestroy();
                            _cameraCtrl?.AddShake(0.24f);
                        }
                        break;

                    case "cannon_shot":
                        if (visible)
                        {
                            if (int.TryParse(ev.actorId, out int cid) && _buildings.TryGetValue(cid, out BuildingVisual3D cVis))
                                cVis.TriggerCannonRecoil();
                            _vfx?.SpawnBurst(pos + Vector3.up * 0.6f, new Color(2.4f, 1.2f, 0.25f, 1f), 5, 0.45f);
                            _audio?.PlayCannon();
                        }
                        break;

                    case "cannon_hit":
                        if (visible)
                            _vfx?.SpawnBurst(pos, new Color(2.6f, 0.95f, 0.22f, 1f), 10, 0.85f);
                        break;

                    case "gold_earned":
                        if (ev.actorId == _localClientId)
                        {
                            _vfx?.SpawnFloatingText(pos + Vector3.up * 0.4f, LocalizationManager.Format("vfx.gold_gain", ev.value), new Color(1f, 0.88f, 0.22f));
                            _audio?.PlayCoin();
                        }
                        break;

                    case "item_bought":
                        if (visible)
                        {
                            bool isFruit = ev.extra == "fruit";
                            _vfx?.SpawnRingPulse(pos, isFruit ? new Color(0.45f, 2.2f, 0.95f, 0.95f) : new Color(0.55f, 1.95f, 2.5f, 0.95f), 2.5f);
                            _vfx?.SpawnBurst(pos, isFruit ? new Color(0.65f, 2.3f, 0.75f, 1f) : new Color(2.2f, 1.85f, 0.45f, 1f), 14, 1.0f);
                            if (ev.actorId == _localClientId)
                            {
                                _audio?.PlayShop();
                                string msg = isFruit
                                    ? LocalizationManager.Get("toast.item_bought_fruit")
                                    : LocalizationManager.Get("toast.item_bought_slot");
                                _hud?.ShowToast(msg, 2.2f);
                            }
                        }
                        break;

                    case "player_died":
                        _vfx?.SpawnBurst(pos, new Color(2.5f, 0.25f, 0.22f, 1f), 18, 1.2f);
                        _audio?.PlayDestroy();
                        _cameraCtrl?.AddShake(0.32f);
                        if (ev.targetId == _localClientId)
                            _hud?.ShowToast(LocalizationManager.Get("toast.you_died"), 3.2f);
                        break;
                }
            }
        }
    }
}

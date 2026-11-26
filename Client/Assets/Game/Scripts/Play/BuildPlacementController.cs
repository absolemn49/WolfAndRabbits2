using System.Collections.Generic;
using UnityEngine;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Play
{
    /// <summary>
    /// Handles Rabbit building placement & upgrade workflow:
    /// computes valid 3x3 tiles around the Rabbit, renders ground highlights, and dispatches server requests.
    /// </summary>
    public class BuildPlacementController : MonoBehaviour
    {
        public bool IsMenuOpen { get; private set; }
        public string ActiveMode { get; private set; } = string.Empty;

        private GameSession _session;
        private MatchStartMsg _matchStart;
        private MapRenderer3D _mapRenderer;
        private MatchHudController _hud;

        private int _lastPlayerTileX = -999;
        private int _lastPlayerTileY = -999;

        public void Initialize(GameSession session, MatchStartMsg matchStart, MapRenderer3D mapRenderer, MatchHudController hud)
        {
            _session = session;
            _matchStart = matchStart;
            _mapRenderer = mapRenderer;
            _hud = hud;
            CancelMode();
        }

        public void ToggleMenu()
        {
            _session?.Audio.PlayClick();
            if (IsMenuOpen)
            {
                CancelMode();
            }
            else
            {
                IsMenuOpen = true;
                ActiveMode = string.Empty;
                _hud?.SetBuildBarVisible(true, string.Empty);
            }
        }

        public void SelectMode(string mode, PlayerSnapshotDto localPlayer, StateSnapshotMsg snap)
        {
            _session?.Audio.PlayClick();
            IsMenuOpen = true;
            ActiveMode = mode;
            _hud?.SetBuildBarVisible(true, mode);
            RefreshHighlights(localPlayer, snap, force: true);
        }

        public void CancelMode()
        {
            IsMenuOpen = false;
            ActiveMode = string.Empty;
            _lastPlayerTileX = -999;
            _lastPlayerTileY = -999;
            _hud?.SetBuildBarVisible(false);
            _mapRenderer?.SetBuildHighlights(null, string.Empty);
        }

        public void RefreshHighlights(PlayerSnapshotDto localPlayer, StateSnapshotMsg snap, bool force)
        {
            if (!IsMenuOpen || string.IsNullOrEmpty(ActiveMode) || localPlayer == null || _matchStart == null)
                return;

            int px = Mathf.FloorToInt(localPlayer.x);
            int py = Mathf.FloorToInt(localPlayer.y);
            if (!force && px == _lastPlayerTileX && py == _lastPlayerTileY)
                return;

            _lastPlayerTileX = px;
            _lastPlayerTileY = py;

            List<Vector2Int> valid = new List<Vector2Int>();

            if (ActiveMode == GameConstants.ModeUpgrade)
            {
                if (snap?.buildings != null)
                {
                    for (int i = 0; i < snap.buildings.Length; i++)
                    {
                        BuildingSnapshotDto b = snap.buildings[i];
                        if (b.ownerId == _session.ClientId && b.level < 3 &&
                            Mathf.Abs(b.tileX - px) <= 1 && Mathf.Abs(b.tileY - py) <= 1)
                        {
                            valid.Add(new Vector2Int(b.tileX, b.tileY));
                        }
                    }
                }
            }
            else
            {
                // Valid tiles strictly inside 3x3 neighborhood around Rabbit
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int tx = px + dx;
                        int ty = py + dy;
                        if (tx < 0 || ty < 0 || tx >= _matchStart.mapWidth || ty >= _matchStart.mapHeight)
                            continue;

                        TileType tt = (TileType)_matchStart.tiles[ty * _matchStart.mapWidth + tx];
                        bool tileMatches = ActiveMode == GameConstants.BuildingGate
                            ? (tt == TileType.BaseEntrance)
                            : (tt == TileType.BaseFloor);
                        if (!tileMatches) continue;

                        if (IsTileOccupied(tx, ty, snap)) continue;
                        valid.Add(new Vector2Int(tx, ty));
                    }
                }
            }

            _mapRenderer?.SetBuildHighlights(valid, ActiveMode);
        }

        public void HandleTileClicked(int tx, int ty, PlayerSnapshotDto localPlayer, StateSnapshotMsg snap)
        {
            if (localPlayer == null || !localPlayer.alive || _matchStart == null) return;
            if (_matchStart.yourRole != GameConstants.RoleRabbit) return;

            if (ActiveMode == GameConstants.ModeUpgrade)
            {
                BuildingSnapshotDto target = FindOwnBuildingAt(tx, ty, snap);
                if (target != null && target.level < 3)
                {
                    _session.SendUpgradeRequest(target.buildingId);
                    CancelMode();
                }
                return;
            }

            if (!string.IsNullOrEmpty(ActiveMode))
            {
                if (_mapRenderer != null && _mapRenderer.IsHighlightedTile(tx, ty))
                {
                    _session.SendBuildRequest(ActiveMode, tx, ty);
                    CancelMode();
                }
                return;
            }

            // Quick-upgrade shortcut: tapping your own Lv1/Lv2 building within 3x3 range
            BuildingSnapshotDto own = FindOwnBuildingAt(tx, ty, snap);
            if (own != null && own.level < 3)
            {
                int px = Mathf.FloorToInt(localPlayer.x);
                int py = Mathf.FloorToInt(localPlayer.y);
                if (Mathf.Abs(tx - px) <= 1 && Mathf.Abs(ty - py) <= 1)
                    _session.SendUpgradeRequest(own.buildingId);
            }
        }

        private bool IsTileOccupied(int tx, int ty, StateSnapshotMsg snap)
        {
            if (snap?.buildings != null)
            {
                for (int i = 0; i < snap.buildings.Length; i++)
                {
                    if (snap.buildings[i].tileX == tx && snap.buildings[i].tileY == ty)
                        return true;
                }
            }
            if (snap?.players != null)
            {
                for (int i = 0; i < snap.players.Length; i++)
                {
                    PlayerSnapshotDto p = snap.players[i];
                    if (p.alive && Mathf.FloorToInt(p.x) == tx && Mathf.FloorToInt(p.y) == ty)
                        return true;
                }
            }
            return false;
        }

        private BuildingSnapshotDto FindOwnBuildingAt(int tx, int ty, StateSnapshotMsg snap)
        {
            if (snap?.buildings == null) return null;
            for (int i = 0; i < snap.buildings.Length; i++)
            {
                BuildingSnapshotDto b = snap.buildings[i];
                if (b.ownerId == _session.ClientId && b.tileX == tx && b.tileY == ty)
                    return b;
            }
            return null;
        }
    }
}

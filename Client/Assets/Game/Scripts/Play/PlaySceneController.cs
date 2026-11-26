using UnityEngine;
using UnityEngine.SceneManagement;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Play
{
    /// <summary>
    /// Entry point for the Play scene (Assets/Game/Scenes/Play.unity).
    /// Wires together Map, Fog of War, Entities, Camera, Input, Build Placement, and HUD.
    /// </summary>
    public class PlaySceneController : MonoBehaviour
    {
        [Header("World Systems")]
        [SerializeField] private MapRenderer3D mapRenderer;
        [SerializeField] private FogOfWarController fogController;
        [SerializeField] private MatchEntityManager entityManager;
        [SerializeField] private VfxManager3D vfxManager;

        [Header("Gameplay Controllers")]
        [SerializeField] private WarcraftCameraController cameraController;
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private BuildPlacementController buildController;

        [Header("UI")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private MatchHudController hud;

        private GameSession _session;
        private MatchStartMsg _matchStart;
        private StateSnapshotMsg _latestSnap;
        private PlayerSnapshotDto _localPlayer;
        private bool _firstSnapshotApplied;

        private void Start()
        {
            _session = GameSession.Instance;
            if (_session == null || _session.CurrentMatchStart == null)
            {
                Debug.Log("[WolfAndRabbits] Chưa có dữ liệu trận đấu — đang chuyển về scene Lobby...");
                SceneManager.LoadScene(GameConstants.SceneLobby);
                return;
            }

            _matchStart = _session.CurrentMatchStart;

            if (cameraController != null)
                cameraController.Initialize(Camera.main);

            if (hud != null && hudCanvas != null)
            {
                hud.Build(hudCanvas.transform);
                hud.ConfigureForMatch(_matchStart);
            }

            mapRenderer.BuildMap(_matchStart);
            fogController.Initialize(_matchStart);
            entityManager.Initialize(_matchStart, _session.ClientId, fogController, vfxManager, cameraController, _session.Audio, hud);
            buildController.Initialize(_session, _matchStart, mapRenderer, hud);

            Vector3 initialCamFocus = new Vector3(_matchStart.shopX + 0.5f, 0f, _matchStart.shopY + 0.5f);
            cameraController.EnterMatch(_matchStart.mapWidth, _matchStart.mapHeight, initialCamFocus);

            WireEvents();

            string intro = _matchStart.yourRole == GameConstants.RoleWolf
                ? LocalizationManager.Get("toast.intro_wolf")
                : LocalizationManager.Get("toast.intro_rabbit");
            hud.ShowToast(intro, 4.0f);
            _session.Audio.PlayUpgrade();

            // Consume any snapshot/match_end that arrived while Play scene was loading
            if (_session.LatestSnapshot != null)
                HandleStateSnapshot(_session.LatestSnapshot);
            if (_session.LatestMatchEnd != null)
                HandleMatchEnd(_session.LatestMatchEnd);
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.OnStateSnapshotReceived -= HandleStateSnapshot;
                _session.OnMatchEnded -= HandleMatchEnd;
                _session.OnServerError -= HandleServerError;
            }

            if (fogController != null)
                fogController.DisableFog();
        }

        private void Update()
        {
            if (_session == null || _matchStart == null) return;

            Vector3? localWorld = entityManager.GetLocalCharacterWorldPosition(_localPlayer);
            if (_localPlayer != null && localWorld.HasValue)
                fogController.UpdateVision(localWorld.Value.x, localWorld.Value.z, _localPlayer.alive, Time.deltaTime, _localPlayer.spawned);

            bool modalOpen = hud.IsShopOpen || hud.IsResultOpen || hud.IsSettingsOpen;
            cameraController.TickCamera(localWorld, inputBlockedByModal: modalOpen);

            bool canControl = _localPlayer != null && _localPlayer.alive && _localPlayer.spawned;
            inputController.TickInput(_session, hud.JoystickDirection, canControl, modalOpen);

            if (!modalOpen && canControl && buildController.IsMenuOpen)
                buildController.RefreshHighlights(_localPlayer, _latestSnap, force: false);
        }

        private void WireEvents()
        {
            _session.OnStateSnapshotReceived += HandleStateSnapshot;
            _session.OnMatchEnded += HandleMatchEnd;
            _session.OnServerError += HandleServerError;

            inputController.OnPrimaryKeyPressed += HandlePrimaryAction;
            inputController.OnShopKeyPressed += HandleWolfShopShortcut;
            inputController.OnCancelKeyPressed += HandleCancelAction;

            cameraController.OnGroundTileClicked += (tx, ty) =>
                buildController.HandleTileClicked(tx, ty, _localPlayer, _latestSnap);

            hud.OnUiClickSound += () => _session.Audio.PlayClick();
            hud.OnFollowButtonHoldChanged += held => cameraController.SetHoldFollow(held);
            hud.OnPrimaryActionPressed += HandlePrimaryAction;
            hud.OnSecondaryActionPressed += HandleSecondaryAction;
            hud.OnBuildTypeSelected += type => buildController.SelectMode(type, _localPlayer, _latestSnap);
            hud.OnUpgradeModeSelected += () => buildController.SelectMode(GameConstants.ModeUpgrade, _localPlayer, _latestSnap);
            hud.OnCancelBuildPressed += () => buildController.CancelMode();
            hud.OnBuyItemRequested += itemId =>
            {
                _session.Audio.PlayClick();
                _session.SendBuyItemRequest(itemId);
            };
            hud.OnReturnToRoomRequested += () =>
            {
                _session.Audio.PlayClick();
                _session.ReturnToWaitingRoom();
            };
            hud.OnLeaveToLobbyRequested += () =>
            {
                _session.Audio.PlayClick();
                _session.LeaveRoomAndGoLobby();
            };
        }

        private void HandleStateSnapshot(StateSnapshotMsg snap)
        {
            _latestSnap = snap;
            _localPlayer = FindLocalPlayer(snap);

            if (!_firstSnapshotApplied && _localPlayer != null)
            {
                _firstSnapshotApplied = true;
                cameraController.FocusOn(new Vector3(_localPlayer.x, 0f, _localPlayer.y), immediate: true);
                fogController.UpdateVision(_localPlayer.x, _localPlayer.y, _localPlayer.alive, 1f, _localPlayer.spawned);
            }

            entityManager.ApplySnapshot(snap);
            hud.UpdateHud(snap, _localPlayer, fogController);

            if (buildController.IsMenuOpen)
                buildController.RefreshHighlights(_localPlayer, snap, force: true);
        }

        private void HandleMatchEnd(MatchEndMsg endMsg)
        {
            buildController.CancelMode();
            bool won = (endMsg.winner == GameConstants.RoleWolf && _matchStart.yourRole == GameConstants.RoleWolf) ||
                       (endMsg.winner == "Rabbits" && _matchStart.yourRole == GameConstants.RoleRabbit);

            if (won) _session.Audio.PlayVictory();
            else _session.Audio.PlayDefeat();

            hud.ShowMatchResult(endMsg, _matchStart.yourRole);
        }

        private void HandleServerError(ErrorMsg err)
        {
            hud.ShowToast(LocalizationManager.TranslateServerError(err.code, err.message), 2.4f);
        }

        private void HandlePrimaryAction()
        {
            if (_localPlayer == null || !_localPlayer.alive) return;
            if (!_localPlayer.spawned)
            {
                hud.ShowToast(LocalizationManager.Get("err.wolf_not_spawned"), 2.0f);
                return;
            }

            if (_matchStart.yourRole == GameConstants.RoleWolf)
                _session.SendAttackRequest();
            else
                buildController.ToggleMenu();
        }

        private void HandleSecondaryAction()
        {
            if (_localPlayer == null || !_localPlayer.alive) return;
            if (!_localPlayer.spawned)
            {
                hud.ShowToast(LocalizationManager.Get("err.wolf_not_spawned"), 2.0f);
                return;
            }

            if (_matchStart.yourRole == GameConstants.RoleWolf)
                ToggleWolfShop();
            else
                buildController.SelectMode(GameConstants.ModeUpgrade, _localPlayer, _latestSnap);
        }

        private void HandleWolfShopShortcut()
        {
            if (_matchStart.yourRole == GameConstants.RoleWolf)
                ToggleWolfShop();
        }

        private void ToggleWolfShop()
        {
            _session.Audio.PlayClick();
            if (_localPlayer == null || !_localPlayer.spawned)
            {
                hud.ShowToast(LocalizationManager.Get("err.wolf_not_spawned"), 2.2f);
                return;
            }
            if (!_localPlayer.nearShop)
            {
                hud.ShowToast(LocalizationManager.Get("toast.near_shop_required"), 2.2f);
                return;
            }
            hud.SetShopOpen(!hud.IsShopOpen, _localPlayer);
        }

        private void HandleCancelAction()
        {
            if (hud.IsSettingsOpen)
                hud.SetSettingsOpen(false);
            else if (hud.IsShopOpen)
                hud.SetShopOpen(false);
            else if (buildController.IsMenuOpen)
                buildController.CancelMode();
        }

        private PlayerSnapshotDto FindLocalPlayer(StateSnapshotMsg snap)
        {
            if (snap?.players == null) return null;
            for (int i = 0; i < snap.players.Length; i++)
            {
                if (snap.players[i].playerId == _session.ClientId)
                    return snap.players[i];
            }
            return null;
        }
    }
}

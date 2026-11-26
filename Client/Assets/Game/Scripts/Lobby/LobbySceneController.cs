using UnityEngine;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Network;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Lobby
{
    /// <summary>
    /// Entry point for the Lobby scene (Assets/Game/Scenes/Lobby.unity).
    /// Connects the Lobby UI and 3D preview diorama to the persistent GameSession.
    /// </summary>
    public class LobbySceneController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private LobbyUiController lobbyUi;
        [SerializeField] private Canvas lobbyCanvas;
        [SerializeField] private MapRenderer3D lobbyDiorama;
        [SerializeField] private Camera previewCamera;

        [Header("Diorama Camera Orbit")]
        [SerializeField] private float orbitSpeedDegrees = 6f;
        [SerializeField] private float orbitRadius = 14f;
        [SerializeField] private float orbitHeight = 14.5f;

        private GameSession _session;
        private float _orbitAngle;

        private void Start()
        {
            _session = GameSession.Instance;
            if (_session == null)
            {
                GameObject sessionGo = new GameObject("GameSession");
                _session = sessionGo.AddComponent<GameSession>();
            }

            if (previewCamera == null)
                previewCamera = Camera.main;

            if (lobbyDiorama != null)
            {
                Shader.SetGlobalFloat("_WRFogEnabled", 0f);
                lobbyDiorama.BuildLobbyPreview();
            }

            if (lobbyUi != null && lobbyCanvas != null)
            {
                lobbyUi.Build(lobbyCanvas.transform, GameWebSocketClient.GetDefaultServerUrl());
                lobbyUi.SetVisible(true);
            }

            WireUiEvents();
            SubscribeSessionEvents();
            SyncInitialUiState();
        }

        private void OnDestroy()
        {
            UnsubscribeSessionEvents();
        }

        private void Update()
        {
            if (previewCamera == null) return;
            _orbitAngle += Time.deltaTime * orbitSpeedDegrees;
            float rad = _orbitAngle * Mathf.Deg2Rad;
            previewCamera.transform.position = new Vector3(
                22.5f + Mathf.Cos(rad) * orbitRadius,
                orbitHeight,
                22.5f + Mathf.Sin(rad) * orbitRadius);
            previewCamera.transform.LookAt(new Vector3(22.5f, 1.2f, 22.5f));
        }

        private void WireUiEvents()
        {
            if (lobbyUi == null) return;

            lobbyUi.OnUiClickSound += () => _session.Audio.PlayClick();

            lobbyUi.OnConnectRequested += url =>
            {
                _session.Audio.PlayClick();
                lobbyUi.SetConnectionStatus(false, true, url);
                _session.Connect(url);
            };

            lobbyUi.OnRefreshRoomsRequested += () => _session.RequestRoomList();

            lobbyUi.OnCreateRoomRequested += (playerName, password) =>
            {
                _session.Audio.PlayClick();
                if (!_session.Network.IsConnected)
                {
                    lobbyUi.ShowError(LocalizationManager.Get("lobby.error.not_connected"));
                    return;
                }
                _session.CreateRoom(playerName, password);
            };

            lobbyUi.OnJoinRoomRequested += (code, playerName, password) =>
            {
                _session.Audio.PlayClick();
                if (!_session.Network.IsConnected)
                {
                    lobbyUi.ShowError(LocalizationManager.Get("lobby.error.not_connected"));
                    return;
                }
                _session.JoinRoom(code, playerName, password);
            };

            lobbyUi.OnLeaveRoomRequested += () =>
            {
                _session.Audio.PlayClick();
                _session.LeaveRoomAndGoLobby();
                lobbyUi.ShowLobby();
            };

            lobbyUi.OnStartMatchRequested += () =>
            {
                _session.Audio.PlayClick();
                _session.StartMatch();
            };
        }

        private void SubscribeSessionEvents()
        {
            if (_session == null) return;
            _session.OnConnected += HandleConnected;
            _session.OnDisconnected += HandleDisconnected;
            _session.OnConnectionError += HandleConnectionError;
            _session.OnServerError += HandleServerError;
            _session.OnRoomListUpdated += HandleRoomListUpdated;
            _session.OnRoomStateUpdated += HandleRoomStateUpdated;
        }

        private void UnsubscribeSessionEvents()
        {
            if (_session == null) return;
            _session.OnConnected -= HandleConnected;
            _session.OnDisconnected -= HandleDisconnected;
            _session.OnConnectionError -= HandleConnectionError;
            _session.OnServerError -= HandleServerError;
            _session.OnRoomListUpdated -= HandleRoomListUpdated;
            _session.OnRoomStateUpdated -= HandleRoomStateUpdated;
        }

        private void SyncInitialUiState()
        {
            if (lobbyUi == null || _session == null) return;

            bool connected = _session.Network.IsConnected;
            bool connecting = _session.Network.IsConnecting;
            lobbyUi.SetConnectionStatus(connected, connecting, connecting ? _session.Network.CurrentUrl : "");

            if (_session.Phase == SessionPhase.WaitingRoom && _session.CurrentRoomState != null)
            {
                lobbyUi.ShowWaitingRoom(_session.CurrentRoomState, _session.ClientId);
            }
            else
            {
                lobbyUi.ShowLobby();
                lobbyUi.UpdateRoomList(_session.LatestRooms);
                if (connected)
                    _session.RequestRoomList();
            }
        }

        private void HandleConnected()
        {
            lobbyUi.SetConnectionStatus(true, false, "");
            lobbyUi.ShowError("");
            _session.RequestRoomList();
        }

        private void HandleDisconnected(string reason)
        {
            lobbyUi.SetConnectionStatus(false, false, "");
            lobbyUi.ShowLobby();
            lobbyUi.ShowError(LocalizationManager.Format("lobby.error.disconnected", reason));
        }

        private void HandleConnectionError(string err)
        {
            lobbyUi.SetConnectionStatus(false, false, "");
            lobbyUi.ShowError(LocalizationManager.Format("lobby.error.connect_failed", err));
        }

        private void HandleServerError(ErrorMsg err)
        {
            lobbyUi.ShowError(LocalizationManager.TranslateServerError(err.code, err.message));
        }

        private void HandleRoomListUpdated(RoomSummaryDto[] rooms)
        {
            lobbyUi.UpdateRoomList(rooms);
        }

        private void HandleRoomStateUpdated(RoomStateMsg state)
        {
            lobbyUi.ShowWaitingRoom(state, _session.ClientId);
        }
    }
}

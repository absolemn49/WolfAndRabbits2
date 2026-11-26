using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using WolfAndRabbits.Client.Audio;
using WolfAndRabbits.Client.Network;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Core
{
    public enum SessionPhase
    {
        Lobby,
        WaitingRoom,
        InMatch
    }

    /// <summary>
    /// Persistent singleton (DontDestroyOnLoad) that owns the WebSocket connection,
    /// audio manager, player session state, and Lobby <-> Play scene transitions.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Core Services")]
        [SerializeField] private GameWebSocketClient network;
        [SerializeField] private ProceduralAudioManager audioManager;

        public GameWebSocketClient Network => network;
        public ProceduralAudioManager Audio => audioManager;

        public string ClientId { get; private set; } = string.Empty;
        public SessionPhase Phase { get; private set; } = SessionPhase.Lobby;
        public RoomSummaryDto[] LatestRooms { get; private set; } = new RoomSummaryDto[0];
        public RoomStateMsg CurrentRoomState { get; private set; }
        public MatchStartMsg CurrentMatchStart { get; private set; }
        public StateSnapshotMsg LatestSnapshot { get; private set; }
        public MatchEndMsg LatestMatchEnd { get; private set; }
        public string LastConnectionMessage { get; private set; } = string.Empty;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnConnectionError;
        public event Action<ErrorMsg> OnServerError;
        public event Action<RoomSummaryDto[]> OnRoomListUpdated;
        public event Action<RoomStateMsg> OnRoomStateUpdated;
        public event Action<MatchStartMsg> OnMatchStarted;
        public event Action<StateSnapshotMsg> OnStateSnapshotReceived;
        public event Action<MatchEndMsg> OnMatchEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ConfigureRuntimePlatform();
            EnsureServices();
            WireNetworkEvents();
        }

        private void Start()
        {
            if (!network.IsConnected && !network.IsConnecting)
            {
                Connect(GameWebSocketClient.GetDefaultServerUrl());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void ConfigureRuntimePlatform()
        {
            Application.runInBackground = true;
#if UNITY_ANDROID || UNITY_IOS
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
#endif
        }

        private void EnsureServices()
        {
            if (network == null)
                network = GetComponent<GameWebSocketClient>();
            if (network == null)
                network = gameObject.AddComponent<GameWebSocketClient>();

            if (audioManager == null)
                audioManager = GetComponent<ProceduralAudioManager>();
            if (audioManager == null)
                audioManager = gameObject.AddComponent<ProceduralAudioManager>();
        }

        private void WireNetworkEvents()
        {
            network.OnConnected += () =>
            {
                LastConnectionMessage = string.Empty;
                OnConnected?.Invoke();
            };

            network.OnDisconnected += reason =>
            {
                LastConnectionMessage = reason;
                ResetMatchState();
                CurrentRoomState = null;
                Phase = SessionPhase.Lobby;
                OnDisconnected?.Invoke(reason);
                EnsureScene(GameConstants.SceneLobby);
            };

            network.OnError += err =>
            {
                LastConnectionMessage = err;
                OnConnectionError?.Invoke(err);
            };

            network.OnMessageReceived += HandleServerJson;
        }

        public void Connect(string url)
        {
            LastConnectionMessage = url;
            network.Connect(url);
        }

        // ---- High-level Client Commands ----

        public void RequestRoomList()
        {
            if (network.IsConnected)
                network.SendObject(new SimpleRequestMsg { type = "list_rooms", requestId = NewRequestId() });
        }

        public void CreateRoom(string playerName, string password)
        {
            network.SendObject(new CreateRoomMsg { playerName = playerName, password = password });
        }

        public void JoinRoom(string roomCode, string playerName, string password)
        {
            network.SendObject(new JoinRoomMsg
            {
                roomCode = roomCode,
                playerName = playerName,
                password = password,
                requestId = NewRequestId()
            });
        }

        public void LeaveRoomAndGoLobby()
        {
            ResetMatchState();
            CurrentRoomState = null;
            Phase = SessionPhase.Lobby;

            if (network.IsConnected)
                network.SendObject(new SimpleRequestMsg { type = "leave_room", requestId = NewRequestId() });

            EnsureScene(GameConstants.SceneLobby);
        }

        public void StartMatch()
        {
            network.SendObject(new SimpleRequestMsg { type = "start_match", requestId = NewRequestId() });
        }

        public void ReturnToWaitingRoom()
        {
            ResetMatchState();
            Phase = SessionPhase.WaitingRoom;

            if (network.IsConnected)
                network.SendObject(new SimpleRequestMsg { type = "return_to_room", requestId = NewRequestId() });

            EnsureScene(GameConstants.SceneLobby);
        }

        public void SendMoveInput(Vector2 dir)
        {
            network.SendObject(new MoveInputMsg { dirX = dir.x, dirY = dir.y });
        }

        public void SendBuildRequest(string buildingType, int tileX, int tileY)
        {
            network.SendObject(new BuildRequestMsg
            {
                requestId = NewRequestId(),
                buildingType = buildingType,
                tileX = tileX,
                tileY = tileY
            });
        }

        public void SendUpgradeRequest(int buildingId)
        {
            network.SendObject(new UpgradeRequestMsg
            {
                requestId = NewRequestId(),
                buildingId = buildingId
            });
        }

        public void SendAttackRequest()
        {
            network.SendObject(new SimpleRequestMsg
            {
                type = "attack_request",
                requestId = NewRequestId()
            });
        }

        public void SendBuyItemRequest(string itemId)
        {
            network.SendObject(new BuyItemMsg
            {
                requestId = NewRequestId(),
                itemId = itemId
            });
        }

        private void HandleServerJson(string json)
        {
            MessageTypePeek peek = JsonUtility.FromJson<MessageTypePeek>(json);
            if (peek == null || string.IsNullOrEmpty(peek.type)) return;

            switch (peek.type)
            {
                case "welcome":
                {
                    WelcomeMsg msg = JsonUtility.FromJson<WelcomeMsg>(json);
                    ClientId = msg.clientId;
                    break;
                }

                case "error":
                {
                    ErrorMsg err = JsonUtility.FromJson<ErrorMsg>(json);
                    OnServerError?.Invoke(err);
                    break;
                }

                case "room_list":
                {
                    RoomListMsg msg = JsonUtility.FromJson<RoomListMsg>(json);
                    LatestRooms = msg.rooms ?? new RoomSummaryDto[0];
                    OnRoomListUpdated?.Invoke(LatestRooms);
                    break;
                }

                case "room_state":
                {
                    RoomStateMsg msg = JsonUtility.FromJson<RoomStateMsg>(json);
                    CurrentRoomState = msg;
                    ResetMatchState();
                    Phase = SessionPhase.WaitingRoom;
                    OnRoomStateUpdated?.Invoke(msg);
                    EnsureScene(GameConstants.SceneLobby);
                    break;
                }

                case "match_start":
                {
                    MatchStartMsg msg = JsonUtility.FromJson<MatchStartMsg>(json);
                    CurrentMatchStart = msg;
                    ClientId = msg.yourPlayerId;
                    LatestSnapshot = null;
                    LatestMatchEnd = null;
                    Phase = SessionPhase.InMatch;
                    OnMatchStarted?.Invoke(msg);
                    EnsureScene(GameConstants.ScenePlay);
                    break;
                }

                case "state_snapshot":
                {
                    StateSnapshotMsg snap = JsonUtility.FromJson<StateSnapshotMsg>(json);
                    LatestSnapshot = snap;
                    OnStateSnapshotReceived?.Invoke(snap);
                    break;
                }

                case "match_end":
                {
                    MatchEndMsg endMsg = JsonUtility.FromJson<MatchEndMsg>(json);
                    LatestMatchEnd = endMsg;
                    OnMatchEnded?.Invoke(endMsg);
                    break;
                }
            }
        }

        private void ResetMatchState()
        {
            CurrentMatchStart = null;
            LatestSnapshot = null;
            LatestMatchEnd = null;
        }

        private static void EnsureScene(string sceneName)
        {
            if (SceneManager.GetActiveScene().name != sceneName)
                SceneManager.LoadScene(sceneName);
        }

        private static string NewRequestId() => Guid.NewGuid().ToString("N");
    }
}

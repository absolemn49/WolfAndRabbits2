using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Lobby
{
    public class LobbyUiController : MonoBehaviour
    {
        public event Action<string> OnConnectRequested;
        public event Action OnRefreshRoomsRequested;
        public event Action<string, string> OnCreateRoomRequested;
        public event Action<string, string, string> OnJoinRoomRequested;
        public event Action OnLeaveRoomRequested;
        public event Action OnStartMatchRequested;
        public event Action OnUiClickSound;

        private GameObject _root;
        private GameObject _lobbyPanel;
        private GameObject _waitingRoomPanel;
        private GameObject _createModal;
        private GameObject _joinModal;
        private SettingsModalWidget _settings;

        // Lobby screen dynamic texts
        private Text _subtitleText;
        private Text _reconnectBtnLabel;
        private Text _createBtnLabel;
        private Text _joinBtnLabel;
        private Text _refreshBtnLabel;
        private Text[] _colHeaders;
        private Text _connStatusText;
        private InputField _serverUrlInput;
        private InputField _defaultNameInput;
        private Text _defaultNamePlaceholder;
        private RectTransform _roomListContent;
        private Text _emptyRoomsText;
        private Text _lobbyErrorText;

        // Create room modal controls
        private Text _createModalTitle;
        private InputField _createNameInput;
        private Text _createNamePh;
        private InputField _createPassInput;
        private Text _createPassPh;
        private Text _createCancelLabel;
        private Text _createOkLabel;

        // Join room modal controls
        private Text _joinModalTitle;
        private InputField _joinNameInput;
        private Text _joinNamePh;
        private InputField _joinCodeInput;
        private Text _joinCodePh;
        private InputField _joinPassInput;
        private Text _joinPassPh;
        private Text _joinHintText;
        private Text _joinCancelLabel;
        private Text _joinOkLabel;
        private bool _joinModalIsLocked;

        // Waiting room controls
        private Text _waitRoomCodeText;
        private Text _waitRoomMetaText;
        private RectTransform _waitPlayerListContent;
        private Button _startMatchBtn;
        private Text _startMatchBtnLabel;
        private Text _leaveRoomBtnLabel;
        private Text _startMatchHintText;

        // Cached state for instant language refresh
        private float _autoRefreshTimer;
        private bool _isVisible;
        private bool _inWaitingRoom;
        private bool _isConnected;
        private bool _isConnecting;
        private string _lastConnMessage = "";
        private RoomSummaryDto[] _lastRooms;
        private RoomStateMsg _lastWaitState;
        private string _lastLocalClientId = "";

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        public void Build(Transform canvasRoot, string defaultServerUrl)
        {
            _root = WarcraftUiFactory.CreateRect("LobbyRoot", canvasRoot).gameObject;
            WarcraftUiFactory.Stretch(_root.GetComponent<RectTransform>());

            Image backdrop = _root.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.06f, 0.10f, 0.52f);

            BuildLobbyScreen(defaultServerUrl);
            BuildWaitingRoomScreen();
            BuildCreateRoomModal();
            BuildJoinRoomModal();

            // Gear Settings Button (top-right corner, always accessible in Lobby & Waiting Room)
            _settings = SettingsModalWidget.Attach(
                _root.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-68f, -68f), new Vector2(-16f, -16f));
            _settings.OnButtonClickedSound += () => OnUiClickSound?.Invoke();

            ApplyLocalization();
            ShowLobby();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_root != null) _root.SetActive(visible);
        }

        public void ShowLobby()
        {
            _inWaitingRoom = false;
            _lobbyPanel.SetActive(true);
            _waitingRoomPanel.SetActive(false);
            _createModal.SetActive(false);
            _joinModal.SetActive(false);
            _autoRefreshTimer = 0.2f;
        }

        public void ShowWaitingRoom(RoomStateMsg state, string localClientId)
        {
            _lastWaitState = state;
            _lastLocalClientId = localClientId;
            _inWaitingRoom = true;
            _lobbyPanel.SetActive(false);
            _waitingRoomPanel.SetActive(true);
            _createModal.SetActive(false);
            _joinModal.SetActive(false);

            int count = state.players != null ? state.players.Length : 0;
            _waitRoomCodeText.text = LocalizationManager.Get("wait.title") +
                                     "<color=#FFD54F>" + state.roomCode + "</color>";
            string lockLabel = state.hasPassword
                ? LocalizationManager.Get("wait.has_pass")
                : LocalizationManager.Get("wait.open_room");
            _waitRoomMetaText.text = LocalizationManager.Format("wait.meta", count, state.maxPlayers, lockLabel);

            for (int i = _waitPlayerListContent.childCount - 1; i >= 0; i--)
                Destroy(_waitPlayerListContent.GetChild(i).gameObject);

            if (state.players != null)
            {
                for (int i = 0; i < state.players.Length; i++)
                {
                    RoomPlayerDto p = state.players[i];
                    CreateWaitingPlayerRow(p, p.playerId == localClientId);
                }
            }

            bool isHost = state.hostId == localClientId;
            _startMatchBtn.gameObject.SetActive(isHost);
            _startMatchBtn.interactable = isHost && count >= 2;
            if (isHost)
            {
                _startMatchHintText.text = count >= 2
                    ? LocalizationManager.Get("wait.hint.host_ready")
                    : LocalizationManager.Get("wait.hint.host_wait");
            }
            else
            {
                _startMatchHintText.text = LocalizationManager.Get("wait.hint.guest_wait");
            }
        }

        public void SetConnectionStatus(bool connected, bool connecting, string message)
        {
            _isConnected = connected;
            _isConnecting = connecting;
            _lastConnMessage = message ?? "";
            RefreshConnectionStatusText();
        }

        public void ShowError(string message)
        {
            if (_lobbyErrorText != null)
                _lobbyErrorText.text = message;
        }

        public void UpdateRoomList(RoomSummaryDto[] rooms)
        {
            _lastRooms = rooms;
            for (int i = _roomListContent.childCount - 1; i >= 0; i--)
                Destroy(_roomListContent.GetChild(i).gameObject);

            bool hasRooms = rooms != null && rooms.Length > 0;
            _emptyRoomsText.gameObject.SetActive(!hasRooms);

            if (!hasRooms) return;

            for (int i = 0; i < rooms.Length; i++)
                CreateRoomRow(rooms[i]);
        }

        private void Update()
        {
            if (!_isVisible) return;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_settings != null && _settings.IsOpen)
                    _settings.SetOpen(false);
                else if (_createModal != null && _createModal.activeSelf)
                    _createModal.SetActive(false);
                else if (_joinModal != null && _joinModal.activeSelf)
                    _joinModal.SetActive(false);
            }

            if (_inWaitingRoom || !_isConnected) return;

            _autoRefreshTimer -= Time.deltaTime;
            if (_autoRefreshTimer <= 0f)
            {
                _autoRefreshTimer = 3.0f;
                OnRefreshRoomsRequested?.Invoke();
            }
        }

        private void HandleLanguageChanged(GameLanguage _)
        {
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            if (_subtitleText == null) return;

            _subtitleText.text = LocalizationManager.Get("lobby.subtitle") + "  •  " +
                BuildEnvironment.BadgeText + "  •  " + PlatformProfile.CurrentModeLabel;

            _defaultNamePlaceholder.text = LocalizationManager.Get("lobby.name_placeholder");
            _reconnectBtnLabel.text = LocalizationManager.Get("lobby.connect");
            _createBtnLabel.text = LocalizationManager.Get("lobby.create_room");
            _joinBtnLabel.text = LocalizationManager.Get("lobby.join_room");
            _refreshBtnLabel.text = LocalizationManager.Get("lobby.refresh");

            if (_colHeaders != null && _colHeaders.Length == 5)
            {
                _colHeaders[0].text = LocalizationManager.Get("lobby.col.code");
                _colHeaders[1].text = LocalizationManager.Get("lobby.col.host");
                _colHeaders[2].text = LocalizationManager.Get("lobby.col.players");
                _colHeaders[3].text = LocalizationManager.Get("lobby.col.status");
                _colHeaders[4].text = LocalizationManager.Get("lobby.col.action");
            }

            _emptyRoomsText.text = LocalizationManager.Get("lobby.empty_rooms");

            _leaveRoomBtnLabel.text = LocalizationManager.Get("wait.leave");
            _startMatchBtnLabel.text = LocalizationManager.Get("wait.start");

            _createModalTitle.text = LocalizationManager.Get("lobby.create.title");
            _createNamePh.text = LocalizationManager.Get("lobby.create.name_ph");
            _createPassPh.text = LocalizationManager.Get("lobby.create.pass_ph");
            _createCancelLabel.text = LocalizationManager.Get("common.cancel");
            _createOkLabel.text = LocalizationManager.Get("lobby.create.confirm");

            _joinModalTitle.text = LocalizationManager.Get("lobby.join.title");
            _joinNamePh.text = LocalizationManager.Get("lobby.name_placeholder");
            _joinCodePh.text = LocalizationManager.Get("lobby.join.code_ph");
            _joinPassPh.text = LocalizationManager.Get("lobby.join.pass_ph");
            _joinCancelLabel.text = LocalizationManager.Get("common.cancel");
            _joinOkLabel.text = LocalizationManager.Get("lobby.join.confirm");
            RefreshJoinModalHint();

            RefreshConnectionStatusText();

            if (_lastRooms != null)
                UpdateRoomList(_lastRooms);
            if (_inWaitingRoom && _lastWaitState != null)
                ShowWaitingRoom(_lastWaitState, _lastLocalClientId);
        }

        private void RefreshConnectionStatusText()
        {
            if (_connStatusText == null) return;
            if (_isConnected)
                _connStatusText.text = "<color=#66F08A>● " + LocalizationManager.Get("lobby.status.connected") + "</color>  " + _lastConnMessage;
            else if (_isConnecting)
                _connStatusText.text = "<color=#FFD54F>● " + LocalizationManager.Get("lobby.status.connecting") + "</color>  " + _lastConnMessage;
            else
                _connStatusText.text = "<color=#FF6B6B>● " + LocalizationManager.Get("lobby.status.offline") + "</color>  " + _lastConnMessage;
        }

        private void RefreshJoinModalHint()
        {
            if (_joinHintText == null) return;
            _joinHintText.text = _joinModalIsLocked
                ? LocalizationManager.Get("lobby.join.hint_locked")
                : LocalizationManager.Get("lobby.join.hint_open");
        }

        private void BuildLobbyScreen(string defaultServerUrl)
        {
            Image panel = WarcraftUiFactory.CreatePanel("LobbyPanel", _root.transform, WarcraftUiFactory.PanelDark);
            _lobbyPanel = panel.gameObject;
            WarcraftUiFactory.SetAnchors(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460f, -310f), new Vector2(460f, 310f));

            Text title = WarcraftUiFactory.CreateText("Title", panel.transform,
                "WOLF AND RABBITS 3D", 34, WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -62f), new Vector2(-20f, -12f));

            _subtitleText = WarcraftUiFactory.CreateText("Subtitle", panel.transform, "",
                15, new Color(0.80f, 0.87f, 0.94f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -88f), new Vector2(-20f, -60f));

            Image connBar = WarcraftUiFactory.CreatePanel("ConnBar", panel.transform, WarcraftUiFactory.PanelInner, false);
            WarcraftUiFactory.SetAnchors(connBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -148f), new Vector2(-24f, -96f));

            _serverUrlInput = WarcraftUiFactory.CreateInputField("ServerUrlInput", connBar.transform,
                "ws://localhost:5000/ws", defaultServerUrl, false, 15);
            WarcraftUiFactory.SetAnchors(_serverUrlInput.GetComponent<RectTransform>(),
                new Vector2(0f, 0.14f), new Vector2(0.42f, 0.86f), new Vector2(10f, 0f), new Vector2(-6f, 0f));

            string savedName = PlayerPrefs.GetString("WR_PlayerName", "Player " + UnityEngine.Random.Range(10, 99));
            _defaultNameInput = WarcraftUiFactory.CreateInputField("DefaultNameInput", connBar.transform,
                "", savedName, false, 15);
            _defaultNamePlaceholder = _defaultNameInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_defaultNameInput.GetComponent<RectTransform>(),
                new Vector2(0.42f, 0.14f), new Vector2(0.70f, 0.86f), new Vector2(4f, 0f), new Vector2(-6f, 0f));

            Button reconnectBtn = WarcraftUiFactory.CreateButton("ReconnectBtn", connBar.transform,
                "", WarcraftUiFactory.BtnSecondary, () =>
                {
                    PlayerPrefs.SetString("WR_PlayerName", GetDefaultName());
                    OnConnectRequested?.Invoke(_serverUrlInput.text);
                }, 15);
            _reconnectBtnLabel = reconnectBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(reconnectBtn.GetComponent<RectTransform>(),
                new Vector2(0.70f, 0.14f), new Vector2(0.84f, 0.86f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

            _connStatusText = WarcraftUiFactory.CreateText("ConnStatus", connBar.transform,
                "", 13, Color.white, TextAnchor.MiddleLeft);
            WarcraftUiFactory.SetAnchors(_connStatusText.rectTransform,
                new Vector2(0.84f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-6f, 0f));

            Button createBtn = WarcraftUiFactory.CreateButton("CreateRoomBtn", panel.transform,
                "", WarcraftUiFactory.BtnPrimary, OpenCreateModal, 19);
            _createBtnLabel = createBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(createBtn.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0.35f, 1f), new Vector2(24f, -206f), new Vector2(-8f, -158f));

            Button joinBtn = WarcraftUiFactory.CreateButton("JoinRoomBtn", panel.transform,
                "", WarcraftUiFactory.BtnGold, () => OpenJoinModal("", false), 19);
            _joinBtnLabel = joinBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(joinBtn.GetComponent<RectTransform>(),
                new Vector2(0.35f, 1f), new Vector2(0.70f, 1f), new Vector2(8f, -206f), new Vector2(-8f, -158f));

            Button refreshBtn = WarcraftUiFactory.CreateButton("RefreshBtn", panel.transform,
                "", WarcraftUiFactory.BtnSecondary, () =>
                {
                    _autoRefreshTimer = 3.0f;
                    OnRefreshRoomsRequested?.Invoke();
                }, 17);
            _refreshBtnLabel = refreshBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(refreshBtn.GetComponent<RectTransform>(),
                new Vector2(0.70f, 1f), new Vector2(1f, 1f), new Vector2(8f, -206f), new Vector2(-24f, -158f));

            Image headerBar = WarcraftUiFactory.CreatePanel("HeaderBar", panel.transform, new Color(0.16f, 0.20f, 0.28f, 0.9f), false);
            WarcraftUiFactory.SetAnchors(headerBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -242f), new Vector2(-24f, -214f));
            _colHeaders = new[]
            {
                AddColumnHeader(headerBar.transform, "Col0", 0.02f, 0.18f),
                AddColumnHeader(headerBar.transform, "Col1", 0.18f, 0.52f),
                AddColumnHeader(headerBar.transform, "Col2", 0.52f, 0.68f),
                AddColumnHeader(headerBar.transform, "Col3", 0.68f, 0.84f),
                AddColumnHeader(headerBar.transform, "Col4", 0.84f, 0.99f)
            };

            _roomListContent = WarcraftUiFactory.CreateScrollList("RoomListScroll", panel.transform, out ScrollRect scroll);
            WarcraftUiFactory.SetAnchors(scroll.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 48f), new Vector2(-24f, -244f));

            _emptyRoomsText = WarcraftUiFactory.CreateText("EmptyRooms", scroll.transform, "",
                17, new Color(0.70f, 0.77f, 0.86f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.Stretch(_emptyRoomsText.rectTransform, 20f, 20f);

            _lobbyErrorText = WarcraftUiFactory.CreateText("LobbyError", panel.transform,
                "", 15, new Color(1f, 0.52f, 0.48f), TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_lobbyErrorText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 10f), new Vector2(-24f, 42f));
        }

        private void BuildWaitingRoomScreen()
        {
            Image panel = WarcraftUiFactory.CreatePanel("WaitingRoomPanel", _root.transform, WarcraftUiFactory.PanelDark);
            _waitingRoomPanel = panel.gameObject;
            WarcraftUiFactory.SetAnchors(panel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-400f, -270f), new Vector2(400f, 270f));

            _waitRoomCodeText = WarcraftUiFactory.CreateText("WaitTitle", panel.transform,
                "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_waitRoomCodeText.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -64f), new Vector2(-20f, -14f));

            _waitRoomMetaText = WarcraftUiFactory.CreateText("WaitMeta", panel.transform,
                "", 16, new Color(0.80f, 0.88f, 0.96f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_waitRoomMetaText.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -94f), new Vector2(-20f, -64f));

            _waitPlayerListContent = WarcraftUiFactory.CreateScrollList("WaitPlayerScroll", panel.transform, out ScrollRect scroll);
            WarcraftUiFactory.SetAnchors(scroll.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 118f), new Vector2(-28f, -104f));

            _startMatchHintText = WarcraftUiFactory.CreateText("StartHint", panel.transform,
                "", 15, new Color(0.95f, 0.85f, 0.48f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_startMatchHintText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 78f), new Vector2(-24f, 110f));

            Button leaveBtn = WarcraftUiFactory.CreateButton("LeaveRoomBtn", panel.transform,
                "", WarcraftUiFactory.BtnDanger, () => OnLeaveRoomRequested?.Invoke(), 19);
            _leaveRoomBtnLabel = leaveBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(leaveBtn.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0.42f, 0f), new Vector2(28f, 20f), new Vector2(-8f, 70f));

            _startMatchBtn = WarcraftUiFactory.CreateButton("StartMatchBtn", panel.transform,
                "", WarcraftUiFactory.BtnPrimary, () => OnStartMatchRequested?.Invoke(), 21);
            _startMatchBtnLabel = _startMatchBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(_startMatchBtn.GetComponent<RectTransform>(),
                new Vector2(0.45f, 0f), new Vector2(1f, 0f), new Vector2(8f, 20f), new Vector2(-28f, 70f));
        }

        private void BuildCreateRoomModal()
        {
            Image overlay = WarcraftUiFactory.CreatePanel("CreateModalOverlay", _root.transform, new Color(0f, 0f, 0f, 0.65f), false);
            _createModal = overlay.gameObject;
            WarcraftUiFactory.Stretch(overlay.rectTransform);

            Image box = WarcraftUiFactory.CreatePanel("CreateBox", overlay.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230f, -150f), new Vector2(230f, 150f));

            _createModalTitle = WarcraftUiFactory.CreateText("Title", box.transform, "", 24,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_createModalTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -54f), new Vector2(-16f, -14f));

            _createNameInput = WarcraftUiFactory.CreateInputField("CreateName", box.transform, "", "", false, 18);
            _createNamePh = _createNameInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_createNameInput.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -118f), new Vector2(-24f, -72f));

            _createPassInput = WarcraftUiFactory.CreateInputField("CreatePass", box.transform, "", "", true, 17);
            _createPassPh = _createPassInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_createPassInput.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -180f), new Vector2(-24f, -134f));

            Button cancel = WarcraftUiFactory.CreateButton("CancelBtn", box.transform, "", WarcraftUiFactory.BtnDanger,
                () => _createModal.SetActive(false), 18);
            _createCancelLabel = cancel.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(cancel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0.46f, 0f), new Vector2(24f, 22f), new Vector2(-6f, 70f));

            Button ok = WarcraftUiFactory.CreateButton("ConfirmCreateBtn", box.transform, "", WarcraftUiFactory.BtnPrimary, () =>
            {
                string pname = string.IsNullOrWhiteSpace(_createNameInput.text) ? GetDefaultName() : _createNameInput.text.Trim();
                PlayerPrefs.SetString("WR_PlayerName", pname);
                _defaultNameInput.text = pname;
                _lobbyErrorText.text = "";
                _createModal.SetActive(false);
                OnCreateRoomRequested?.Invoke(pname, _createPassInput.text ?? "");
            }, 18);
            _createOkLabel = ok.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(ok.GetComponent<RectTransform>(),
                new Vector2(0.50f, 0f), new Vector2(1f, 0f), new Vector2(6f, 22f), new Vector2(-24f, 70f));
        }

        private void BuildJoinRoomModal()
        {
            Image overlay = WarcraftUiFactory.CreatePanel("JoinModalOverlay", _root.transform, new Color(0f, 0f, 0f, 0.65f), false);
            _joinModal = overlay.gameObject;
            WarcraftUiFactory.Stretch(overlay.rectTransform);

            Image box = WarcraftUiFactory.CreatePanel("JoinBox", overlay.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-235f, -180f), new Vector2(235f, 180f));

            _joinModalTitle = WarcraftUiFactory.CreateText("Title", box.transform, "", 24,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_joinModalTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -52f), new Vector2(-16f, -12f));

            _joinNameInput = WarcraftUiFactory.CreateInputField("JoinName", box.transform, "", "", false, 17);
            _joinNamePh = _joinNameInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_joinNameInput.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -110f), new Vector2(-24f, -66f));

            _joinCodeInput = WarcraftUiFactory.CreateInputField("JoinCode", box.transform, "", "", false, 18);
            _joinCodePh = _joinCodeInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_joinCodeInput.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -168f), new Vector2(-24f, -124f));

            _joinPassInput = WarcraftUiFactory.CreateInputField("JoinPass", box.transform, "", "", true, 17);
            _joinPassPh = _joinPassInput.placeholder as Text;
            WarcraftUiFactory.SetAnchors(_joinPassInput.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -226f), new Vector2(-24f, -182f));

            _joinHintText = WarcraftUiFactory.CreateText("JoinHint", box.transform, "", 14,
                new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_joinHintText.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 78f), new Vector2(-24f, 108f));

            Button cancel = WarcraftUiFactory.CreateButton("CancelJoinBtn", box.transform, "", WarcraftUiFactory.BtnDanger,
                () => _joinModal.SetActive(false), 18);
            _joinCancelLabel = cancel.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(cancel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0.46f, 0f), new Vector2(24f, 20f), new Vector2(-6f, 68f));

            Button ok = WarcraftUiFactory.CreateButton("ConfirmJoinBtn", box.transform, "", WarcraftUiFactory.BtnPrimary, () =>
            {
                string pname = string.IsNullOrWhiteSpace(_joinNameInput.text) ? GetDefaultName() : _joinNameInput.text.Trim();
                PlayerPrefs.SetString("WR_PlayerName", pname);
                _defaultNameInput.text = pname;
                _lobbyErrorText.text = "";
                _joinModal.SetActive(false);
                OnJoinRoomRequested?.Invoke(_joinCodeInput.text.Trim().ToUpperInvariant(), pname, _joinPassInput.text ?? "");
            }, 18);
            _joinOkLabel = ok.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(ok.GetComponent<RectTransform>(),
                new Vector2(0.50f, 0f), new Vector2(1f, 0f), new Vector2(6f, 20f), new Vector2(-24f, 68f));
        }

        private void OpenCreateModal()
        {
            _createNameInput.text = GetDefaultName();
            _createPassInput.text = "";
            _createModal.SetActive(true);
        }

        private void OpenJoinModal(string prefillCode, bool isLocked)
        {
            _joinNameInput.text = GetDefaultName();
            _joinCodeInput.text = prefillCode;
            _joinPassInput.text = "";
            _joinModalIsLocked = isLocked;
            RefreshJoinModalHint();
            _joinModal.SetActive(true);
        }

        private void CreateRoomRow(RoomSummaryDto r)
        {
            Image row = WarcraftUiFactory.CreatePanel("RoomRow_" + r.roomCode, _roomListContent, WarcraftUiFactory.PanelInner);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;

            Text codeTxt = WarcraftUiFactory.CreateText("Code", row.transform, r.roomCode, 20,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(codeTxt.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.18f, 1f), Vector2.zero, Vector2.zero);

            Text hostTxt = WarcraftUiFactory.CreateText("Host", row.transform, r.hostName, 18,
                Color.white, TextAnchor.MiddleLeft);
            WarcraftUiFactory.SetAnchors(hostTxt.rectTransform, new Vector2(0.18f, 0f), new Vector2(0.52f, 1f), Vector2.zero, Vector2.zero);

            Text countTxt = WarcraftUiFactory.CreateText("Count", row.transform, r.playerCount + " / " + r.maxPlayers, 18,
                new Color(0.75f, 0.95f, 0.80f), TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(countTxt.rectTransform, new Vector2(0.52f, 0f), new Vector2(0.68f, 1f), Vector2.zero, Vector2.zero);

            string lockStr = r.hasPassword
                ? "<color=#FFB74D>" + LocalizationManager.Get("lobby.room.locked") + "</color>"
                : "<color=#66F08A>" + LocalizationManager.Get("lobby.room.open") + "</color>";
            Text lockTxt = WarcraftUiFactory.CreateText("Lock", row.transform, lockStr, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(lockTxt.rectTransform, new Vector2(0.68f, 0f), new Vector2(0.83f, 1f), Vector2.zero, Vector2.zero);

            if (r.hasPassword)
            {
                RectTransform iconRt = WarcraftUiFactory.CreateRect("LockIcon", row.transform);
                Image iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = ProceduralTextureFactory.GetIcon("lock");
                WarcraftUiFactory.SetAnchors(iconRt, new Vector2(0.68f, 0.5f), new Vector2(0.68f, 0.5f),
                    new Vector2(2f, -12f), new Vector2(26f, 12f));
            }

            Button joinRowBtn = WarcraftUiFactory.CreateButton("JoinBtn", row.transform,
                LocalizationManager.Get("lobby.room.join"),
                WarcraftUiFactory.BtnPrimary, () =>
                {
                    if (r.hasPassword)
                    {
                        OpenJoinModal(r.roomCode, true);
                    }
                    else
                    {
                        _lobbyErrorText.text = "";
                        OnJoinRoomRequested?.Invoke(r.roomCode, GetDefaultName(), "");
                    }
                }, 17);
            WarcraftUiFactory.SetAnchors(joinRowBtn.GetComponent<RectTransform>(),
                new Vector2(0.84f, 0.14f), new Vector2(0.98f, 0.86f), Vector2.zero, Vector2.zero);
        }

        private void CreateWaitingPlayerRow(RoomPlayerDto p, bool isMe)
        {
            Color bg = p.role == GameConstants.RoleWolf
                ? new Color(0.28f, 0.12f, 0.14f, 0.94f)
                : new Color(0.11f, 0.22f, 0.18f, 0.94f);

            Image row = WarcraftUiFactory.CreatePanel("PlayerRow_" + p.playerId, _waitPlayerListContent, bg);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 60f;

            string youTag = isMe ? ("  <color=#FFD54F>(" + LocalizationManager.Get("common.you") + ")</color>") : "";
            string hostTag = p.isHost ? ("  [" + LocalizationManager.Get("common.host") + "]") : "";
            Text nameTxt = WarcraftUiFactory.CreateText("Name", row.transform, p.name + youTag + hostTag, 20, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(nameTxt.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.68f, 1f), Vector2.zero, Vector2.zero);

            string roleStr = p.role == GameConstants.RoleWolf
                ? "<color=#FF7070>" + LocalizationManager.Get("wait.role.wolf") + "</color>"
                : "<color=#70FF9A>" + LocalizationManager.Get("wait.role.rabbit") + "</color>";
            Text roleTxt = WarcraftUiFactory.CreateText("Role", row.transform, roleStr, 18, Color.white,
                TextAnchor.MiddleRight, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(roleTxt.rectTransform, new Vector2(0.68f, 0f), new Vector2(0.96f, 1f), Vector2.zero, Vector2.zero);
        }

        private static Text AddColumnHeader(Transform parent, string name, float xMin, float xMax)
        {
            Text t = WarcraftUiFactory.CreateText(name, parent, "", 14,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold, shadow: false);
            WarcraftUiFactory.SetAnchors(t.rectTransform, new Vector2(xMin, 0f), new Vector2(xMax, 1f), Vector2.zero, Vector2.zero);
            return t;
        }

        private string GetDefaultName()
        {
            string val = _defaultNameInput != null ? _defaultNameInput.text.Trim() : "";
            return string.IsNullOrEmpty(val) ? LocalizationManager.Get("common.default_player") : val;
        }
    }
}

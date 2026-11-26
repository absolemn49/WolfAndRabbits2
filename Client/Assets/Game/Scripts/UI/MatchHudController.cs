using System;
using UnityEngine;
using UnityEngine.UI;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Protocol;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.UI
{
    public class MatchHudController : MonoBehaviour
    {
        public event Action<bool> OnFollowButtonHoldChanged;
        public event Action OnPrimaryActionPressed;
        public event Action OnSecondaryActionPressed;
        public event Action<string> OnBuildTypeSelected;
        public event Action OnUpgradeModeSelected;
        public event Action OnCancelBuildPressed;
        public event Action<string> OnBuyItemRequested;
        public event Action OnReturnToRoomRequested;
        public event Action OnLeaveToLobbyRequested;
        public event Action OnUiClickSound;

        public Vector2 JoystickDirection => _joystick != null ? _joystick.Direction : Vector2.zero;
        public bool IsShopOpen => _shopModal != null && _shopModal.activeSelf;
        public bool IsResultOpen => _resultModal != null && _resultModal.activeSelf;
        public bool IsSettingsOpen => _settings != null && _settings.IsOpen;

        private GameObject _root;
        private SettingsModalWidget _settings;
        private Text _phaseText;
        private Text _timerText;
        private Text _rabbitsAliveText;
        private Text _goldText;
        private Text _goldRateText;

        private Text _heroTitleText;
        private Text _heroHpText;
        private Image _heroHpFill;
        private Text _heroEquipText;

        // Wolf inventory slots bar (below HeroCard, beside Minimap)
        private GameObject _wolfInventoryBar;
        private Text _wolfSlotSummaryText;
        private RectTransform _wolfSlotGridRoot;
        private Image[] _wolfSlotIcons;
        private int _wolfMaxSlots = 6;

        // Live Minimap
        private RawImage _minimapImage;
        private Texture2D _minimapTex;
        private Color32[] _minimapPixels;
        private int _mapW;
        private int _mapH;
        private int[] _mapTiles;
        private float _minimapTimer;

        // Virtual joystick
        private VirtualJoystickWidget _joystick;

        // Bottom-right fan buttons
        private Text _followBtnHintText;
        private Button _primaryFanBtn;
        private Image _primaryFanIcon;
        private Text _primaryFanLabel;
        private Button _secondaryFanBtn;
        private Image _secondaryFanBg;
        private Image _secondaryFanIcon;
        private Text _secondaryFanLabel;

        // Build bar
        private GameObject _buildBar;
        private Text _buildBarHint;
        private Button _buildGateBtn;
        private Text _buildGateTitle;
        private Button _buildMineBtn;
        private Text _buildMineTitle;
        private Button _buildCannonBtn;
        private Text _buildCannonTitle;
        private Button _buildUpgradeBtn;
        private Text _buildUpgradeTitle;
        private Text _buildCancelLabel;
        private string _activeBuildMode = "";

        // Wolf shop modal
        private GameObject _shopModal;
        private Text _shopTitleText;
        private Text _shopCloseBtnLabel;
        private Button _shopSlotTabBtn;
        private Image _shopSlotTabBg;
        private Text _shopSlotTabLabel;
        private Button _shopFruitTabBtn;
        private Image _shopFruitTabBg;
        private Text _shopFruitTabLabel;
        private RectTransform _shopListContent;
        private Text _shopGoldHeader;
        private ShopItemDto[] _shopCatalog;
        private string _activeShopTab = "slot";
        private string _lastShopRenderKey = "";

        // Match Result modal
        private GameObject _resultModal;
        private Text _resultWinnerText;
        private Text _resultReasonText;
        private Text _resultBackLobbyLabel;
        private Text _resultBackRoomLabel;
        private RectTransform _resultStatsContent;
        private MatchEndMsg _lastMatchEnd;
        private string _lastLocalRole = "";

        // Toast & Wolf Spawn Wait Banner
        private GameObject _toastGo;
        private Text _toastText;
        private float _toastTimer;
        private GameObject _wolfWaitBanner;
        private Text _wolfWaitBannerText;

        private bool _isWolf;
        private StateSnapshotMsg _lastSnap;
        private PlayerSnapshotDto _lastLocalPlayer;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        public void SetSettingsOpen(bool open)
        {
            _settings?.SetOpen(open);
        }

        public void Build(Transform canvasRoot)
        {
            _root = WarcraftUiFactory.CreateRect("MatchHudRoot", canvasRoot).gameObject;
            WarcraftUiFactory.Stretch(_root.GetComponent<RectTransform>());
            if (PlatformProfile.UseSafeArea)
                _root.AddComponent<SafeAreaFitter>();

            BuildTopCenterHud();
            BuildTopRightGoldHud();
            BuildTopLeftHeroAndMinimap();
            if (PlatformProfile.ShowVirtualJoystick)
                BuildBottomLeftJoystick();
            BuildBottomRightFanButtons();
            BuildRabbitBuildBar();
            BuildWolfShopModal();
            BuildMatchResultModal();
            BuildToastBanner();

            // Gear Settings button next to top-right Gold panel
            _settings = SettingsModalWidget.Attach(
                _root.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-272f, -68f), new Vector2(-220f, -16f));
            _settings.OnButtonClickedSound += () => OnUiClickSound?.Invoke();

            ApplyStaticLocalization();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (!visible)
            {
                _joystick?.ResetStick();
                SetBuildBarVisible(false);
                SetShopOpen(false);
                if (_resultModal != null) _resultModal.SetActive(false);
                if (_wolfWaitBanner != null) _wolfWaitBanner.SetActive(false);
            }
        }

        public void ConfigureForMatch(MatchStartMsg startMsg)
        {
            _isWolf = startMsg.yourRole == "Wolf";
            _shopCatalog = WolfShopClientCatalog.ResolveCatalog(startMsg.shopItems, out _wolfMaxSlots, startMsg.wolfMaxSlots);
            _activeShopTab = "slot";
            _lastShopRenderKey = "";
            _mapW = startMsg.mapWidth;
            _mapH = startMsg.mapHeight;
            _mapTiles = startMsg.tiles;

            if (_wolfInventoryBar != null)
            {
                _wolfInventoryBar.SetActive(_isWolf);
                if (_isWolf)
                    RebuildWolfSlotBoxes(_wolfMaxSlots);
            }

            if (_minimapTex != null) Destroy(_minimapTex);
            _minimapTex = new Texture2D(_mapW, _mapH, TextureFormat.RGBA32, false);
            _minimapTex.filterMode = FilterMode.Point;
            _minimapPixels = new Color32[_mapW * _mapH];
            _minimapImage.texture = _minimapTex;

            // Configure primary & secondary fan buttons by role & platform
            if (_isWolf)
            {
                _primaryFanIcon.sprite = ProceduralTextureFactory.GetIcon("attack");
                _secondaryFanIcon.sprite = ProceduralTextureFactory.GetIcon("shop");
            }
            else
            {
                _primaryFanIcon.sprite = ProceduralTextureFactory.GetIcon("build");
                _secondaryFanIcon.sprite = ProceduralTextureFactory.GetIcon("upgrade");
            }

            ApplyStaticLocalization();
            SetBuildBarVisible(false);
            SetShopOpen(false);
            if (_resultModal != null) _resultModal.SetActive(false);
            SetVisible(true);
        }

        private void HandleLanguageChanged(GameLanguage _)
        {
            _lastShopRenderKey = "";
            ApplyStaticLocalization();
            if (_lastSnap != null && _lastLocalPlayer != null)
                UpdateHud(_lastSnap, _lastLocalPlayer, null);
            if (_buildBar != null && _buildBar.activeSelf)
                SetBuildBarVisible(true, _activeBuildMode);
            if (_resultModal != null && _resultModal.activeSelf && _lastMatchEnd != null)
                ShowMatchResult(_lastMatchEnd, _lastLocalRole);
        }

        private void ApplyStaticLocalization()
        {
            bool showKeys = PlatformProfile.ShowKeyboardHints;

            if (_followBtnHintText != null)
                _followBtnHintText.text = LocalizationManager.Get("hud.hold_follow");

            if (_primaryFanLabel != null && _secondaryFanLabel != null)
            {
                if (_isWolf)
                {
                    _primaryFanLabel.text = showKeys ? LocalizationManager.Get("hud.btn.attack_key") : LocalizationManager.Get("hud.btn.attack");
                    _secondaryFanLabel.text = LocalizationManager.Get("hud.btn.shop");
                }
                else
                {
                    _primaryFanLabel.text = showKeys ? LocalizationManager.Get("hud.btn.build_key") : LocalizationManager.Get("hud.btn.build");
                    _secondaryFanLabel.text = LocalizationManager.Get("hud.btn.upgrade");
                }
            }

            if (_buildGateTitle != null) _buildGateTitle.text = LocalizationManager.Get("building.gate");
            if (_buildMineTitle != null) _buildMineTitle.text = LocalizationManager.Get("building.gold_mine");
            if (_buildCannonTitle != null) _buildCannonTitle.text = LocalizationManager.Get("building.cannon");
            if (_buildUpgradeTitle != null) _buildUpgradeTitle.text = LocalizationManager.Get("building.upgrade");
            if (_buildCancelLabel != null) _buildCancelLabel.text = LocalizationManager.Get("common.cancel");

            if (_shopTitleText != null)
                _shopTitleText.text = LocalizationManager.Get("shop.title");
            if (_shopCloseBtnLabel != null)
                _shopCloseBtnLabel.text = showKeys ? LocalizationManager.Get("common.close_esc") : LocalizationManager.Get("common.close");

            if (_resultBackLobbyLabel != null)
                _resultBackLobbyLabel.text = LocalizationManager.Get("result.back_lobby");
            if (_resultBackRoomLabel != null)
                _resultBackRoomLabel.text = LocalizationManager.Get("result.rematch");
        }

        public void UpdateHud(StateSnapshotMsg snap, PlayerSnapshotDto localPlayer, FogOfWarController fog)
        {
            if (snap == null || localPlayer == null) return;
            _lastSnap = snap;
            _lastLocalPlayer = localPlayer;

            bool isPrep = snap.phase == "Prep";
            _phaseText.text = isPrep
                ? "<color=#70FF9A>" + LocalizationManager.Get("hud.phase.prep") + "</color>"
                : "<color=#FF785A>" + LocalizationManager.Get("hud.phase.hunt") + "</color>";

            int totalSec = Mathf.CeilToInt(Mathf.Max(0f, snap.phaseTimeRemaining));
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            _timerText.text = string.Format("{0:00}:{1:00}", mins, secs);
            _rabbitsAliveText.text = LocalizationManager.Format("hud.rabbits_alive", snap.aliveRabbits, snap.totalRabbits);

            _goldText.text = localPlayer.gold.ToString();
            _goldRateText.text = "+" + localPlayer.goldPerSec.ToString("0.#") + "/s";

            string roleTitle = _isWolf
                ? "<color=#FF7070>" + LocalizationManager.Get("role.wolf.upper") + "</color>"
                : "<color=#70FF9A>" + LocalizationManager.Get("role.rabbit.upper") + "</color>";
            string statusTag = "";
            if (!localPlayer.alive)
                statusTag = "  <color=#FF5555>(" + LocalizationManager.Get("hud.defeated") + ")</color>";
            else if (!localPlayer.spawned)
                statusTag = "  <color=#FFD54F>(" + LocalizationManager.Format("hud.wolf_waiting_tag", totalSec) + ")</color>";
            _heroTitleText.text = roleTitle + "  " + localPlayer.name + statusTag;
            _heroHpText.text = localPlayer.hp + " / " + localPlayer.maxHp + " HP";
            float hpRatio = localPlayer.maxHp > 0 ? Mathf.Clamp01(localPlayer.hp / (float)localPlayer.maxHp) : 0f;
            _heroHpFill.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            _heroHpFill.color = _isWolf ? new Color(0.90f, 0.24f, 0.22f) : new Color(0.26f, 0.85f, 0.45f);

            if (_wolfWaitBanner != null)
            {
                bool showWait = _isWolf && localPlayer.alive && !localPlayer.spawned;
                _wolfWaitBanner.SetActive(showWait);
                if (showWait)
                    _wolfWaitBannerText.text = LocalizationManager.Format("hud.wolf_wait_banner", string.Format("{0:00}:{1:00}", mins, secs));
            }

            if (_isWolf)
            {
                _heroEquipText.text = LocalizationManager.Format(
                    "hud.wolf_stats",
                    localPlayer.attackDamage,
                    Mathf.RoundToInt(localPlayer.attackSpeedPercent),
                    Mathf.RoundToInt(localPlayer.moveSpeedPercent),
                    localPlayer.armor);

                UpdateWolfInventoryBar(localPlayer);

                bool showKeys = PlatformProfile.ShowKeyboardHints;
                bool wolfActive = localPlayer.alive && localPlayer.spawned;
                _primaryFanBtn.interactable = wolfActive;
                if (!localPlayer.spawned)
                    _primaryFanLabel.text = totalSec + "s";
                else if (localPlayer.attackCooldown > 0.05f)
                    _primaryFanLabel.text = localPlayer.attackCooldown.ToString("0.0") + "s";
                else
                    _primaryFanLabel.text = showKeys ? LocalizationManager.Get("hud.btn.attack_key") : LocalizationManager.Get("hud.btn.attack");

                bool canShop = wolfActive && localPlayer.nearShop;
                _secondaryFanBtn.interactable = canShop;
                _secondaryFanBg.color = canShop ? new Color(0.75f, 0.56f, 0.16f, 0.96f) : new Color(0.18f, 0.20f, 0.24f, 0.45f);
                _secondaryFanIcon.color = canShop ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                _secondaryFanLabel.text = (canShop && showKeys)
                    ? LocalizationManager.Get("hud.btn.shop_key")
                    : LocalizationManager.Get("hud.btn.shop");

                if (_shopModal.activeSelf)
                    RefreshShopItems(localPlayer);
            }
            else
            {
                _heroEquipText.text = PlatformProfile.ShowKeyboardHints
                    ? LocalizationManager.Get("hud.rabbit_hint_web")
                    : LocalizationManager.Get("hud.rabbit_hint_mobile");
                _secondaryFanBtn.interactable = localPlayer.alive;
                _secondaryFanBg.color = new Color(0.20f, 0.46f, 0.32f, 0.92f);
                _secondaryFanIcon.color = Color.white;

                if (_buildBar.activeSelf)
                {
                    _buildGateBtn.interactable = localPlayer.gold >= 50;
                    _buildMineBtn.interactable = localPlayer.gold >= 60;
                    _buildCannonBtn.interactable = localPlayer.gold >= 90;
                }
            }

            if (fog != null)
            {
                _minimapTimer -= Time.deltaTime;
                if (_minimapTimer <= 0f)
                {
                    _minimapTimer = 0.2f;
                    RedrawMinimap(snap, localPlayer, fog);
                }
            }
        }

        public void SetBuildBarVisible(bool visible, string activeMode = "")
        {
            _activeBuildMode = activeMode ?? "";
            if (_buildBar != null) _buildBar.SetActive(visible);
            if (visible && _buildBarHint != null)
            {
                string cancelHint = PlatformProfile.ShowKeyboardHints
                    ? LocalizationManager.Get("build.cancel_web")
                    : LocalizationManager.Get("build.cancel_mobile");
                switch (activeMode)
                {
                    case "gate": _buildBarHint.text = LocalizationManager.Get("build.hint.gate") + cancelHint; break;
                    case "gold_mine": _buildBarHint.text = LocalizationManager.Get("build.hint.gold_mine") + cancelHint; break;
                    case "cannon": _buildBarHint.text = LocalizationManager.Get("build.hint.cannon") + cancelHint; break;
                    case "upgrade": _buildBarHint.text = LocalizationManager.Get("build.hint.upgrade") + cancelHint; break;
                    default: _buildBarHint.text = LocalizationManager.Get("build.hint.default") + cancelHint; break;
                }
            }
        }

        public void SetShopOpen(bool open, PlayerSnapshotDto wolf = null)
        {
            if (_shopModal == null) return;
            _shopModal.SetActive(open);
            if (open)
            {
                _lastShopRenderKey = "";
                PlayerSnapshotDto target = wolf ?? _lastLocalPlayer;
                if (target != null)
                    RefreshShopItems(target);
            }
        }

        public void ShowMatchResult(MatchEndMsg endMsg, string localRole)
        {
            _lastMatchEnd = endMsg;
            _lastLocalRole = localRole;
            SetBuildBarVisible(false);
            SetShopOpen(false);
            if (_wolfWaitBanner != null) _wolfWaitBanner.SetActive(false);
            _resultModal.SetActive(true);

            bool iWon = (endMsg.winner == "Wolf" && localRole == "Wolf") ||
                        (endMsg.winner == "Rabbits" && localRole == "Rabbit");

            string teamLabel = endMsg.winner == "Wolf"
                ? LocalizationManager.Get("result.wolf_win")
                : LocalizationManager.Get("result.rabbits_win");
            string prefix = iWon
                ? ("<color=#FFD54F>" + LocalizationManager.Get("result.victory"))
                : ("<color=#FF7070>" + LocalizationManager.Get("result.defeat"));
            _resultWinnerText.text = prefix + teamLabel + "</color>";

            int dSec = Mathf.RoundToInt(endMsg.durationSec);
            string durStr = LocalizationManager.Format("result.duration", dSec / 60, dSec % 60);
            _resultReasonText.text = LocalizationManager.TranslateMatchReason(endMsg.reason) + "   •   " + durStr;

            for (int i = _resultStatsContent.childCount - 1; i >= 0; i--)
                Destroy(_resultStatsContent.GetChild(i).gameObject);

            if (endMsg.stats != null)
            {
                for (int i = 0; i < endMsg.stats.Length; i++)
                {
                    PlayerMatchStatDto st = endMsg.stats[i];
                    Image row = WarcraftUiFactory.CreatePanel("StatRow_" + i, _resultStatsContent, WarcraftUiFactory.PanelInner);
                    LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
                    le.preferredHeight = 48f;

                    string rTag = st.role == "Wolf"
                        ? "<color=#FF7070>" + LocalizationManager.Get("role.wolf.short") + "</color>"
                        : "<color=#70FF9A>" + LocalizationManager.Get("role.rabbit.short") + "</color>";
                    string statusTag = st.alive
                        ? "<color=#80FF9E>" + LocalizationManager.Get("result.alive") + "</color>"
                        : "<color=#FF7A7A>" + LocalizationManager.Get("result.slain") + "</color>";
                    string detail = st.role == "Wolf"
                        ? LocalizationManager.Format("result.stat_wolf", st.buildingsDestroyed, st.damageDealt, st.goldEarned)
                        : LocalizationManager.Format("result.stat_rabbit", st.buildingsBuilt, st.damageDealt, st.goldEarned);

                    Text tLeft = WarcraftUiFactory.CreateText("Left", row.transform,
                        rTag + "  <b>" + st.name + "</b>  (" + statusTag + ")", 16, Color.white, TextAnchor.MiddleLeft);
                    WarcraftUiFactory.SetAnchors(tLeft.rectTransform, new Vector2(0.03f, 0f), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);

                    Text tRight = WarcraftUiFactory.CreateText("Right", row.transform, detail, 15,
                        new Color(0.92f, 0.90f, 0.72f), TextAnchor.MiddleRight);
                    WarcraftUiFactory.SetAnchors(tRight.rectTransform, new Vector2(0.46f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);
                }
            }
        }

        public void ShowToast(string message, float duration = 2.6f)
        {
            if (_toastGo == null) return;
            _toastText.text = message;
            _toastGo.SetActive(true);
            _toastTimer = duration;
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && _toastGo != null)
                    _toastGo.SetActive(false);
            }
        }

        private void BuildTopCenterHud()
        {
            Image box = WarcraftUiFactory.CreatePanel("TopCenterHud", _root.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-175f, -86f), new Vector2(175f, -10f));

            _phaseText = WarcraftUiFactory.CreateText("Phase", box.transform, "CHUẨN BỊ", 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_phaseText.rectTransform, new Vector2(0.06f, 0.50f), new Vector2(0.55f, 0.94f), Vector2.zero, Vector2.zero);

            _timerText = WarcraftUiFactory.CreateText("Timer", box.transform, "00:30", 26, WarcraftUiFactory.GoldAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_timerText.rectTransform, new Vector2(0.52f, 0.44f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);

            _rabbitsAliveText = WarcraftUiFactory.CreateText("AliveRabbits", box.transform, "Thỏ sống: 1/1", 15,
                new Color(0.82f, 0.92f, 0.96f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_rabbitsAliveText.rectTransform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero);
        }

        private void BuildTopRightGoldHud()
        {
            // Gold next to Coin icon at top-right corner!
            Image box = WarcraftUiFactory.CreatePanel("TopRightGold", _root.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-210f, -72f), new Vector2(-14f, -12f));

            RectTransform coinRt = WarcraftUiFactory.CreateRect("CoinIcon", box.transform);
            Image coinImg = coinRt.gameObject.AddComponent<Image>();
            coinImg.sprite = ProceduralTextureFactory.GetIcon("coin");
            WarcraftUiFactory.SetAnchors(coinRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, -21f), new Vector2(54f, 21f));

            _goldText = WarcraftUiFactory.CreateText("GoldValue", box.transform, "0", 28,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_goldText.rectTransform, new Vector2(0f, 0.32f), new Vector2(1f, 0.95f),
                new Vector2(64f, 0f), new Vector2(-12f, 0f));

            _goldRateText = WarcraftUiFactory.CreateText("GoldRate", box.transform, "+1/s", 13,
                new Color(0.75f, 0.96f, 0.72f), TextAnchor.MiddleLeft);
            WarcraftUiFactory.SetAnchors(_goldRateText.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.36f),
                new Vector2(64f, 0f), new Vector2(-12f, 0f));
        }

        private void BuildTopLeftHeroAndMinimap()
        {
            // Compact Hero HP card (widened to fit all 5 Wolf stats cleanly)
            Image heroCard = WarcraftUiFactory.CreatePanel("HeroCard", _root.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(heroCard.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -96f), new Vector2(364f, -12f));

            _heroTitleText = WarcraftUiFactory.CreateText("HeroTitle", heroCard.transform, "", 16, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_heroTitleText.rectTransform, new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);

            Image hpBg = WarcraftUiFactory.CreatePanel("HpBg", heroCard.transform, new Color(0.06f, 0.08f, 0.11f, 0.95f), false);
            WarcraftUiFactory.SetAnchors(hpBg.rectTransform, new Vector2(0.04f, 0.32f), new Vector2(0.96f, 0.58f), Vector2.zero, Vector2.zero);

            Image hpFill = WarcraftUiFactory.CreatePanel("HpFill", hpBg.transform, new Color(0.28f, 0.85f, 0.45f), false);
            _heroHpFill = hpFill;
            WarcraftUiFactory.SetAnchors(hpFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            _heroHpText = WarcraftUiFactory.CreateText("HpVal", hpBg.transform, "100/100", 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.Stretch(_heroHpText.rectTransform);

            _heroEquipText = WarcraftUiFactory.CreateText("HeroEquip", heroCard.transform, "", 11,
                new Color(0.84f, 0.90f, 0.98f), TextAnchor.MiddleLeft);
            WarcraftUiFactory.SetAnchors(_heroEquipText.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.30f), Vector2.zero, Vector2.zero);

            // Compact Warcraft-style Minimap below hero card
            Image mapFrame = WarcraftUiFactory.CreatePanel("MinimapFrame", _root.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(mapFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -244f), new Vector2(154f, -104f));

            RectTransform rawRt = WarcraftUiFactory.CreateRect("MinimapRaw", mapFrame.transform);
            WarcraftUiFactory.Stretch(rawRt, 6f, 6f);
            _minimapImage = rawRt.gameObject.AddComponent<RawImage>();

            // Wolf Equipment Slots Bar beside the Minimap (only visible when playing as Wolf)
            Image invBar = WarcraftUiFactory.CreatePanel("WolfInventoryBar", _root.transform, WarcraftUiFactory.PanelDark);
            _wolfInventoryBar = invBar.gameObject;
            WarcraftUiFactory.SetAnchors(invBar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(162f, -206f), new Vector2(364f, -104f));

            _wolfSlotSummaryText = WarcraftUiFactory.CreateText("WolfSlotSummary", invBar.transform, "", 11,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_wolfSlotSummaryText.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

            _wolfSlotGridRoot = WarcraftUiFactory.CreateRect("WolfSlotGrid", invBar.transform);
            WarcraftUiFactory.SetAnchors(_wolfSlotGridRoot, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.70f), Vector2.zero, Vector2.zero);
            GridLayoutGroup grid = _wolfSlotGridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(28f, 28f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperLeft;

            _wolfInventoryBar.SetActive(false);
        }

        private void RebuildWolfSlotBoxes(int maxSlots)
        {
            if (_wolfSlotGridRoot == null) return;
            for (int i = _wolfSlotGridRoot.childCount - 1; i >= 0; i--)
                Destroy(_wolfSlotGridRoot.GetChild(i).gameObject);

            int clamped = Mathf.Clamp(maxSlots, 1, 12);
            _wolfSlotIcons = new Image[clamped];
            for (int i = 0; i < clamped; i++)
            {
                Image slotBg = WarcraftUiFactory.CreatePanel("Slot_" + i, _wolfSlotGridRoot, new Color(0.08f, 0.11f, 0.16f, 0.95f));
                RectTransform iconRt = WarcraftUiFactory.CreateRect("Icon", slotBg.transform);
                WarcraftUiFactory.Stretch(iconRt, 3f, 3f);
                Image icon = iconRt.gameObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.enabled = false;
                _wolfSlotIcons[i] = icon;
            }
        }

        private void UpdateWolfInventoryBar(PlayerSnapshotDto wolf)
        {
            if (_wolfInventoryBar == null || !_wolfInventoryBar.activeSelf) return;

            int slotCount = wolf.slotItems != null ? wolf.slotItems.Length : 0;
            _wolfSlotSummaryText.text = LocalizationManager.Format("hud.wolf_slots_label", slotCount, _wolfMaxSlots, wolf.fruitsEaten);

            if (_wolfSlotIcons == null) return;
            for (int i = 0; i < _wolfSlotIcons.Length; i++)
            {
                Image icon = _wolfSlotIcons[i];
                if (icon == null) continue;

                if (wolf.slotItems != null && i < wolf.slotItems.Length && !string.IsNullOrEmpty(wolf.slotItems[i]))
                {
                    ShopItemDto item = WolfShopClientCatalog.FindById(_shopCatalog, wolf.slotItems[i]);
                    string iconKey = item != null ? item.icon : wolf.slotItems[i];
                    icon.sprite = WolfShopClientCatalog.GetItemIcon(iconKey);
                    icon.color = Color.white;
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }
        }

        private void BuildBottomLeftJoystick()
        {
            RectTransform bgRt = WarcraftUiFactory.CreateRect("VirtualJoystick", _root.transform);
            WarcraftUiFactory.SetAnchors(bgRt, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 28f), new Vector2(172f, 172f));
            Image bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = ProceduralTextureFactory.CircleSprite;
            bgImg.color = new Color(0.10f, 0.14f, 0.20f, 0.50f);

            RectTransform ringRt = WarcraftUiFactory.CreateRect("StickRim", bgRt);
            WarcraftUiFactory.Stretch(ringRt, 4f, 4f);
            Image rimImg = ringRt.gameObject.AddComponent<Image>();
            rimImg.sprite = ProceduralTextureFactory.CircleSprite;
            rimImg.color = new Color(0.95f, 0.78f, 0.32f, 0.22f);
            rimImg.raycastTarget = false;

            RectTransform knobRt = WarcraftUiFactory.CreateRect("StickKnob", bgRt);
            knobRt.anchorMin = new Vector2(0.5f, 0.5f);
            knobRt.anchorMax = new Vector2(0.5f, 0.5f);
            knobRt.sizeDelta = new Vector2(58f, 58f);
            Image knobImg = knobRt.gameObject.AddComponent<Image>();
            knobImg.sprite = ProceduralTextureFactory.CircleSprite;
            knobImg.color = new Color(0.95f, 0.80f, 0.36f, 0.82f);
            knobImg.raycastTarget = false;

            _joystick = bgRt.gameObject.AddComponent<VirtualJoystickWidget>();
            _joystick.Setup(bgRt, knobRt, 54f);
        }

        private void BuildBottomRightFanButtons()
        {
            // Anchor container at bottom-right corner
            RectTransform corner = WarcraftUiFactory.CreateRect("BottomRightFan", _root.transform);
            WarcraftUiFactory.SetAnchors(corner, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-280f, 16f), new Vector2(-16f, 280f));

            // 1. Large Hold-to-Follow Character Button at bottom-right!
            RectTransform followRt = WarcraftUiFactory.CreateRect("FollowHeroBtn", corner);
            followRt.anchorMin = new Vector2(1f, 0f);
            followRt.anchorMax = new Vector2(1f, 0f);
            followRt.pivot = new Vector2(0.5f, 0.5f);
            followRt.anchoredPosition = new Vector2(-60f, 60f);
            followRt.sizeDelta = new Vector2(106f, 106f);

            Image followBg = followRt.gameObject.AddComponent<Image>();
            followBg.sprite = ProceduralTextureFactory.CircleSprite;
            followBg.color = new Color(0.16f, 0.24f, 0.36f, 0.92f);

            RectTransform followIconRt = WarcraftUiFactory.CreateRect("Icon", followRt);
            WarcraftUiFactory.Stretch(followIconRt, 22f, 22f);
            Image followIcon = followIconRt.gameObject.AddComponent<Image>();
            followIcon.sprite = ProceduralTextureFactory.GetIcon("follow");
            followIcon.raycastTarget = false;

            _followBtnHintText = WarcraftUiFactory.CreateText("Hint", followRt, "GIỮ", 13, WarcraftUiFactory.GoldAccent, TextAnchor.LowerCenter, FontStyle.Bold);
            WarcraftUiFactory.Stretch(_followBtnHintText.rectTransform, 0f, 8f);
            _followBtnHintText.raycastTarget = false;

            HoldButtonHandler hold = followRt.gameObject.AddComponent<HoldButtonHandler>();
            hold.OnHoldChanged += held => OnFollowButtonHoldChanged?.Invoke(held);

            // 2. Four circular buttons arranged in a fan arc above-and-left of the large button!
            // Angles from 95 deg (top-left of big button) sweeping to 180 deg (left of big button)
            float arcRadius = 132f;
            Vector2 arcCenter = new Vector2(-60f, 60f);
            float[] anglesDeg = { 176f, 149f, 122f, 95f };

            // Slot 0: Primary Action (Build for Rabbit / Attack for Wolf)
            _primaryFanBtn = CreateFanCircleButton(corner, "FanBtn_Primary",
                arcCenter + Polar(anglesDeg[0], arcRadius), 74f,
                new Color(0.76f, 0.34f, 0.16f, 0.96f), "build", "Xây [E]",
                () => OnPrimaryActionPressed?.Invoke(), out _, out _primaryFanIcon, out _primaryFanLabel);

            // Slot 1: Secondary Action (Upgrade for Rabbit / Shop for Wolf)
            _secondaryFanBtn = CreateFanCircleButton(corner, "FanBtn_Secondary",
                arcCenter + Polar(anglesDeg[1], arcRadius), 70f,
                new Color(0.22f, 0.48f, 0.34f, 0.94f), "upgrade", "Nâng cấp",
                () => OnSecondaryActionPressed?.Invoke(), out _secondaryFanBg, out _secondaryFanIcon, out _secondaryFanLabel);

            // Slot 2 & 3: Unused function slots — displayed dimmed as specified!
            Button unused1 = CreateFanCircleButton(corner, "FanBtn_Unused1",
                arcCenter + Polar(anglesDeg[2], arcRadius), 64f,
                new Color(0.16f, 0.19f, 0.24f, 0.40f), "default", "", null, out _, out Image uIcon1, out _);
            unused1.interactable = false;
            uIcon1.color = new Color(1f, 1f, 1f, 0.22f);

            Button unused2 = CreateFanCircleButton(corner, "FanBtn_Unused2",
                arcCenter + Polar(anglesDeg[3], arcRadius), 64f,
                new Color(0.16f, 0.19f, 0.24f, 0.40f), "default", "", null, out _, out Image uIcon2, out _);
            unused2.interactable = false;
            uIcon2.color = new Color(1f, 1f, 1f, 0.22f);
        }

        private Button CreateFanCircleButton(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            float size,
            Color bgCol,
            string iconKey,
            string label,
            Action onClick,
            out Image bgOut,
            out Image iconOut,
            out Text labelOut)
        {
            RectTransform rt = WarcraftUiFactory.CreateRect(name, parent);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(size, size);

            bgOut = rt.gameObject.AddComponent<Image>();
            bgOut.sprite = ProceduralTextureFactory.CircleSprite;
            bgOut.color = bgCol;

            Button btn = rt.gameObject.AddComponent<Button>();
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            RectTransform iconRt = WarcraftUiFactory.CreateRect("Icon", rt);
            WarcraftUiFactory.Stretch(iconRt, size * 0.22f, size * 0.22f);
            iconOut = iconRt.gameObject.AddComponent<Image>();
            iconOut.sprite = ProceduralTextureFactory.GetIcon(iconKey);
            iconOut.raycastTarget = false;

            labelOut = WarcraftUiFactory.CreateText("Label", rt, label, 12, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
            labelOut.rectTransform.anchorMin = new Vector2(-0.3f, -0.32f);
            labelOut.rectTransform.anchorMax = new Vector2(1.3f, 0.12f);
            labelOut.rectTransform.offsetMin = Vector2.zero;
            labelOut.rectTransform.offsetMax = Vector2.zero;
            labelOut.raycastTarget = false;

            return btn;
        }

        private void BuildRabbitBuildBar()
        {
            Image panel = WarcraftUiFactory.CreatePanel("BuildMenuBar", _root.transform, WarcraftUiFactory.PanelDark);
            _buildBar = panel.gameObject;
            WarcraftUiFactory.SetAnchors(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-285f, 18f), new Vector2(285f, 152f));

            _buildBarHint = WarcraftUiFactory.CreateText("Hint", panel.transform,
                "Chọn công trình muốn xây trong vùng 3×3 quanh bạn", 14,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_buildBarHint.rectTransform, new Vector2(0.02f, 0.72f), new Vector2(0.98f, 0.96f), Vector2.zero, Vector2.zero);

            _buildGateBtn = CreateBuildCard(panel.transform, "Card_Gate", 0.02f, 0.24f, "gate", "Cổng", "50g • 300 HP",
                () => OnBuildTypeSelected?.Invoke("gate"), out _buildGateTitle);
            _buildMineBtn = CreateBuildCard(panel.transform, "Card_Mine", 0.25f, 0.47f, "gold_mine", "Nhà tiền", "60g • +2g/s",
                () => OnBuildTypeSelected?.Invoke("gold_mine"), out _buildMineTitle);
            _buildCannonBtn = CreateBuildCard(panel.transform, "Card_Cannon", 0.48f, 0.70f, "cannon", "Pháo", "90g • 12 DMG",
                () => OnBuildTypeSelected?.Invoke("cannon"), out _buildCannonTitle);
            _buildUpgradeBtn = CreateBuildCard(panel.transform, "Card_Upgrade", 0.71f, 0.88f, "upgrade", "Nâng cấp", "Lv2 / Lv3",
                () => OnUpgradeModeSelected?.Invoke(), out _buildUpgradeTitle);

            Button cancelBtn = WarcraftUiFactory.CreateButton("CancelBuild", panel.transform, "Hủy", WarcraftUiFactory.BtnDanger,
                () => OnCancelBuildPressed?.Invoke(), 15);
            _buildCancelLabel = cancelBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(cancelBtn.GetComponent<RectTransform>(),
                new Vector2(0.89f, 0.10f), new Vector2(0.98f, 0.68f), Vector2.zero, Vector2.zero);
        }

        private Button CreateBuildCard(Transform parent, string name, float x0, float x1, string iconKey, string title, string sub, Action onClick, out Text titleOut)
        {
            Image bg = WarcraftUiFactory.CreatePanel(name, parent, WarcraftUiFactory.PanelInner);
            WarcraftUiFactory.SetAnchors(bg.rectTransform, new Vector2(x0, 0.08f), new Vector2(x1, 0.70f), Vector2.zero, Vector2.zero);
            Button btn = bg.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            RectTransform ic = WarcraftUiFactory.CreateRect("Icon", bg.transform);
            Image im = ic.gameObject.AddComponent<Image>();
            im.sprite = ProceduralTextureFactory.GetIcon(iconKey);
            im.raycastTarget = false;
            WarcraftUiFactory.SetAnchors(ic, new Vector2(0.5f, 0.46f), new Vector2(0.5f, 0.46f),
                new Vector2(-18f, -10f), new Vector2(18f, 26f));

            titleOut = WarcraftUiFactory.CreateText("Title", bg.transform, title, 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(titleOut.rectTransform, new Vector2(0f, 0.20f), new Vector2(1f, 0.44f), Vector2.zero, Vector2.zero);

            Text t2 = WarcraftUiFactory.CreateText("Sub", bg.transform, sub, 11, WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(t2.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero);

            return btn;
        }

        private void BuildWolfShopModal()
        {
            Image overlay = WarcraftUiFactory.CreatePanel("WolfShopOverlay", _root.transform, new Color(0f, 0f, 0f, 0.58f), false);
            _shopModal = overlay.gameObject;
            WarcraftUiFactory.Stretch(overlay.rectTransform);

            Image box = WarcraftUiFactory.CreatePanel("ShopBox", overlay.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-370f, -270f), new Vector2(370f, 270f));

            _shopTitleText = WarcraftUiFactory.CreateText("ShopTitle", box.transform, "", 23,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_shopTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(0.54f, 1f),
                new Vector2(22f, -50f), new Vector2(0f, -10f));

            _shopGoldHeader = WarcraftUiFactory.CreateText("ShopGold", box.transform, "", 16,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleRight, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_shopGoldHeader.rectTransform, new Vector2(0.46f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -50f), new Vector2(-22f, -10f));

            // 2 Category Tabs: Slot Equipment vs Permanent Fruit
            _shopSlotTabBtn = WarcraftUiFactory.CreateButton("TabSlotBtn", box.transform, "",
                WarcraftUiFactory.BtnPrimary, () => SelectShopTab("slot"), 16);
            _shopSlotTabBg = _shopSlotTabBtn.GetComponent<Image>();
            _shopSlotTabLabel = _shopSlotTabBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(_shopSlotTabBtn.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(0.50f, 1f), new Vector2(20f, -96f), new Vector2(-6f, -56f));

            _shopFruitTabBtn = WarcraftUiFactory.CreateButton("TabFruitBtn", box.transform, "",
                WarcraftUiFactory.BtnSecondary, () => SelectShopTab("fruit"), 16);
            _shopFruitTabBg = _shopFruitTabBtn.GetComponent<Image>();
            _shopFruitTabLabel = _shopFruitTabBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(_shopFruitTabBtn.GetComponent<RectTransform>(),
                new Vector2(0.50f, 1f), new Vector2(1f, 1f), new Vector2(6f, -96f), new Vector2(-20f, -56f));

            _shopListContent = WarcraftUiFactory.CreateScrollList("ShopScroll", box.transform, out ScrollRect sr);
            WarcraftUiFactory.SetAnchors(sr.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 70f), new Vector2(-20f, -106f));

            Button close = WarcraftUiFactory.CreateButton("CloseShopBtn", box.transform, "",
                WarcraftUiFactory.BtnDanger, () =>
                {
                    OnUiClickSound?.Invoke();
                    SetShopOpen(false);
                }, 18);
            _shopCloseBtnLabel = close.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(close.GetComponent<RectTransform>(),
                new Vector2(0.35f, 0f), new Vector2(0.65f, 0f), new Vector2(0f, 14f), new Vector2(0f, 56f));

            _shopModal.SetActive(false);
        }

        private void SelectShopTab(string tab)
        {
            OnUiClickSound?.Invoke();
            if (_activeShopTab == tab) return;
            _activeShopTab = tab;
            _lastShopRenderKey = "";
            if (_lastLocalPlayer != null)
                RefreshShopItems(_lastLocalPlayer);
        }

        private void RefreshShopItems(PlayerSnapshotDto wolf)
        {
            int slotCount = wolf.slotItems != null ? wolf.slotItems.Length : 0;

            _shopGoldHeader.text = LocalizationManager.Format("shop.gold_header", wolf.gold, slotCount, _wolfMaxSlots, wolf.fruitsEaten);

            bool slotTabActive = _activeShopTab != "fruit";
            if (_shopSlotTabLabel != null)
                _shopSlotTabLabel.text = LocalizationManager.Format("shop.tab.slot", slotCount, _wolfMaxSlots);
            if (_shopFruitTabLabel != null)
                _shopFruitTabLabel.text = LocalizationManager.Get("shop.tab.fruit");
            if (_shopSlotTabBg != null)
                _shopSlotTabBg.color = slotTabActive ? WarcraftUiFactory.BtnPrimary : new Color(0.18f, 0.22f, 0.30f, 1f);
            if (_shopFruitTabBg != null)
                _shopFruitTabBg.color = !slotTabActive ? new Color(0.18f, 0.56f, 0.32f, 1f) : new Color(0.18f, 0.22f, 0.30f, 1f);

            string renderKey = _activeShopTab + "|" + wolf.gold + "|" + slotCount + "|" + _wolfMaxSlots + "|" +
                               wolf.fruitsEaten + "|" + wolf.nearShop + "|" + (int)LocalizationManager.Current;
            if (renderKey == _lastShopRenderKey)
                return;
            _lastShopRenderKey = renderKey;

            for (int i = _shopListContent.childCount - 1; i >= 0; i--)
                Destroy(_shopListContent.GetChild(i).gameObject);

            if (_shopCatalog == null) return;

            bool slotsFull = slotCount >= _wolfMaxSlots;
            string goldUnit = LocalizationManager.Get("common.gold_unit");

            for (int i = 0; i < _shopCatalog.Length; i++)
            {
                ShopItemDto item = _shopCatalog[i];
                if (item == null) continue;

                bool isFruit = string.Equals(item.itemType, "fruit", StringComparison.OrdinalIgnoreCase);
                if (slotTabActive && isFruit) continue;
                if (!slotTabActive && !isFruit) continue;

                bool blockedBySlots = !isFruit && slotsFull;
                bool canBuy = wolf.gold >= item.cost && wolf.nearShop && !blockedBySlots;

                Image row = WarcraftUiFactory.CreatePanel("ShopItem_" + item.itemId, _shopListContent, WarcraftUiFactory.PanelInner);
                LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 66f;

                // Item Icon Box
                Image iconFrame = WarcraftUiFactory.CreatePanel("IconFrame", row.transform,
                    isFruit ? new Color(0.12f, 0.24f, 0.16f, 0.96f) : new Color(0.12f, 0.16f, 0.24f, 0.96f));
                WarcraftUiFactory.SetAnchors(iconFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(10f, -24f), new Vector2(58f, 24f));

                RectTransform iconRt = WarcraftUiFactory.CreateRect("ItemIcon", iconFrame.transform);
                WarcraftUiFactory.Stretch(iconRt, 5f, 5f);
                Image iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = WolfShopClientCatalog.GetItemIcon(item.icon);
                iconImg.raycastTarget = false;

                string itemName = LocalizationManager.GetShopItemDisplayName(item);
                string statLine = LocalizationManager.FormatShopStatLine(item.statType, item.statValue);
                string badgeColor = isFruit ? "#70FF9A" : "#80C8FF";
                string badgeText = isFruit
                    ? LocalizationManager.Get("shop.type.fruit_badge")
                    : LocalizationManager.Get("shop.type.slot_badge");

                Text nameTxt = WarcraftUiFactory.CreateText("Name", row.transform,
                    "<b>" + itemName + "</b>   <size=12><color=" + badgeColor + ">[" + badgeText + "]</color></size>\n" +
                    "<size=13><color=#FFD878>" + statLine + "</color></size>",
                    16, Color.white, TextAnchor.MiddleLeft);
                WarcraftUiFactory.SetAnchors(nameTxt.rectTransform, new Vector2(0f, 0f), new Vector2(0.74f, 1f),
                    new Vector2(68f, 0f), Vector2.zero);

                string btnText = blockedBySlots
                    ? LocalizationManager.Get("shop.slots_full")
                    : (item.cost + " " + goldUnit);
                Color btnCol = blockedBySlots
                    ? new Color(0.24f, 0.26f, 0.32f, 1f)
                    : (canBuy ? (isFruit ? new Color(0.18f, 0.56f, 0.30f, 1f) : WarcraftUiFactory.BtnPrimary) : new Color(0.36f, 0.24f, 0.20f, 1f));

                string capturedId = item.itemId;
                Button buyBtn = WarcraftUiFactory.CreateButton("BuyBtn", row.transform, btnText, btnCol,
                    () => OnBuyItemRequested?.Invoke(capturedId), 15);
                buyBtn.interactable = canBuy;
                WarcraftUiFactory.SetAnchors(buyBtn.GetComponent<RectTransform>(),
                    new Vector2(0.76f, 0.14f), new Vector2(0.97f, 0.86f), Vector2.zero, Vector2.zero);
            }
        }

        private void BuildMatchResultModal()
        {
            Image overlay = WarcraftUiFactory.CreatePanel("ResultOverlay", _root.transform, new Color(0f, 0f, 0f, 0.72f), false);
            _resultModal = overlay.gameObject;
            WarcraftUiFactory.Stretch(overlay.rectTransform);

            Image box = WarcraftUiFactory.CreatePanel("ResultBox", overlay.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -240f), new Vector2(380f, 240f));

            _resultWinnerText = WarcraftUiFactory.CreateText("Winner", box.transform, "KẾT QUẢ TRẬN ĐẤU", 28,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_resultWinnerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -62f), new Vector2(-20f, -14f));

            _resultReasonText = WarcraftUiFactory.CreateText("Reason", box.transform, "", 16,
                new Color(0.88f, 0.92f, 0.98f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_resultReasonText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -96f), new Vector2(-20f, -62f));

            _resultStatsContent = WarcraftUiFactory.CreateScrollList("ResultStats", box.transform, out ScrollRect sr);
            WarcraftUiFactory.SetAnchors(sr.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 82f), new Vector2(-24f, -104f));

            Button backLobby = WarcraftUiFactory.CreateButton("BackLobbyBtn", box.transform, "Về danh sách phòng",
                WarcraftUiFactory.BtnDanger, () => OnLeaveToLobbyRequested?.Invoke(), 18);
            _resultBackLobbyLabel = backLobby.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(backLobby.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0f), new Vector2(0.47f, 0f), new Vector2(0f, 20f), new Vector2(0f, 68f));

            Button backRoom = WarcraftUiFactory.CreateButton("BackRoomBtn", box.transform, "Chơi lại (Phòng chờ)",
                WarcraftUiFactory.BtnPrimary, () => OnReturnToRoomRequested?.Invoke(), 18);
            _resultBackRoomLabel = backRoom.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(backRoom.GetComponent<RectTransform>(),
                new Vector2(0.51f, 0f), new Vector2(0.95f, 0f), new Vector2(0f, 20f), new Vector2(0f, 68f));

            _resultModal.SetActive(false);
        }

        private void BuildToastBanner()
        {
            Image box = WarcraftUiFactory.CreatePanel("ToastBanner", _root.transform, new Color(0.08f, 0.12f, 0.18f, 0.92f));
            _toastGo = box.gameObject;
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-280f, -138f), new Vector2(280f, -94f));

            _toastText = WarcraftUiFactory.CreateText("ToastText", box.transform, "", 16, WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.Stretch(_toastText.rectTransform, 12f, 4f);
            _toastGo.SetActive(false);

            // Center countdown banner shown exclusively to the Wolf during the first 30s before spawning
            Image waitBox = WarcraftUiFactory.CreatePanel("WolfWaitBanner", _root.transform, new Color(0.26f, 0.08f, 0.08f, 0.90f));
            _wolfWaitBanner = waitBox.gameObject;
            WarcraftUiFactory.SetAnchors(waitBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-290f, 110f), new Vector2(290f, 162f));

            _wolfWaitBannerText = WarcraftUiFactory.CreateText("WolfWaitText", waitBox.transform, "", 20,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.Stretch(_wolfWaitBannerText.rectTransform, 16f, 6f);
            _wolfWaitBanner.SetActive(false);
        }

        private void RedrawMinimap(StateSnapshotMsg snap, PlayerSnapshotDto localPlayer, FogOfWarController fog)
        {
            if (_minimapTex == null || _mapTiles == null) return;

            for (int y = 0; y < _mapH; y++)
            {
                for (int x = 0; x < _mapW; x++)
                {
                    int idx = y * _mapW + x;
                    TileType tt = (TileType)_mapTiles[idx];
                    Color32 col;
                    switch (tt)
                    {
                        case TileType.Tree: col = new Color32(22, 68, 36, 255); break;
                        case TileType.BaseFloor: col = new Color32(158, 134, 92, 255); break;
                        case TileType.BaseEntrance: col = new Color32(235, 196, 86, 255); break;
                        case TileType.CenterPlaza: col = new Color32(130, 135, 146, 255); break;
                        case TileType.CenterShop: col = new Color32(255, 205, 55, 255); break;
                        default: col = new Color32(58, 126, 62, 255); break;
                    }

                    if (fog != null && !fog.IsTileVisible(x, y))
                    {
                        col = new Color32((byte)(col.r * 0.55f), (byte)(col.g * 0.55f), (byte)(col.b * 0.58f), 255);
                    }
                    _minimapPixels[idx] = col;
                }
            }

            // Plot visible players (Rabbits always see other Rabbits)
            if (snap.players != null)
            {
                for (int i = 0; i < snap.players.Length; i++)
                {
                    PlayerSnapshotDto p = snap.players[i];
                    if (!p.alive || !p.spawned) continue;
                    bool isSelf = p.playerId == localPlayer.playerId;
                    bool isAllyRabbit = !_isWolf && p.role == "Rabbit";
                    bool vis = isSelf || isAllyRabbit || (fog != null && fog.IsWorldPosVisible(p.x, p.y));
                    if (!vis) continue;

                    Color32 dot = isSelf
                        ? new Color32(255, 255, 255, 255)
                        : (p.role == "Wolf" ? new Color32(255, 55, 55, 255) : new Color32(80, 255, 135, 255));
                    PlotDot(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), dot);
                }
            }

            _minimapTex.SetPixels32(_minimapPixels);
            _minimapTex.Apply(false);
        }

        private void PlotDot(int cx, int cy, Color32 c)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = cx + ox;
                    int y = cy + oy;
                    if (x >= 0 && y >= 0 && x < _mapW && y < _mapH)
                        _minimapPixels[y * _mapW + x] = c;
                }
            }
        }

        private static Vector2 Polar(float angleDeg, float radius)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
        }
    }
}

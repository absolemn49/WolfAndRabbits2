using System;
using UnityEngine;
using UnityEngine.UI;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.UI
{
    /// <summary>
    /// Reusable Settings gear icon button and Settings modal with Vietnamese / English language selection.
    /// Used in both Lobby and Play scenes.
    /// </summary>
    public class SettingsModalWidget : MonoBehaviour
    {
        public event Action OnButtonClickedSound;

        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        private GameObject _overlay;
        private Text _titleText;
        private Text _langSectionLabel;
        private Button _viBtn;
        private Image _viBtnBg;
        private Text _viBtnLabel;
        private Button _enBtn;
        private Image _enBtnBg;
        private Text _enBtnLabel;
        private Text _infoFooterText;
        private Text _closeBtnLabel;

        public static SettingsModalWidget Attach(
            Transform parentRoot,
            Vector2 gearAnchorMin,
            Vector2 gearAnchorMax,
            Vector2 gearOffsetMin,
            Vector2 gearOffsetMax)
        {
            SettingsModalWidget widget = parentRoot.gameObject.AddComponent<SettingsModalWidget>();
            widget.Build(parentRoot, gearAnchorMin, gearAnchorMax, gearOffsetMin, gearOffsetMax);
            return widget;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        public void SetOpen(bool open)
        {
            if (_overlay != null)
            {
                _overlay.SetActive(open);
                if (open)
                {
                    _overlay.transform.SetAsLastSibling();
                    RefreshTexts();
                }
            }
        }

        private void Build(
            Transform parentRoot,
            Vector2 gearAnchorMin,
            Vector2 gearAnchorMax,
            Vector2 gearOffsetMin,
            Vector2 gearOffsetMax)
        {
            // 1. Gear Icon Button
            Image gearBg = WarcraftUiFactory.CreatePanel("SettingsGearBtn", parentRoot, new Color(0.14f, 0.19f, 0.28f, 0.94f));
            WarcraftUiFactory.SetAnchors(gearBg.rectTransform, gearAnchorMin, gearAnchorMax, gearOffsetMin, gearOffsetMax);

            Button gearBtn = gearBg.gameObject.AddComponent<Button>();
            ColorBlock cb = gearBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.16f, 1.12f, 1.0f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            gearBtn.colors = cb;
            gearBtn.onClick.AddListener(() =>
            {
                OnButtonClickedSound?.Invoke();
                SetOpen(!IsOpen);
            });

            RectTransform iconRt = WarcraftUiFactory.CreateRect("GearIcon", gearBg.transform);
            WarcraftUiFactory.Stretch(iconRt, 7f, 7f);
            Image iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.sprite = ProceduralTextureFactory.GetIcon("settings");
            iconImg.raycastTarget = false;

            // 2. Modal Overlay
            Image overlay = WarcraftUiFactory.CreatePanel("SettingsModalOverlay", parentRoot, new Color(0f, 0f, 0f, 0.62f), false);
            _overlay = overlay.gameObject;
            WarcraftUiFactory.Stretch(overlay.rectTransform);

            // Tap dimmed background to close
            Button bgClose = overlay.gameObject.AddComponent<Button>();
            bgClose.transition = Selectable.Transition.None;
            bgClose.onClick.AddListener(() => SetOpen(false));

            Image box = WarcraftUiFactory.CreatePanel("SettingsBox", overlay.transform, WarcraftUiFactory.PanelDark);
            WarcraftUiFactory.SetAnchors(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230f, -155f), new Vector2(230f, 155f));

            // Header with small gear icon
            _titleText = WarcraftUiFactory.CreateText("SettingsTitle", box.transform, "", 25,
                WarcraftUiFactory.GoldAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -56f), new Vector2(-20f, -12f));

            // Language Section Panel
            Image langBox = WarcraftUiFactory.CreatePanel("LanguageSection", box.transform, WarcraftUiFactory.PanelInner);
            WarcraftUiFactory.SetAnchors(langBox.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -196f), new Vector2(-24f, -68f));

            _langSectionLabel = WarcraftUiFactory.CreateText("LangLabel", langBox.transform, "", 17,
                new Color(0.88f, 0.92f, 0.98f), TextAnchor.MiddleLeft, FontStyle.Bold);
            WarcraftUiFactory.SetAnchors(_langSectionLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -44f), new Vector2(-16f, -10f));

            _viBtn = WarcraftUiFactory.CreateButton("LangViBtn", langBox.transform, "Tiếng Việt",
                WarcraftUiFactory.BtnPrimary, () => SelectLanguage(GameLanguage.Vietnamese), 17);
            _viBtnBg = _viBtn.GetComponent<Image>();
            _viBtnLabel = _viBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(_viBtn.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(0.48f, 0f), new Vector2(16f, 16f), new Vector2(-4f, 68f));

            _enBtn = WarcraftUiFactory.CreateButton("LangEnBtn", langBox.transform, "English",
                WarcraftUiFactory.BtnSecondary, () => SelectLanguage(GameLanguage.English), 17);
            _enBtnBg = _enBtn.GetComponent<Image>();
            _enBtnLabel = _enBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(_enBtn.GetComponent<RectTransform>(),
                new Vector2(0.52f, 0f), new Vector2(1f, 0f), new Vector2(4f, 16f), new Vector2(-16f, 68f));

            // Mode info footer
            _infoFooterText = WarcraftUiFactory.CreateText("ModeFooter", box.transform, "", 13,
                new Color(0.72f, 0.80f, 0.90f), TextAnchor.MiddleCenter);
            WarcraftUiFactory.SetAnchors(_infoFooterText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 74f), new Vector2(-20f, 102f));

            // Close button
            Button closeBtn = WarcraftUiFactory.CreateButton("CloseSettingsBtn", box.transform, "",
                WarcraftUiFactory.BtnDanger, () =>
                {
                    OnButtonClickedSound?.Invoke();
                    SetOpen(false);
                }, 18);
            _closeBtnLabel = closeBtn.GetComponentInChildren<Text>();
            WarcraftUiFactory.SetAnchors(closeBtn.GetComponent<RectTransform>(),
                new Vector2(0.28f, 0f), new Vector2(0.72f, 0f), new Vector2(0f, 18f), new Vector2(0f, 64f));

            RefreshTexts();
            _overlay.SetActive(false);
        }

        private void SelectLanguage(GameLanguage lang)
        {
            OnButtonClickedSound?.Invoke();
            LocalizationManager.SetLanguage(lang);
            RefreshTexts();
        }

        private void HandleLanguageChanged(GameLanguage _)
        {
            RefreshTexts();
        }

        private void RefreshTexts()
        {
            if (_titleText == null) return;

            bool isVi = LocalizationManager.IsVietnamese;
            _titleText.text = LocalizationManager.Get("settings.title");
            _langSectionLabel.text = LocalizationManager.Get("settings.language_label");

            _viBtnLabel.text = isVi ? "●  Tiếng Việt" : "Tiếng Việt";
            _enBtnLabel.text = !isVi ? "●  English" : "English";

            _viBtnBg.color = isVi ? WarcraftUiFactory.BtnPrimary : new Color(0.18f, 0.23f, 0.32f, 1f);
            _enBtnBg.color = !isVi ? WarcraftUiFactory.BtnPrimary : new Color(0.18f, 0.23f, 0.32f, 1f);

            _infoFooterText.text = BuildEnvironment.BadgeText + "  •  " + PlatformProfile.CurrentModeLabel;
            _closeBtnLabel.text = PlatformProfile.ShowKeyboardHints
                ? LocalizationManager.Get("common.close_esc")
                : LocalizationManager.Get("common.close");
        }
    }
}

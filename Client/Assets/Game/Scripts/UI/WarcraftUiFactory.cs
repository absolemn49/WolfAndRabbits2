using System;
using UnityEngine;
using UnityEngine.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.UI
{
    public static class WarcraftUiFactory
    {
        public static readonly Color PanelDark = new Color(0.08f, 0.11f, 0.16f, 0.94f);
        public static readonly Color PanelInner = new Color(0.12f, 0.16f, 0.23f, 0.92f);
        public static readonly Color GoldAccent = new Color(0.98f, 0.80f, 0.30f, 1f);
        public static readonly Color BtnPrimary = new Color(0.19f, 0.50f, 0.32f, 1f);
        public static readonly Color BtnSecondary = new Color(0.22f, 0.34f, 0.54f, 1f);
        public static readonly Color BtnDanger = new Color(0.64f, 0.20f, 0.18f, 1f);
        public static readonly Color BtnGold = new Color(0.66f, 0.48f, 0.14f, 1f);

        public static Font UiFont => WorldHealthBar3D.GetSafeFont();

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image CreatePanel(string name, Transform parent, Color tint, bool useBorderFrame = true)
        {
            RectTransform rt = CreateRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            if (useBorderFrame)
            {
                img.sprite = ProceduralTextureFactory.PanelSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = tint;
            return img;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            TextAnchor align = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal,
            bool shadow = true)
        {
            RectTransform rt = CreateRect(name, parent);
            Text txt = rt.gameObject.AddComponent<Text>();
            txt.font = UiFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = align;
            txt.fontStyle = style;
            txt.supportRichText = true;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            if (shadow)
            {
                Shadow sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.82f);
                sh.effectDistance = new Vector2(1.4f, -1.4f);
            }
            return txt;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color bgColor,
            Action onClick,
            int fontSize = 18)
        {
            Image img = CreatePanel(name, parent, bgColor, useBorderFrame: true);
            Button btn = img.gameObject.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.05f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.72f);
            btn.colors = cb;

            Text txt = CreateText("Label", img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(txt.rectTransform, 8f, 4f);

            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static InputField CreateInputField(
            string name,
            Transform parent,
            string placeholder,
            string initialValue = "",
            bool isPassword = false,
            int fontSize = 18)
        {
            Image bg = CreatePanel(name, parent, new Color(0.05f, 0.08f, 0.12f, 0.98f), useBorderFrame: true);
            InputField field = bg.gameObject.AddComponent<InputField>();
            if (isPassword)
                field.contentType = InputField.ContentType.Password;

            Text ph = CreateText("Placeholder", bg.transform, placeholder, fontSize, new Color(0.55f, 0.62f, 0.72f, 0.85f),
                TextAnchor.MiddleLeft, FontStyle.Italic, shadow: false);
            Stretch(ph.rectTransform, 14f, 6f);

            Text txt = CreateText("Text", bg.transform, initialValue, fontSize, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Normal, shadow: false);
            txt.supportRichText = false;
            Stretch(txt.rectTransform, 14f, 6f);

            field.placeholder = ph;
            field.textComponent = txt;
            field.text = initialValue;
            return field;
        }

        public static RectTransform CreateScrollList(string name, Transform parent, out ScrollRect scrollRect)
        {
            Image bg = CreatePanel(name, parent, new Color(0.05f, 0.07f, 0.11f, 0.86f), useBorderFrame: true);
            scrollRect = bg.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            RectTransform viewport = CreateRect("Viewport", bg.transform);
            Stretch(viewport, 8f, 8f);
            Image maskImg = viewport.gameObject.AddComponent<Image>();
            maskImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return content;
        }

        public static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}

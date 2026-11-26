using UnityEngine;
using UnityEngine.UI;

namespace WolfAndRabbits.Client.Visuals
{
    /// <summary>
    /// Billboarded 3D world-space health bar with label rendered above characters and buildings.
    /// </summary>
    public class WorldHealthBar3D : MonoBehaviour
    {
        private Canvas _canvas;
        private Image _fillImage;
        private Text _labelText;
        private GameObject _barRoot;
        private Camera _cam;

        private static Font _cachedFont;

        public static WorldHealthBar3D Attach(Transform parent, Vector3 localOffset, float width, Color fillColor, string label = "")
        {
            GameObject go = new GameObject("WorldHPBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;
            WorldHealthBar3D bar = go.AddComponent<WorldHealthBar3D>();
            bar.Build(width, fillColor, label);
            return bar;
        }

        public static Font GetSafeFont()
        {
            if (_cachedFont != null) return _cachedFont;

            // 1. Primary bundled Vietnamese-complete TTF font (works on WebGL, Android, iOS, PC, Editor)
            _cachedFont = Resources.Load<Font>("Fonts/GameFont");
            if (_cachedFont != null) return _cachedFont;

#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Roboto", "Helvetica" }, 18);
            }
            catch { }
#endif
            if (_cachedFont == null)
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _cachedFont;
        }

        private void Build(float width, Color fillColor, string label)
        {
            _barRoot = gameObject;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 34f);
            float scale = width / 120f;
            rt.localScale = new Vector3(scale, scale, scale);

            GameObject txtGo = new GameObject("Label");
            txtGo.transform.SetParent(transform, false);
            _labelText = txtGo.AddComponent<Text>();
            _labelText.text = label;
            _labelText.font = GetSafeFont();
            _labelText.fontSize = 16;
            _labelText.alignment = TextAnchor.MiddleCenter;
            _labelText.color = Color.white;
            _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline outline = txtGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            RectTransform trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.45f);
            trt.anchorMax = new Vector2(1f, 1.15f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            GameObject bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(transform, false);
            Image bg = bgGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.11f, 0.88f);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.05f, 0.08f);
            bgRt.anchorMax = new Vector2(0.95f, 0.44f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.color = fillColor;
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0.03f, 0.14f);
            fillRt.anchorMax = new Vector2(0.97f, 0.86f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
        }

        public void SetProgress(int hp, int maxHp, string label = null)
        {
            if (_fillImage != null)
            {
                float ratio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
                RectTransform rt = _fillImage.rectTransform;
                rt.anchorMax = new Vector2(0.03f + 0.94f * ratio, 0.86f);
            }
            if (label != null && _labelText != null)
                _labelText.text = label;
        }

        public void SetVisible(bool visible)
        {
            if (_barRoot != null && _barRoot.activeSelf != visible)
                _barRoot.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                transform.rotation = _cam.transform.rotation;
        }
    }
}

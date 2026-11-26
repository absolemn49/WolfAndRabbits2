using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WolfAndRabbits.Client.UI
{
    public class HoldButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public event Action<bool> OnHoldChanged;

        public void OnPointerDown(PointerEventData eventData) => OnHoldChanged?.Invoke(true);
        public void OnPointerUp(PointerEventData eventData) => OnHoldChanged?.Invoke(false);
        public void OnPointerExit(PointerEventData eventData) => OnHoldChanged?.Invoke(false);
    }

    public class VirtualJoystickWidget : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Direction { get; private set; }

        private RectTransform _bg;
        private RectTransform _handle;
        private float _radius = 56f;

        public void Setup(RectTransform bg, RectTransform handle, float radius)
        {
            _bg = bg;
            _handle = handle;
            _radius = radius;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (_bg == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bg, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPos, _radius);
                if (_handle != null) _handle.anchoredPosition = clamped;
                Vector2 dir = clamped / _radius;
                Direction = dir.magnitude < 0.12f ? Vector2.zero : dir;
            }
        }

        public void OnPointerUp(PointerEventData eventData) => ResetStick();

        public void ResetStick()
        {
            Direction = Vector2.zero;
            if (_handle != null) _handle.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// Keeps HUD controls inside Screen.safeArea on mobile devices with notches or camera cutouts.
    /// </summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplyIfChanged();
        }

        private void Update()
        {
            ApplyIfChanged();
        }

        private void ApplyIfChanged()
        {
            if (_rect == null) return;
            Rect safe = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (safe == _lastSafeArea && size == _lastScreenSize) return;
            if (size.x <= 0 || size.y <= 0) return;

            _lastSafeArea = safe;
            _lastScreenSize = size;

            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;
            anchorMin.x /= size.x;
            anchorMin.y /= size.y;
            anchorMax.x /= size.x;
            anchorMax.y /= size.y;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}

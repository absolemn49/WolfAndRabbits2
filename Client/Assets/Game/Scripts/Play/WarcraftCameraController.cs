using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace WolfAndRabbits.Client.Play
{
    public class WarcraftCameraController : MonoBehaviour
    {
        public event Action<int, int> OnGroundTileClicked;

        [SerializeField] private float pitchDegrees = 56f;
        [SerializeField] private float cameraHeight = 14.2f;
        [SerializeField] private float minHeight = 9.5f;
        [SerializeField] private float maxHeight = 22.0f;

        private Camera _cam;
        private Vector3 _focusPoint = new Vector3(22f, 0f, 22f);
        private float _mapWidth = 44f;
        private float _mapHeight = 44f;
        private bool _inMatch;
        private bool _holdFollowButton;

        private bool _isDraggingPointer;
        private bool _dragExceededThreshold;
        private int _activeTouchId = -1;
        private Vector2 _pointerDownScreenPos;
        private Vector3 _dragStartWorldPoint;
        private Vector3 _dragStartFocusPoint;

        private float _shakeIntensity;
        private float _orbitAngle;

        public Vector3 FocusPoint => _focusPoint;

        public void Initialize(Camera cam)
        {
            _cam = cam;
            if (_cam != null)
            {
                _cam.fieldOfView = 40f;
                _cam.nearClipPlane = 0.5f;
                _cam.farClipPlane = 180f;
            }
            ApplyCameraTransform();
        }

        public void EnterMatch(float mapWidth, float mapHeight, Vector3 initialFocus)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            _focusPoint = new Vector3(initialFocus.x, 0f, initialFocus.z);
            _inMatch = true;
            _isDraggingPointer = false;
            _dragExceededThreshold = false;
            _activeTouchId = -1;
            ApplyCameraTransform();
        }

        public void ExitMatch()
        {
            _inMatch = false;
            _holdFollowButton = false;
            _isDraggingPointer = false;
            _activeTouchId = -1;
        }

        public void SetHoldFollow(bool held)
        {
            _holdFollowButton = held;
        }

        public void FocusOn(Vector3 worldPos, bool immediate = false)
        {
            Vector3 target = ClampFocus(new Vector3(worldPos.x, 0f, worldPos.z));
            if (immediate)
                _focusPoint = target;
            else
                _focusPoint = Vector3.Lerp(_focusPoint, target, Mathf.Clamp01(Time.deltaTime * 11f));
        }

        public void AddShake(float intensity)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        }

        public void TickCamera(Vector3? localPlayerWorldPos, bool inputBlockedByModal)
        {
            if (_cam == null) return;

            if (!_inMatch)
            {
                // Scenic Warcraft 3 menu background view
                _orbitAngle += Time.deltaTime * 6f;
                float rad = _orbitAngle * Mathf.Deg2Rad;
                _cam.transform.position = new Vector3(22f + Mathf.Cos(rad) * 14f, 15f, 22f + Mathf.Sin(rad) * 14f);
                _cam.transform.LookAt(new Vector3(22f, 1f, 22f));
                return;
            }

            if (!inputBlockedByModal)
            {
                HandleZoom();
                HandlePointerDragAndClick();
            }
            else
            {
                _isDraggingPointer = false;
                _activeTouchId = -1;
            }

            bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            if ((spaceHeld || _holdFollowButton) && localPlayerWorldPos.HasValue)
            {
                FocusOn(localPlayerWorldPos.Value, immediate: false);
            }

            _focusPoint = ClampFocus(_focusPoint);
            if (_shakeIntensity > 0f)
                _shakeIntensity = Mathf.MoveTowards(_shakeIntensity, 0f, Time.deltaTime * 1.8f);

            ApplyCameraTransform();
        }

        private void HandleZoom()
        {
            if (Mouse.current == null) return;
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                cameraHeight = Mathf.Clamp(cameraHeight - Mathf.Sign(scrollY) * 1.15f, minHeight, maxHeight);
            }
        }

        private void HandlePointerDragAndClick()
        {
            // Mobile touch drag/tap first if active touches exist
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                bool handledTouch = false;
                for (int i = 0; i < ts.touches.Count; i++)
                {
                    TouchControl tc = ts.touches[i];
                    if (!tc.press.isPressed && !tc.press.wasReleasedThisFrame) continue;

                    int tid = tc.touchId.ReadValue();
                    Vector2 pos = tc.position.ReadValue();

                    if (tc.press.wasPressedThisFrame && _activeTouchId < 0)
                    {
                        if (IsPointerOverUi(tid)) continue;
                        if (ScreenToGround(pos, out Vector3 hitPt))
                        {
                            _activeTouchId = tid;
                            _isDraggingPointer = true;
                            _dragExceededThreshold = false;
                            _pointerDownScreenPos = pos;
                            _dragStartWorldPoint = hitPt;
                            _dragStartFocusPoint = _focusPoint;
                            handledTouch = true;
                        }
                    }
                    else if (_activeTouchId == tid)
                    {
                        handledTouch = true;
                        if (tc.press.isPressed)
                        {
                            if (Vector2.Distance(pos, _pointerDownScreenPos) > 12f)
                                _dragExceededThreshold = true;
                            if (_dragExceededThreshold && ScreenToGround(pos, out Vector3 curPt))
                            {
                                Vector3 delta = _dragStartWorldPoint - curPt;
                                _focusPoint = ClampFocus(_dragStartFocusPoint + new Vector3(delta.x, 0f, delta.z));
                                _dragStartFocusPoint = _focusPoint;
                                ScreenToGround(pos, out _dragStartWorldPoint);
                            }
                        }
                        if (tc.press.wasReleasedThisFrame)
                        {
                            if (!_dragExceededThreshold && ScreenToGround(pos, out Vector3 clickPt))
                            {
                                OnGroundTileClicked?.Invoke(Mathf.FloorToInt(clickPt.x), Mathf.FloorToInt(clickPt.z));
                            }
                            _activeTouchId = -1;
                            _isDraggingPointer = false;
                        }
                    }
                }
                if (handledTouch) return;
            }

            // Desktop / WebGL mouse left-drag pan & left-click tile
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mPos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverUi(-1) && ScreenToGround(mPos, out Vector3 hitPt))
                {
                    _isDraggingPointer = true;
                    _dragExceededThreshold = false;
                    _pointerDownScreenPos = mPos;
                    _dragStartWorldPoint = hitPt;
                    _dragStartFocusPoint = _focusPoint;
                }
            }
            else if (_isDraggingPointer && mouse.leftButton.isPressed)
            {
                if (Vector2.Distance(mPos, _pointerDownScreenPos) > 9f)
                    _dragExceededThreshold = true;

                if (_dragExceededThreshold && ScreenToGround(mPos, out Vector3 curPt))
                {
                    Vector3 delta = _dragStartWorldPoint - curPt;
                    _focusPoint = ClampFocus(_dragStartFocusPoint + new Vector3(delta.x, 0f, delta.z));
                    _dragStartFocusPoint = _focusPoint;
                    ScreenToGround(mPos, out _dragStartWorldPoint);
                }
            }
            else if (_isDraggingPointer && mouse.leftButton.wasReleasedThisFrame)
            {
                if (!_dragExceededThreshold && !IsPointerOverUi(-1) && ScreenToGround(mPos, out Vector3 clickPt))
                {
                    OnGroundTileClicked?.Invoke(Mathf.FloorToInt(clickPt.x), Mathf.FloorToInt(clickPt.z));
                }
                _isDraggingPointer = false;
            }
        }

        private bool ScreenToGround(Vector2 screenPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (_cam == null) return false;
            Ray ray = _cam.ScreenPointToRay(screenPos);
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        private void ApplyCameraTransform()
        {
            if (_cam == null) return;
            float pitchRad = pitchDegrees * Mathf.Deg2Rad;
            float backDist = cameraHeight / Mathf.Tan(pitchRad);

            Vector3 shake = Vector3.zero;
            if (_shakeIntensity > 0.001f)
            {
                shake = new Vector3(
                    Mathf.Sin(Time.time * 67f) * _shakeIntensity,
                    Mathf.Cos(Time.time * 53f) * _shakeIntensity * 0.5f,
                    Mathf.Cos(Time.time * 79f) * _shakeIntensity);
            }

            _cam.transform.position = _focusPoint + new Vector3(0f, cameraHeight, -backDist) + shake;
            _cam.transform.rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        private Vector3 ClampFocus(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, 1.5f, _mapWidth - 1.5f);
            p.z = Mathf.Clamp(p.z, 1.5f, _mapHeight - 1.5f);
            p.y = 0f;
            return p;
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            if (EventSystem.current == null) return false;
            if (pointerId >= 0)
                return EventSystem.current.IsPointerOverGameObject(pointerId);
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}

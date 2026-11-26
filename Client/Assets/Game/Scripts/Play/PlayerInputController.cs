using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WolfAndRabbits.Client.Core;

namespace WolfAndRabbits.Client.Play
{
    /// <summary>
    /// Reads keyboard (WASD/Arrows) and mobile virtual joystick movement, sending throttled 20Hz move_input
    /// packets to the authoritative server, plus E / B / Esc action hotkeys.
    /// </summary>
    public class PlayerInputController : MonoBehaviour
    {
        public event Action OnPrimaryKeyPressed;
        public event Action OnShopKeyPressed;
        public event Action OnCancelKeyPressed;

        [SerializeField] private float moveSendIntervalSec = 0.05f; // 20 Hz

        private Vector2 _lastSentMove;
        private float _moveSendTimer;

        public void TickInput(GameSession session, Vector2 virtualJoystickDir, bool canControlHero, bool modalOpen)
        {
            Keyboard kb = Keyboard.current;

            if (modalOpen)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    OnCancelKeyPressed?.Invoke();

                SendStopIfNeeded(session);
                return;
            }

            if (!canControlHero)
            {
                SendStopIfNeeded(session);
                return;
            }

            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame)
                    OnPrimaryKeyPressed?.Invoke();

                if (kb.bKey.wasPressedThisFrame)
                    OnShopKeyPressed?.Invoke();

                if (kb.escapeKey.wasPressedThisFrame)
                    OnCancelKeyPressed?.Invoke();
            }

            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }

            if (PlatformProfile.ShowVirtualJoystick && virtualJoystickDir.sqrMagnitude > 0.01f)
                move = virtualJoystickDir;

            if (move.sqrMagnitude > 1f)
                move.Normalize();

            _moveSendTimer -= Time.deltaTime;
            bool changed = (move - _lastSentMove).sqrMagnitude > 0.002f;
            if (changed || (_moveSendTimer <= 0f && move.sqrMagnitude > 0.001f))
            {
                _moveSendTimer = moveSendIntervalSec;
                _lastSentMove = move;
                session.SendMoveInput(move);
            }
        }

        private void SendStopIfNeeded(GameSession session)
        {
            if (_lastSentMove.sqrMagnitude > 0.001f)
            {
                _lastSentMove = Vector2.zero;
                session?.SendMoveInput(Vector2.zero);
            }
        }
    }
}

using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LostBreadcrumbs.Runtime.Core.Input
{
    public static class RuntimeInputAdapter
    {
        public static bool GetKeyDown(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (!TryMapKey(keyCode, out Key key))
            {
                return false;
            }

            return keyboard[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(keyCode);
#endif
        }

        public static bool GetKey(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return IsPressed(keyboard, keyCode);
#else
            return Input.GetKey(keyCode);
#endif
        }

        public static Vector2 GetMoveVector(KeyCode left, KeyCode right, KeyCode down, KeyCode up)
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 move = Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (IsPressed(keyboard, left))
                {
                    move.x -= 1f;
                }

                if (IsPressed(keyboard, right))
                {
                    move.x += 1f;
                }

                if (IsPressed(keyboard, down))
                {
                    move.y -= 1f;
                }

                if (IsPressed(keyboard, up))
                {
                    move.y += 1f;
                }

                // Arrow-key fallback for desktop debug play.
                if (keyboard.leftArrowKey.isPressed)
                {
                    move.x -= 1f;
                }

                if (keyboard.rightArrowKey.isPressed)
                {
                    move.x += 1f;
                }

                if (keyboard.downArrowKey.isPressed)
                {
                    move.y -= 1f;
                }

                if (keyboard.upArrowKey.isPressed)
                {
                    move.y += 1f;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                move += gamepad.leftStick.ReadValue();
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            return move;
#else
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(left))
            {
                x -= 1f;
            }

            if (Input.GetKey(right))
            {
                x += 1f;
            }

            if (Input.GetKey(down))
            {
                y -= 1f;
            }

            if (Input.GetKey(up))
            {
                y += 1f;
            }

            Vector2 move = new(x, y);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            return move;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool IsPressed(Keyboard keyboard, KeyCode keyCode)
        {
            return TryMapKey(keyCode, out Key key) && keyboard[key].isPressed;
        }

        private static bool TryMapKey(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Backspace:
                    key = Key.Backspace;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;
                case KeyCode.Alpha0:
                    key = Key.Digit0;
                    return true;
                case KeyCode.Alpha1:
                    key = Key.Digit1;
                    return true;
                case KeyCode.Alpha2:
                    key = Key.Digit2;
                    return true;
                case KeyCode.Alpha3:
                    key = Key.Digit3;
                    return true;
                case KeyCode.Alpha4:
                    key = Key.Digit4;
                    return true;
                case KeyCode.Alpha5:
                    key = Key.Digit5;
                    return true;
                case KeyCode.Alpha6:
                    key = Key.Digit6;
                    return true;
                case KeyCode.Alpha7:
                    key = Key.Digit7;
                    return true;
                case KeyCode.Alpha8:
                    key = Key.Digit8;
                    return true;
                case KeyCode.Alpha9:
                    key = Key.Digit9;
                    return true;
                case KeyCode.Keypad0:
                    key = Key.Numpad0;
                    return true;
                case KeyCode.Keypad1:
                    key = Key.Numpad1;
                    return true;
                case KeyCode.Keypad2:
                    key = Key.Numpad2;
                    return true;
                case KeyCode.Keypad3:
                    key = Key.Numpad3;
                    return true;
                case KeyCode.Keypad4:
                    key = Key.Numpad4;
                    return true;
                case KeyCode.Keypad5:
                    key = Key.Numpad5;
                    return true;
                case KeyCode.Keypad6:
                    key = Key.Numpad6;
                    return true;
                case KeyCode.Keypad7:
                    key = Key.Numpad7;
                    return true;
                case KeyCode.Keypad8:
                    key = Key.Numpad8;
                    return true;
                case KeyCode.Keypad9:
                    key = Key.Numpad9;
                    return true;
                default:
                    return Enum.TryParse(keyCode.ToString(), true, out key);
            }
        }
#endif
    }
}

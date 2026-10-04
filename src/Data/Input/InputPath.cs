using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 설정 파일의 바인딩 경로 문자열("&lt;Keyboard&gt;/w", "&lt;Mouse&gt;/leftButton")과 Godot 입력 이벤트를 서로 변환한다.
    /// 키 이름은 UnityXOPS config.json 과 같은 표기를 쓰며, 키보드는 자판 배열과 무관한 물리 키 위치로 대응한다.
    /// </summary>
    public static class InputPath
    {
        public const string MouseDelta = "<Mouse>/delta";

        private const string k_keyboardPrefix = "<Keyboard>/";
        private const string k_mousePrefix = "<Mouse>/";

        private static readonly Dictionary<string, (Key key, KeyLocation location)> s_keys = BuildKeyTable();
        private static readonly Dictionary<string, MouseButton> s_mouseButtons = new Dictionary<string, MouseButton>(StringComparer.OrdinalIgnoreCase)
        {
            { "leftButton", MouseButton.Left },
            { "rightButton", MouseButton.Right },
            { "middleButton", MouseButton.Middle },
            { "backButton", MouseButton.Xbutton1 },
            { "forwardButton", MouseButton.Xbutton2 },
        };

        /// <summary>
        /// 바인딩 경로를 Godot 입력 이벤트로 바꾼다.
        /// </summary>
        /// <param name="path">바인딩 경로.</param>
        /// <returns>InputMap 에 등록할 수 있는 이벤트. 마우스 이동("&lt;Mouse&gt;/delta")이나 알 수 없는 경로면 null.</returns>
        public static InputEvent Parse(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (path.StartsWith(k_keyboardPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!s_keys.TryGetValue(path.Substring(k_keyboardPrefix.Length), out (Key key, KeyLocation location) entry))
                {
                    return null;
                }
                return new InputEventKey { PhysicalKeycode = entry.key, Location = entry.location };
            }

            if (path.StartsWith(k_mousePrefix, StringComparison.OrdinalIgnoreCase)
                && s_mouseButtons.TryGetValue(path.Substring(k_mousePrefix.Length), out MouseButton button))
            {
                return new InputEventMouseButton { ButtonIndex = button };
            }

            return null;
        }

        /// <summary>
        /// 키 이름("w", "leftShift")을 Godot 물리 키로 바꾼다.
        /// </summary>
        /// <param name="keyName">키 이름 (대소문자 무시).</param>
        /// <param name="key">대응하는 물리 키.</param>
        /// <returns>아는 이름이면 true.</returns>
        public static bool TryGetKey(string keyName, out Key key)
        {
            bool found = s_keys.TryGetValue(keyName, out (Key key, KeyLocation location) entry);
            key = entry.key;
            return found;
        }

        /// <summary>
        /// Godot 입력 이벤트를 바인딩 경로로 바꾼다. 키 리바인딩에서 눌린 키를 설정에 저장할 때 쓴다.
        /// </summary>
        /// <param name="inputEvent">키보드 키 또는 마우스 버튼 이벤트.</param>
        /// <returns>바인딩 경로. 대응하는 이름이 없으면 빈 문자열.</returns>
        public static string ToPath(InputEvent inputEvent)
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                string fallback = null;
                foreach (KeyValuePair<string, (Key key, KeyLocation location)> pair in s_keys)
                {
                    if (pair.Value.key != keyEvent.PhysicalKeycode)
                    {
                        continue;
                    }
                    if (pair.Value.location == keyEvent.Location)
                    {
                        return k_keyboardPrefix + pair.Key;
                    }
                    // 좌우 구분이 없는 이벤트(Unspecified)는 같은 키의 첫 이름(왼쪽)으로 받는다.
                    fallback ??= pair.Key;
                }
                return fallback != null ? k_keyboardPrefix + fallback : string.Empty;
            }

            if (inputEvent is InputEventMouseButton buttonEvent)
            {
                foreach (KeyValuePair<string, MouseButton> pair in s_mouseButtons)
                {
                    if (pair.Value == buttonEvent.ButtonIndex)
                    {
                        return k_mousePrefix + pair.Key;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 키 이름 → 물리 키 대응표를 만든다. 삽입 순서가 ToPath 의 역방향 조회 우선순위다(왼쪽 키가 먼저).
        /// </summary>
        /// <returns>대소문자를 무시하는 대응표.</returns>
        private static Dictionary<string, (Key, KeyLocation)> BuildKeyTable()
        {
            var table = new Dictionary<string, (Key, KeyLocation)>(StringComparer.OrdinalIgnoreCase);
            const KeyLocation any = KeyLocation.Unspecified;

            for (int i = 0; i < 26; i++)
            {
                table[((char)('a' + i)).ToString()] = (Key.A + i, any);
            }
            for (int i = 0; i < 10; i++)
            {
                table[i.ToString()] = (Key.Key0 + i, any);
                table["numpad" + i] = (Key.Kp0 + i, any);
            }
            for (int i = 0; i < 12; i++)
            {
                table["f" + (i + 1)] = (Key.F1 + i, any);
            }

            table["space"] = (Key.Space, any);
            table["enter"] = (Key.Enter, any);
            table["tab"] = (Key.Tab, any);
            table["escape"] = (Key.Escape, any);
            table["backspace"] = (Key.Backspace, any);
            table["leftShift"] = (Key.Shift, KeyLocation.Left);
            table["rightShift"] = (Key.Shift, KeyLocation.Right);
            table["leftCtrl"] = (Key.Ctrl, KeyLocation.Left);
            table["rightCtrl"] = (Key.Ctrl, KeyLocation.Right);
            table["leftAlt"] = (Key.Alt, KeyLocation.Left);
            table["rightAlt"] = (Key.Alt, KeyLocation.Right);
            table["upArrow"] = (Key.Up, any);
            table["downArrow"] = (Key.Down, any);
            table["leftArrow"] = (Key.Left, any);
            table["rightArrow"] = (Key.Right, any);
            table["insert"] = (Key.Insert, any);
            table["delete"] = (Key.Delete, any);
            table["home"] = (Key.Home, any);
            table["end"] = (Key.End, any);
            table["pageUp"] = (Key.Pageup, any);
            table["pageDown"] = (Key.Pagedown, any);
            table["capsLock"] = (Key.Capslock, any);
            table["minus"] = (Key.Minus, any);
            table["equals"] = (Key.Equal, any);
            table["leftBracket"] = (Key.Bracketleft, any);
            table["rightBracket"] = (Key.Bracketright, any);
            table["semicolon"] = (Key.Semicolon, any);
            table["quote"] = (Key.Apostrophe, any);
            table["comma"] = (Key.Comma, any);
            table["period"] = (Key.Period, any);
            table["slash"] = (Key.Slash, any);
            table["backslash"] = (Key.Backslash, any);
            table["backquote"] = (Key.Quoteleft, any);
            table["numpadEnter"] = (Key.KpEnter, any);
            table["numpadPlus"] = (Key.KpAdd, any);
            table["numpadMinus"] = (Key.KpSubtract, any);
            table["numpadMultiply"] = (Key.KpMultiply, any);
            table["numpadDivide"] = (Key.KpDivide, any);
            table["numpadPeriod"] = (Key.KpPeriod, any);
            return table;
        }
    }
}

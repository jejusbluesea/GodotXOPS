using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace GodotXOPS
{
    public partial class EventManager
    {
        // 이벤트가 화면에 놓을 수 있는 글자 칸의 수. 스크립트가 글자를 끝없이 만드는 것을 막는다.
        public const int HudSlotCount = 32;
        // 글꼴 종류: OS 글꼴(어느 나라 글자든 된다)과 char.dds 스프라이트 글꼴(글자 코드 0 에서 255 사이만).
        public const int HudFontOS = 0;
        public const int HudFontSprite = 1;

        // 칸 하나에 들어가는 글자 수의 상한.
        private const int k_maxHudChars = 256;
        // 기준점의 종류 수 (3 × 3: 0 왼쪽 위, 1 위 가운데, … 4 화면 가운데, … 8 오른쪽 아래).
        private const int k_hudAnchorCount = 9;
        private const float k_hudDefaultSize = 16f;
        // 스프라이트 글자의 너비를 적지 않았을 때 높이에 곱하는 비율 (HUD 의 무기 이름이 16 × 20 이다).
        private const float k_hudSpriteWidthRatio = 0.8f;
        private const float k_hudMinSize = 4f;
        private const float k_hudMaxSize = 128f;
        // 스프라이트 글꼴에 없는 글자를 대신하는 글자.
        private const char k_hudMissingGlyph = '?';
        private const int k_spriteGlyphCount = 256;

        /// <summary>
        /// 화면 글자 칸 하나.
        /// </summary>
        private sealed class HudSlot
        {
            public bool active;
            public string text = string.Empty;
            public int font;
            public int anchor;
            public float x;
            public float y;
            public float size;
            public float width;
            public Color color = Colors.White;
            // 남은 틱. 음수면 지울 때까지 남는다.
            public int ticksLeft;
            // 놓인 순서. 작을수록 먼저 놓였고 뒤에 그려진다.
            public long order;
        }

        private readonly HudSlot[] m_hud = new HudSlot[HudSlotCount];
        private long m_hudOrder;
        private int m_hudRevision;
        private bool m_interactPending;
        private bool m_interactNow;
        // Interact 키를 누르고 있는지: 프레임에서 들어온 값과 이번 틱의 것으로 확정한 값.
        private bool m_interactHeldInput;
        private bool m_interactHeldNow;
        // 커스텀 바인딩의 입력: 지난 틱 뒤에 눌린 것, 이번 틱의 것으로 확정한 것, 프레임에서 누르고 있던 것, 이번 틱에 누르고 있는 것. 액션 이름이다.
        private readonly HashSet<string> m_customPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_customNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_customHeldInput = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> m_customHeldNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 화면 글자의 내용이 바뀔 때마다 오르는 값. HUD 는 이 값이 바뀌었을 때만 HudTexts 를 다시 읽는다.
        public int HudRevision => m_hudRevision;
        // 이번 틱에 플레이어가 Interact 키를 눌렀는지. 살아 있는 플레이어가 지난 틱 뒤에 누른 것만 친다.
        public bool InteractPressed => m_interactNow;
        // 이번 틱에 플레이어가 Interact 키를 누르고 있는지 (꾹 눌러야 하는 이벤트가 쓴다). 살아 있는 플레이어만 친다.
        public bool InteractHeld => m_interactHeldNow;

        /// <summary>
        /// 플레이어가 Interact 키를 눌렀음을 알린다 (PlayerController). 다음 틱 한 번 동안 InteractPressed 가 켜진다.
        /// </summary>
        public void QueueInteract()
        {
            m_interactPending = true;
        }

        /// <summary>
        /// 플레이어가 Interact 키를 누르고 있는지 알린다 (PlayerController 가 프레임마다). 다음 틱부터 InteractHeld 에 반영된다.
        /// </summary>
        /// <param name="held">누르고 있으면 true.</param>
        public void SetInteractHeld(bool held)
        {
            m_interactHeldInput = held;
        }

        /// <summary>
        /// 플레이어의 커스텀 바인딩 입력을 받는다 (PlayerController 가 프레임마다). 눌린 것은 다음 틱 한 번, 누르고 있는 것은 다음 틱부터 반영된다.
        /// </summary>
        /// <param name="input">입력 매니저.</param>
        public void ReadCustomInput(InputManager input)
        {
            foreach (string action in input.CustomActionNames)
            {
                if (input.WasPressed(action)) QueueCustomPressed(action);
                SetCustomHeld(action, input.IsPressed(action));
            }
        }

        /// <summary>
        /// 플레이어가 커스텀 바인딩을 눌렀음을 알린다. 다음 틱 한 번 동안 CustomPressed 가 참이다.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        public void QueueCustomPressed(string action)
        {
            if (!string.IsNullOrEmpty(action)) m_customPending.Add(action);
        }

        /// <summary>
        /// 플레이어가 커스텀 바인딩을 누르고 있는지 알린다. 다음 틱부터 CustomHeld 에 반영된다.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        /// <param name="held">누르고 있으면 true.</param>
        public void SetCustomHeld(string action, bool held)
        {
            if (string.IsNullOrEmpty(action)) return;
            if (held) m_customHeldInput.Add(action);
            else m_customHeldInput.Remove(action);
        }

        /// <summary>
        /// 이번 틱에 플레이어가 그 커스텀 바인딩을 눌렀는지. 살아 있는 플레이어가 지난 틱 뒤에 누른 것만 친다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>눌렀으면 true. 없는 이름이면 false.</returns>
        public bool CustomPressed(string action)
        {
            return action != null && m_customNow.Contains(action);
        }

        /// <summary>
        /// 이번 틱에 플레이어가 그 커스텀 바인딩을 누르고 있는지. 살아 있는 플레이어만 친다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>누르고 있으면 true. 없는 이름이면 false.</returns>
        public bool CustomHeld(string action)
        {
            return action != null && m_customHeldNow.Contains(action);
        }

        /// <summary>
        /// 화면에 보이는 글자들을 놓인 순서대로 돌려준다 (먼저 놓인 것이 앞. HUD 는 이 순서대로 그려서 나중 것이 위에 온다).
        /// 좌표는 화면 높이를 480 으로 본 값이고, 기준점에서 +x 오른쪽, +y 위쪽이다.
        /// </summary>
        /// <returns>칸마다 slot, text, font, anchor, x, y, size, width, color 를 담은 사전의 배열.</returns>
        public Godot.Collections.Array HudTexts()
        {
            var result = new Godot.Collections.Array();
            long last = -1;
            while (true)
            {
                int next = -1;
                for (int i = 0; i < HudSlotCount; i++)
                {
                    HudSlot slot = m_hud[i];
                    if (slot == null || !slot.active || slot.order <= last) continue;
                    if (next < 0 || slot.order < m_hud[next].order) next = i;
                }
                if (next < 0) break;

                HudSlot found = m_hud[next];
                last = found.order;
                result.Add(new Godot.Collections.Dictionary
                {
                    ["slot"] = next,
                    ["text"] = found.text,
                    ["font"] = found.font,
                    ["anchor"] = found.anchor,
                    ["x"] = found.x,
                    ["y"] = found.y,
                    ["size"] = found.size,
                    ["width"] = found.width,
                    ["color"] = found.color,
                });
            }
            return result;
        }

        /// <summary>
        /// 화면의 한 칸에 글자를 놓는다. 같은 칸에 다시 놓으면 내용만 바뀌고 그리는 순서는 그대로다.
        /// </summary>
        /// <param name="slot">칸 번호 (0 부터 HudSlotCount 미만).</param>
        /// <param name="text">글자. 비어 있으면 칸을 지운다.</param>
        /// <param name="options">
        /// 선택 값. 없는 키는 기본값이다: font(0 OS 글꼴, 1 char.dds), anchor(0~8, 기본 4 화면 가운데), x·y(기준점에서의 오프셋, 화면 높이 480 기준),
        /// size(글자 높이), width(스프라이트 글자 한 칸의 너비. 0 이면 size × 0.8), color(0xRRGGBB 정수, 기본 흰색), alpha(0~1), seconds(보이는 시간. 0 이면 지울 때까지).
        /// </param>
        public void SetHudText(int slot, string text, Godot.Collections.Dictionary options)
        {
            if (slot < 0 || slot >= HudSlotCount) return;
            if (string.IsNullOrEmpty(text))
            {
                ClearHudText(slot);
                return;
            }

            int font = ReadInt(options, "font", HudFontOS) == HudFontSprite ? HudFontSprite : HudFontOS;
            if (text.Length > k_maxHudChars) text = text.Substring(0, k_maxHudChars);
            if (font == HudFontSprite) text = ToSpriteText(text);

            int anchor = Mathf.Clamp(ReadInt(options, "anchor", 4), 0, k_hudAnchorCount - 1);
            float x = ReadFloat(options, "x", 0f);
            float y = ReadFloat(options, "y", 0f);
            float size = Mathf.Clamp(ReadFloat(options, "size", k_hudDefaultSize), k_hudMinSize, k_hudMaxSize);
            float width = ReadFloat(options, "width", 0f);
            width = width > 0f ? Mathf.Clamp(width, k_hudMinSize, k_hudMaxSize) : size * k_hudSpriteWidthRatio;
            int rgb = ReadInt(options, "color", 0xFFFFFF);
            var color = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, Mathf.Clamp(ReadFloat(options, "alpha", 1f), 0f, 1f));
            float seconds = ReadFloat(options, "seconds", 0f);
            int ticks = seconds > 0f ? Mathf.Max(1, Mathf.RoundToInt(seconds * SimClock.FrameRate)) : -1;

            HudSlot target = m_hud[slot] ??= new HudSlot();
            bool changed = !target.active || target.text != text || target.font != font || target.anchor != anchor || target.x != x || target.y != y
                || target.size != size || target.width != width || target.color != color;
            if (!target.active) target.order = ++m_hudOrder;
            target.active = true;
            target.text = text;
            target.font = font;
            target.anchor = anchor;
            target.x = x;
            target.y = y;
            target.size = size;
            target.width = width;
            target.color = color;
            target.ticksLeft = ticks;
            if (changed) m_hudRevision++;
        }

        /// <summary>
        /// 화면의 글자 칸을 지운다.
        /// </summary>
        /// <param name="slot">칸 번호. 음수면 전부 지운다.</param>
        public void ClearHudText(int slot)
        {
            for (int i = 0; i < HudSlotCount; i++)
            {
                if ((slot >= 0 && i != slot) || m_hud[i] == null || !m_hud[i].active) continue;

                m_hud[i].active = false;
                m_hudRevision++;
            }
        }

        /// <summary>
        /// 틱의 처음에: 프레임에서 들어온 Interact 와 커스텀 바인딩의 입력을 이번 틱의 것으로 확정한다.
        /// </summary>
        private void LatchInteract()
        {
            Human player = MapLoader.Player;
            bool alive = player != null && player.Alive;
            m_interactNow = m_interactPending && alive;
            m_interactPending = false;
            m_interactHeldNow = m_interactHeldInput && alive;

            m_customNow.Clear();
            m_customHeldNow.Clear();
            if (alive)
            {
                m_customNow.UnionWith(m_customPending);
                m_customHeldNow.UnionWith(m_customHeldInput);
            }
            m_customPending.Clear();
        }

        /// <summary>
        /// 틱의 끝에: 시간이 정해진 화면 글자의 남은 틱을 줄이고 다 된 것을 지운다.
        /// </summary>
        private void TickHud()
        {
            for (int i = 0; i < HudSlotCount; i++)
            {
                HudSlot slot = m_hud[i];
                if (slot == null || !slot.active || slot.ticksLeft < 0) continue;

                if (--slot.ticksLeft <= 0)
                {
                    slot.active = false;
                    m_hudRevision++;
                }
            }
        }

        /// <summary>
        /// 화면 글자와 Interact·커스텀 바인딩의 입력을 처음 상태로 되돌린다 (미션 시작, 종료, 맵 내리기).
        /// </summary>
        private void ResetHud()
        {
            ClearHudText(-1);
            ResetHudImages();
            m_interactPending = false;
            m_interactNow = false;
            m_interactHeldInput = false;
            m_interactHeldNow = false;
            m_customPending.Clear();
            m_customNow.Clear();
            m_customHeldInput.Clear();
            m_customHeldNow.Clear();
        }

        /// <summary>
        /// 스프라이트 글꼴(char.dds, 글자 코드 0 에서 255 사이)로 그릴 수 없는 글자를 물음표로 바꾼다.
        /// </summary>
        /// <param name="text">글자.</param>
        /// <returns>그릴 수 있는 글자만 남긴 것.</returns>
        private static string ToSpriteText(string text)
        {
            StringBuilder builder = null;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < k_spriteGlyphCount) continue;

                builder ??= new StringBuilder(text);
                builder[i] = k_hudMissingGlyph;
            }
            return builder != null ? builder.ToString() : text;
        }

        /// <summary>
        /// 선택 값 사전에서 정수를 읽는다. 스크립트가 넘긴 값이라 형식을 확인한다.
        /// </summary>
        /// <param name="options">사전. null 이어도 된다.</param>
        /// <param name="key">키.</param>
        /// <param name="fallback">키가 없거나 수가 아닐 때의 값.</param>
        /// <returns>값.</returns>
        private static int ReadInt(Godot.Collections.Dictionary options, string key, int fallback)
        {
            if (options == null || !options.TryGetValue(key, out Variant value)) return fallback;
            if (value.VariantType == Variant.Type.Int) return value.AsInt32();
            if (value.VariantType == Variant.Type.Float) return (int)value.AsDouble();
            return fallback;
        }

        /// <summary>
        /// 선택 값 사전에서 실수를 읽는다. 유한한 수가 아니면 기본값이다.
        /// </summary>
        /// <param name="options">사전. null 이어도 된다.</param>
        /// <param name="key">키.</param>
        /// <param name="fallback">키가 없거나 수가 아닐 때의 값.</param>
        /// <returns>값.</returns>
        private static float ReadFloat(Godot.Collections.Dictionary options, string key, float fallback)
        {
            if (options == null || !options.TryGetValue(key, out Variant value)) return fallback;
            if (value.VariantType != Variant.Type.Int && value.VariantType != Variant.Type.Float) return fallback;

            float result = value.AsSingle();
            return float.IsFinite(result) ? result : fallback;
        }
    }
}

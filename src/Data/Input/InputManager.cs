using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// ConfigManager가 소유한 바인딩 정의로 Godot InputMap 액션을 data-driven으로 구성하고, 이름으로 입력을 조회하게 해 주는 싱글톤 매니저.
    /// 버튼은 IsPressed/WasPressed/WasReleased 로, 방향 묶음(move/look)은 ReadVector 로 읽는다.
    /// 조회는 _Process 단계 기준이다. Autoload 순서상 ConfigManager 뒤에 등록해야 한다.
    /// </summary>
    public partial class InputManager : Singleton<InputManager>
    {
        public const string Look = "look";
        public const string Move = "move";
        public const string Jump = "jump";
        public const string Walk = "walk";
        public const string Drop = "drop";
        public const string Fire = "fire";
        public const string Zoom = "zoom";
        public const string Previous = "previous";
        public const string Next = "next";
        public const string Reload = "reload";
        public const string First = "first";
        public const string Second = "second";
        public const string Interact = "interact";
        public const string Escape = "escape";

        // InputMap 에 등록할 때 붙이는 접두사. Godot 내장 액션(ui_*)과 이름이 겹치지 않게 한다.
        private const string k_mapPrefix = "xops_";

        // 리바인드에서 제외할 예약 키(메뉴/치트·기능키).
        private static readonly HashSet<Key> s_reservedBindKeys = new HashSet<Key>
        {
            Key.Escape, Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        };

        /// <summary>
        /// 빌드된 액션 하나의 InputMap 이름들. 버튼 바인딩은 button 에, 방향 묶음은 up/down/left/right 에 등록된다.
        /// </summary>
        private class ActionEntry
        {
            public InputActionDefinition definition;
            public StringName button;
            public StringName up;
            public StringName down;
            public StringName left;
            public StringName right;
            public bool usesMouseDelta;
        }

        private readonly Dictionary<string, ActionEntry> m_actions = new Dictionary<string, ActionEntry>(StringComparer.OrdinalIgnoreCase);

        // 마우스 이동량은 폴링할 수 없어 이벤트로 모은다. _Input 이 채운 pending 을 _Process 첫머리에 이번 프레임 값으로 넘긴다.
        private Vector2 m_pendingMouseDelta;
        private Vector2 m_mouseDelta;
        private readonly HashSet<Key> m_pendingPressedKeys = new HashSet<Key>();
        private readonly HashSet<Key> m_pressedKeys = new HashSet<Key>();
        private string m_pendingPressedPath = string.Empty;
        private string m_pressedPath = string.Empty;

        public override void _Ready()
        {
            // 다른 모든 노드의 _Process 보다 먼저 돌아 이번 프레임 입력을 확정한다.
            ProcessPriority = int.MinValue;

            foreach (InputActionDefinition def in ConfigManager.Instance.Bindings)
            {
                BuildAction(def);
            }
        }

        public override void _Input(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseMotion motion)
            {
                m_pendingMouseDelta += motion.ScreenRelative;
            }
            else if (inputEvent is InputEventKey key && key.Pressed && !key.Echo)
            {
                m_pendingPressedKeys.Add(key.PhysicalKeycode);
                if (m_pendingPressedPath.Length == 0 && !s_reservedBindKeys.Contains(key.PhysicalKeycode))
                {
                    m_pendingPressedPath = InputPath.ToPath(key);
                }
            }
            else if (inputEvent is InputEventMouseButton button && button.Pressed && m_pendingPressedPath.Length == 0)
            {
                m_pendingPressedPath = InputPath.ToPath(button);
            }
        }

        public override void _Process(double delta)
        {
            m_mouseDelta = m_pendingMouseDelta;
            m_pendingMouseDelta = Vector2.Zero;

            m_pressedKeys.Clear();
            m_pressedKeys.UnionWith(m_pendingPressedKeys);
            m_pendingPressedKeys.Clear();

            m_pressedPath = m_pendingPressedPath;
            m_pendingPressedPath = string.Empty;
        }

        public override void _ExitTree()
        {
            foreach (ActionEntry entry in m_actions.Values)
            {
                foreach (StringName name in new[] { entry.button, entry.up, entry.down, entry.left, entry.right })
                {
                    if (name != null && InputMap.HasAction(name))
                    {
                        InputMap.EraseAction(name);
                    }
                }
            }
            m_actions.Clear();

            base._ExitTree();
        }

        /// <summary>
        /// 액션이 지금 눌려 있는지 반환한다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>눌려 있으면 true. 없는 액션이면 false.</returns>
        public bool IsPressed(string action)
        {
            return m_actions.TryGetValue(action, out ActionEntry entry) && Input.IsActionPressed(entry.button);
        }

        /// <summary>
        /// 액션이 이번 프레임에 눌렸는지 반환한다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>이번 프레임에 눌렸으면 true. 없는 액션이면 false.</returns>
        public bool WasPressed(string action)
        {
            return m_actions.TryGetValue(action, out ActionEntry entry) && Input.IsActionJustPressed(entry.button);
        }

        /// <summary>
        /// 액션이 이번 프레임에 떼어졌는지 반환한다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>이번 프레임에 떼어졌으면 true. 없는 액션이면 false.</returns>
        public bool WasReleased(string action)
        {
            return m_actions.TryGetValue(action, out ActionEntry entry) && Input.IsActionJustReleased(entry.button);
        }

        /// <summary>
        /// 방향 묶음 액션(move/look)의 2D 값을 읽는다. X는 오른쪽이 +, Y는 위쪽(전진)이 + 다.
        /// 마우스 이동에 묶인 액션은 이번 프레임 마우스 이동량(픽셀)을 우선 반환하고, 이동이 없으면 방향키 묶음 값을 반환한다.
        /// 방향키 묶음은 대각선에서 길이 1로 정규화된다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>2D 값. 없는 액션이면 (0, 0).</returns>
        public Vector2 ReadVector(string action)
        {
            if (!m_actions.TryGetValue(action, out ActionEntry entry))
            {
                return Vector2.Zero;
            }

            if (entry.usesMouseDelta && m_mouseDelta != Vector2.Zero)
            {
                // Godot 화면 좌표는 아래가 + 이므로 위쪽 + 규약에 맞춰 Y 를 뒤집는다.
                return new Vector2(m_mouseDelta.X, -m_mouseDelta.Y);
            }

            if (entry.up == null)
            {
                return Vector2.Zero;
            }

            var value = new Vector2(
                (Input.IsActionPressed(entry.right) ? 1f : 0f) - (Input.IsActionPressed(entry.left) ? 1f : 0f),
                (Input.IsActionPressed(entry.up) ? 1f : 0f) - (Input.IsActionPressed(entry.down) ? 1f : 0f));
            return value.LengthSquared() > 1f ? value.Normalized() : value;
        }

        /// <summary>
        /// 바인딩과 무관하게 물리 키가 지금 눌려 있는지 반환한다. 치트 키처럼 설정에 노출하지 않는 키에 쓴다.
        /// </summary>
        /// <param name="key">물리 키.</param>
        /// <returns>눌려 있으면 true.</returns>
        public bool IsKeyPressed(Key key)
        {
            return Input.IsPhysicalKeyPressed(key);
        }

        /// <summary>
        /// 바인딩과 무관하게 물리 키가 이번 프레임에 눌렸는지 반환한다.
        /// </summary>
        /// <param name="key">물리 키.</param>
        /// <returns>이번 프레임에 눌렸으면 true.</returns>
        public bool WasKeyPressed(Key key)
        {
            return m_pressedKeys.Contains(key);
        }

        /// <summary>
        /// 이번 프레임에 처음 눌린 키/마우스 버튼의 바인딩 경로를 반환한다. 키 리바인딩(리스닝 중 키 캡처)에 쓴다.
        /// 예약 키(Escape/F1~F12)는 건너뛴다.
        /// </summary>
        /// <returns>바인딩 경로(예 "&lt;Keyboard&gt;/w", "&lt;Mouse&gt;/leftButton"), 없으면 빈 문자열.</returns>
        public string GetFirstPressedKeyPath()
        {
            return m_pressedPath;
        }

        /// <summary>
        /// 단일 버튼 액션의 바인딩(인덱스 0)을 지정 경로로 교체한다. 라이브 액션에 즉시 반영하고,
        /// ConfigManager의 bindings 데이터도 갱신해 저장(Save) 시 유지되게 한다. 방향 묶음에는 쓰지 않는다.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        /// <param name="path">새 바인딩 경로(예 "&lt;Keyboard&gt;/space").</param>
        /// <returns>성공하면 true.</returns>
        public bool SetActionBinding(string action, string path)
        {
            if (!m_actions.TryGetValue(action, out ActionEntry entry) || entry.definition.bindings.Length == 0
                || InputPath.Parse(path) == null)
            {
                return false;
            }

            entry.definition.bindings[0] = path;
            ApplyButtonBindings(entry);
            return true;
        }

        /// <summary>
        /// ConfigManager의 bindings 데이터를 라이브 액션에 다시 적용한다.
        /// 옵션 BACK(되돌리기)/RESET(초기화)로 설정 쪽 바인딩이 바뀐 뒤 라이브를 동기화하는 데 쓴다.
        /// </summary>
        public void ReapplyBindings()
        {
            foreach (ActionEntry entry in m_actions.Values)
            {
                ApplyButtonBindings(entry);
            }
        }

        /// <summary>
        /// 현재 등록된 모든 액션 이름을 반환한다.
        /// </summary>
        /// <returns>액션 이름 배열.</returns>
        public string[] GetActionNames()
        {
            var names = new string[m_actions.Count];
            m_actions.Keys.CopyTo(names, 0);
            return names;
        }

        /// <summary>
        /// 액션이 등록돼 있는지 반환한다.
        /// </summary>
        /// <param name="action">액션 이름 (대소문자 무시).</param>
        /// <returns>등록돼 있으면 true.</returns>
        public bool HasAction(string action)
        {
            return m_actions.ContainsKey(action);
        }

        /// <summary>
        /// 런타임에 액션 정의 하나를 추가 등록한다. 같은 이름이 이미 있으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="def">추가할 액션 정의.</param>
        /// <returns>새로 등록했으면 true, 이미 있거나 정의가 잘못됐으면 false.</returns>
        public bool RegisterAction(InputActionDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.name) || m_actions.ContainsKey(def.name))
            {
                return false;
            }

            BuildAction(def);
            return true;
        }

        /// <summary>
        /// 액션 정의 하나를 InputMap에 추가하고 이름→액션 사전에 등록한다.
        /// </summary>
        /// <param name="def">액션 이름/바인딩/컴포짓 정의.</param>
        private void BuildAction(InputActionDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.name))
            {
                return;
            }

            def.bindings ??= new string[0];
            def.composites ??= new InputCompositeDefinition[0];

            var entry = new ActionEntry
            {
                definition = def,
                button = AddMapAction(k_mapPrefix + def.name),
            };
            ApplyButtonBindings(entry);

            if (def.composites.Length > 0)
            {
                entry.up = AddMapAction(k_mapPrefix + def.name + ".up");
                entry.down = AddMapAction(k_mapPrefix + def.name + ".down");
                entry.left = AddMapAction(k_mapPrefix + def.name + ".left");
                entry.right = AddMapAction(k_mapPrefix + def.name + ".right");
                foreach (InputCompositeDefinition composite in def.composites)
                {
                    AddMapEvent(entry.up, composite.up, def.name);
                    AddMapEvent(entry.down, composite.down, def.name);
                    AddMapEvent(entry.left, composite.left, def.name);
                    AddMapEvent(entry.right, composite.right, def.name);
                }
            }

            m_actions[def.name] = entry;
        }

        /// <summary>
        /// 액션의 단순 바인딩 목록을 InputMap 에 처음부터 다시 등록한다. 마우스 이동 바인딩은 이벤트 대신 플래그로 기록한다.
        /// </summary>
        /// <param name="entry">대상 액션.</param>
        private void ApplyButtonBindings(ActionEntry entry)
        {
            InputMap.ActionEraseEvents(entry.button);
            entry.usesMouseDelta = false;

            foreach (string path in entry.definition.bindings)
            {
                if (string.Equals(path, InputPath.MouseDelta, StringComparison.OrdinalIgnoreCase))
                {
                    entry.usesMouseDelta = true;
                }
                else
                {
                    AddMapEvent(entry.button, path, entry.definition.name);
                }
            }
        }

        private static StringName AddMapAction(string mapName)
        {
            var name = new StringName(mapName);
            if (!InputMap.HasAction(name))
            {
                InputMap.AddAction(name);
            }
            return name;
        }

        private static void AddMapEvent(StringName mapAction, string path, string actionName)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            InputEvent inputEvent = InputPath.Parse(path);
            if (inputEvent == null)
            {
                Debugger.LogWarning($"알 수 없는 바인딩 경로 \"{path}\" 를 무시합니다 (액션 {actionName}).", nameof(InputManager));
                return;
            }
            InputMap.ActionAddEvent(mapAction, inputEvent);
        }

        /// <summary>
        /// 마우스 커서 표시 여부, 잠금 모드, 화면 중앙 이동 여부를 설정한다.
        /// </summary>
        /// <param name="hideInWindow">true면 창 안에 마우스가 있을 때 커서를 숨긴다.</param>
        /// <param name="centered">true면 커서를 숨기고 창에 가둔다(시점 조작용).</param>
        /// <param name="moveToCenter">true면 커서를 즉시 화면 중앙으로 이동시킨다.</param>
        public void MouseCursorMode(bool hideInWindow, bool centered, bool moveToCenter)
        {
            if (centered)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                return;
            }

            Input.MouseMode = hideInWindow ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
            if (moveToCenter)
            {
                Viewport viewport = GetViewport();
                viewport.WarpMouse(viewport.GetVisibleRect().Size / 2f);
            }
        }
    }
}

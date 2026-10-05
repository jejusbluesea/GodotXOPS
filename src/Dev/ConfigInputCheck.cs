using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. ConfigManager 의 읽기/쓰기/되돌리기와 InputManager 의 바인딩 변환·입력 조회를
    /// 가짜 입력 이벤트로 확인하고 결과를 출력한 뒤 종료한다. 설정 파일은 저장하지 않는다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/config_input_check.tscn
    /// </summary>
    public partial class ConfigInputCheck : Node
    {
        private readonly List<string> m_failures = new List<string>();
        private int m_checks;

        public override async void _Ready()
        {
            ConfigManager config = ConfigManager.Instance;
            InputManager input = InputManager.Instance;

            // --- 설정 ---
            int settingCount = 0;
            foreach (string section in config.GetSectionNames())
            {
                settingCount += config.GetSettingNames(section).Length;
            }
            GD.Print($"섹션 {config.GetSectionNames().Length}개, 설정 {settingCount}개, 액션 {input.GetActionNames().Length}개");

            Expect("기본 fov", config.GetInt(ConfigManager.SectionGraphic, "fov"), 65);
            config.SetInt(ConfigManager.SectionGraphic, "fov", 200);
            Expect("fov 상한 클램프", config.GetInt(ConfigManager.SectionGraphic, "fov"), 90);

            float savedSensitivity = config.MouseSensitivity;
            config.SetFloat(ConfigManager.SectionInput, ConfigManager.KeySensitivity, 0.5f);
            Expect("감도 캐시 갱신", config.MouseSensitivity, 0.5f);

            Expect("없는 설정은 fallback", config.GetInt("Nope", "nope", 7), 7);

            Expect("리바인드 성공", input.SetActionBinding(InputManager.Jump, "<Keyboard>/k"), true);
            Expect("잘못된 경로 리바인드 거부", input.SetActionBinding(InputManager.Jump, "<Keyboard>/nope"), false);
            Expect("리바인드 뒤 현재 바인딩 조회", input.GetActionBinding(InputManager.Jump), "<Keyboard>/k");
            Expect("없는 액션의 바인딩 조회", input.GetActionBinding("nope"), string.Empty);
            Expect("fov 범위 최솟값", config.GetMin(ConfigManager.SectionGraphic, "fov"), 60f);
            Expect("fov 범위 최댓값", config.GetMax(ConfigManager.SectionGraphic, "fov"), 90f);

            config.RevertToSaved();
            Expect("BACK: fov 복원", config.GetInt(ConfigManager.SectionGraphic, "fov"), 65);
            Expect("BACK: 감도 복원", config.MouseSensitivity, savedSensitivity);
            Expect("BACK: 바인딩 복원", FindBinding(InputManager.Jump), "<Keyboard>/space");

            config.SetBool(ConfigManager.SectionInput, ConfigManager.KeyInvertY, true);
            input.SetActionBinding(InputManager.Reload, "<Mouse>/rightButton");
            // AllowConsole 은 옵션 화면에 없는 설정이라 RESET 이 건드리지 않는다. 파일에 저장된 값이 무엇이든 그 반대로 바꿔 놓고 본다.
            bool savedAllowConsole = config.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowConsole);
            Expect("AllowConsole 설정 존재", config.FindSetting(ConfigManager.SectionGeneral, ConfigManager.KeyAllowConsole) != null, true);
            config.SetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowConsole, !savedAllowConsole);
            config.ResetToDefaults();
            Expect("RESET: invertY", config.InvertY, false);
            Expect("RESET: 바인딩", FindBinding(InputManager.Reload), "<Keyboard>/r");
            Expect("RESET: AllowConsole 유지", config.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowConsole), !savedAllowConsole);
            config.RevertToSaved();
            Expect("BACK: AllowConsole 복원", config.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowConsole), savedAllowConsole);

            config.SetInt(ConfigManager.SectionGraphic, ConfigManager.KeyResolution, 13);
            Expect("1920x1080 의 UIScale 상한", config.MaxUIScale(), 2.2f);
            config.RevertToSaved();

            // --- 바인딩 경로 변환 ---
            foreach (InputActionDefinition def in config.Bindings)
            {
                var paths = new List<string>(def.bindings);
                foreach (InputCompositeDefinition composite in def.composites)
                {
                    paths.AddRange(new[] { composite.up, composite.down, composite.left, composite.right });
                }
                foreach (string path in paths)
                {
                    if (path == InputPath.MouseDelta)
                    {
                        continue;
                    }
                    InputEvent parsed = InputPath.Parse(path);
                    Expect($"경로 변환 {path}", parsed != null, true);
                    if (parsed != null)
                    {
                        Expect($"경로 왕복 {path}", InputPath.ToPath(parsed), path);
                    }
                }
            }

            // --- 가짜 입력으로 조회 확인 ---
            // _Ready 는 첫 프레임 전이라, 프레임 안(InputManager._Process 직전)에서 시작하도록 한 프레임 넘긴다.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Send(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });
            Send(new InputEventKey { PhysicalKeycode = Key.D, Pressed = true });
            Send(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
            Send(new InputEventKey { PhysicalKeycode = Key.Shift, Location = KeyLocation.Left, Pressed = true });
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Send(new InputEventMouseMotion { Relative = new Vector2(10f, -5f), ScreenRelative = new Vector2(10f, -5f) });

            Expect("jump 눌림", input.IsPressed(InputManager.Jump), true);
            Expect("jump 이번 프레임 눌림", input.WasPressed(InputManager.Jump), true);
            Expect("zoom(leftShift) 눌림", input.IsPressed(InputManager.Zoom), true);
            Expect("fire(마우스 왼쪽) 눌림", input.IsPressed(InputManager.Fire), true);
            Expect("reload 안 눌림", input.IsPressed(InputManager.Reload), false);
            Vector2 move = input.ReadVector(InputManager.Move);
            Expect("move 대각선 정규화", Mathf.IsEqualApprox(move.X, Mathf.Sqrt2 / 2f) && Mathf.IsEqualApprox(move.Y, Mathf.Sqrt2 / 2f), true);

            // 마우스 이동량과 키 엣지는 InputManager._Process 가 확정하므로 한 프레임 뒤에 읽는다.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Expect("look 마우스 이동량(Y 위쪽 +)", input.ReadVector(InputManager.Look), new Vector2(10f, 5f));
            Expect("W 키 엣지", input.WasKeyPressed(Key.W), true);
            Expect("처음 눌린 키 경로", input.GetFirstPressedKeyPath(), "<Keyboard>/w");
            Expect("jump 엣지는 한 프레임만", input.WasPressed(InputManager.Jump), false);

            Send(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = false });
            Expect("jump 뗌", input.WasReleased(InputManager.Jump), true);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Send(new InputEventKey { PhysicalKeycode = Key.Up, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Expect("look 방향키 묶음", input.ReadVector(InputManager.Look), new Vector2(0f, 1f));

            GD.Print($"점검 {m_checks}건 — 실패 {m_failures.Count}건");
            foreach (string failure in m_failures)
            {
                GD.Print($"실패: {failure}");
            }
            GetTree().Quit(m_failures.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 가짜 입력 이벤트를 엔진 입력 경로로 흘려보내고 즉시 처리시킨다.
        /// </summary>
        /// <param name="inputEvent">보낼 이벤트.</param>
        private static void Send(InputEvent inputEvent)
        {
            Input.ParseInputEvent(inputEvent);
            Input.FlushBufferedEvents();
        }

        /// <summary>
        /// 설정에 저장된 액션의 0번 바인딩 경로를 찾는다.
        /// </summary>
        /// <param name="action">액션 이름.</param>
        /// <returns>바인딩 경로. 없으면 빈 문자열.</returns>
        private static string FindBinding(string action)
        {
            foreach (InputActionDefinition def in ConfigManager.Instance.Bindings)
            {
                if (def.name == action && def.bindings.Length > 0)
                {
                    return def.bindings[0];
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 실제 값이 기대 값과 같은지 확인하고 다르면 실패 목록에 기록한다.
        /// </summary>
        /// <param name="label">점검 이름.</param>
        /// <param name="actual">실제 값.</param>
        /// <param name="expected">기대 값.</param>
        private void Expect<T>(string label, T actual, T expected)
        {
            m_checks++;
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                m_failures.Add($"{label}: 기대 {expected}, 실제 {actual}");
            }
        }
    }
}

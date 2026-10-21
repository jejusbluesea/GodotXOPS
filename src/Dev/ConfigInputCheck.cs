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

            Expect("crouch 기본 키는 왼쪽 Ctrl", input.GetActionBinding(InputManager.Crouch), "<Keyboard>/leftCtrl");
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

            CheckGraphicSettings(config);
            CheckCustomBindings(config, input);

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

            // 커스텀 바인딩도 기본 액션처럼 눌림을 읽는다.
            input.SetCustomLayer(CustomBindingLayer.Installed, CheckList(("check_light", "Light", "<Keyboard>/v")));
            Send(new InputEventKey { PhysicalKeycode = Key.V, Pressed = true });
            Expect("커스텀 바인딩 눌림", input.IsPressed("check_light"), true);
            Expect("커스텀 바인딩 이번 프레임 눌림", input.WasPressed("check_light"), true);
            Send(new InputEventKey { PhysicalKeycode = Key.V, Pressed = false });
            input.SetCustomLayer(CustomBindingLayer.Installed, null);

            GD.Print($"점검 {m_checks}건 — 실패 {m_failures.Count}건");
            foreach (string failure in m_failures)
            {
                GD.Print($"실패: {failure}");
            }
            GetTree().Quit(m_failures.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// Graphic 탭의 Near / Far / Fog 설정의 기본값과 범위, RESET 을 확인한다.
        /// </summary>
        /// <param name="config">설정 매니저.</param>
        private void CheckGraphicSettings(ConfigManager config)
        {
            string graphic = ConfigManager.SectionGraphic;
            Expect("enableFog 설정 존재", config.FindSetting(graphic, ConfigManager.KeyEnableFog) != null, true);
            Expect("enableFog 형", config.GetSettingType(graphic, ConfigManager.KeyEnableFog), ConfigManager.TypeBool);
            Expect("near 범위", (config.GetMin(graphic, ConfigManager.KeyNearClippingPlane), config.GetMax(graphic, ConfigManager.KeyNearClippingPlane)), (0.03f, 0.1f));
            Expect("far 범위", (config.GetMin(graphic, ConfigManager.KeyFarClippingPlane), config.GetMax(graphic, ConfigManager.KeyFarClippingPlane)), (80f, 1000f));

            // 안개를 끄면 하늘의 안개가 꺼지고, 켜면 하늘 데이터대로 다시 켜진다.
            config.SetBool(graphic, ConfigManager.KeyEnableFog, false);
            MapLoader.ApplySkyFog(0);
            Expect("enableFog 끔: 안개 없음", MapLoader.FogActive, false);
            config.SetBool(graphic, ConfigManager.KeyEnableFog, true);
            MapLoader.ApplySkyFog(0);
            Expect("enableFog 켬: 하늘 데이터대로", MapLoader.FogActive, DataManager.Instance.SkyData.fog);
            MapLoader.ClearFog();
            config.SetBool(graphic, ConfigManager.KeyEnableFog, false);
            config.SetFloat(graphic, ConfigManager.KeyNearClippingPlane, 0.05f);
            config.SetFloat(graphic, ConfigManager.KeyFarClippingPlane, 5000f);
            Expect("far 상한 클램프", config.GetFloat(graphic, ConfigManager.KeyFarClippingPlane), 1000f);
            config.ResetToDefaults();
            Expect("RESET: enableFog", config.GetBool(graphic, ConfigManager.KeyEnableFog), true);
            Expect("RESET: near", config.GetFloat(graphic, ConfigManager.KeyNearClippingPlane), 0.1f);
            Expect("RESET: far", config.GetFloat(graphic, ConfigManager.KeyFarClippingPlane), 80f);
            config.RevertToSaved();
        }

        /// <summary>
        /// 커스텀 바인딩: 층의 등록과 거절, 층의 순서, 리바인드가 customBindings 에 남는지, BACK 과 RESET, 층을 비우면 액션이 사라지는지.
        /// 점검용 이름(check_)만 쓰고 끝에 층을 비우고 설정을 되돌린다.
        /// </summary>
        /// <param name="config">설정 매니저.</param>
        /// <param name="input">입력 매니저.</param>
        private void CheckCustomBindings(ConfigManager config, InputManager input)
        {
            CustomBindingListData installed = CheckList(
                ("check_light", "Light", "<Keyboard>/v"),
                ("check_radio", "", "<Keyboard>/t"),
                ("bad name", "", "<Keyboard>/v"),
                (InputManager.Jump, "", "<Keyboard>/j"),
                ("check_reserved", "", "<Keyboard>/f5"),
                ("check_unknown", "", "<Keyboard>/nope"));
            input.SetCustomLayer(CustomBindingLayer.Installed, installed);
            Expect("커스텀: 등록 수 (잘못된 이름과 기본 액션 이름은 뺀다)", string.Join(",", input.GetCustomActionNames()),
                "check_light,check_radio,check_reserved,check_unknown");
            Expect("커스텀: 처음 키", input.GetActionBinding("check_light"), "<Keyboard>/v");
            Expect("커스텀: label", input.GetCustomLabel("check_light"), "Light");
            Expect("커스텀: label 이 없으면 이름", input.GetCustomLabel("check_radio"), "check_radio");
            Expect("커스텀: 예약 키는 묶지 않는다", input.GetActionBinding("check_reserved"), string.Empty);
            Expect("커스텀: 모르는 키는 묶지 않는다", input.GetActionBinding("check_unknown"), string.Empty);
            Expect("커스텀: 기본 액션은 그대로", input.IsCustomAction(InputManager.Jump), false);
            Expect("커스텀: 전체 액션 목록에 든다", System.Array.IndexOf(input.GetActionNames(), "check_light") >= 0, true);

            // 층의 순서: 설치형 → 맵팩(미션이 붙인 것이 메뉴의 것보다 먼저). 같은 이름은 앞의 층의 것을 쓴다.
            input.SetCustomLayer(CustomBindingLayer.MenuPack, CheckList(("check_menu", "", "<Keyboard>/m")));
            Expect("커스텀: 메뉴 맵팩 층", input.IsCustomAction("check_menu"), true);
            input.SetCustomLayer(CustomBindingLayer.Pack, CheckList(("check_pack", "", "<Keyboard>/p"), ("check_light", "Other", "<Keyboard>/o")));
            Expect("커스텀: 미션의 맵팩이 있으면 메뉴의 맵팩은 쓰지 않는다", string.Join(",", input.GetCustomActionNames()),
                "check_light,check_radio,check_reserved,check_unknown,check_pack");
            Expect("커스텀: 같은 이름은 앞의 층", input.GetCustomLabel("check_light"), "Light");
            input.SetCustomLayer(CustomBindingLayer.Pack, null);
            Expect("커스텀: 미션의 맵팩을 떼면 메뉴의 맵팩", input.IsCustomAction("check_menu") && !input.IsCustomAction("check_pack"), true);
            Expect("커스텀: 뗀 층의 액션은 없다", input.HasAction("check_pack"), false);

            // 리바인드는 config 의 customBindings 에 남고, 처음 키로 되돌리면 지워진다.
            Expect("커스텀: 리바인드", input.SetActionBinding("check_light", "<Keyboard>/y"), true);
            Expect("커스텀: 바꾼 키가 설정에 남는다", config.GetCustomBinding("check_light", out string saved) && saved == "<Keyboard>/y", true);
            input.SetActionBinding("check_radio", "<Keyboard>/u");
            input.SetActionBinding("check_radio", "<Keyboard>/t");
            Expect("커스텀: 처음 키와 같으면 남기지 않는다", config.GetCustomBinding("check_radio", out _), false);

            // 층을 뗐다 붙여도 바꾼 키는 남는다 (같은 이름은 맵팩이 달라도 같은 값).
            input.SetCustomLayer(CustomBindingLayer.Installed, null);
            input.SetCustomLayer(CustomBindingLayer.Installed, installed);
            Expect("커스텀: 다시 붙여도 바꾼 키", input.GetActionBinding("check_light"), "<Keyboard>/y");

            config.RevertToSaved();
            Expect("커스텀 BACK: 저장하지 않은 키는 되돌린다", input.GetActionBinding("check_light"), "<Keyboard>/v");
            input.SetActionBinding("check_light", "<Keyboard>/y");
            config.ResetToDefaults();
            Expect("커스텀 RESET: 처음 키", input.GetActionBinding("check_light"), "<Keyboard>/v");
            Expect("커스텀 RESET: 설정에 남은 값이 없다", config.Config.customBindings.Count, 0);
            config.RevertToSaved();

            input.SetCustomLayer(CustomBindingLayer.Installed, null);
            input.SetCustomLayer(CustomBindingLayer.MenuPack, null);
            Expect("커스텀: 층을 다 비우면 없다", input.GetCustomActionNames().Length, 0);
            Expect("커스텀: 비운 뒤 액션도 없다", input.HasAction("check_light"), false);
        }

        /// <summary>
        /// 점검용 커스텀 바인딩 등록을 만든다.
        /// </summary>
        /// <param name="entries">(이름, label, 처음 키) 목록.</param>
        /// <returns>등록.</returns>
        private static CustomBindingListData CheckList(params (string name, string label, string baseValue)[] entries)
        {
            var data = new CustomBindingListData();
            foreach ((string name, string label, string baseValue) in entries)
            {
                data.bindings.Add(new CustomBindingData { name = name, label = label, baseValue = baseValue });
            }
            return data;
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

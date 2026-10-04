using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 맵 뷰어. 미션을 골라 블록과 스카이를 로드하고 자유 카메라로 날아다니며 확인한다.
    /// 조작: 마우스 오른쪽 버튼을 누른 채 시점 회전, 이동 키(move 바인딩)로 이동, Q/E 하강/상승, 왼쪽 Shift 가속.
    /// 명령행 인자("--" 뒤): --selftest 는 모든 미션을 로드해 보고 종료, --screenshot 경로 는 화면을 PNG 로 저장하고 종료,
    /// --mission 번호 / --addon 은 시작 미션 지정, --cam x,y,z,yaw,pitch 는 카메라 위치·각도(도) 지정.
    /// </summary>
    public partial class MapViewer : Node3D
    {
        private const float k_moveSpeed = 8f;
        private const float k_fastMultiplier = 4f;
        private const float k_pitchLimit = 89f;
        private const int k_screenshotWaitFrames = 12;

        private Camera3D m_camera;
        private OptionButton m_missionSelect;
        private Label m_info;

        // 목록 순번 → (어드온 여부, 페이지, 인덱스).
        private readonly List<(bool mif, int page, int index)> m_entries = new List<(bool, int, int)>();

        // 시점 각도는 UnityXOPS 규약(yaw 오른쪽 +, pitch 아래 +, 도 단위)으로 들고 Coord 로 변환해 적용한다.
        private float m_yaw;
        private float m_pitch;

        private string m_screenshotPath;
        private int m_screenshotCountdown = -1;

        public override void _Ready()
        {
            // 게임 설정(ConfigManager)이 적용한 전체화면·저해상도 렌더를 도구용 창 설정으로 되돌린다.
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            root.Mode = Window.ModeEnum.Windowed;
            root.Size = new Vector2I(1280, 720);
            root.MoveToCenter();
            Engine.MaxFps = 0;

            m_camera = new Camera3D { Name = "Camera" };
            AddChild(m_camera);
            MapLoader.ApplyCameraSettings(m_camera);
            m_camera.MakeCurrent();

            BuildInterface();
            BuildMissionList();

            string[] args = OS.GetCmdlineUserArgs();
            if (Array.IndexOf(args, "--selftest") >= 0)
            {
                RunSelfTest();
                return;
            }

            int start = 0;
            int missionArg = Array.IndexOf(args, "--mission");
            if (missionArg >= 0 && missionArg + 1 < args.Length && int.TryParse(args[missionArg + 1], out int missionIndex))
            {
                bool addon = Array.IndexOf(args, "--addon") >= 0;
                start = m_entries.FindIndex(entry => entry.mif == addon && entry.index == missionIndex);
            }
            if (start >= 0 && start < m_entries.Count)
            {
                m_missionSelect.Select(start);
                LoadEntry(start);
            }

            int camArg = Array.IndexOf(args, "--cam");
            if (camArg >= 0 && camArg + 1 < args.Length)
            {
                string[] parts = args[camArg + 1].Split(',');
                if (parts.Length == 5)
                {
                    float[] v = Array.ConvertAll(parts, part => float.Parse(part, CultureInfo.InvariantCulture));
                    m_camera.Position = new Vector3(v[0], v[1], v[2]);
                    m_yaw = v[3];
                    m_pitch = v[4];
                    ApplyCameraRotation();
                }
            }

            int screenshotArg = Array.IndexOf(args, "--screenshot");
            if (screenshotArg >= 0 && screenshotArg + 1 < args.Length)
            {
                m_screenshotPath = args[screenshotArg + 1];
                m_screenshotCountdown = k_screenshotWaitFrames;
                m_missionSelect.Visible = false;
            }
        }

        public override void _Process(double delta)
        {
            if (m_screenshotCountdown >= 0)
            {
                UpdateInfo();
                if (m_screenshotCountdown-- == 0)
                {
                    Error error = GetViewport().GetTexture().GetImage().SavePng(m_screenshotPath);
                    GD.Print($"스크린샷 {(error == Error.Ok ? "저장" : "실패")}: {m_screenshotPath}");
                    GetTree().Quit(error == Error.Ok ? 0 : 1);
                }
                return;
            }

            InputManager input = InputManager.Instance;

            bool looking = Input.IsMouseButtonPressed(MouseButton.Right);
            input.MouseCursorMode(false, looking, false);
            if (looking)
            {
                Vector2 look = input.ReadVector(InputManager.Look);
                float sensitivity = ConfigManager.Instance.MouseSensitivity;
                float invertY = ConfigManager.Instance.InvertY ? -1f : 1f;
                m_yaw += look.X * sensitivity;
                m_pitch = Mathf.Clamp(m_pitch - look.Y * sensitivity * invertY, -k_pitchLimit, k_pitchLimit);
                ApplyCameraRotation();
            }

            Vector2 move = input.ReadVector(InputManager.Move);
            float vertical = (input.IsKeyPressed(Key.E) ? 1f : 0f) - (input.IsKeyPressed(Key.Q) ? 1f : 0f);
            float speed = k_moveSpeed * (input.IsKeyPressed(Key.Shift) ? k_fastMultiplier : 1f);
            Basis basis = m_camera.GlobalBasis;
            m_camera.Position += (basis.X * move.X - basis.Z * move.Y + Vector3.Up * vertical) * speed * (float)delta;

            UpdateInfo();
        }

        /// <summary>
        /// 미션 선택 목록과 정보 라벨을 만든다.
        /// </summary>
        private void BuildInterface()
        {
            var layer = new CanvasLayer();
            AddChild(layer);

            var box = new VBoxContainer { Position = new Vector2(8f, 8f) };
            layer.AddChild(box);

            m_missionSelect = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            m_missionSelect.ItemSelected += index => LoadEntry((int)index);
            box.AddChild(m_missionSelect);

            m_info = new Label();
            m_info.AddThemeColorOverride("font_outline_color", Colors.Black);
            m_info.AddThemeConstantOverride("outline_size", 4);
            box.AddChild(m_info);
        }

        /// <summary>
        /// 공식 미션과 어드온 미션을 선택 목록에 채운다.
        /// </summary>
        private void BuildMissionList()
        {
            MissionData missionData = DataManager.Instance.MissionData;

            for (int i = 0; i < missionData.officialMissions.Count; i++)
            {
                m_entries.Add((false, 0, i));
                m_missionSelect.AddItem($"{i}: {missionData.officialMissions[i].name}");
            }

            for (int page = 0; page < missionData.addonMissions.Count; page++)
            {
                for (int i = 0; i < missionData.addonMissions[page].Count; i++)
                {
                    m_entries.Add((true, page, i));
                    m_missionSelect.AddItem($"addon {page}/{i}: {missionData.addonMissions[page][i].name}");
                }
            }
        }

        /// <summary>
        /// 목록의 미션 하나를 로드하고 카메라를 맵 위쪽에 둔다.
        /// </summary>
        /// <param name="entryIndex">목록 순번.</param>
        /// <returns>블록 로드까지 성공했으면 true.</returns>
        private bool LoadEntry(int entryIndex)
        {
            (bool mif, int page, int index) entry = m_entries[entryIndex];
            if (!MapLoader.LoadMissionData(entry.index, entry.mif, entry.page))
            {
                return false;
            }

            MapLoader loader = MapLoader.Instance;
            bool ok = MapLoader.LoadBlockData(loader.MissionBD1Path);
            MapLoader.LoadSkyData(loader.SkyIndex);

            if (ok && GetMapBounds(out Vector3 min, out Vector3 max))
            {
                Vector3 center = (min + max) * 0.5f;
                Vector3 size = max - min;
                m_camera.Position = new Vector3(center.X, max.Y + 5f, center.Z + size.Z * 0.35f);
                m_yaw = 0f;
                m_pitch = 25f;
                ApplyCameraRotation();
            }
            return ok;
        }

        /// <summary>
        /// UnityXOPS 규약의 yaw/pitch 를 Godot 회전으로 바꿔 카메라에 적용한다.
        /// </summary>
        private void ApplyCameraRotation()
        {
            m_camera.Rotation = Coord.FromUnityEuler(new Vector3(m_pitch, m_yaw, 0f));
        }

        /// <summary>
        /// 로드된 모든 블록을 감싸는 범위를 구한다.
        /// </summary>
        /// <param name="min">최소 코너.</param>
        /// <param name="max">최대 코너.</param>
        /// <returns>블록이 하나라도 있으면 true.</returns>
        private static bool GetMapBounds(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Block block in MapLoader.Blocks)
            {
                min = min.Min(block.boundsMin);
                max = max.Max(block.boundsMax);
            }
            return MapLoader.Blocks.Count > 0;
        }

        /// <summary>
        /// 미션 정보와 카메라 정면 레이의 충돌 결과를 라벨에 표시한다.
        /// </summary>
        private void UpdateInfo()
        {
            MapLoader loader = MapLoader.Instance;
            Vector3 position = m_camera.GlobalPosition;
            Vector3 forward = -m_camera.GlobalBasis.Z;

            string hit = MapLoader.RaycastBlock(position, forward, 0f, out float dist, out Vector3 normal)
                ? $"{dist:0.00} m, 법선 ({normal.X:0.00}, {normal.Y:0.00}, {normal.Z:0.00})"
                : "없음";

            m_info.Text =
                $"{loader.MissionFullname}  |  하늘 {loader.SkyIndex}, 어두운 화면 {(loader.DarkScreen ? "예" : "아니오")}\n" +
                $"블록 {MapLoader.Blocks.Count} (충돌 {MapLoader.BlockColliders.Count})  |  {Engine.GetFramesPerSecond():0} fps\n" +
                $"카메라 ({position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}) yaw {m_yaw:0} pitch {m_pitch:0}\n" +
                $"정면 레이: {hit}  |  블록 내부: {(MapLoader.IsInsideBlock(position) ? "예" : "아니오")}\n" +
                "마우스 오른쪽 버튼 + 이동: 시점  |  이동 키  |  Q/E 하강/상승  |  Shift 가속";
        }

        /// <summary>
        /// 모든 미션을 차례로 로드해 블록 수와 위에서 아래로 쏜 레이의 충돌 여부를 확인하고 결과를 출력한 뒤 종료한다.
        /// </summary>
        private void RunSelfTest()
        {
            int loaded = 0;
            int totalBlocks = 0;
            var failures = new List<string>();

            for (int i = 0; i < m_entries.Count; i++)
            {
                string label = m_missionSelect.GetItemText(i);
                if (!LoadEntry(i) || MapLoader.Blocks.Count == 0)
                {
                    failures.Add($"{label}: 블록 로드 실패");
                    continue;
                }

                loaded++;
                totalBlocks += MapLoader.Blocks.Count;

                // 충돌 블록의 한가운데 위에서 아래로 쏘면 반드시 그 블록(또는 더 위의 블록)에 맞아야 한다.
                if (MapLoader.BlockColliders.Count > 0)
                {
                    Block block = MapLoader.BlockColliders[0];
                    Vector3 center = (block.boundsMin + block.boundsMax) * 0.5f;
                    var origin = new Vector3(center.X, 1000f, center.Z);
                    if (!MapLoader.RaycastBlock(origin, Vector3.Down, 0f, out float dist) || dist > 1000f - block.boundsMin.Y + 0.01f)
                    {
                        failures.Add($"{label}: 아래 방향 레이가 블록에 맞지 않음");
                    }
                    if (!MapLoader.IsInsideBlock(block.position) && block.Contains(block.position))
                    {
                        failures.Add($"{label}: 내부 판정 불일치");
                    }
                }
            }

            GD.Print($"미션 {m_entries.Count}개 중 {loaded}개 로드, 블록 합계 {totalBlocks} — 문제 {failures.Count}건");
            foreach (string failure in failures)
            {
                GD.Print($"문제: {failure}");
            }
            // 한 프레임 안에 수천 개의 노드를 만들고 지운 직후 종료하면 종료 과정에서 간헐적으로 죽는다. 관리 객체를 먼저 정리해 둔다.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}

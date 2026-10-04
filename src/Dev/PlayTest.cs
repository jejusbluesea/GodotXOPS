using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 플레이 점검 씬. 미션의 맵과 사람을 로드하고 플레이어를 직접 조작해 이동·충돌·카메라를 확인한다.
    /// 조작: 마우스 시점, 이동 키, Space 점프, Tab 걷기, F1 1인칭/3인칭, F5+Enter 상승, F8+←/→ 조작 대상 교체, Delete 플레이어 사망, Esc 마우스 풀기/잡기.
    /// 명령행 인자("--" 뒤): --selftest 는 모든 미션에서 틱을 돌려 보고 종료, --screenshot 경로 는 화면을 PNG 로 저장하고 종료,
    /// --mission 번호 / --addon 은 시작 미션, --third 는 3인칭으로 시작, --walk 는 스크린샷 전까지 전진 입력을 넣는다,
    /// --probe x,y,z,yaw,틱수 는 플레이어를 그 자리에 놓고 전진시킨 결과를 출력하고 종료한다.
    /// </summary>
    public partial class PlayTest : Node3D
    {
        private const int k_screenshotWaitFrames = 90;
        private const int k_selfTestTicks = 150;

        private PlayerController m_playerController;
        private OptionButton m_missionSelect;
        private Label m_info;

        // 목록 순번 → (어드온 여부, 페이지, 인덱스).
        private readonly List<(bool mif, int page, int index)> m_entries = new List<(bool, int, int)>();

        private bool m_mouseCaptured;
        private string m_screenshotPath;
        private int m_screenshotCountdown = -1;
        private bool m_autoWalk;

        public override void _Ready()
        {
            // 게임 설정(ConfigManager)이 적용한 전체화면·저해상도 렌더를 도구용 창 설정으로 되돌린다.
            Window root = GetTree().Root;
            root.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
            root.Mode = Window.ModeEnum.Windowed;
            root.Size = new Vector2I(1280, 720);
            root.MoveToCenter();
            Engine.MaxFps = 0;

            // PlayerController 가 먼저 돌아 조작·카메라를 확정한 뒤 이 노드가 정보를 표시한다.
            ProcessPriority = 200;

            m_playerController = new PlayerController { Name = "PlayerController" };
            AddChild(m_playerController);

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
                start = Mathf.Max(0, m_entries.FindIndex(entry => entry.mif == addon && entry.index == missionIndex));
            }
            m_missionSelect.Select(start);
            LoadEntry(start);
            SimClock.TickEnabled = true;

            int probeArg = Array.IndexOf(args, "--probe");
            if (probeArg >= 0 && probeArg + 1 < args.Length)
            {
                RunProbe(args[probeArg + 1]);
                return;
            }

            if (Array.IndexOf(args, "--third") >= 0)
            {
                m_playerController.ToggleViewMode();
            }
            m_autoWalk = Array.IndexOf(args, "--walk") >= 0;

            int screenshotArg = Array.IndexOf(args, "--screenshot");
            if (screenshotArg >= 0 && screenshotArg + 1 < args.Length)
            {
                m_screenshotPath = args[screenshotArg + 1];
                m_screenshotCountdown = k_screenshotWaitFrames;
                m_missionSelect.Visible = false;
            }
            else
            {
                SetMouseCaptured(true);
            }
        }

        public override void _Process(double delta)
        {
            UpdateInfo();

            if (m_screenshotCountdown >= 0)
            {
                if (m_autoWalk && MapLoader.Player != null)
                {
                    HumanController controller = MapLoader.Player.Controller;
                    var walk = new HumanInput { moveFlag = HumanMoveFlag.Forward, yaw = controller.Yaw, pitch = controller.Pitch };
                    controller.SetInput(in walk);
                }

                if (m_screenshotCountdown-- == 0)
                {
                    Error error = GetViewport().GetTexture().GetImage().SavePng(m_screenshotPath);
                    GD.Print($"스크린샷 {(error == Error.Ok ? "저장" : "실패")}: {m_screenshotPath}");
                    GetTree().Quit(error == Error.Ok ? 0 : 1);
                }
                return;
            }

            // Delete — 플레이어를 즉시 죽여 사망 동작과 사망 카메라를 확인한다.
            Human player = MapLoader.Player;
            if (player != null && InputManager.Instance.WasKeyPressed(Key.Delete))
            {
                player.ApplyDamage(player.HP);
            }

            if (InputManager.Instance.WasPressed(InputManager.Escape))
            {
                SetMouseCaptured(!m_mouseCaptured);
            }
        }

        public override void _ExitTree()
        {
            SimClock.TickEnabled = false;
        }

        private void SetMouseCaptured(bool captured)
        {
            m_mouseCaptured = captured;
            InputManager.Instance.MouseCursorMode(false, captured, false);
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
            m_missionSelect.ItemSelected += index =>
            {
                LoadEntry((int)index);
                SetMouseCaptured(true);
            };
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
        /// 목록의 미션 하나를 로드한다 (미션 정보 → 블록 → 스카이 → 포인트/사람).
        /// </summary>
        /// <param name="entryIndex">목록 순번.</param>
        /// <returns>블록과 포인트 로드까지 성공했으면 true.</returns>
        private bool LoadEntry(int entryIndex)
        {
            (bool mif, int page, int index) entry = m_entries[entryIndex];
            if (!MapLoader.LoadMissionData(entry.index, entry.mif, entry.page))
            {
                return false;
            }

            MapLoader loader = MapLoader.Instance;
            GameRandom.ReseedEntropy();
            bool blocks = MapLoader.LoadBlockData(loader.MissionBD1Path);
            MapLoader.LoadSkyData(loader.SkyIndex);
            bool points = MapLoader.LoadPointData(loader.MissionPD1Path);
            return blocks && points;
        }

        /// <summary>
        /// 미션과 플레이어 상태를 라벨에 표시한다.
        /// </summary>
        private void UpdateInfo()
        {
            MapLoader loader = MapLoader.Instance;
            Human player = MapLoader.Player;
            if (player == null)
            {
                m_info.Text = $"{loader.MissionFullname}  |  플레이어 없음";
                return;
            }

            HumanController controller = player.Controller;
            Vector3 position = controller.Position;
            Vector3 velocity = controller.MoveVelocity;
            float horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();

            m_info.Text =
                $"{loader.MissionFullname}  |  사람 {MapLoader.HumanCount}, 추가 충돌 {(loader.AdjustCollision ? "켜짐" : "꺼짐")}  |  {Engine.GetFramesPerSecond():0} fps\n" +
                $"플레이어 #{MapLoader.PlayerIndex} {player.HumanData?.name}  HP {player.HP:0}  상태 {player.DeadState}\n" +
                $"위치 ({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})  수평 속도 {horizontalSpeed:0.00} m/s  수직 {velocity.Y:0.00}  접지 {(controller.Grounded ? "예" : "아니오")}\n" +
                $"yaw {controller.Yaw:0} pitch {controller.Pitch:0}  시점 {m_playerController.ViewMode}\n" +
                "이동 키 | Space 점프 | Tab 걷기 | F1 시점 | F5+Enter 상승 | F8+←/→ 대상 교체 | Delete 사망 | Esc 마우스";
        }

        /// <summary>
        /// 플레이어를 지정 위치·방향에 놓고 전진 입력으로 틱을 돌린 뒤 위치 변화를 출력하고 종료한다.
        /// 특정 지점을 통과할 수 있는지 수치로 확인할 때 쓴다 (헤드리스 가능).
        /// </summary>
        /// <param name="spec">"x,y,z,yaw,틱수" 형식.</param>
        private void RunProbe(string spec)
        {
            float[] v = Array.ConvertAll(spec.Split(','), part => float.Parse(part, System.Globalization.CultureInfo.InvariantCulture));
            Human human = MapLoader.Player;
            if (v.Length != 5 || human == null)
            {
                GD.Print("probe: 인자가 잘못됐거나 플레이어가 없음");
                GetTree().Quit(1);
                return;
            }

            GameRandom.Reseed(1u);
            HumanController controller = human.Controller;
            var start = new Vector3(v[0], v[1], v[2]);
            controller.Teleport(start);

            int ticks = (int)v[4];
            for (int tick = 0; tick < ticks; tick++)
            {
                var input = new HumanInput { moveFlag = HumanMoveFlag.Forward, yaw = v[3], pitch = 0f };
                controller.SetInput(in input);
                SimClock.Step();
            }

            Vector3 end = controller.Position;
            Vector3 moved = end - start;
            GD.Print($"probe: 키 {controller.Height:0.00} m, {ticks}틱 전진 → ({end.X:0.00}, {end.Y:0.00}, {end.Z:0.00}), " +
                $"수평 이동 {new Vector2(moved.X, moved.Z).Length():0.00} m, 높이 변화 {moved.Y:0.00} m, HP {human.HP:0}");
            GetTree().Quit(0);
        }

        /// <summary>
        /// 모든 미션을 로드해 플레이어에게 전진 입력을 넣고 틱을 돌린 뒤, 사람이 맵 아래로 빠지거나 좌표가 깨지지 않았는지 확인하고 종료한다.
        /// </summary>
        private void RunSelfTest()
        {
            float deadlineY = DataManager.Instance.HumanParameterData.humanControllerData.deadlineY;
            int loaded = 0;
            int totalHumans = 0;
            int totalDead = 0;
            var failures = new List<string>();

            for (int i = 0; i < m_entries.Count; i++)
            {
                string label = m_missionSelect.GetItemText(i);
                if (!LoadEntry(i) || MapLoader.Player == null)
                {
                    failures.Add($"{label}: 로드 실패 또는 플레이어 없음");
                    continue;
                }

                loaded++;
                GameRandom.Reseed(1u);
                HumanController player = MapLoader.Player.Controller;
                Vector3 start = player.Position;

                for (int tick = 0; tick < k_selfTestTicks; tick++)
                {
                    var input = new HumanInput { moveFlag = HumanMoveFlag.Forward, yaw = player.Yaw, pitch = player.Pitch };
                    player.SetInput(in input);
                    SimClock.Step();
                }

                int fellOut = 0;
                int dead = 0;
                bool broken = false;
                foreach (Human human in MapLoader.Humans)
                {
                    Vector3 position = human.Controller.Position;
                    if (!position.IsFinite()) broken = true;
                    if (position.Y <= deadlineY + 0.001f) fellOut++;
                    // 플레이어는 계속 전진시키므로 지붕 등에서 떨어져 죽을 수 있다. 서 있기만 한 다른 사람이 죽는 것만 문제로 본다.
                    if (!human.Alive && human != MapLoader.Player) dead++;
                }

                totalHumans += MapLoader.HumanCount;
                totalDead += dead;
                if (broken) failures.Add($"{label}: 좌표가 깨진 사람이 있음");
                if (fellOut > 0) failures.Add($"{label}: 맵 아래로 빠진 사람 {fellOut}명 / {MapLoader.HumanCount}명");
                if (dead > fellOut) failures.Add($"{label}: 낙하 등으로 죽은 사람 {dead - fellOut}명");
                if (!MapLoader.Player.Alive) continue;
                if ((player.Position - start).Length() < 0.01f) failures.Add($"{label}: 플레이어가 전진 입력에도 움직이지 않음");
            }

            MapLoader.UnloadPointData();
            GD.Print($"미션 {m_entries.Count}개 중 {loaded}개 로드, 사람 합계 {totalHumans}명, 틱 {k_selfTestTicks}회 — 사망 {totalDead}명, 문제 {failures.Count}건");
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

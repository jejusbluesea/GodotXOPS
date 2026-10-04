using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 플레이 점검 씬. 미션의 맵과 사람을 로드하고 플레이어를 직접 조작해 이동·충돌·카메라·무기를 확인한다.
    /// 조작: 마우스 시점, 이동 키, Space 점프, Tab 걷기, 좌클릭 발사, R 재장전, 1/2 슬롯, Z/X 무기 종류 전환, Shift 스코프,
    /// F1 1인칭/3인칭, F3 총알 판정 원기둥 표시, F5+Enter 상승, F6+Enter 탄약 추가, F7+←/→ 무기 교체, F8+←/→ 조작 대상 교체,
    /// F9+↑/↓ 복제(따라오기/제자리), F2 AI 정지/재개, F4 전원 비전투 켜기/끄기, End 전원 경계, Insert 플레이어 무적 켜기/끄기,
    /// Home 디버그 텍스트 켜기/끄기, Delete 플레이어 사망, Esc 마우스 풀기/잡기.
    /// 명령행 인자("--" 뒤): --selftest 는 모든 미션에서 틱을 돌려 보고 종료, --screenshot 경로 는 화면을 PNG 로 저장하고 종료,
    /// --mission 번호 / --addon 은 시작 미션, --third 는 3인칭으로 시작, --walk 는 스크린샷 전까지 전진 입력을 넣는다,
    /// --fire 는 스크린샷 전까지 발사 입력을 넣는다, --weapon 번호 는 플레이어의 현재 무기를 바꾼다, --hitbox 는 판정 원기둥을 켠 채 시작한다,
    /// --look yaw,pitch 는 스크린샷 동안 시선을 고정한다, --pos x,y,z 는 플레이어를 그 자리로 옮긴다, --drop 은 시작할 때 현재 무기를 버린다,
    /// --probe x,y,z,yaw,틱수 는 플레이어를 그 자리에 놓고 전진시킨 결과를 출력하고 종료한다, --noai 는 AI 를 끈 채 시작한다,
    /// --invincible 은 플레이어 무적을 켠 채 시작한다, --notext 는 디버그 텍스트를 끈 채 시작한다.
    /// </summary>
    public partial class PlayTest : Node3D
    {
        private const int k_screenshotWaitFrames = 90;
        // 스크린샷 전에 최소한 이만큼의 시간(초)이 지나야 한다. 프레임이 매우 빠를 때 틱이 거의 돌지 않은 화면이 찍히는 것을 막는다.
        private const double k_screenshotWaitSeconds = 0.7;
        private const int k_selfTestTicks = 300;
        // 상태 표시에 올리는 사람 수 (플레이어에게 가까운 순).
        private const int k_aiInfoCount = 6;
        // 판정 원기둥을 그릴 때 둘레를 나누는 수.
        private const int k_hitboxSegments = 16;

        private PlayerController m_playerController;
        private OptionButton m_missionSelect;
        private Label m_info;

        // 목록 순번 → (어드온 여부, 페이지, 인덱스).
        private readonly List<(bool mif, int page, int index)> m_entries = new List<(bool, int, int)>();

        private bool m_mouseCaptured;
        private string m_screenshotPath;
        private int m_screenshotCountdown = -1;
        private double m_screenshotElapsed;
        private bool m_autoWalk;
        private bool m_autoFire;
        private bool m_fixedLook;
        private float m_lookYaw;
        private float m_lookPitch;

        private bool m_noFight;
        // 플레이어 무적. 조작 대상이 바뀌면(치트 F8) 새 대상으로 옮겨 간다.
        private bool m_invincible;
        private Human m_invinciblePlayer;
        private bool m_showText = true;
        private bool m_showHitbox;
        private MeshInstance3D m_hitboxMesh;
        private ImmediateMesh m_hitboxLines;

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
            AIController.Enabled = Array.IndexOf(args, "--noai") < 0;
            AIController.DrivePlayer = false;
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
            m_autoFire = Array.IndexOf(args, "--fire") >= 0;
            m_showHitbox = Array.IndexOf(args, "--hitbox") >= 0;
            m_invincible = Array.IndexOf(args, "--invincible") >= 0;
            m_showText = Array.IndexOf(args, "--notext") < 0;

            int weaponArg = Array.IndexOf(args, "--weapon");
            if (weaponArg >= 0 && weaponArg + 1 < args.Length && int.TryParse(args[weaponArg + 1], out int weaponIndex) && MapLoader.Player != null)
            {
                MapLoader.Player.SetWeapon(MapLoader.Player.SelectWeapon, weaponIndex);
            }

            int posArg = Array.IndexOf(args, "--pos");
            if (posArg >= 0 && posArg + 1 < args.Length && MapLoader.Player != null)
            {
                string[] parts = args[posArg + 1].Split(',');
                if (parts.Length == 3
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                {
                    MapLoader.Player.Controller.Teleport(new Vector3(x, y, z));
                }
            }

            if (Array.IndexOf(args, "--drop") >= 0 && MapLoader.Player != null)
            {
                MapLoader.Player.DropCurrentWeapon();
            }

            int lookArg = Array.IndexOf(args, "--look");
            if (lookArg >= 0 && lookArg + 1 < args.Length)
            {
                string[] parts = args[lookArg + 1].Split(',');
                if (parts.Length == 2
                    && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m_lookYaw)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m_lookPitch))
                {
                    m_fixedLook = true;
                }
            }

            int screenshotArg = Array.IndexOf(args, "--screenshot");
            if (screenshotArg >= 0 && screenshotArg + 1 < args.Length)
            {
                m_screenshotPath = args[screenshotArg + 1];
                m_screenshotCountdown = k_screenshotWaitFrames;
                m_missionSelect.Visible = false;
            }
            else if (!m_showText)
            {
                m_missionSelect.Visible = false;
                SetMouseCaptured(true);
            }
            else
            {
                SetMouseCaptured(true);
            }
        }

        public override void _Process(double delta)
        {
            ApplyInvincible();
            UpdateInfo();
            UpdateHitboxLines();

            if (m_screenshotCountdown >= 0)
            {
                if (MapLoader.Player != null)
                {
                    HumanController controller = MapLoader.Player.Controller;
                    var auto = new HumanInput
                    {
                        moveFlag = m_autoWalk ? HumanMoveFlag.Forward : HumanMoveFlag.None,
                        yaw = m_fixedLook ? m_lookYaw : controller.Yaw,
                        pitch = m_fixedLook ? m_lookPitch : controller.Pitch,
                    };
                    controller.SetInput(in auto);
                    if (m_autoFire) MapLoader.Player.QueueWeaponInput(HumanWeaponAction.Fire);
                }

                m_screenshotElapsed += delta;
                if (m_screenshotCountdown > 0) m_screenshotCountdown--;
                if (m_screenshotCountdown == 0 && m_screenshotElapsed >= k_screenshotWaitSeconds)
                {
                    m_screenshotCountdown = -1;
                    Error error = GetViewport().GetTexture().GetImage().SavePng(m_screenshotPath);
                    GD.Print($"스크린샷 {(error == Error.Ok ? "저장" : "실패")}: {m_screenshotPath}");
                    GetTree().Quit(error == Error.Ok ? 0 : 1);
                }
                return;
            }

            // Delete — 플레이어를 즉시 죽여 사망 동작과 사망 카메라를 확인한다. 무적이 켜져 있으면 끈다.
            Human player = MapLoader.Player;
            if (player != null && InputManager.Instance.WasKeyPressed(Key.Delete))
            {
                m_invincible = false;
                player.SetInvincible(false);
                player.ApplyDamage(player.HP);
            }

            if (InputManager.Instance.WasKeyPressed(Key.Insert))
            {
                m_invincible = !m_invincible;
            }

            // 디버그 텍스트만 끈다. 치트 키와 점검 기능은 그대로 동작한다.
            if (InputManager.Instance.WasKeyPressed(Key.Home))
            {
                m_showText = !m_showText;
                m_missionSelect.Visible = m_showText;
            }

            if (InputManager.Instance.WasKeyPressed(Key.F3))
            {
                m_showHitbox = !m_showHitbox;
            }

            // AI 디버그 치트 (원본은 콘솔 명령): F2 AI 정지/재개, F4 전원 비전투, End 전원 경계.
            if (InputManager.Instance.WasKeyPressed(Key.F2))
            {
                AIController.Enabled = !AIController.Enabled;
            }
            if (InputManager.Instance.WasKeyPressed(Key.F4))
            {
                m_noFight = !m_noFight;
                AIController.SetNoFightAll(m_noFight);
            }
            if (InputManager.Instance.WasKeyPressed(Key.End))
            {
                AIController.SetCautionAll();
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

        /// <summary>
        /// 무적 설정을 지금의 플레이어에게 맞춘다. 조작 대상이 바뀌었으면 옛 대상의 무적을 푼다.
        /// </summary>
        private void ApplyInvincible()
        {
            Human player = MapLoader.Player;
            if (m_invinciblePlayer != player && IsInstanceValid(m_invinciblePlayer))
            {
                m_invinciblePlayer.SetInvincible(false);
            }

            m_invinciblePlayer = player;
            player?.SetInvincible(m_invincible);
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

            // 총알 판정 원기둥을 선으로 그리는 메시. 깊이 테스트를 꺼서 벽과 몸에 가려지지 않게 한다.
            m_hitboxLines = new ImmediateMesh();
            m_hitboxMesh = new MeshInstance3D
            {
                Name = "HitboxLines",
                Mesh = m_hitboxLines,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                    NoDepthTest = true,
                },
            };
            AddChild(m_hitboxMesh);
        }

        /// <summary>
        /// 살아 있는 사람들의 머리(빨강)·상반신(노랑)·다리(초록) 판정 원기둥을 논리 위치에 선으로 그린다.
        /// </summary>
        private void UpdateHitboxLines()
        {
            m_hitboxLines.ClearSurfaces();
            if (!m_showHitbox || MapLoader.HumanCount == 0) return;

            bool any = false;
            foreach (Human human in MapLoader.Humans)
            {
                if (!human.Alive || human.HitboxSize == null) continue;
                // 1인칭에서는 자기 원기둥이 화면을 가리므로 그리지 않는다.
                if (human == MapLoader.Player && m_playerController.ViewMode == ViewMode.FirstPerson) continue;

                if (!any)
                {
                    m_hitboxLines.SurfaceBegin(Mesh.PrimitiveType.Lines);
                    any = true;
                }

                Vector3 position = human.Controller.Position;
                AddCylinderLines(human.HitboxSize.head, position, Colors.Red);
                AddCylinderLines(human.HitboxSize.body, position, Colors.Yellow);
                AddCylinderLines(human.HitboxSize.leg, position, Colors.Green);
            }
            if (any) m_hitboxLines.SurfaceEnd();
        }

        /// <summary>
        /// 원기둥 하나의 윗면·밑면 둘레와 세로선을 선 목록에 넣는다.
        /// </summary>
        /// <param name="part">부위 크기 데이터.</param>
        /// <param name="humanPosition">사람의 논리 위치.</param>
        /// <param name="color">선 색.</param>
        private void AddCylinderLines(HitboxPartSizeData part, Vector3 humanPosition, Color color)
        {
            if (part == null) return;

            Vector3 center = humanPosition + Coord.FromUnity(part.position);
            float bottom = center.Y - part.height * 0.5f;
            float top = bottom + part.height;

            for (int i = 0; i < k_hitboxSegments; i++)
            {
                float a0 = Mathf.Tau * i / k_hitboxSegments;
                float a1 = Mathf.Tau * (i + 1) / k_hitboxSegments;
                var p0 = new Vector3(center.X + Mathf.Cos(a0) * part.radius, 0f, center.Z + Mathf.Sin(a0) * part.radius);
                var p1 = new Vector3(center.X + Mathf.Cos(a1) * part.radius, 0f, center.Z + Mathf.Sin(a1) * part.radius);

                AddLine(new Vector3(p0.X, bottom, p0.Z), new Vector3(p1.X, bottom, p1.Z), color);
                AddLine(new Vector3(p0.X, top, p0.Z), new Vector3(p1.X, top, p1.Z), color);
                if (i % 4 == 0) AddLine(new Vector3(p0.X, bottom, p0.Z), new Vector3(p0.X, top, p0.Z), color);
            }
        }

        private void AddLine(Vector3 from, Vector3 to, Color color)
        {
            m_hitboxLines.SurfaceSetColor(color);
            m_hitboxLines.SurfaceAddVertex(from);
            m_hitboxLines.SurfaceSetColor(color);
            m_hitboxLines.SurfaceAddVertex(to);
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
            m_noFight = false;
            if (blocks && points) EventManager.Instance.BeginMission();
            return blocks && points;
        }

        /// <summary>
        /// 미션과 플레이어 상태를 라벨에 표시한다.
        /// </summary>
        private void UpdateInfo()
        {
            if (!m_showText)
            {
                m_info.Text = "Home 디버그 텍스트 켜기";
                return;
            }

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

            Weapon weapon = player.CurrentWeapon;
            string weaponState = player.IsReloading ? "재장전 중" : player.IsSwitchingWeapon ? "전환 중" : player.IsScoping ? "스코프" : "대기";
            int alive = 0;
            int normal = 0;
            int caution = 0;
            int action = 0;
            foreach (Human human in MapLoader.Humans)
            {
                if (!human.Alive) continue;

                alive++;
                if (human == player) continue;
                switch (human.Brain.Mode)
                {
                    case AIBattleMode.Action: action++; break;
                    case AIBattleMode.Caution: caution++; break;
                    default: normal++; break;
                }
            }

            EventManager events = EventManager.Instance;
            string result = events.Result == (int)MissionResult.Complete ? "클리어" : events.Result == (int)MissionResult.Failed ? "실패" : "진행 중";
            string message = events.MessageId >= 0 ? $"  메시지 #{events.MessageId}: {events.MessageText}" : string.Empty;

            m_info.Text =
                $"{loader.MissionFullname}  |  사람 {MapLoader.HumanCount} (생존 {alive}), 추가 충돌 {(loader.AdjustCollision ? "켜짐" : "꺼짐")}  |  {Engine.GetFramesPerSecond():0} fps\n" +
                $"플레이어 #{MapLoader.PlayerIndex} {player.HumanData?.name}  팀 {player.Team}  HP {player.HP:0}{(player.Invincible ? " (무적)" : string.Empty)}  상태 {player.DeadState}\n" +
                $"위치 ({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})  수평 속도 {horizontalSpeed:0.00} m/s  수직 {velocity.Y:0.00}  접지 {(controller.Grounded ? "예" : "아니오")}\n" +
                $"yaw {controller.Yaw:0.0} pitch {controller.Pitch:0.0}  시점 {m_playerController.ViewMode}\n" +
                $"무기 [슬롯 {player.SelectWeapon}] #{weapon.WeaponIndex} {weapon.Data.name}  탄약 {weapon.Magazine}/{weapon.Reserve}  {weaponState}  조준 오차 {player.CurrentErrorRange()}  날아가는 탄환 {BulletManager.Instance.CountActive()}\n" +
                $"떨어진 무기 {WeaponManager.Instance.CountActive()}  소물 {MapLoader.SmallObjects.Count}  이펙트 {EffectManager.Instance.CountActive()}  |  발사 {MapLoader.Stats.Fire} 명중 {MapLoader.Stats.OnTargetInt} 헤드샷 {MapLoader.Stats.Headshot} 킬 {MapLoader.Stats.Kill}  {MapLoader.Stats.PlayTime:0.0}초\n" +
                $"AI {(AIController.Enabled ? "켜짐" : "정지")}{(m_noFight ? " (비전투)" : string.Empty)}  평상시 {normal} 경계 {caution} 전투 {action}  |  미션 {result}{message}\n" +
                NearestAIInfo(player) +
                "이동 키 | Space 점프 | Tab 걷기 | 좌클릭 발사 | R 재장전 | 1/2 슬롯 | Z/X 종류 전환 | G 버리기 | Shift 스코프\n" +
                "F1 시점 | F3 판정 원기둥 | F5+Enter 상승 | F6+Enter 탄약 | F7+←/→ 무기 교체 | F8+←/→ 대상 교체 | F9+↑/↓ 복제 | Delete 사망 | Esc 마우스\n" +
                "F2 AI 정지/재개 | F4 전원 비전투 | End 전원 경계 | Insert 무적 | Home 디버그 텍스트 끄기";
        }

        /// <summary>
        /// 플레이어에게 가까운 사람 몇 명의 AI 상태를 한 줄씩 만든다: 번호, 팀, 상태, 이동 모드, 표적, 거리.
        /// </summary>
        /// <param name="player">플레이어.</param>
        /// <returns>표시할 문자열 (줄마다 줄바꿈으로 끝난다).</returns>
        private static string NearestAIInfo(Human player)
        {
            var nearest = new List<(float distance, int index)>();
            IReadOnlyList<Human> humans = MapLoader.Humans;
            for (int i = 0; i < humans.Count; i++)
            {
                if (humans[i] == player || !humans[i].Alive) continue;
                nearest.Add(((humans[i].Controller.Position - player.Controller.Position).Length(), i));
            }
            nearest.Sort((a, b) => a.distance.CompareTo(b.distance));

            var text = new System.Text.StringBuilder();
            for (int i = 0; i < nearest.Count && i < k_aiInfoCount; i++)
            {
                Human human = humans[nearest[i].index];
                AIBrain brain = human.Brain;
                string enemy = "-";
                for (int j = 0; j < humans.Count; j++)
                {
                    if (humans[j] == brain.Enemy) enemy = $"#{j}";
                }
                string range = brain.Mode == AIBattleMode.Action ? (brain.LongAttack ? " 원거리" : " 근거리") : string.Empty;
                text.Append($"  #{nearest[i].index} 팀 {human.Team} {brain.Mode}{range} 경로 {brain.Navi.Mode} 표적 {enemy} 거리 {nearest[i].distance:0.0} m HP {human.HP:0}\n");
            }
            return text.ToString();
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
            AIController.Enabled = false;
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
        /// 모든 미션을 로드해 AI 와 이벤트를 켠 채 플레이어에게 전진 입력을 넣고 틱을 돌린 뒤, 사람이 맵 아래로 빠지거나 좌표가 깨지지 않았는지 확인하고 종료한다.
        /// AI 가 서로 싸우므로 죽은 사람 수는 문제로 보지 않고 합계만 출력한다.
        /// </summary>
        private void RunSelfTest()
        {
            float deadlineY = DataManager.Instance.HumanParameterData.humanControllerData.deadlineY;
            int loaded = 0;
            int totalHumans = 0;
            int totalDead = 0;
            int totalAction = 0;
            int totalMoved = 0;
            int totalEnded = 0;
            int totalWeapons = 0;
            int totalObjects = 0;
            int embeddedObjects = 0;
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
                // 소리가 AI 에게 전달되려면 시뮬레이션이 켜져 있어야 한다. 틱은 아래에서 직접 돌린다.
                SimClock.TickEnabled = true;
                totalWeapons += WeaponManager.Instance.CountActive();
                totalObjects += MapLoader.SmallObjects.Count;
                foreach (SmallObject smallObject in MapLoader.SmallObjects)
                {
                    if (!smallObject.LogicPosition.IsFinite()) embeddedObjects++;
                }
                HumanController player = MapLoader.Player.Controller;
                Vector3 start = player.Position;

                for (int tick = 0; tick < k_selfTestTicks; tick++)
                {
                    var input = new HumanInput { moveFlag = HumanMoveFlag.Forward, yaw = player.Yaw, pitch = player.Pitch };
                    player.SetInput(in input);
                    SimClock.Step();
                }
                SimClock.TickEnabled = false;

                int fellOut = 0;
                int dead = 0;
                bool broken = false;
                foreach (Human human in MapLoader.Humans)
                {
                    Vector3 position = human.Controller.Position;
                    if (!position.IsFinite()) broken = true;
                    // 플레이어는 계속 전진시키므로 지붕 등에서 떨어질 수 있다. AI 가 움직이는 다른 사람이 맵 아래로 빠지는 것만 문제로 본다.
                    if (position.Y <= deadlineY + 0.001f && human != MapLoader.Player) fellOut++;
                    if (!human.Alive && human != MapLoader.Player) dead++;
                    if (human != MapLoader.Player && human.Alive)
                    {
                        if (human.Brain.Mode == AIBattleMode.Action) totalAction++;
                        if ((human.Controller.Position - human.HumanParam.position).Length() > 1f) totalMoved++;
                    }
                }
                if (EventManager.Instance.Result != (int)MissionResult.InProgress) totalEnded++;

                totalHumans += MapLoader.HumanCount;
                totalDead += dead;
                if (broken) failures.Add($"{label}: 좌표가 깨진 사람이 있음");
                if (fellOut > 0) failures.Add($"{label}: 맵 아래로 빠진 사람 {fellOut}명 / {MapLoader.HumanCount}명");
                if (!MapLoader.Player.Alive) continue;
                if ((player.Position - start).Length() < 0.01f) failures.Add($"{label}: 플레이어가 전진 입력에도 움직이지 않음");
            }

            MapLoader.UnloadPointData();
            if (embeddedObjects > 0) failures.Add($"좌표가 깨진 소물 {embeddedObjects}개");
            GD.Print($"미션 {m_entries.Count}개 중 {loaded}개 로드, 사람 합계 {totalHumans}명, 맵 배치 무기 {totalWeapons}개, 소물 {totalObjects}개, 틱 {k_selfTestTicks}회 — 사망 {totalDead}명, 전투 중 {totalAction}명, 1 m 넘게 움직인 생존자 {totalMoved}명, 끝난 미션 {totalEnded}개, 문제 {failures.Count}건");
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

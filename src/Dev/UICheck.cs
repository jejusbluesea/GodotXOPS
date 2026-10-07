using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 화면(GDScript)이 쓰는 창구 GameBridge 의 값과 화면 전환 흐름(메뉴 → 브리핑 → 메인게임 → 결과)을 수치로 확인하고 종료한다. 문제가 있으면 종료 코드 1.
    /// 화면 자체가 제대로 그려지는지는 보지 않는다. 그것은 "--ui-shot" 스크린샷으로 확인한다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/ui_check.tscn
    /// </summary>
    public partial class UICheck : Node
    {
        private readonly List<string> m_problems = new List<string>();
        private int m_checks;

        public override void _Ready()
        {
            GameBridge game = GameBridge.Instance;

            CheckMissionList(game);
            CheckBackgroundMaps(game);
            CheckMissionFlow(game);
            CheckPlayerValues(game);
            CheckConsole(game);
            CheckConsoleLoading(game);

            game.UnloadMission();
            GD.Print($"UI 창구 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 조건을 확인하고, 거짓이면 문제 목록에 넣는다.
        /// </summary>
        /// <param name="ok">확인할 조건.</param>
        /// <param name="what">무엇을 확인했는지.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// 미션 목록과 버전·크레딧: 데이터와 같은 수·이름이 나오고, 범위 밖은 빈 값이다.
        /// </summary>
        /// <param name="game">창구.</param>
        private void CheckMissionList(GameBridge game)
        {
            MissionData data = DataManager.Instance.MissionData;

            Expect(game.OfficialMissionCount() == data.officialMissions.Count && game.OfficialMissionCount() > 0, "공식 미션 수가 데이터와 다르거나 0");
            Expect(game.OfficialMissionName(0) == data.officialMissions[0].Name, "첫 공식 미션 이름이 데이터와 다름");
            Expect(game.OfficialMissionName(-1) == string.Empty && game.OfficialMissionName(9999) == string.Empty, "범위 밖 공식 미션 이름이 비어 있지 않음");

            Expect(game.AddonPageCount() == data.addonMissions.Count, "어드온 페이지 수가 데이터와 다름");
            Expect(game.AddonMissionCount(0) == data.addonMissions[0].Count, "어드온 미션 수가 데이터와 다름");
            Expect(game.AddonMissionCount(99) == 0 && game.AddonMissionName(99, 0) == string.Empty && game.AddonPageName(99) == string.Empty,
                "범위 밖 어드온 페이지가 빈 값이 아님");
            if (data.addonMissions[0].Count > 0)
            {
                Expect(game.AddonMissionName(0, 0) == data.addonMissions[0][0].Name, "첫 어드온 미션 이름이 데이터와 다름");
            }

            Expect(game.Version() == DataManager.Instance.GlobalData.Version && game.Version().Length > 0, "버전 문자열이 비었거나 데이터와 다름");
            Expect(game.CreditText().Contains(DataManager.Instance.GlobalData.productName), "크레딧 본문에 제품명이 없음");
            Expect(game.LoadTexture("data/char.dds") != null && game.LoadTexture("data/title.dds") != null, "글꼴이나 타이틀 텍스처를 읽지 못함");
            Expect(game.LoadTexture(string.Empty) == null, "빈 경로의 텍스처가 null 이 아님");
        }

        /// <summary>
        /// 오프닝과 메뉴 배경: 맵이 로드되고 시뮬레이션이 켜지며, 플레이어까지 AI 가 움직이고 이벤트는 돌지 않는다.
        /// </summary>
        /// <param name="game">창구.</param>
        private void CheckBackgroundMaps(GameBridge game)
        {
            Expect(game.LoadOpening() && MapLoader.HumanCount > 0 && MapLoader.Blocks.Count > 0, "오프닝 맵 로드 실패");
            Expect(SimClock.TickEnabled && AIController.DrivePlayer && !EventManager.Instance.Running, "오프닝에서 시뮬레이션·플레이어 AI·이벤트 상태가 맞지 않음");

            Expect(game.LoadDemo() && game.PlayerExists(), "메뉴 배경 맵 로드 실패");
            Expect(SimClock.TickEnabled && AIController.DrivePlayer && !EventManager.Instance.Running, "메뉴 배경에서 시뮬레이션·플레이어 AI·이벤트 상태가 맞지 않음");

            // 장면 카메라는 UnityXOPS 공간 값을 받아 Godot 공간에 놓는다 (Z 반전).
            Vector3 position = game.PlayerPosition();
            Expect(position.IsEqualApprox(Coord.FromUnity(MapLoader.Player.Controller.VisualPosition)), "플레이어 위치가 UnityXOPS 공간 값이 아님");
            game.SetSceneCamera(new Vector3(1f, 2f, 3f), new Vector3(0f, 90f, 0f), 65f);
            Camera3D camera = game.GetNode<Camera3D>("SceneCamera");
            Expect(camera.Position.IsEqualApprox(new Vector3(1f, 2f, -3f)) && Mathf.IsEqualApprox(camera.Fov, 65f), "장면 카메라 위치·시야각이 맞지 않음");
            Expect((-camera.Basis.Z).IsEqualApprox(Coord.YawForward(90f)), "장면 카메라가 yaw 90° 방향을 보지 않음");
        }

        /// <summary>
        /// 미션 흐름: 로드(멈춤) → 시작 → 재시작 → 맵만 내리기(미션 정보·통계 유지) → 전부 내리기.
        /// </summary>
        /// <param name="game">창구.</param>
        private void CheckMissionFlow(GameBridge game)
        {
            Expect(!game.LoadMission(9999, false, 0), "없는 미션이 로드됨");

            Expect(game.LoadMission(1, false, 0), "미션 1 로드 실패");
            Expect(!SimClock.TickEnabled && !EventManager.Instance.Running, "브리핑 단계인데 시뮬레이션이나 이벤트가 켜져 있음");
            Expect(game.MissionFullname().Length > 0 && game.MissionBriefing().Length > 0, "미션 이름이나 브리핑 본문이 비어 있음");
            Expect(game.MissionImage(0) != null, "브리핑 이미지가 없음");

            int startCount = EventManager.Instance.StartCount;
            game.BeginMission();
            Expect(SimClock.TickEnabled && EventManager.Instance.Running && !AIController.DrivePlayer, "미션 시작 뒤 시뮬레이션·이벤트·플레이어 AI 상태가 맞지 않음");
            Expect(EventManager.Instance.StartCount == startCount + 1 && EventManager.Instance.Result == (int)MissionResult.InProgress, "미션 시작 횟수나 결과가 맞지 않음");

            for (int i = 0; i < 40; i++) SimClock.Step();
            Godot.Collections.Dictionary stats = game.GetStats();
            Expect(stats.ContainsKey("playTime") && stats.ContainsKey("fire") && stats.ContainsKey("onTarget")
                && stats.ContainsKey("accuracy") && stats.ContainsKey("kill") && stats.ContainsKey("headshot"), "통계 사전에 빠진 값이 있음");
            Expect((float)stats["playTime"] > 1f, $"플레이 시간이 흐르지 않음 ({stats["playTime"]})");

            Human before = MapLoader.Player;
            Expect(game.RestartMission() && MapLoader.Player != before && MapLoader.Stats.PlayTicks == 0, "재시작 뒤 사람이나 통계가 새로 만들어지지 않음");
            Expect(EventManager.Instance.StartCount == startCount + 2 && EventManager.Instance.Running, "재시작 뒤 이벤트가 처음부터 다시 돌지 않음");

            string fullname = game.MissionFullname();
            game.UnloadMap();
            Expect(!SimClock.TickEnabled && MapLoader.HumanCount == 0 && MapLoader.Blocks.Count == 0, "맵을 내렸는데 사람이나 블록이 남아 있음");
            Expect(game.MissionFullname() == fullname && !game.PlayerExists(), "맵만 내렸는데 미션 정보가 사라졌거나 플레이어가 남아 있음");

            Expect(game.ReloadMission() && MapLoader.HumanCount > 0 && !SimClock.TickEnabled, "미션 다시 로드 실패");
            game.UnloadMission();
            Expect(game.MissionFullname().Length == 0, "전부 내렸는데 미션 정보가 남아 있음");
        }

        /// <summary>
        /// 플레이어 값: HUD 가 읽는 값이 사람의 상태와 같고, 플레이어가 없을 때는 기본값이다.
        /// </summary>
        /// <param name="game">창구.</param>
        private void CheckPlayerValues(GameBridge game)
        {
            Expect(!game.PlayerExists() && game.PlayerHP() == 0f && game.WeaponName() == string.Empty && game.Magazine() == 0
                && game.PlayerIndex() == -1 && !game.ConsumeHit() && game.ActiveScope().Count == 0 && game.GetWallBlind() == 0,
                "플레이어가 없는데 기본값이 아닌 값이 있음");

            if (!game.LoadMission(2, false, 0))
            {
                Expect(false, "미션 2 로드 실패");
                return;
            }
            game.BeginMission();
            AIController.Enabled = false;

            Human player = MapLoader.Player;
            Weapon weapon = player.CurrentWeapon;
            Expect(game.PlayerExists() && game.PlayerAlive() && game.PlayerIndex() == MapLoader.PlayerIndex, "플레이어 존재·생존·순번이 맞지 않음");
            Expect(game.PlayerHP() == player.HP && game.Magazine() == weapon.Magazine && game.Reserve() == weapon.Reserve && game.WeaponName() == weapon.Data.name,
                "체력·탄약·무기 이름이 사람의 상태와 다름");
            Expect(game.ShowsCrosshair() == weapon.Data.crosshair && game.ErrorRange() == player.GunsightErrorRange, "조준선 표시 여부나 조준 오차가 다름");
            Expect(!game.IsFirstPerson(), "조작하는 컨트롤러가 없는데 1인칭으로 나옴");

            player.SetHitYaw(0f);
            Expect(game.ConsumeHit() && !game.ConsumeHit(), "피격 확인이 한 번만 참이 아님");

            player.ReloadWeapon();
            Expect(game.IsReloading() == player.IsReloading, "재장전 상태가 다름");

            // 스코프가 있는 무기를 쥐여 주고 켠다.
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            int scoped = parameter.weaponData.FindIndex(data => data.scope && parameter.scopeData.Has(data.scopeIndex));
            if (scoped >= 0)
            {
                player.SetWeapon(player.SelectWeapon, scoped);
                for (int i = 0; i < 400 && player.IsChanging; i++) player.TickWeapon();
                player.ToggleScope();
                Godot.Collections.Dictionary scope = game.ActiveScope();
                Expect(game.IsScoping() && game.ScopeIndex() == parameter.weaponData[scoped].scopeIndex, "스코프 상태나 번호가 맞지 않음");
                Expect(scope.ContainsKey("aspect") && scope.ContainsKey("texturePath") && scope.ContainsKey("hideCrosshair") && scope.ContainsKey("lines"),
                    "스코프 사전에 빠진 값이 있음");
                player.ToggleScope();
                Expect(!game.IsScoping() && game.ActiveScope().Count == 0, "스코프를 껐는데 표시 정보가 남아 있음");
            }

            // 3D 무기 표시: 텍스처가 만들어지고, 없앤 뒤에는 설정 호출이 무시된다.
            Texture2D texture = game.CreateWeaponView(256);
            Expect(texture != null && game.CreateWeaponView(256) != null, "무기 표시 텍스처가 만들어지지 않음");
            game.SetWeaponViewMain(new Vector3(0f, -4.3f, 0f), 0.8f, 30f);
            game.SetWeaponViewSub(new Vector3(2.5f, -5f, 0f), 0.4f, 90f);
            game.SetWeaponViewCamera(new Vector3(0f, 0f, -10f), Vector3.Zero, 65f);
            game.FreeWeaponView();
            game.SetWeaponViewMain(Vector3.Zero, 1f, 0f);
            Expect(game.GetNodeOrNull("WeaponView") == null, "무기 표시를 없앴는데 노드가 남아 있음");

            // 죽으면 다음 틱에 사망 상태가 되고 조준선을 그리지 않는다.
            player.ApplyDamage(player.HP);
            SimClock.Step();
            Expect(game.PlayerHP() == 0f && !game.PlayerAlive() && !game.ShowsCrosshair(), "죽은 플레이어의 체력·생존·조준선 값이 맞지 않음");

            AIController.Enabled = true;
        }

        /// <summary>
        /// 비행 모드: 입력이 없으면 공중에 그대로 떠 있고, 전진하면 시선 방향(위아래 포함)으로 블록을 뚫고 나아가며, 점프는 무시되고, 사람에게 밀리지 않고, 끄면 다시 떨어진다.
        /// </summary>
        /// <param name="game">창구.</param>
        /// <param name="player">플레이어.</param>
        /// <param name="other">겹쳐 세워 볼 다른 사람.</param>
        private void CheckFlight(GameBridge game, Human player, Human other)
        {
            bool aiWasEnabled = AIController.Enabled;
            AIController.Enabled = false;
            HumanController controller = player.Controller;

            // 블록이 없는 높은 공중에서 시작한다.
            var start = new Vector3(0f, 300f, 0f);
            controller.Teleport(start);
            Expect(game.ConsoleExecute("flight") == "Flight on" && controller.Flight, "flight 로 비행 모드가 켜지지 않음");

            for (int i = 0; i < 10; i++) SimClock.Step();
            Expect(controller.Position == start, $"비행 중 입력이 없는데 움직임 ({controller.Position})");
            // 날고 있는 동안은 공중에 뜬 상태가 아니다: 공중 조준 오차 가산이 붙지 않는다.
            int airbornePenalty = DataManager.Instance.WeaponParameterData.weaponAccuracyData.airborneAccuracyPenalty;
            Expect(controller.Grounded && (airbornePenalty <= 0 || player.GunsightErrorRange < airbornePenalty), $"비행 중인데 공중 상태로 판정됨 (조준 오차 {player.GunsightErrorRange})");

            // 30° 내려다보며 전진 + 점프: 시선 방향으로만 움직인다.
            const float yaw = 40f;
            const float pitch = 30f;
            for (int i = 0; i < 10; i++)
            {
                var input = new HumanInput { moveFlag = HumanMoveFlag.Forward | HumanMoveFlag.Jump, yaw = yaw, pitch = pitch };
                controller.SetInput(in input);
                SimClock.Step();
            }
            Vector3 moved = controller.Position - start;
            Expect(moved.Length() > 0.5f && moved.Normalized().Dot(Coord.AimDirection(yaw, pitch)) > 0.999f,
                $"비행 중 전진이 시선 방향이 아님 (이동 {moved})");
            Expect(player.HP == player.HumanData.hp, "비행 중 데미지를 입음");

            // 다른 사람과 겹쳐 있어도 밀리지 않는다.
            Vector3 overlap = controller.Position;
            other.Controller.Teleport(overlap);
            for (int i = 0; i < 5; i++) SimClock.Step();
            Expect(controller.Position == overlap, "비행 중인데 사람에게 밀림");

            // 블록 안으로 들어가도 밀려나지 않는다. 맵의 블록 하나의 한가운데로 옮겨 놓고 틱을 돌린다.
            if (MapLoader.GetBlockColliders(BlockLayer.Human).Count > 0)
            {
                Block block = MapLoader.GetBlockColliders(BlockLayer.Human)[0];
                Vector3 inside = (block.boundsMin + block.boundsMax) * 0.5f;
                controller.Teleport(inside);
                for (int i = 0; i < 5; i++) SimClock.Step();
                Expect(controller.Position == inside, "비행 중인데 블록에 밀려남");
            }

            // 끄면 평소 이동으로 돌아와 떨어진다.
            controller.Teleport(start);
            Expect(game.ConsoleExecute("flight") == "Flight off" && !controller.Flight, "flight 를 다시 쳐도 꺼지지 않음");
            Expect(!controller.Grounded, "공중에서 비행을 껐는데 바로 공중 상태가 되지 않음");
            bool airborneWhileFalling = true;
            for (int i = 0; i < 10; i++)
            {
                SimClock.Step();
                if (controller.Grounded) airborneWhileFalling = false;
            }
            Expect(controller.Position.Y < start.Y - 0.1f, "비행을 껐는데 떨어지지 않음");
            Expect(airborneWhileFalling && (airbornePenalty <= 0 || player.GunsightErrorRange >= airbornePenalty), $"비행을 끄고 떨어지는 동안 공중 상태가 아님 (조준 오차 {player.GunsightErrorRange})");

            // 이어지는 점검을 위해 플레이어를 살려 두고 원래 자리 근처로 되돌린다.
            controller.Teleport(other.Controller.Position + new Vector3(50f, 0f, 0f));
            other.Controller.Teleport(other.Controller.Position + new Vector3(-50f, 0f, 0f));
            player.RestoreHP();
            AIController.Enabled = aiWasEnabled;
        }

        /// <summary>
        /// 디버그 콘솔: 명령이 게임 상태를 바꾸고, 틀린 입력은 상태를 바꾸지 않고, 화면에 맡기는 일이 한 번만 나오고, 입력 차단이 조회를 막는다.
        /// </summary>
        /// <param name="game">창구.</param>
        /// <summary>
        /// 콘솔의 맵 로드 명령과 로그 전달, 배경 맵 다시 올리기. 로드된 맵을 바꾸므로 다른 점검이 끝난 뒤에 돈다.
        /// </summary>
        /// <param name="game">창구.</param>
        private void CheckConsoleLoading(GameBridge game)
        {
            DemoData demo = DataManager.Instance.MissionData.openingData;

            // 파일이 없으면 지금 화면을 그대로 둔다 (화면에 맡기는 일이 없다).
            Expect(game.ConsoleExecute("loadmap").StartsWith("Usage:"), "인자 없는 loadmap 이 사용법을 보여 주지 않음");
            Expect(game.ConsoleExecute("loadmap no/such.bd1 no/such.pd1") == "Block data open failed: no/such.bd1" && game.ConsoleTakeAction() == string.Empty,
                "없는 블록 파일의 loadmap 결과가 다름");
            Expect(game.ConsoleExecute($"loadmap {demo.bd1Path} no/such.pd1") == "Point data open failed: no/such.pd1" && game.ConsoleTakeAction() == string.Empty,
                "없는 포인트 파일의 loadmap 결과가 다름");
            Expect(game.ConsoleExecute($"loadmap {demo.bd1Path} {demo.pd1Path} x").Contains("must be a number"), "숫자가 아닌 하늘 번호를 받음");
            Expect(game.ConsoleExecute("loadmissionmif no/such.mif") == "Mission file open failed: no/such.mif", "없는 미션 파일의 loadmissionmif 결과가 다름");
            Expect(game.ConsoleExecute("loadmissionmif no/such.mif maybe").Contains("true or false"), "참·거짓이 아닌 skipbriefing 을 받음");
            Expect(game.ConsoleExecute("loadmission").Contains("index is required") && game.ConsoleExecute("loadmission 9999").Contains("index is required")
                && game.ConsoleExecute("loadmission x").Contains("index is required") && game.ConsoleTakeAction() == string.Empty,
                "인덱스가 없거나 범위 밖인 loadmission 결과가 다름");
            Expect(game.ConsoleExecute("loadmission 0 maybe").Contains("true or false"), "참·거짓이 아닌 skipbriefing 을 받음 (loadmission)");

            // 공식 미션을 인덱스로 로드한다. 브리핑을 건너뛰면 메인게임, 아니면 브리핑으로 넘긴다.
            string official = DataManager.Instance.MissionData.officialMissions[1].name;
            Expect(game.ConsoleExecute("loadmission 1") == $"Mission loaded: 1 {official}" && game.ConsoleTakeAction() == DebugConsole.UiActionScenePrefix + "maingame"
                && MapLoader.Instance.MissionName == official && MapLoader.HumanCount > 0, "loadmission 이 공식 미션을 로드해 메인게임으로 넘기지 않음");
            Expect(game.ConsoleExecute("loadmission 1 false").StartsWith("Mission loaded:") && game.ConsoleTakeAction() == DebugConsole.UiActionScenePrefix + "briefing",
                "skipbriefing false 가 브리핑으로 넘기지 않음");

            // 큰따옴표로 묶은 경로는 인자 하나다. 경로의 대소문자는 그대로 전달된다.
            Expect(game.ConsoleExecute("loadmap \"No Such/Map File.bd1\" x.pd1") == "Block data open failed: No Such/Map File.bd1", "따옴표로 묶은 경로가 인자 하나로 전달되지 않음");

            // 파일 둘을 직접 로드한다. 미션 정보는 파일 이름과 하늘 번호만 있다.
            string result = game.ConsoleExecute($"loadmap {demo.bd1Path} {demo.pd1Path} 3");
            Expect(result.StartsWith("Map loaded:") && game.ConsoleTakeAction() == DebugConsole.UiActionScenePrefix + "maingame", "loadmap 이 메인게임으로 넘기지 않음");
            Expect(MapLoader.Blocks.Count > 0 && MapLoader.HumanCount > 0 && MapLoader.Instance.SkyIndex == 3 && !SimClock.TickEnabled
                && MapLoader.Instance.MissionName == System.IO.Path.GetFileName(demo.bd1Path) && game.LastLoadError() == string.Empty,
                "loadmap 으로 로드한 맵의 상태가 다름");
            game.BeginMission();
            Expect(game.RestartMission() && MapLoader.HumanCount > 0 && MapLoader.Instance.SkyIndex == 3, "loadmap 으로 로드한 맵을 다시 시작하지 못함");

            // 미션 로드가 실패하면 이유가 남고, 그 이유는 콘솔로 가는 로그에도 있다.
            game.ConsoleTakeLogs();
            Expect(!game.LoadMission(9999, false, 0) && game.LastLoadError() == "Mission load failed", "범위 밖 미션의 실패 이유가 다름");
            Expect(!game.LoadMapFiles(GamePath.Resolve("no/such.bd1"), GamePath.Resolve("no/such.pd1"), 0)
                && game.LastLoadError().Replace('\\', '/') == "Block data open failed: no/such.bd1", "없는 블록 파일의 실패 이유가 다름");
            string[] logs = game.ConsoleTakeLogs();
            Expect(logs.Length > 0 && logs[0].StartsWith("2[MapLoader] Block data open failed") && game.ConsoleTakeLogs().Length == 0,
                "에러 로그가 콘솔로 한 번만 전달되지 않음");
            Debugger.LogWarning("check warning", "UICheck");
            logs = game.ConsoleTakeLogs();
            Expect(logs.Length == 1 && logs[0] == "1[UICheck] check warning", "경고 로그의 수준 표시가 다름");

            // 배경 맵을 다시 올린다 (오프닝과 메뉴에서의 restart).
            Expect(game.LoadOpening() && MapLoader.HumanCount > 0, "오프닝 맵 로드 실패");
            int humans = MapLoader.HumanCount;
            SimClock.Step();
            Expect(game.ReloadBackground() && MapLoader.HumanCount == humans && SimClock.TickEnabled && AIController.DrivePlayer, "배경 맵을 다시 올리지 못함");
            game.UnloadMission();
        }

        private void CheckConsole(GameBridge game)
        {
            if (!game.LoadMission(2, false, 0))
            {
                Expect(false, "콘솔 점검용 미션 2 로드 실패");
                return;
            }
            game.BeginMission();

            Human player = MapLoader.Player;
            int other = MapLoader.PlayerIndex == 0 ? 1 : 0;
            Human target = MapLoader.GetHuman(other);

            string help = game.ConsoleExecute("help");
            Expect(help.Contains("nodamage") && help.Contains("teleport") && help.Contains("ss"), "help 에 명령 이름이 나오지 않음");
            Expect(game.ConsoleExecute("help kill").Contains("kill <id>"), "help 명령 이 사용법을 보여 주지 않음");
            Expect(game.ConsoleExecute("ver").Contains(game.Version()), "ver 에 버전이 없음");
            Expect(game.ConsoleExecute("  ") == string.Empty && game.ConsoleExecute("nosuchcommand").Contains("Unknown command"), "빈 줄이나 없는 명령의 처리가 다름");

            game.ConsoleExecute("NoDamage");
            Expect(player.Invincible, "nodamage 로 플레이어가 무적이 되지 않음 (대소문자 무시 포함)");
            game.ConsoleExecute("nodamage");
            game.ConsoleExecute($"nodamage {other}");
            Expect(!player.Invincible && target.Invincible, "nodamage 번호 가 그 사람에게 적용되지 않음");
            game.ConsoleExecute($"kill {other}");
            Expect(target.HP > 0f, "무적인 사람이 kill 로 죽음");
            game.ConsoleExecute($"nodamage {other}");

            player.ApplyDamage(10f);
            game.ConsoleExecute("treat");
            Expect(player.HP == player.HumanData.hp, "treat 로 HP 가 돌아오지 않음");

            game.ConsoleExecute($"teleport {other}");
            Expect(player.Controller.Position == target.Controller.Position, "teleport 로 플레이어가 옮겨지지 않음");

            int none = DataManager.Instance.WeaponParameterData.weaponGeneralData.noneWeaponIndex;
            int weaponIndex = none == 1 ? 2 : 1;
            game.ConsoleExecute($"weapon {weaponIndex}");
            Expect(player.CurrentWeapon.WeaponIndex == weaponIndex, "weapon 으로 무기가 바뀌지 않음");
            game.ConsoleExecute("weapon 99999");
            game.ConsoleExecute("weapon abc");
            Expect(player.CurrentWeapon.WeaponIndex == weaponIndex, "틀린 번호로 무기가 바뀜");

            // 탄 수를 주면 장탄수만큼 장전하고 나머지가 예비 탄이다. 주지 않으면 사람 종류의 초기 탄약 배수다.
            int magazineSize = DataManager.Instance.WeaponParameterData.weaponData[weaponIndex].magazineSize;
            int multiplier = player.HumanTypeData != null ? player.HumanTypeData.autoBulletMultiplier : Weapon.DefaultAutoBulletMultiplier;
            Expect(player.CurrentWeapon.Magazine == magazineSize && player.CurrentWeapon.Reserve == magazineSize * Mathf.Max(0, multiplier - 1),
                $"weapon 번호 의 기본 탄약이 초기 탄약 배수와 다름 ({player.CurrentWeapon.Magazine}/{player.CurrentWeapon.Reserve})");
            game.ConsoleExecute($"weapon {weaponIndex} {magazineSize + 7}");
            Expect(player.CurrentWeapon.Magazine == magazineSize && player.CurrentWeapon.Reserve == 7, "weapon 번호 탄수 가 장전 탄과 예비 탄으로 나뉘지 않음");
            game.ConsoleExecute($"weapon {weaponIndex} 1");
            Expect(player.CurrentWeapon.Magazine == 1 && player.CurrentWeapon.Reserve == 0, "장탄수보다 적은 탄 수가 그대로 장전되지 않음");
            game.ConsoleExecute($"weapon {weaponIndex} -5");
            Expect(player.CurrentWeapon.Magazine == 1, "틀린 탄 수로 무기가 바뀜");

            // 좌표로 옮기기: info 와 같은 좌표다. 인자 수가 맞지 않거나 수가 아니면 옮기지 않는다.
            game.ConsoleExecute("teleport 12.5 -3 40.25");
            Expect(player.Controller.Position == new Vector3(12.5f, -3f, 40.25f), $"teleport x y z 로 옮겨지지 않음 ({player.Controller.Position})");
            game.ConsoleExecute("teleport 1 2");
            game.ConsoleExecute("teleport a b c");
            game.ConsoleExecute("teleport 1 2 nan");
            Expect(player.Controller.Position == new Vector3(12.5f, -3f, 40.25f), "틀린 좌표로 플레이어가 옮겨짐");

            CheckFlight(game, player, target);

            // 판정 표시: 종류별로 켜고 끄고, 켠 채로 한 프레임을 그려도 문제가 없다.
            ColliderView view = game.ColliderView;
            game.ConsoleExecute("collider human");
            game.ConsoleExecute("collider weapon");
            game.ConsoleExecute("collider object");
            Expect(view.ShowHuman && view.ShowWeapon && view.ShowObject, "collider 로 표시가 켜지지 않음");
            player.DropCurrentWeapon();
            view._Process(0.0);
            Expect(view.Mesh.GetSurfaceCount() == 1, "판정 표시를 켰는데 그려진 선이 없음");
            game.ConsoleExecute("collider human");
            Expect(!view.ShowHuman && view.ShowWeapon && game.ConsoleExecute("collider box").Contains("Usage"), "collider 를 다시 쳐도 꺼지지 않거나 틀린 종류의 안내가 없음");
            game.ConsoleExecute("collider weapon");
            game.ConsoleExecute("collider object");
            view._Process(0.0);
            Expect(view.Mesh.GetSurfaceCount() == 0, "판정 표시를 전부 껐는데 선이 남아 있음");

            game.ConsoleExecute($"kill {other}");
            Expect(target.HP == 0f, "kill 로 HP 가 0 이 되지 않음");
            Expect(game.ConsoleExecute("kill 99999").Contains("No such human") && game.ConsoleExecute("kill").Contains("id is required"), "틀린 사람 번호의 안내가 없음");

            game.ConsoleExecute("stop");
            Expect(!AIController.Enabled, "stop 으로 AI 가 멈추지 않음");
            game.ConsoleExecute("stop");
            game.ConsoleExecute("bot");
            Expect(AIController.Enabled && AIController.DrivePlayer, "stop 을 다시 쳐도 AI 가 재개되지 않거나 bot 이 켜지지 않음");
            game.ConsoleExecute("bot");
            game.ConsoleExecute("nofight");
            Expect(MapLoader.GetHuman(MapLoader.HumanCount - 1).Brain.NoFight, "nofight 가 적용되지 않음");
            game.ConsoleExecute("nofight");
            Expect(!MapLoader.GetHuman(MapLoader.HumanCount - 1).Brain.NoFight, "nofight 를 다시 쳐도 풀리지 않음");

            game.ConsoleExecute("estop");
            Expect(EventManager.Instance.LinesPaused && game.ConsoleExecute("event").Contains("stopped"), "estop 으로 이벤트가 멈추지 않음");
            game.ConsoleExecute("info");
            Expect(game.ConsoleInfoVisible() && game.ConsoleInfoText().Contains(MapLoader.Instance.MissionFullname), "info 로 디버그 텍스트가 켜지지 않음");
            game.ConsoleExecute("info");
            Expect(game.ConsoleExecute("human").Contains($"Humans {MapLoader.HumanCount},") && game.ConsoleExecute("result").Contains("Shots"), "human 이나 result 의 내용이 없음");

            game.ConsoleExecute("fog");
            game.ConsoleExecute("sky 1");
            Expect(game.ConsoleExecute("sky 99999").Contains("sky index"), "틀린 하늘 번호의 안내가 없음");

            // 콘솔의 글자는 영어만 쓴다. 명령마다 설명을 보고, 몇 가지는 결과와 틀린 입력의 안내까지 본다.
            bool ascii = true;
            string printed = game.ConsoleInfoText() + game.ConsoleExecute("nosuch") + game.ConsoleExecute("kill") + game.ConsoleExecute("weapon") + game.ConsoleExecute("sky");
            foreach (string name in game.ConsoleExecute("help").Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                printed += game.ConsoleExecute($"help {name}");
            }
            foreach (string name in new[] { "ver", "human", "result", "event" })
            {
                printed += game.ConsoleExecute(name);
            }
            foreach (char character in printed)
            {
                if (character > 126) ascii = false;
            }
            Expect(ascii, "콘솔 출력에 영어가 아닌 글자가 있음");

            // 화면에 맡기는 일은 한 번만 꺼내진다.
            game.ConsoleExecute("clear");
            Expect(game.ConsoleTakeAction() == DebugConsole.UiActionClear && game.ConsoleTakeAction() == string.Empty, "clear 가 화면에 한 번만 전달되지 않음");
            game.ConsoleExecute("ss");
            Expect(game.ConsoleTakeAction() == DebugConsole.UiActionScreenshot, "ss 가 화면에 전달되지 않음");
            game.ConsoleExecute("screenshot");
            Expect(game.ConsoleTakeAction() == DebugConsole.UiActionScreenshot, "screenshot 이 화면에 전달되지 않음");
            game.ConsoleExecute("RESTART");
            Expect(game.ConsoleTakeAction() == DebugConsole.UiActionRestart, "restart 가 화면에 전달되지 않음 (명령 이름은 대소문자를 가리지 않는다)");
            Expect(game.ConsoleExecute("exit").Contains("Unknown command") && game.ConsoleExecute("f12").Contains("Unknown command"), "없앤 명령(exit, f12)이 남아 있음");

            Expect(game.ConsoleExecute("comp") == "Mission complete" && EventManager.Instance.Result == (int)MissionResult.Complete, "comp 로 미션이 끝나지 않음");
            Expect(game.ConsoleExecute("fail").Contains("No mission") && EventManager.Instance.Result == (int)MissionResult.Complete, "끝난 미션이 fail 로 다시 바뀜");

            // 다시 시작하면 이벤트 멈춤이 풀린다.
            game.RestartMission();
            Expect(!EventManager.Instance.LinesPaused && EventManager.Instance.Result == (int)MissionResult.InProgress, "재시작 뒤 이벤트 멈춤이나 결과가 남아 있음");

            // 입력 차단: 켜면 조회가 전부 "안 눌림"이다.
            InputManager input = InputManager.Instance;
            input.InputBlocked = true;
            Expect(!input.IsPressed(InputManager.Fire) && !input.WasKeyPressed(Key.Escape) && !input.IsClickPressed()
                && input.ReadVector(InputManager.Move) == Vector2.Zero, "입력을 막았는데 눌린 것으로 나옴");
            input.InputBlocked = false;
            Expect(!input.InputBlocked, "입력 차단이 풀리지 않음");
        }
    }
}

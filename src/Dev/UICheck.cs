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
            int scoped = parameter.weaponData.FindIndex(data => data.scope && data.scopeIndex >= 0 && data.scopeIndex < parameter.scopeData.Count);
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
    }
}

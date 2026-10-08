using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 스크립트 이벤트(SafeGDScript)를 수치로 확인한다.
    /// 점검용 묶음(등록 JSON 과 .sgd)과 PD2, MIF2 를 게임 폴더의 build/event_check/ 에 만들어 로드하고 틱을 직접 돌린다:
    /// 파라미터 전달, 출구와 분기, 줄의 저장 칸, 미션 변수, API 함수, 줄 제어, 자동 판정 끄기, 실패한 줄만 멈추는지, 로드 때 거절되는 경우.
    /// 이어서 기본 제공 묶음(godotdata/event/base.json)의 이벤트 전부를 한 미션에서 돌려 본다 (화면 글자, Interact, 카운트다운 포함).
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/event_check.tscn
    /// </summary>
    public partial class EventCheck : Node
    {
        private const string k_workFolder = "build/event_check";
        private const int k_enemyId = 500;
        private const int k_objectId = 7;
        private const int k_pathId = 50;

        // 점검용 묶음의 종류 번호 (미션 전용 구간).
        private const int k_setVar = 10000;
        private const int k_waitVar = 10001;
        private const int k_branch = 10002;
        private const int k_countTicks = 10003;
        private const int k_probe = 10004;
        private const int k_act = 10005;
        private const int k_badReturn = 10006;
        private const int k_noReturn = 10007;
        private const int k_spin = 10008;
        private const int k_escape = 10009;
        private const int k_flood = 10010;
        private const int k_badExit = 10011;
        private const int k_redirect = 10012;
        private const int k_killAll = 10013;
        private const int k_finish = 10014;
        private const int k_bump = 10015;
        private const int k_many = 10016;

        private const string k_script = @"var api
var bumps = 0

func init(a):
	api = a

func set_var(p, state):
	state[""n""] = 100
	api[""set_var""].call(p[""var""], p[""value""])
	return 0

func wait_var(p, state):
	if api[""get_var""].call(p[""var""]) >= p[""value""]:
		return 0
	return -1

func branch(p, state):
	if api[""get_var""].call(p[""var""]) != 0:
		return 0
	return 1

func count_ticks(p, state):
	var n = state.get(""n"", 0) + 1
	state[""n""] = n
	if n >= p[""ticks""]:
		return 0
	return -1

func probe(p, state):
	api[""set_var""].call(40, api[""human_count""].call())
	api[""set_var""].call(41, api[""find_human""].call(500))
	api[""set_var""].call(42, api[""team_alive""].call(1))
	var h = api[""human""].call(api[""player""].call())
	if h[""alive""] and h[""team""] == 0 and h[""id""] == 0 and h[""hp""] > 0.0:
		api[""set_var""].call(43, 1)
	var o = api[""object""].call(7)
	if o[""exists""] and not o[""destroyed""]:
		api[""set_var""].call(44, 1)
	if p[""f""] > 1.4 and p[""f""] < 1.6 and p[""flag""] and p[""id""] == 121 and p[""y""] > 400.0:
		api[""set_var""].call(45, 1)
	api[""set_var""].call(46, api[""tick""].call())
	api[""set_var""].call(47, api[""line_count""].call())
	return 0

func act(p, state):
	var e = api[""find_human""].call(500)
	api[""set_team""].call(e, 2)
	api[""teleport""].call(e, 50.0, 500.0, 0.0)
	api[""give_weapon""].call(api[""player""].call(), 1, 1, 10)
	api[""message""].call(0)
	api[""set_path_mode""].call(50, 0)
	api[""destroy_object""].call(7)
	api[""set_auto_judge""].call(false)
	api[""set_var""].call(30, api[""spawn_human""].call(3, 10.0, 500.0, 10.0, 90.0, 777, -1))
	if api[""spawn_weapon""].call(1, 30, 5.0, 500.0, 5.0, 0.0):
		api[""set_var""].call(31, 1)
	if api[""spawn_object""].call(0, 8, 6.0, 500.0, 6.0, 0.0, false):
		api[""set_var""].call(32, 1)
	api[""set_var""].call(33, api[""random""].call(10) + 1)
	api[""effect""].call(0, 0.0, 500.0, 0.0)
	api[""log""].call(""act done"")
	return 0

func bad_return(p, state):
	return ""next""

func no_return(p, state):
	state[""x""] = 1

func spin(p, state):
	var i = 0
	while true:
		i += 1
	return 0

func escape(p, state):
	var f = FileAccess.open(""build/event_check/pwned.txt"", FileAccess.WRITE)
	f.store_string(""x"")
	f.close()
	return 0

func flood(p, state):
	for i in range(2000):
		api[""get_var""].call(0)
	return 0

func bad_exit(p, state):
	return 5

func redirect(p, state):
	api[""start_line""].call(p[""line""], p[""target""])
	return 0

func kill_all(p, state):
	api[""kill""].call(api[""find_human""].call(500))
	api[""kill""].call(api[""get_var""].call(30))
	return 0

func finish(p, state):
	api[""end_mission""].call(true)
	return -1

func many(p, state):
	var alive = 0
	for i in range(p[""count""]):
		if api[""human""].call(i % 2)[""alive""]:
			alive += 1
	api[""set_var""].call(70, alive)
	return 0

func bump(p, state):
	bumps += 1
	api[""set_var""].call(60, bumps)
	return 0
";

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();
        private string m_folder;

        public override void _Ready()
        {
            AIController.Enabled = false;
            m_folder = GamePath.Resolve(k_workFolder);
            Directory.CreateDirectory(m_folder);

            if (!ScriptEventPack.Available)
            {
                Expect(false, "Godot Sandbox 확장이 로드되지 않음 (--headless --import 를 한 번 돌려야 한다)");
            }
            else
            {
                CheckFlow();
                CheckRejected();
                CheckBasePack();
                CheckScreenText();
                CheckDocumentExample();
            }

            MapLoader.UnloadPointData();
            MapLoader.UnloadMissionData();
            Directory.Delete(m_folder, true);

            GD.Print($"이벤트 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(m_problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 점검 한 건을 센다.
        /// </summary>
        /// <param name="ok">통과했는지.</param>
        /// <param name="what">실패했을 때 남길 설명.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// 이벤트 포인트 하나를 만든다.
        /// </summary>
        /// <param name="type">종류 번호.</param>
        /// <param name="id">식별번호.</param>
        /// <param name="p2">P2.</param>
        /// <param name="next">P3 (0번 출구).</param>
        /// <param name="extra">추가 파라미터 칸.</param>
        /// <returns>포인트.</returns>
        private static PD2Point Event(int type, int id, int p2, int next, params int[] extra)
        {
            return new PD2Point { type = type, id = id, param1 = p2, param2 = next, extra = extra, position = new Vector3(0f, 500f, 0f) };
        }

        /// <summary>
        /// 점검용 등록 파일의 이벤트 하나를 만든다.
        /// </summary>
        /// <param name="type">종류 번호.</param>
        /// <param name="function">함수 이름.</param>
        /// <param name="slots">"이름:칸:형식" 꼴의 파라미터들. "exit:칸" 은 출구다.</param>
        /// <returns>등록 정보.</returns>
        private static EventDefinitionData Define(int type, string function, params string[] slots)
        {
            var definition = new EventDefinitionData { type = type, name = function, function = function };
            foreach (string slot in slots)
            {
                string[] parts = slot.Split(':');
                var data = new EventSlotData { name = parts[0], slot = parts[1], kind = parts.Length > 2 ? parts[2] : "int" };
                if (parts[0] == "exit") definition.exits.Add(data);
                else definition.parameters.Add(data);
            }
            return definition;
        }

        /// <summary>
        /// 점검용 묶음의 등록 정보.
        /// </summary>
        /// <returns>묶음.</returns>
        private static EventPackData BuildPack()
        {
            var pack = new EventPackData { scriptPath = $"{k_workFolder}/pack.sgd" };
            pack.events.Add(Define(k_setVar, "set_var", "var:p2", "value:e0"));
            pack.events.Add(Define(k_waitVar, "wait_var", "var:p2", "value:e0"));
            pack.events.Add(Define(k_branch, "branch", "var:p2", "exit:p3", "exit:e0"));
            pack.events.Add(Define(k_countTicks, "count_ticks", "ticks:p2"));
            pack.events.Add(Define(k_probe, "probe", "f:e0:float", "flag:e1:bool"));
            pack.events.Add(Define(k_act, "act"));
            pack.events.Add(Define(k_badReturn, "bad_return"));
            pack.events.Add(Define(k_noReturn, "no_return"));
            pack.events.Add(Define(k_spin, "spin"));
            pack.events.Add(Define(k_escape, "escape"));
            pack.events.Add(Define(k_flood, "flood"));
            pack.events.Add(Define(k_badExit, "bad_exit"));
            pack.events.Add(Define(k_redirect, "redirect", "line:p2", "target:e0"));
            pack.events.Add(Define(k_killAll, "kill_all"));
            pack.events.Add(Define(k_finish, "finish"));
            pack.events.Add(Define(k_bump, "bump"));
            pack.events.Add(Define(k_many, "many", "count:p2"));
            return pack;
        }

        /// <summary>
        /// 점검용 파일 한 벌을 쓰고 미션으로 로드한다.
        /// </summary>
        /// <param name="pack">등록 정보.</param>
        /// <param name="script">스크립트 소스.</param>
        /// <param name="file">포인트 데이터.</param>
        /// <param name="scriptFile">스크립트 파일 이름.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        private bool WriteAndLoad(EventPackData pack, string script, PD2File file, string scriptFile = "pack.sgd")
        {
            File.WriteAllText(Path.Combine(m_folder, scriptFile), script);
            File.WriteAllText(Path.Combine(m_folder, "pack.json"), JsonData.ToJson(pack));
            File.WriteAllLines(Path.Combine(m_folder, "map.msg"), new[] { "hello", "USE {interact} \uD55C" });
            var mission = new ExtendedMissionData
            {
                name = "EVENT", blockPath = $"{k_workFolder}/map.bd2", pointPath = $"{k_workFolder}/map.pd2",
                addonEventDataPath = $"{k_workFolder}/pack.json",
            };
            string missionPath = Path.Combine(m_folder, "event.mif2");
            return file.Write(Path.Combine(m_folder, "map.pd2"), out _) && MIF2File.Write(missionPath, mission, out _)
                && MapLoader.LoadMissionFile(missionPath) && MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path);
        }

        /// <summary>
        /// 사람 둘(플레이어와 적), 소물 하나, 경로 포인트 하나만 있는 포인트 데이터를 만든다.
        /// </summary>
        /// <returns>포인트 데이터.</returns>
        private static PD2File BaseFile()
        {
            var file = new PD2File();
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 0, id = 1 });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 1, param2 = -1, id = 0, position = new Vector3(0f, 500f, 0f) });
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 1, id = 2 });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 2, param2 = -1, id = k_enemyId, position = new Vector3(100f, 500f, 0f) });
            // 맵에 세우지 않는 사람 정보 (팀 3). 이벤트가 이 정보로 사람을 스폰한다.
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 3, id = 3 });
            file.points.Add(new PD2Point { type = MapLoader.PointSmallObject, param1 = 0, param2 = 0, id = k_objectId, position = new Vector3(5f, 500f, 0f) });
            file.points.Add(new PD2Point { type = MapLoader.PointAIPath, param1 = 1, param2 = -1, id = k_pathId, position = new Vector3(0f, 500f, 0f) });
            return file;
        }

        /// <summary>
        /// 정상 흐름과 실행 중 실패: 줄 열두 개를 한 미션에서 돌려 틱마다의 상태를 본다.
        /// </summary>
        private void CheckFlow()
        {
            const int farWait = 1000;
            PD2File file = BaseFile();
            file.eventEntryIds.AddRange(new[] { 100, 200, 300, 310, 320, 330, 340, 350, 400, 9999, 600, 700, 800 });

            // 줄 0: 변수 쓰기 → 변수 기다리기 → 분기(거짓 출구) → 3틱 세기 → 조회 → 조작 → 시간 대기.
            file.points.Add(Event(k_setVar, 100, 1, 101, 5));
            file.points.Add(Event(k_waitVar, 101, 2, 102, 1));
            file.points.Add(Event(k_branch, 102, 3, 110, 120));
            file.points.Add(Event(k_setVar, 110, 9, 111, 1));
            file.points.Add(Event(k_countTicks, 120, 3, 121));
            file.points.Add(Event(k_probe, 121, 0, 122, PD2File.FloatCell(1.5f), 1));
            file.points.Add(Event(k_act, 122, 0, 123));
            file.points.Add(Event((int)EventType.WaitTime, 123, farWait, 124));
            // 줄 1: 2틱 세기 → 변수 2 를 세워 줄 0 을 풀어 준다.
            file.points.Add(Event(k_countTicks, 200, 2, 201));
            file.points.Add(Event(k_setVar, 201, 2, 202, 1));
            file.points.Add(Event((int)EventType.WaitTime, 202, farWait, 203));
            // 줄 2~7: 실패하는 이벤트. 뒤의 변수 쓰기까지 가면 안 된다.
            int[] failing = { k_badReturn, k_noReturn, k_spin, k_escape, k_flood, k_badExit };
            for (int i = 0; i < failing.Length; i++)
            {
                int id = 300 + i * 10;
                file.points.Add(Event(failing[i], id, 0, id + 1));
                file.points.Add(Event(k_setVar, id + 1, 10 + i, id + 2, 1));
            }
            // 줄 8: 줄 9 를 500 번에서 시작시킨다. 줄 9 는 없는 번호에서 출발한다.
            file.points.Add(Event(k_redirect, 400, 9, 401, 500));
            file.points.Add(Event((int)EventType.WaitTime, 401, farWait, 402));
            file.points.Add(Event(k_setVar, 500, 20, 501, 7));
            // 줄 10: 변수 50 을 기다렸다가 적을 전부 죽이고, 변수 51 을 기다렸다가 미션을 끝낸다.
            file.points.Add(Event(k_waitVar, 600, 50, 601, 1));
            file.points.Add(Event(k_killAll, 601, 0, 602));
            file.points.Add(Event(k_waitVar, 602, 51, 603, 1));
            file.points.Add(Event(k_finish, 603, 0, 604));
            // 줄 11: 스크립트의 전역 변수를 올린다.
            file.points.Add(Event(k_bump, 700, 0, 701));
            // 줄 12: 한 번에 사람 정보를 96번 조회한다 (사람 수의 상한). 실행 예산 안에 들어야 한다.
            file.points.Add(Event(k_many, 800, 96, 801));

            int errors = Debugger.ErrorCount;
            if (!WriteAndLoad(BuildPack(), k_script, file))
            {
                Expect(false, $"점검용 미션 로드 실패: {Debugger.FirstErrorSince(errors)}");
                return;
            }
            Expect(MapLoader.GetEventPoint(120)?.param0 == k_countTicks, "스크립트 이벤트의 포인트를 이벤트 포인트로 조회하지 못함");

            EventManager events = EventManager.Instance;
            events.BeginMission();
            SimClock.Step();
            Expect(events.GetVariable(1) == 5 && events.LineCursor(0) == 101, "변수 쓰기 뒤 변수 기다리기에서 멈추지 않음");
            Expect(events.LineCursor(1) == 200, "틱 세기가 첫 틱에 넘어감");
            for (int i = 0; i < failing.Length; i++)
            {
                Expect(events.LineStopped(2 + i) && events.GetVariable(10 + i) == 0, $"실패한 이벤트의 줄이 멈추지 않음 (종류 {failing[i]})");
            }
            Expect(Debugger.ErrorCount - errors >= failing.Length, "실패한 이벤트가 에러를 남기지 않음");
            Expect(!File.Exists(Path.Combine(m_folder, "pwned.txt")), "스크립트가 파일을 씀 (격리가 뚫림)");
            Expect(!events.LineStopped(0) && !events.LineStopped(1), "실패하지 않은 줄까지 멈춤");
            Expect(events.GetVariable(20) == 7 && events.LineCursor(8) == 401, "start_line 으로 다른 줄을 시작시키지 못함");
            Expect(events.GetVariable(60) == 1, "스크립트의 전역 변수가 한 번 오르지 않음");
            Expect(events.GetVariable(70) == 96 && !events.LineStopped(12), "사람 정보를 96번 조회하는 이벤트가 실행 예산에 걸림");

            SimClock.Step();
            Expect(events.GetVariable(2) == 1 && events.LineCursor(0) == 101, "두 번째 틱의 상태가 다름 (줄 1 이 변수를 세우고, 먼저 돈 줄 0 은 아직 기다린다)");
            SimClock.Step();
            Expect(events.LineCursor(0) == 120 && events.GetVariable(9) == 0, "분기가 거짓 출구(추가 파라미터 칸)로 나가지 않음");
            SimClock.Step();
            Expect(events.LineCursor(0) == 120, "줄의 저장 칸이 넘어갈 때 비워지지 않음 (틱 세기가 일찍 끝남)");
            SimClock.Step();
            Expect(events.LineCursor(0) == 123, $"틱 세기 → 조회 → 조작을 지나 시간 대기에 가지 않음 (지금 {events.LineCursor(0)})");

            // 조회 (조작 전의 상태).
            Expect(events.GetVariable(40) == 2 && events.GetVariable(41) == 1 && events.GetVariable(42) == 1, "사람 수·찾기·팀 인원 조회가 다름");
            Expect(events.GetVariable(43) == 1 && events.GetVariable(44) == 1, "사람 정보나 소물 정보 조회가 다름");
            Expect(events.GetVariable(45) == 1, "파라미터(실수, 불, 식별번호, 위치)가 스크립트에 그대로 가지 않음");
            Expect(events.GetVariable(46) == 5 && events.GetVariable(47) == 13, "틱 수나 줄 수 조회가 다름");

            // 조작.
            Human enemy = MapLoader.SearchHuman(k_enemyId);
            Human player = MapLoader.Player;
            Expect(enemy != null && enemy.Team == 2, "set_team 이 듣지 않음");
            Expect(enemy != null && Mathf.Abs(enemy.Controller.Position.X - 50f) < 0.01f, "teleport 가 듣지 않음");
            Expect(player != null && player.CurrentWeapon.WeaponIndex == 1, "give_weapon 이 듣지 않음");
            Expect(events.MessageId == 0 && events.MessageText == "hello", "message 가 듣지 않음");
            Expect(MapLoader.GetPathPoint(k_pathId)?.param1 == 0, "set_path_mode 가 듣지 않음");
            Expect(MapLoader.SearchSmallObject(k_objectId)?.IsDestroyed == true, "destroy_object 가 듣지 않음");
            Expect(!events.AutoJudge, "set_auto_judge 가 듣지 않음");
            Human spawned = MapLoader.SearchHuman(777);
            Expect(events.GetVariable(30) == 2 && spawned != null && spawned.Team == 3 && spawned.Alive, "spawn_human 이 듣지 않음");
            Expect(events.GetVariable(31) == 1 && WeaponManager.Instance.CountActive() == 1, "spawn_weapon 이 듣지 않음");
            Expect(events.GetVariable(32) == 1 && MapLoader.SearchSmallObject(8) != null, "spawn_object 가 듣지 않음");
            Expect(events.GetVariable(33) >= 1 && events.GetVariable(33) <= 10, "random 의 범위가 다름");

            // 자동 판정을 끈 채 적이 전부 죽어도 미션이 끝나지 않고, 이벤트가 끝낸다.
            events.SetVariable(50, 1);
            SimClock.Step();
            SimClock.Step();
            Expect(enemy != null && enemy.HP <= 0f && spawned != null && spawned.HP <= 0f, "kill 이 듣지 않음");
            Expect(events.Result == (int)MissionResult.InProgress, "자동 판정을 껐는데 적이 전멸하자 미션이 끝남");
            events.SetVariable(51, 1);
            SimClock.Step();
            Expect(events.Result == (int)MissionResult.Complete, "end_mission 이 듣지 않음");

            // 다시 로드하면 스크립트의 전역 변수와 미션 변수가 처음으로 돌아간다.
            bool reloaded = MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path);
            events.BeginMission();
            SimClock.Step();
            Expect(reloaded && events.GetVariable(60) == 1 && events.GetVariable(51) == 0 && events.AutoJudge, "다시 로드한 뒤 스크립트나 변수가 처음 상태가 아님");
        }

        /// <summary>
        /// 로드 때 거절돼야 하는 경우들. 거절되면 맵이 로드되지 않고 이유가 에러로 남는다.
        /// </summary>
        private void CheckRejected()
        {
            PD2File file = BaseFile();
            file.eventEntryIds.Add(100);
            file.points.Add(Event(k_setVar, 100, 1, 101, 5));

            Reject("없는 종류 번호", "A point uses event type", () =>
            {
                PD2File other = BaseFile();
                other.points.Add(Event(12345, 100, 0, 101));
                return WriteAndLoad(BuildPack(), k_script, other);
            });
            Reject("문법이 틀린 스크립트", "event script compile error", () => WriteAndLoad(BuildPack(), "func set_var(:\n\treturn\n", file));
            Reject("없는 함수", "function \"set_var\" is not in", () => WriteAndLoad(BuildPack(), "func other(p, state):\n\treturn 0\n", file));
            Reject("init 에서 실패하는 스크립트", "event script failed in init()", () =>
                WriteAndLoad(BuildPack(), "func init(a):\n\tvar f = FileAccess.open(\"x\", FileAccess.WRITE)\n\tf.close()\n\nfunc set_var(p, state):\n\treturn 0\n", file));
            Reject("일반 GDScript 파일", "event script must be a .sgd file", () =>
            {
                EventPackData pack = BuildPack();
                pack.scriptPath = $"{k_workFolder}/pack.gd";
                return WriteAndLoad(pack, k_script, file, "pack.gd");
            });
            Reject("게임 폴더 밖의 스크립트", "Traversal directory detected", () =>
            {
                EventPackData pack = BuildPack();
                pack.scriptPath = "../outside.sgd";
                return WriteAndLoad(pack, k_script, file);
            });
            Reject("같은 번호를 두 번 등록", "is registered more than once", () =>
            {
                EventPackData pack = BuildPack();
                pack.events.Add(Define(k_setVar, "wait_var"));
                return WriteAndLoad(pack, k_script, file);
            });
            // 99 는 설치형 구간의 번호라 미션 묶음이 등록할 수 없다 (기본 묶음이 쓰지 않는 번호를 골랐다).
            Reject("구간 밖의 번호(미션 묶음의 99)", "A point uses event type", () =>
            {
                EventPackData pack = BuildPack();
                pack.events.Add(Define(99, "set_var"));
                PD2File other = BaseFile();
                other.points.Add(Event(99, 100, 0, 101));
                return WriteAndLoad(pack, k_script, other);
            });
            Reject("틀린 칸 표기", "unknown slot", () =>
            {
                EventPackData pack = BuildPack();
                pack.events[0].parameters[0].slot = "q9";
                return WriteAndLoad(pack, k_script, file);
            });
            Reject("예약된 파라미터 이름", "empty or reserved", () =>
            {
                EventPackData pack = BuildPack();
                pack.events[0].parameters[0].name = "id";
                return WriteAndLoad(pack, k_script, file);
            });

            ConfigManager config = ConfigManager.Instance;
            bool saved = config.GetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, true);
            config.SetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, false);
            Reject("AllowEventScript 끔", "AllowEventScript is off", () => WriteAndLoad(BuildPack(), k_script, file));
            config.SetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, saved);

            // 스크립트 이벤트가 없는 미션은 설정이나 묶음과 무관하게 로드된다.
            config.SetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, false);
            Expect(WriteAndLoad(BuildPack(), "func broken(:\n", BaseFile()), "스크립트 이벤트를 쓰지 않는 미션이 묶음 때문에 로드되지 않음");
            config.SetBool(ConfigManager.SectionGeneral, ConfigManager.KeyAllowEventScript, saved);

            Expect(WriteAndLoad(BuildPack(), k_script, file), "거절 점검 뒤에 정상 미션이 로드되지 않음");
        }

        /// <summary>
        /// 기본 제공 묶음(종류 20~99)의 이벤트 전부를 한 미션에서 돌린다. 등록 파일의 칸과 스크립트의 함수가 맞는지도 여기서 드러난다 (틀리면 로드가 거절된다).
        /// </summary>
        private void CheckBasePack()
        {
            const int waitVar = 20, waitTeamAlive = 21, waitArea = 22, waitHp = 23, waitWeapon = 24, waitTicks = 25;
            const int setVar = 40, addVar = 41, spawnHuman = 42, spawnWeapon = 43, spawnObject = 44, killHuman = 45, damageHuman = 46, moveHuman = 47;
            const int setTeam = 48, setPathMode = 49, destroyObject = 50, playEffect = 51, setAutoJudge = 52, giveWeaponPrimary = 53, giveWeaponSecondary = 54;
            const int tweenObject = 55, playSound = 56, stopSound = 57, moveBlock = 58, toggleBlock = 59;
            // 소물 8 을 경로 포인트로 옮기는 데 걸리는 시간 (초)과 그 틱 수, 소리를 재생할 칸.
            const float tweenSeconds = 0.3f;
            const int tweenTicks = 10, loopSlot = 2, missingSlot = 3, tweenPathId = 77;
            const int branchVar = 60, branchRandom = 61, randomExit = 62, startLine = 63, stopLine = 64;
            const int equal = 0, greaterEqual = 5;

            // 플레이어가 처음에 들지 않은 무기와 적의 처음 체력을 알아 둔다.
            if (!WriteAndLoad(BuildPack(), k_script, BaseFile()))
            {
                Expect(false, "기본 묶음 점검의 준비 로드 실패");
                return;
            }
            Human probe = MapLoader.Player;
            int weapon = 1;
            while (probe.GetWeapon(0).WeaponIndex == weapon || probe.GetWeapon(1).WeaponIndex == weapon
                || weapon == DataManager.Instance.WeaponParameterData.weaponGeneralData.noneWeaponIndex)
            {
                weapon++;
            }
            float enemyHp = MapLoader.SearchHuman(k_enemyId).HP;

            PD2File file = BaseFile();
            file.eventEntryIds.AddRange(new[] { 100, 200, 9998, 400, 500, 600, 700, 800, 900 });
            // 줄 0: 줄 8 멈추기 → 변수 → 분기 → 변수 3 을 기다림 → 스폰과 조작 → 변수 4 를 기다림 → 죽이기.
            file.points.Add(Event(stopLine, 100, 8, 101));
            file.points.Add(Event(setVar, 101, 1, 102, 3));
            file.points.Add(Event(addVar, 102, 1, 103, 2));
            file.points.Add(Event(branchVar, 103, 1, 110, equal, 5, 119));
            file.points.Add(Event(setVar, 110, 2, 120, 1));
            file.points.Add(Event(setVar, 119, 2, 120, 2));
            file.points.Add(Event(waitVar, 120, 3, 121, greaterEqual, 1));
            file.points.Add(Event(spawnHuman, 121, 3, 122, 777, -1));
            file.points.Add(Event(spawnWeapon, 122, weapon, 123, 30));
            file.points.Add(Event(spawnObject, 123, 0, 124, 8, 0));
            file.points.Add(Event(setTeam, 124, k_enemyId, 125, 2));
            file.points.Add(Event(moveHuman, 125, k_enemyId, 126));
            file.points.Add(Event(giveWeaponPrimary, 126, 0, 139, weapon, 10));
            file.points.Add(Event(giveWeaponSecondary, 139, 0, 127, weapon, 10));
            file.points.Add(Event(setPathMode, 127, k_pathId, 128, 0));
            file.points.Add(Event(destroyObject, 128, k_objectId, 129));
            file.points.Add(Event(playEffect, 129, 0, 130));
            file.points.Add(Event(setAutoJudge, 130, 0, 131));
            file.points.Add(Event(damageHuman, 131, k_enemyId, 1310, PD2File.FloatCell(10f)));
            // 소물 8 을 경로 포인트로 (방향은 그 포인트의 것, 부드럽게), 되풀이하는 소리, 목록에 없는 소리.
            file.points.Add(new PD2Point { type = MapLoader.PointAIPath, param1 = 0, param2 = -1, id = tweenPathId, position = new Vector3(3f, 501f, 2f), direction = 40f });
            file.points.Add(Event(tweenObject, 1310, 8, 1311, tweenPathId, PD2File.FloatCell(-1f), PD2File.FloatCell(tweenSeconds), 1));
            file.points.Add(Event(playSound, 1311, loopSlot, 1312, 0, PD2File.FloatCell(0f), 1, 1));
            file.points.Add(Event(playSound, 1312, missingSlot, 1313, 9999, PD2File.FloatCell(1f), 0, 0));
            // 블록 0 을 위로 2 m, 세로축 둘레로 90° (0.3초). 블록 1 을 끈다. 없는 블록은 아무 일도 없다.
            file.points.Add(Event(moveBlock, 1313, 0, 1314, PD2File.FloatCell(0f), PD2File.FloatCell(2f), PD2File.FloatCell(0f),
                PD2File.FloatCell(0f), PD2File.FloatCell(0f), PD2File.FloatCell(90f), PD2File.FloatCell(tweenSeconds), 0));
            file.points.Add(Event(toggleBlock, 1314, 1, 1315, 0));
            file.points.Add(Event(toggleBlock, 1315, 999, 132, 0));
            file.points.Add(Event(startLine, 132, 2, 133, 300));
            file.points.Add(Event(waitVar, 133, 4, 1340, greaterEqual, 1));
            file.points.Add(Event(stopSound, 1340, loopSlot, 134));
            file.points.Add(Event(killHuman, 134, k_enemyId, 135));
            file.points.Add(Event(killHuman, 135, 777, 136));
            file.points.Add(Event(waitTicks, 136, 1000, 137));
            // 줄 1: 적이 이 포인트의 5 m 안에 오기를 기다린다 (줄 0 이 옮겨 준다).
            file.points.Add(Event(waitArea, 200, k_enemyId, 201, PD2File.FloatCell(5f)));
            file.points.Add(Event(setVar, 201, 10, 202, 1));
            // 줄 2: 줄 0 이 300 번에서 시작시킨다.
            file.points.Add(Event(setVar, 300, 11, 301, 1));
            // 줄 3: 변수 4 뒤에 팀 3 이 전멸하기를 기다린다.
            file.points.Add(Event(waitVar, 400, 4, 401, greaterEqual, 1));
            file.points.Add(Event(waitTeamAlive, 401, 3, 402, 0));
            file.points.Add(Event(setVar, 402, 12, 403, 1));
            // 줄 4: 적의 체력이 처음보다 5 넘게 깎이기를 기다린다.
            file.points.Add(Event(waitHp, 500, k_enemyId, 501, PD2File.FloatCell(enemyHp - 5f)));
            file.points.Add(Event(setVar, 501, 13, 502, 1));
            // 줄 5: 플레이어가 그 무기를 들기를 기다린다.
            file.points.Add(Event(waitWeapon, 600, 0, 601, weapon));
            file.points.Add(Event(setVar, 601, 14, 602, 1));
            // 줄 6: 확률 100 과 0 의 난수 분기.
            file.points.Add(Event(branchRandom, 700, 100, 701, 702));
            file.points.Add(Event(setVar, 701, 15, 703, 1));
            file.points.Add(Event(setVar, 702, 15, 703, 2));
            file.points.Add(Event(branchRandom, 703, 0, 704, 705));
            file.points.Add(Event(setVar, 704, 16, 706, 1));
            file.points.Add(Event(setVar, 705, 16, 706, 2));
            // 줄 7: 출구 셋 가운데 하나.
            file.points.Add(Event(randomExit, 800, 3, 801, 802, 803, 804));
            for (int i = 0; i < 4; i++)
            {
                file.points.Add(Event(setVar, 801 + i, 17, 810, i + 1));
            }
            // 줄 8: 줄 0 이 첫 틱에 멈춘다.
            file.points.Add(Event(waitTicks, 900, 2, 901));
            file.points.Add(Event(setVar, 901, 18, 902, 1));

            int errors = Debugger.ErrorCount;
            if (!WriteAndLoad(BuildPack(), k_script, file))
            {
                Expect(false, $"기본 묶음을 쓰는 미션 로드 실패: {Debugger.FirstErrorSince(errors)}");
                return;
            }

            // 블록 둘: 0 번은 가로 4 × 높이 1 × 세로 2 m 의 상자, 1 번은 한 변 1 m 의 상자. 사람들이 있는 높이(500 m)와 떨어진 곳에 둔다.
            var blockFile = new BD2File { textureListPath = string.Empty };
            blockFile.blocks.Add(CheckBox(new Vector3(0f, 0f, 0f), new Vector3(2f, 0.5f, 1f)));
            blockFile.blocks.Add(CheckBox(new Vector3(20f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f)));
            MapLoader.LoadBlockData(blockFile);
            Expect(MapLoader.Blocks.Count == 2 && MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 0f, 0f)) && MapLoader.IsInsideBlock(BlockLayer.Bullet, new Vector3(20f, 0f, 0f)),
                "블록 이벤트 점검용 블록이 로드되지 않음");

            EventManager events = EventManager.Instance;
            events.BeginMission();
            SimClock.Step();
            Expect(events.GetVariable(1) == 5 && events.GetVariable(2) == 1 && events.LineCursor(0) == 120, "변수 설정·가산·변수 분기·변수 기다리기가 다름");
            Expect(events.GetVariable(15) == 1 && events.GetVariable(16) == 2, "난수 분기의 확률 100 / 0 이 다름");
            Expect(events.GetVariable(17) >= 1 && events.GetVariable(17) <= 3, "난수 출구가 지정한 개수를 벗어남");
            Expect(events.LineStopped(8), "줄 멈추기가 듣지 않음");
            Expect(events.GetVariable(10) == 0 && events.GetVariable(13) == 0 && events.GetVariable(14) == 0, "기다리는 이벤트가 조건 전에 넘어감");

            events.SetVariable(3, 1);
            SimClock.Step();
            Human enemy = MapLoader.SearchHuman(k_enemyId);
            Human spawned = MapLoader.SearchHuman(777);
            Human player = MapLoader.Player;
            Expect(events.LineCursor(0) == 133, $"조작 이벤트들을 지나 변수 기다리기에 가지 않음 (지금 {events.LineCursor(0)})");
            Expect(spawned != null && spawned.Team == 3 && spawned.Alive, "사람 스폰이 듣지 않음");
            Expect(WeaponManager.Instance.CountActive() == 1 && MapLoader.SearchSmallObject(8) != null, "무기·오브젝트 스폰이 듣지 않음");
            Expect(enemy != null && enemy.Team == 2 && Mathf.Abs(enemy.Controller.Position.X) < 0.01f, "팀 변경이나 사람 옮기기가 듣지 않음");
            Expect(player != null && player.SelectWeapon == 1 && player.GetWeapon(1).WeaponIndex == weapon, "주 무기 슬롯에 무기 지급이 듣지 않음");
            Expect(player != null && player.GetWeapon(0).WeaponIndex == weapon, "보조 무기 슬롯에 무기 지급이 듣지 않음");
            Expect(MapLoader.SpawnHuman(9999, Vector3.Zero, 0f, 1, -1) == -1, "없는 사람 정보 포인트로 사람이 스폰됨");
            Expect(MapLoader.GetPathPoint(k_pathId)?.param1 == 0 && MapLoader.SearchSmallObject(k_objectId)?.IsDestroyed == true, "경로 모드나 오브젝트 파괴가 듣지 않음");
            Expect(!events.AutoJudge, "자동 판정 끄기가 듣지 않음");
            Expect(enemy != null && Mathf.Abs(enemy.HP - (enemyHp - 10f)) < 0.01f, "피해 주기가 듣지 않음");
            Expect(events.GetVariable(10) == 1, "반경 기다리기가 넘어가지 않음");
            Expect(events.GetVariable(11) == 1, "줄 시작이 듣지 않음");
            Expect(events.GetVariable(13) == 1 && events.GetVariable(14) == 1, "체력 기다리기나 무기 기다리기가 넘어가지 않음");
            SmallObject moved = MapLoader.SearchSmallObject(8);
            RawPointData destination = MapLoader.GetPathPoint(tweenPathId);
            Vector3 movedFrom = moved != null ? moved.LogicPosition : Vector3.Zero;
            Expect(moved != null && moved.IsTweening && destination != null && movedFrom.DistanceTo(destination.position) > 0.01f, "오브젝트 움직이기가 시작되지 않았거나 바로 도착함 (순간이동이면 안 된다)");
            Expect(SoundManager.Instance.SlotActive(loopSlot) && !SoundManager.Instance.SlotActive(missingSlot), "되풀이하는 소리가 칸에서 나지 않거나 목록에 없는 소리가 재생됨");
            Expect(MapLoader.IsBlockMoving(0) && MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 0f, 0f)), "블록 움직이기가 시작되지 않았거나 바로 도착함 (순간이동이면 안 된다)");
            Expect(!MapLoader.Blocks[1].enabled && !MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(20f, 0f, 0f)) && !MapLoader.IsInsideBlock(BlockLayer.Bullet, new Vector3(20f, 0f, 0f))
                && !MapLoader.RaycastBlock(BlockLayer.Sight, new Vector3(20f, 5f, 0f), Vector3.Down, 10f, out _), "끈 블록이 판정에 남아 있음");

            events.SetVariable(4, 1);
            SimClock.Step();
            Expect(enemy != null && enemy.HP <= 0f && spawned != null && spawned.HP <= 0f, "죽이기가 듣지 않음");
            for (int tick = 0; tick < 5; tick++) SimClock.Step();
            Expect(events.GetVariable(12) == 1, "팀 인원 기다리기가 넘어가지 않음");
            Expect(events.GetVariable(18) == 0 && events.Result == (int)MissionResult.InProgress, "멈춘 줄이 진행됐거나 자동 판정을 껐는데 미션이 끝남");
            Expect(!SoundManager.Instance.SlotActive(loopSlot), "소리 멈추기가 듣지 않음");

            // 오브젝트 움직이기: 도중에는 출발과 도착 사이에 있고, 시간이 지나면 도착해 멈춘다. 방향은 경로 포인트의 것이다.
            Expect(moved != null && moved.IsTweening && moved.LogicPosition.DistanceTo(movedFrom) > 0.01f && moved.LogicPosition.DistanceTo(destination.position) > 0.01f,
                "움직이는 오브젝트가 도중에 출발과 도착 사이에 있지 않음");
            for (int tick = 0; tick < tweenTicks + 2; tick++) SimClock.Step();
            Expect(moved != null && !moved.IsTweening && !moved.IsDestroyed && moved.LogicPosition.DistanceTo(destination.position) < 0.001f, "움직인 오브젝트가 경로 포인트에 도착하지 않음");
            Expect(moved != null && moved.Position.DistanceTo(destination.position) < 0.001f
                && Mathf.Abs(Mathf.AngleDifference(moved.Rotation.Y, Coord.FromUnityEuler(new Vector3(0f, destination.look, 0f)).Y)) < 0.001f, "움직인 오브젝트의 노드 자리나 방향이 다름");
            if (moved?.ColliderData != null && moved.ColliderData.shapes.Count > 0)
            {
                Vector3 shapeCenter = moved.ColliderBasis * Coord.FromUnity(moved.ColliderData.shapes[0].center);
                Expect(moved.Contains(destination.position + shapeCenter) && !moved.Contains(movedFrom + shapeCenter), "움직인 오브젝트의 판정이 따라오지 않음");
            }
            // 블록 움직이기: 위로 2 m 올라가고 세로축 둘레로 90° 돌아서, 가로로 길던 상자가 세로로 길어진다. 처음 자리의 판정은 사라진다.
            Expect(!MapLoader.IsBlockMoving(0) && MapLoader.Blocks[0].position.DistanceTo(new Vector3(0f, 2f, 0f)) < 0.001f, "움직인 블록이 도착하지 않음");
            Expect(MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(0f, 2f, 1.5f)) && !MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 2f, 0f))
                && !MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 0f, 0f)), "움직인 블록의 내부 판정이 따라오지 않음");
            Expect(MapLoader.RaycastBlock(BlockLayer.Bullet, new Vector3(0f, 10f, 1.5f), Vector3.Down, 20f, out float blockHit) && Mathf.Abs(blockHit - 7.5f) < 0.01f
                && !MapLoader.RaycastBlock(BlockLayer.Bullet, new Vector3(1.5f, 10f, 0f), Vector3.Down, 20f, out _), "움직인 블록의 레이 판정이 따라오지 않음");
            // 켜면 돌아오고, 미션을 내리면 옮긴 블록도 처음으로 돌아간다.
            Expect(MapLoader.SetBlockEnabled(1, true) && MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(20f, 0f, 0f)) && !MapLoader.SetBlockEnabled(999, true), "블록을 다시 켜지 못하거나 없는 블록을 켬");
            MapLoader.SetBlockEnabled(1, false);
            MapLoader.MoveBlock(0, new Vector3(0f, 9f, 0f), Vector3.Zero, 0, false);
            Expect(MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 9f, 0f)), "시간 0 으로 옮긴 블록이 바로 옮겨지지 않음");
            MapLoader.ResetBlockMotion();
            Expect(MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 0f, 0f)) && !MapLoader.IsInsideBlock(BlockLayer.Human, new Vector3(1.5f, 9f, 0f))
                && MapLoader.Blocks[1].enabled && MapLoader.IsInsideBlock(BlockLayer.Sight, new Vector3(20f, 0f, 0f)), "되돌린 블록이 처음 상태가 아님");
            Expect(MapLoader.GetBlockColliders(BlockLayer.Human).Count == 2 && MapLoader.GetBlockColliders(BlockLayer.Human)[0].index == 0, "되돌린 뒤 판정 목록의 순서가 처음과 다름");

            moved?.StartTween(movedFrom, 0f, tweenTicks, false);
            SimClock.Step();
            moved?.Break();
            Vector3 brokenAt = moved != null ? moved.LogicPosition : Vector3.Zero;
            SimClock.Step();
            Expect(moved != null && moved.IsDestroyed && !moved.IsTweening && moved.LogicPosition.DistanceTo(brokenAt) < 0.001f, "움직이다 부서진 오브젝트가 계속 움직임");
        }

        /// <summary>
        /// 점검용 상자 블록 하나를 만든다 (텍스처 없음, 판정 전부 충돌).
        /// </summary>
        /// <param name="center">가운데.</param>
        /// <param name="half">축마다의 절반 크기 (m).</param>
        /// <returns>블록.</returns>
        private static BD2Block CheckBox(Vector3 center, Vector3 half)
        {
            // BD1 과 같은 순서: 0~3 이 윗면, 4~7 이 아랫면.
            Vector3[] corners =
            {
                new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f),
                new Vector3(1f, -1f, -1f), new Vector3(-1f, -1f, -1f), new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
            };
            var block = new BD2Block();
            for (int i = 0; i < BD2Block.VertexCount; i++) block.vertices[i] = center + corners[i] * half;
            for (int f = 0; f < BD2Block.FaceCount; f++)
            {
                block.materialIndices[f] = -1;
                block.textureIndices[f] = -1;
            }
            return block;
        }

        /// <summary>
        /// 화면 글자를 찾는다.
        /// </summary>
        /// <param name="texts">EventManager.HudTexts 의 결과.</param>
        /// <param name="slot">칸 번호.</param>
        /// <returns>그 칸의 사전. 없으면 null.</returns>
        private static Godot.Collections.Dictionary FindText(Godot.Collections.Array texts, int slot)
        {
            foreach (Variant item in texts)
            {
                Godot.Collections.Dictionary entry = item.AsGodotDictionary();
                if (entry["slot"].AsInt32() == slot) return entry;
            }
            return null;
        }

        /// <summary>
        /// 화면 글자와 Interact: 글자 놓기·지우기·변수 표시·카운트다운(기본 묶음 70~73)과 Interact 기다리기(26)를 한 미션에서 돌린다.
        /// 놓인 순서, 스프라이트 글꼴에 없는 글자, 키 이름 치환, 시간이 다 된 글자, 미션이 끝날 때 지워지는지를 본다.
        /// </summary>
        private void CheckScreenText()
        {
            const int waitInteract = 26, waitTicks = 25, setVar = 40, showText = 70, clearText = 71, countdown = 72, showVar = 73;
            const int promptSlot = 31, sprite = EventManager.HudFontSprite, topRight = 2;
            const int red = 0xFF0000;

            if (!WriteAndLoad(BuildPack(), k_script, BaseFile()))
            {
                Expect(false, "화면 글자 점검의 준비 로드 실패");
                return;
            }
            // 플레이어의 눈에서 정면으로 5 m 앞과 뒤. Interact 의 각도 조건에 쓴다.
            HumanController controller = MapLoader.Player.Controller;
            Vector3 eye = controller.Position + Vector3.Up * controller.CameraHeight;
            Vector3 forward = Coord.AimDirection(controller.Yaw, controller.Pitch);

            PD2File file = BaseFile();
            file.eventEntryIds.AddRange(new[] { 100, 200, 300, 400 });
            int far = PD2File.FloatCell(1000f);
            // 줄 0: 글자 놓기(스프라이트, 오른쪽 위, 빨강) → Interact(각도 무관. 안내 칸을 함께 쓰는 줄 3 과 같은 문구) → 변수 → 글자 지우기 → 변수 표시 → 대기.
            file.points.Add(Event(showText, 100, 0, 101, 1, sprite, topRight, PD2File.FloatCell(-10f), PD2File.FloatCell(-12f), PD2File.FloatCell(20f), red, PD2File.FloatCell(0f)));
            file.points.Add(Event(waitInteract, 101, 0, 102, far, PD2File.FloatCell(180f), 1));
            file.points.Add(Event(setVar, 102, 1, 103, 7));
            file.points.Add(Event(clearText, 103, 0, 104));
            file.points.Add(Event(showVar, 104, 1, 105, 1, 0, 0, 4, 0, 0, PD2File.FloatCell(16f), 0xFFFFFF, PD2File.FloatCell(0.09f)));
            file.points.Add(Event(waitTicks, 105, 1000, 106));
            // 줄 1: 65초 카운트다운 (분:초, 남은 초를 변수 5 에).
            file.points.Add(Event(countdown, 200, 2, 201, 65, 1, 5, sprite, topRight, PD2File.FloatCell(-10f), PD2File.FloatCell(-40f), PD2File.FloatCell(16f), 0xFFFFFF));
            file.points.Add(Event(setVar, 201, 6, 202, 1));
            // 줄 2 / 3: 20° 안으로 바라봐야 하는 Interact. 하나는 등 뒤, 하나는 정면에 있다. 메시지 1번을 안내로 쓴다.
            PD2Point behind = Event(waitInteract, 300, 0, 301, far, PD2File.FloatCell(20f), 1);
            behind.position = eye - forward * 5f;
            file.points.Add(behind);
            file.points.Add(Event(setVar, 301, 7, 302, 1));
            PD2Point front = Event(waitInteract, 400, 0, 401, far, PD2File.FloatCell(20f), 1);
            front.position = eye + forward * 5f;
            file.points.Add(front);
            file.points.Add(Event(setVar, 401, 8, 402, 1));

            int errors = Debugger.ErrorCount;
            if (!WriteAndLoad(BuildPack(), k_script, file))
            {
                Expect(false, $"화면 글자 미션 로드 실패: {Debugger.FirstErrorSince(errors)}");
                return;
            }

            EventManager events = EventManager.Instance;
            string key = InputManager.Instance.GetActionBinding(InputManager.Interact);
            key = key.Substring(key.LastIndexOf('/') + 1).ToUpperInvariant();
            events.BeginMission();
            int revision = events.HudRevision;
            SimClock.Step();

            Godot.Collections.Array texts = events.HudTexts();
            Godot.Collections.Dictionary shown = FindText(texts, 0);
            Expect(shown != null && shown["text"].AsString() == $"USE {key} ?", $"글자 놓기의 문구가 다름: 키 이름 치환, 스프라이트 글꼴에 없는 글자 (\"{shown?["text"]}\")");
            Expect(shown != null && shown["font"].AsInt32() == sprite && shown["anchor"].AsInt32() == topRight && shown["x"].AsSingle() == -10f
                && shown["y"].AsSingle() == -12f && shown["size"].AsSingle() == 20f && shown["color"].AsColor().IsEqualApprox(new Color(1f, 0f, 0f)),
                "글자 놓기의 글꼴·기준점·위치·크기·색이 다름");
            Godot.Collections.Dictionary prompt = FindText(texts, promptSlot);
            Expect(prompt != null && prompt["text"].AsString() == $"USE {key} ?" && prompt["font"].AsInt32() == sprite, "Interact 안내가 뜨지 않음");
            Godot.Collections.Dictionary timer = FindText(texts, 2);
            Expect(timer != null && timer["text"].AsString() == "1:05" && events.GetVariable(5) == 65, $"카운트다운의 표시나 변수가 다름 (\"{timer?["text"]}\", {events.GetVariable(5)})");
            Expect(texts.Count == 3 && texts[0].AsGodotDictionary()["slot"].AsInt32() == 0 && texts[2].AsGodotDictionary()["slot"].AsInt32() == 2,
                "화면 글자가 놓인 순서대로 오지 않음");
            Expect(events.HudRevision != revision && events.GetVariable(1) == 0 && events.GetVariable(8) == 0, "글자가 바뀌었는데 알림 값이 그대로이거나, 누르기 전에 Interact 가 넘어감");

            // 같은 내용을 다시 놓는 것(안내, 같은 초의 카운트다운)으로는 알림 값이 오르지 않는다.
            revision = events.HudRevision;
            SimClock.Step();
            Expect(events.HudRevision == revision, "내용이 그대로인데 화면 글자의 알림 값이 오름");

            events.QueueInteract();
            SimClock.Step();
            texts = events.HudTexts();
            Expect(events.GetVariable(1) == 7 && events.GetVariable(8) == 1, "Interact 를 눌렀는데 넘어가지 않음");
            Expect(events.GetVariable(7) == 0, "등 뒤에 있는 것에 Interact 가 통함 (각도 조건이 듣지 않음)");
            Expect(FindText(texts, 0) == null, "글자 지우기가 듣지 않음");
            Godot.Collections.Dictionary value = FindText(texts, 1);
            Expect(value != null && value["text"].AsString() == "7" && value["font"].AsInt32() == EventManager.HudFontOS, "변수 표시가 다름");
            Expect(texts.Count > 0 && texts[texts.Count - 1].AsGodotDictionary()["slot"].AsInt32() == 1, "나중에 놓은 글자가 맨 뒤(맨 위)에 오지 않음");
            SimClock.Step();
            Expect(!events.InteractPressed, "Interact 입력이 한 틱 넘게 남음");

            for (int tick = 0; tick < 40; tick++) SimClock.Step();
            texts = events.HudTexts();
            timer = FindText(texts, 2);
            Expect(timer != null && timer["text"].AsString() == "1:04" && events.GetVariable(5) == 64, $"44틱 뒤의 카운트다운이 다름 (\"{timer?["text"]}\")");
            Expect(FindText(texts, 1) == null, "시간이 정해진 글자가 사라지지 않음");
            Expect(FindText(texts, promptSlot) == null, "넘어간 Interact 의 안내가 남아 있음");

            // 칸의 범위와 미션 종료.
            events.SetHudText(EventManager.HudSlotCount, "x", null);
            events.SetHudText(-1, "x", null);
            events.SetHudText(3, new string('a', 1000), null);
            Expect(events.HudTexts().Count == 2 && FindText(events.HudTexts(), 3)["text"].AsString().Length == 256, "칸 번호의 범위나 글자 수의 상한이 듣지 않음");
            events.ForceEnd(false);
            Expect(events.HudTexts().Count == 0, "미션이 끝났는데 화면 글자가 남아 있음");
        }

        /// <summary>
        /// 모딩 문서(docs/modding.md 의 "스크립트")에 실은 예제 스크립트가 그대로 컴파일되고 도는지 본다. 문서의 예제를 고치면 여기도 같이 고친다.
        /// </summary>
        private void CheckDocumentExample()
        {
            const string example = @"var api

# 미션을 로드할 때 한 번 불립니다. 게임이 내주는 함수 표를 받아 둡니다.
func init(a):
	api = a

# 이벤트마다 하나. 줄이 그 포인트에 있는 동안 1/33초마다 불립니다.
func wait_enemies_near(p, state):
	var count = 0
	for i in range(api[""human_count""].call()):
		var h = api[""human""].call(i)
		if h[""alive""] and h[""team""] == p[""team""]:
			var dx = h[""x""] - p[""x""]
			var dz = h[""z""] - p[""z""]
			if dx * dx + dz * dz <= p[""radius""] * p[""radius""]:
				count += 1
	if count >= 3:
		return 0
	return -1
";
            var pack = new EventPackData { scriptPath = $"{k_workFolder}/pack.sgd" };
            pack.events.Add(Define(10000, "wait_enemies_near", "team:p2:team", "radius:e0:float", "exit:p3"));

            PD2File file = BaseFile();
            file.eventEntryIds.Add(100);
            // 팀 1 은 한 명뿐이라 3명 조건을 채우지 못하고 기다린다.
            file.points.Add(Event(10000, 100, 1, 101, PD2File.FloatCell(1000f)));

            int errors = Debugger.ErrorCount;
            bool loaded = WriteAndLoad(pack, example, file);
            EventManager events = EventManager.Instance;
            if (loaded)
            {
                events.BeginMission();
                SimClock.Step();
            }
            Expect(loaded && !events.LineStopped(0) && events.LineCursor(0) == 100 && Debugger.ErrorCount == errors,
                $"모딩 문서의 예제 스크립트가 돌지 않음: {Debugger.FirstErrorSince(errors)}");
        }

        /// <summary>
        /// 로드가 거절되고 기대한 에러가 남는지 본다.
        /// </summary>
        /// <param name="what">무엇을 시험하는지.</param>
        /// <param name="expectedError">에러 문구에 들어 있어야 하는 말.</param>
        /// <param name="load">로드를 시도하는 함수.</param>
        private void Reject(string what, string expectedError, Func<bool> load)
        {
            int errors = Debugger.ErrorCount;
            bool loaded = load();
            string error = Debugger.FirstErrorSince(errors);
            Expect(!loaded && MapLoader.HumanCount == 0, $"{what}: 미션이 로드됨");
            Expect(error.Contains(expectedError, StringComparison.Ordinal), $"{what}: 에러가 다름 (\"{error}\")");
        }
    }
}

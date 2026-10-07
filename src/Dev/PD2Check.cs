using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. 모든 미션의 PD1 을 PD2 로 바꿔 저장하고 다시 읽어, 두 형식에서 포인트와 스폰된 사람·무기·소물이 같은지 대조한다.
    /// 이어서 직접 만든 PD2 로 넓은 파라미터, 추가 파라미터, 방향, 이벤트 줄 수, 깨진 파일을 확인한다. 파일은 임시 폴더에 쓰고 끝나면 지운다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/pd2_check.tscn
    /// 명령행 인자("--" 뒤): --convert 입력.pd1 출력.pd2 는 점검 대신 파일 하나를 변환하고 종료한다 (경로는 exe 폴더 기준. 같은 이름의 .msg 도 복사한다).
    /// </summary>
    public partial class PD2Check : Node
    {
        private const float k_angleTolerance = 1e-3f;

        private int m_checks;
        private readonly List<string> m_problems = new List<string>();
        private string m_workFolder;

        /// <summary>
        /// 로드된 포인트와 스폰 결과를 대조용으로 떠 둔 것.
        /// </summary>
        private class Snapshot
        {
            public RawPointData[] points;
            public string[] humans;
            public float[] humanYaws;
            public string[] objects;
            public float[] objectYaws;
            public int weapons;
            public int[] eventEntryIds;
        }

        public override void _Ready()
        {
            string[] args = OS.GetCmdlineUserArgs();
            int convertArg = Array.IndexOf(args, "--convert");
            if (convertArg >= 0)
            {
                bool ok = convertArg + 2 < args.Length && Convert(args[convertArg + 1], args[convertArg + 2]);
                if (!ok) GD.Print("사용법: --convert 입력.pd1 출력.pd2 (경로는 exe 폴더 기준)");
                Finish(ok);
                return;
            }

            AIController.Enabled = false;
            m_workFolder = Path.Combine(Path.GetTempPath(), "godotxops_pd2_check");
            Directory.CreateDirectory(m_workFolder);

            int maps = CheckAllMissions();
            CheckExtended();
            CheckBrokenFiles();

            MapLoader.UnloadPointData();
            Directory.Delete(m_workFolder, true);

            GD.Print($"PD2 점검 {m_checks}항목 (미션 {maps}개 대조) — 문제 {m_problems.Count}건");
            foreach (string problem in m_problems)
            {
                GD.Print($"문제: {problem}");
            }
            Finish(m_problems.Count == 0);
        }

        /// <summary>
        /// 관리 객체를 정리하고 종료한다. 노드를 대량으로 만들고 지운 직후 종료하면 간헐적으로 죽는 것을 막는다.
        /// </summary>
        /// <param name="ok">true 면 종료 코드 0.</param>
        private void Finish(bool ok)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit(ok ? 0 : 1);
        }

        /// <summary>
        /// 점검 한 건을 센다.
        /// </summary>
        /// <param name="ok">통과했으면 true.</param>
        /// <param name="what">실패했을 때 남길 설명.</param>
        private void Expect(bool ok, string what)
        {
            m_checks++;
            if (!ok) m_problems.Add(what);
        }

        /// <summary>
        /// PD1 파일 하나를 PD2 로 바꿔 쓴다. 같은 이름의 .msg 가 있으면 출력 옆에 복사한다.
        /// </summary>
        /// <param name="input">PD1 경로 (exe 폴더 기준).</param>
        /// <param name="output">PD2 경로 (exe 폴더 기준).</param>
        /// <returns>성공했으면 true.</returns>
        private static bool Convert(string input, string output)
        {
            string inputPath = GamePath.Resolve(input);
            string outputPath = GamePath.Resolve(output);
            if (inputPath == null || outputPath == null) return false;

            if (!MapLoader.ConvertPD1(inputPath, out PD2File file))
            {
                GD.Print($"PD1 을 읽지 못했습니다: {input}");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            if (!file.Write(outputPath, out string error))
            {
                GD.Print($"PD2 를 쓰지 못했습니다: {error}");
                return false;
            }

            string inputMessages = Path.ChangeExtension(inputPath, ".msg");
            string outputMessages = Path.ChangeExtension(outputPath, ".msg");
            bool copied = File.Exists(inputMessages) && !string.Equals(inputMessages, outputMessages, StringComparison.OrdinalIgnoreCase);
            if (copied) File.Copy(inputMessages, outputMessages, true);

            GD.Print($"변환: {input} → {output} (포인트 {file.points.Count}개{(copied ? ", .msg 복사" : string.Empty)})");
            return true;
        }

        /// <summary>
        /// 모든 미션의 PD1 을 PD2 로 바꿔, 파일을 쓰고 읽은 결과와 로드한 뒤의 포인트·스폰 결과가 같은지 본다.
        /// 랜덤 무기 포인트가 있으므로 두 번 다 같은 난수 씨앗으로 로드한다.
        /// </summary>
        /// <returns>대조한 미션 수.</returns>
        private int CheckAllMissions()
        {
            MissionData missionData = DataManager.Instance.MissionData;
            var entries = new List<(bool mif, int page, int index, string label)>();
            for (int i = 0; i < missionData.officialMissions.Count; i++)
            {
                entries.Add((false, 0, i, missionData.officialMissions[i].name));
            }
            for (int page = 0; page < missionData.addonMissions.Count; page++)
            {
                for (int i = 0; i < missionData.addonMissions[page].Count; i++)
                {
                    entries.Add((true, page, i, $"addon {page}/{missionData.addonMissions[page][i].name}"));
                }
            }

            string pd2Path = Path.Combine(m_workFolder, "map.pd2");
            string copyPath = Path.Combine(m_workFolder, "copy.pd2");
            string msgPath = Path.ChangeExtension(pd2Path, ".msg");
            int maps = 0;

            foreach ((bool mif, int page, int index, string label) in entries)
            {
                if (!MapLoader.LoadMissionData(index, mif, page)) continue;

                string pd1Path = MapLoader.Instance.MissionPD1Path;
                GameRandom.Reseed(1u);
                if (!MapLoader.LoadPointData(pd1Path))
                {
                    Expect(false, $"{label}: PD1 로드 실패");
                    continue;
                }
                Snapshot before = TakeSnapshot();
                int messages = MapLoader.MessageCount;

                if (!MapLoader.ConvertPD1(pd1Path, out PD2File file) || !file.Write(pd2Path, out _))
                {
                    Expect(false, $"{label}: PD2 변환 실패");
                    continue;
                }
                string pd1Messages = Path.ChangeExtension(pd1Path, ".msg");
                if (File.Exists(pd1Messages)) File.Copy(pd1Messages, msgPath, true);
                else File.Delete(msgPath);

                // 쓴 파일을 읽어 다시 쓰면 바이트가 같아야 한다. 추가 파라미터가 없으면 포인트 하나가 36바이트다.
                bool reread = PD2File.Read(pd2Path, out PD2File copy, out _) && copy.Write(copyPath, out _);
                Expect(reread && copy.points.Count == file.points.Count
                    && File.ReadAllBytes(pd2Path).AsSpan().SequenceEqual(File.ReadAllBytes(copyPath)),
                    $"{label}: PD2 를 읽어 다시 쓴 파일이 다름");
                Expect(new FileInfo(pd2Path).Length == 8 + 4 + 4 * 3 + 4 + 36L * file.points.Count, $"{label}: PD2 파일 크기가 레이아웃과 다름");

                GameRandom.Reseed(1u);
                if (!MapLoader.LoadPointData(pd2Path))
                {
                    Expect(false, $"{label}: PD2 로드 실패");
                    continue;
                }
                Snapshot after = TakeSnapshot();

                string difference = Compare(before, after);
                Expect(difference == null, $"{label}: PD1 과 PD2 가 다름 — {difference}");
                Expect(MapLoader.MessageCount == messages, $"{label}: 메시지 수 {messages} / {MapLoader.MessageCount}");
                maps++;
            }

            return maps;
        }

        /// <summary>
        /// 지금 로드된 포인트와 스폰된 사람·무기·소물을 떠 둔다.
        /// </summary>
        /// <returns>떠 둔 상태.</returns>
        private static Snapshot TakeSnapshot()
        {
            var points = new List<RawPointData>();
            for (int type = 0; type < 20; type++)
            {
                // 종류별·식별번호별 첫 포인트만 조회할 수 있으므로, 조회 가능한 번호 범위를 훑는다 (PD1 의 번호는 0 에서 255 사이다).
                for (int id = 0; id < 256; id++)
                {
                    RawPointData point = MapLoader.GetPoint(type, id);
                    if (point != null) points.Add(point);
                }
            }

            IReadOnlyList<Human> humans = MapLoader.Humans;
            IReadOnlyList<SmallObject> objects = MapLoader.SmallObjects;
            var snapshot = new Snapshot
            {
                points = points.ToArray(),
                humans = new string[humans.Count],
                humanYaws = new float[humans.Count],
                objects = new string[objects.Count],
                objectYaws = new float[objects.Count],
                weapons = WeaponManager.Instance.CountActive(),
                eventEntryIds = new List<int>(MapLoader.EventEntryIds).ToArray(),
            };
            for (int i = 0; i < humans.Count; i++)
            {
                Human human = humans[i];
                snapshot.humans[i] = $"{human.Controller.Position}/{human.Team}/{human.Identifier}/{human.PathStartId}/{human.GetWeapon(0).WeaponIndex}/{human.GetWeapon(1).WeaponIndex}/{human == MapLoader.Player}";
                snapshot.humanYaws[i] = human.Controller.Yaw;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                snapshot.objects[i] = $"{objects[i].LogicPosition}/{objects[i].Identifier}";
                snapshot.objectYaws[i] = Mathf.RadToDeg(objects[i].Rotation.Y);
            }
            return snapshot;
        }

        /// <summary>
        /// 두 상태를 대조한다. 방향은 180° 를 뺐다가 더하는 과정의 반올림만큼 다를 수 있어 허용 오차를 둔다.
        /// </summary>
        /// <param name="a">PD1 에서 뜬 상태.</param>
        /// <param name="b">PD2 에서 뜬 상태.</param>
        /// <returns>처음 발견한 차이의 설명. 같으면 null.</returns>
        private static string Compare(Snapshot a, Snapshot b)
        {
            if (a.points.Length != b.points.Length) return $"조회되는 포인트 수 {a.points.Length} / {b.points.Length}";
            for (int i = 0; i < a.points.Length; i++)
            {
                RawPointData p = a.points[i];
                RawPointData q = b.points[i];
                if (p.position != q.position || p.param0 != q.param0 || p.param1 != q.param1 || p.param2 != q.param2 || p.param3 != q.param3)
                {
                    return $"포인트 {p.param0}/{p.param3} 의 위치나 파라미터";
                }
                if (Mathf.Abs(p.look - q.look) > k_angleTolerance) return $"포인트 {p.param0}/{p.param3} 의 방향 {p.look} / {q.look}";
                if (q.extra == null || q.extra.Length != 0) return $"포인트 {p.param0}/{p.param3} 에 추가 파라미터가 있음";
            }

            if (a.humans.Length != b.humans.Length) return $"사람 수 {a.humans.Length} / {b.humans.Length}";
            for (int i = 0; i < a.humans.Length; i++)
            {
                if (a.humans[i] != b.humans[i]) return $"사람 {i}: {a.humans[i]} / {b.humans[i]}";
                if (Mathf.Abs(Coord.DeltaAngle(a.humanYaws[i], b.humanYaws[i])) > k_angleTolerance) return $"사람 {i} 의 방향";
            }

            if (a.objects.Length != b.objects.Length) return $"소물 수 {a.objects.Length} / {b.objects.Length}";
            for (int i = 0; i < a.objects.Length; i++)
            {
                if (a.objects[i] != b.objects[i]) return $"소물 {i}: {a.objects[i]} / {b.objects[i]}";
                if (Mathf.Abs(Coord.DeltaAngle(a.objectYaws[i], b.objectYaws[i])) > k_angleTolerance) return $"소물 {i} 의 방향";
            }

            if (a.weapons != b.weapons) return $"떨어진 무기 수 {a.weapons} / {b.weapons}";
            if (a.eventEntryIds.Length != 3 || !a.eventEntryIds.AsSpan().SequenceEqual(b.eventEntryIds)) return "이벤트 시작 번호";
            return null;
        }

        /// <summary>
        /// 직접 만든 PD2 로 PD1 에 없는 것을 확인한다: 255 를 넘는 파라미터와 식별번호, 추가 파라미터(정수·실수·불), 방향 규칙, 이벤트 줄 수.
        /// </summary>
        private void CheckExtended()
        {
            const int infoId = 100000;
            const int objectId = 300000;
            const int lineA = 70000;
            const int lineB = 70010;
            const float direction = 30f;

            WeaponParameterData weapons = DataManager.Instance.WeaponParameterData;
            int weaponIndex = weapons.weaponData.FindIndex(weapon => weapons.weaponData.IndexOf(weapon) != weapons.weaponGeneralData.noneWeaponIndex && weapon.magazineSize > 0);

            var file = new PD2File();
            file.eventEntryIds.Add(lineA);
            file.eventEntryIds.Add(lineB);
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 0, id = infoId });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = infoId, param2 = -1, id = 0, direction = direction, position = new Vector3(0f, 500f, 0f) });
            // 다른 팀이 한 명도 없으면 자동 판정이 바로 미션을 끝내서 이벤트를 볼 수 없다.
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 1, id = infoId + 1 });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = infoId + 1, param2 = -1, id = 500, position = new Vector3(100f, 500f, 0f) });
            file.points.Add(new PD2Point { type = MapLoader.PointSmallObject, param1 = 0, param2 = 0, id = objectId, direction = direction, position = new Vector3(5f, 500f, 0f) });
            file.points.Add(new PD2Point { type = MapLoader.PointWeapon, param1 = weaponIndex, param2 = 1000, id = 0, direction = direction, position = new Vector3(10f, 500f, 0f) });
            file.points.Add(new PD2Point
            {
                type = (int)EventType.Message, param1 = 0, param2 = lineA + 1, id = lineA,
                extra = new[] { 7, PD2File.FloatCell(1.5f), 1, 0 },
            });
            file.points.Add(new PD2Point { type = (int)EventType.MissionComplete, id = lineA + 1 });
            file.points.Add(new PD2Point { type = (int)EventType.WaitTime, param1 = 1000, param2 = lineB + 1, id = lineB });

            string pd2Path = Path.Combine(m_workFolder, "extended.pd2");
            File.WriteAllLines(Path.ChangeExtension(pd2Path, ".msg"), new[] { "hello" });
            if (weaponIndex < 0 || !file.Write(pd2Path, out _) || !MapLoader.LoadPointData(pd2Path))
            {
                Expect(false, "확장 점검용 PD2 로드 실패");
                return;
            }

            // 넓은 파라미터: 255 를 넘는 식별번호로 사람 정보와 소물을 찾는다.
            Human human = MapLoader.Player;
            Expect(MapLoader.HumanCount == 2 && human != null && human.Identifier == 0 && MapLoader.SearchHuman(500)?.Team == 1,
                "255 를 넘는 사람 정보 번호로 사람이 스폰되지 않음");
            SmallObject smallObject = MapLoader.SearchSmallObject(objectId);
            Expect(smallObject != null, "255 를 넘는 식별번호의 소물을 찾지 못함");
            Expect(WeaponManager.Instance.CountActive() == 1, "무기 포인트의 무기가 놓이지 않음");

            // 방향: PD2 의 값이 곧 그 자리에 놓이는 것의 yaw 다. 사람과 무기는 look 이 같고 소물은 look 이 180° 크다.
            Expect(human != null && Mathf.Abs(Coord.DeltaAngle(human.Controller.Yaw, direction)) < k_angleTolerance, "사람의 yaw 가 PD2 의 방향과 다름");
            Expect(smallObject != null && Mathf.Abs(Coord.DeltaAngle(Mathf.RadToDeg(smallObject.Rotation.Y), -direction)) < k_angleTolerance,
                "소물의 yaw 가 PD2 의 방향과 다름");
            RawPointData objectPoint = MapLoader.GetPoint(MapLoader.PointSmallObject, objectId);
            Expect(objectPoint != null && Mathf.Abs(objectPoint.look - (direction + 180f)) < k_angleTolerance, "소물 포인트의 look 이 방향 + 180 이 아님");
            RawPointData weaponPoint = MapLoader.GetPoint(MapLoader.PointWeapon, 0);
            Expect(weaponPoint != null && Mathf.Abs(weaponPoint.look - direction) < k_angleTolerance, "무기 포인트의 look 이 방향과 다름");

            // 추가 파라미터: 정수, 실수, 불, 없는 칸의 기본값.
            RawPointData message = MapLoader.GetEventPoint(lineA);
            Expect(message != null && message.extra.Length == 4 && message.GetExtraInt(0) == 7 && Mathf.Abs(message.GetExtraFloat(1) - 1.5f) < 1e-6f
                && message.GetExtraBool(2) && !message.GetExtraBool(3), "추가 파라미터를 정수·실수·불로 읽은 값이 다름");
            Expect(message != null && message.GetExtraInt(9, 42) == 42 && message.GetExtraFloat(9, 2.5f) == 2.5f && message.GetExtraBool(9, true),
                "없는 추가 파라미터 칸이 기본값을 돌려주지 않음");
            Expect(MapLoader.GetPoint(MapLoader.PointHuman, 0)?.extra.Length == 0, "추가 파라미터가 없는 포인트의 배열이 비어 있지 않음");

            // 이벤트 줄: 파일이 정한 두 줄이 각자의 시작 번호에서 출발한다.
            IReadOnlyList<int> entryIds = MapLoader.EventEntryIds;
            Expect(entryIds.Count == 2 && entryIds[0] == lineA && entryIds[1] == lineB, "이벤트 시작 번호가 파일과 다름");
            EventManager events = EventManager.Instance;
            events.BeginMission();
            Expect(events.LineCount == 2 && events.LineCursor(0) == lineA && events.LineCursor(1) == lineB, "이벤트 줄 수나 시작 위치가 파일과 다름");
            SimClock.Step();
            Expect(events.MessageId == 0 && events.MessageText == "hello" && events.Result == (int)MissionResult.Complete,
                "255 를 넘는 번호의 이벤트 줄이 메시지와 미션 완료까지 가지 않음");
            Expect(events.LineCursor(1) == lineB, "시간 대기 줄이 기다리지 않음");

            // 이벤트 줄이 없는 파일.
            file.eventEntryIds.Clear();
            bool loaded = file.Write(pd2Path, out _) && MapLoader.LoadPointData(pd2Path);
            events.BeginMission();
            SimClock.Step();
            Expect(loaded && events.LineCount == 0 && events.Result == (int)MissionResult.InProgress, "이벤트 줄이 없는 PD2 가 그대로 진행되지 않음");

            CheckLimits(pd2Path);

            // PD1 은 항상 세 줄이고, 포인트를 내리면 기본값으로 돌아간다.
            MapLoader.UnloadPointData();
            entryIds = MapLoader.EventEntryIds;
            Expect(entryIds.Count == 3 && entryIds[0] == 156 && entryIds[1] == 146 && entryIds[2] == 136, "포인트를 내린 뒤 이벤트 시작 번호가 기본값이 아님");
            Expect(MapLoader.LoadMissionData(0, false, 0) && MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path)
                && MapLoader.EventEntryIds.Count == 3 && MapLoader.GetPoint(MapLoader.PointHuman, 0)?.extra.Length == 0,
                "PD1 의 이벤트 줄이 셋이 아니거나 추가 파라미터가 있음");
        }

        /// <summary>
        /// 원본 형식에만 있는 제한이 확장 형식에는 없는지 확인한다: 메시지 16개, 한 틱에 한 줄이 처리하는 이벤트 6개.
        /// 바로 넘어가는 이벤트끼리 고리를 이뤄도 틱이 끝나야 한다.
        /// </summary>
        /// <param name="pd2Path">점검용 PD2 를 쓸 전체 경로.</param>
        private void CheckLimits(string pd2Path)
        {
            const int farType = 250;
            const int lineA = 1000;
            const int lineB = 2000;
            const int chainLength = 10;
            const int messageIndex = 20;

            var file = new PD2File();
            file.eventEntryIds.Add(lineA);
            file.eventEntryIds.Add(lineB);
            // 자동 판정이 미션을 끝내지 않게 두 팀을 한 명씩 둔다.
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 0, id = 1 });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 1, param2 = -1, id = 0, position = new Vector3(0f, 500f, 0f) });
            file.points.Add(new PD2Point { type = MapLoader.PointHumanInfo, param1 = 0, param2 = 1, id = 2 });
            file.points.Add(new PD2Point { type = MapLoader.PointHuman, param1 = 2, param2 = -1, id = 500, position = new Vector3(100f, 500f, 0f) });

            // 줄 A: 바로 넘어가는 이벤트 열 개 → 메시지 20번 → 시간 대기.
            for (int i = 0; i < chainLength; i++)
            {
                file.points.Add(new PD2Point { type = (int)EventType.ChangeTeam, param1 = 9999, param2 = lineA + i + 1, id = lineA + i });
            }
            file.points.Add(new PD2Point { type = (int)EventType.Message, param1 = messageIndex, param2 = lineA + chainLength + 1, id = lineA + chainLength });
            file.points.Add(new PD2Point { type = (int)EventType.WaitTime, param1 = 1000, param2 = lineA + chainLength + 2, id = lineA + chainLength + 1 });

            // 줄 B: 바로 넘어가는 이벤트 둘이 서로를 가리킨다.
            file.points.Add(new PD2Point { type = (int)EventType.ChangeTeam, param1 = 9999, param2 = lineB + 1, id = lineB });
            file.points.Add(new PD2Point { type = (int)EventType.ChangeTeam, param1 = 9999, param2 = lineB, id = lineB + 1 });

            var messages = new string[messageIndex + 1];
            for (int i = 0; i < messages.Length; i++) messages[i] = $"message {i}";
            File.WriteAllLines(Path.ChangeExtension(pd2Path, ".msg"), messages);

            if (!file.Write(pd2Path, out _) || !MapLoader.LoadPointData(pd2Path))
            {
                Expect(false, "제한 점검용 PD2 로드 실패");
                return;
            }

            Expect(MapLoader.PointDataExtended, "PD2 를 로드했는데 확장 형식으로 표시되지 않음");

            EventManager events = EventManager.Instance;
            events.BeginMission();
            SimClock.Step();
            Expect(events.LineCursor(0) == lineA + chainLength + 1, $"한 틱에 이벤트 {chainLength}개와 메시지를 지나 시간 대기까지 가지 않음 (지금 {events.LineCursor(0)})");
            Expect(events.MessageId == messageIndex && events.MessageText == $"message {messageIndex}", "16번 이상의 메시지가 표시되지 않음");
            Expect(events.Result == (int)MissionResult.InProgress && (events.LineCursor(1) == lineB || events.LineCursor(1) == lineB + 1), "이벤트 고리가 있는 줄의 상태가 다름");
            for (int tick = 0; tick < 5; tick++) SimClock.Step();
            Expect(events.LineCursor(0) == lineA + chainLength + 1 && events.Result == (int)MissionResult.InProgress, "이벤트 고리가 있는 미션이 계속 돌지 않음");

            // 20 이상의 종류는 스크립트 이벤트다. 어느 묶음에도 등록되지 않은 번호를 쓰는 맵은 로드하지 않는다.
            file.points.Add(new PD2Point { type = farType, param1 = 7, id = 42 });
            int errors = Debugger.ErrorCount;
            Expect(file.Write(pd2Path, out _) && !MapLoader.LoadPointData(pd2Path) && Debugger.FirstErrorSince(errors).StartsWith("A point uses event type"),
                "등록되지 않은 종류 번호의 포인트가 있는 PD2 가 로드됨");
        }

        /// <summary>
        /// 깨진 파일을 읽지 않는지 확인한다.
        /// </summary>
        private void CheckBrokenFiles()
        {
            string goodPath = Path.Combine(m_workFolder, "good.pd2");
            string badPath = Path.Combine(m_workFolder, "bad.pd2");

            var file = new PD2File();
            file.eventEntryIds.Add(1);
            file.points.Add(new PD2Point { type = MapLoader.PointAIPath, id = 5, extra = new[] { 1, 2, 3 } });
            file.Write(goodPath, out _);
            byte[] bytes = File.ReadAllBytes(goodPath);
            Expect(bytes.Length == 8 + 4 + 4 + 4 + 36 + 12, "추가 파라미터가 있는 PD2 의 크기가 레이아웃과 다름");

            byte[] wrongMagic = (byte[])bytes.Clone();
            wrongMagic[7] = (byte)'X';
            File.WriteAllBytes(badPath, wrongMagic);
            Expect(!PD2File.Read(badPath, out _, out string magicError) && magicError != null, "매직이 다른 파일을 읽음");

            File.WriteAllBytes(badPath, bytes.AsSpan(0, bytes.Length - 4).ToArray());
            Expect(!PD2File.Read(badPath, out _, out _), "추가 파라미터가 잘린 파일을 읽음");

            File.WriteAllBytes(badPath, bytes.AsSpan(0, 30).ToArray());
            Expect(!PD2File.Read(badPath, out _, out _), "포인트가 잘린 파일을 읽음");

            File.WriteAllBytes(badPath, bytes.AsSpan(0, 10).ToArray());
            Expect(!PD2File.Read(badPath, out _, out _), "머리말이 잘린 파일을 읽음");

            Expect(!MapLoader.LoadPointData(badPath) && MapLoader.HumanCount == 0, "깨진 PD2 를 로드함");
            Expect(PD2File.Read(goodPath, out PD2File good, out _) && good.points.Count == 1 && good.points[0].extra.Length == 3 && good.eventEntryIds[0] == 1,
                "정상 파일을 읽지 못함");
        }
    }
}

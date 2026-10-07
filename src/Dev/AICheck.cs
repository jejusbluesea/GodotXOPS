using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. AI(탐색·청각·경계·전투·조준 예측·무기 운용·좀비·경로)와 미션 이벤트·판정을 수치로 확인하고 종료한다. 문제가 있으면 종료 코드 1.
    /// 점검용 PD1 파일을 임시 폴더에 직접 만들어 로드한다. 사람들은 블록이 없는 공중에 놓고 이동 틱 없이 AI 틱과 무기 틱만 직접 돌린다.
    /// 벽에 가려지는 시야만 실제 미션의 블록을 쓴다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/ai_check.tscn
    /// </summary>
    public partial class AICheck : Node
    {
        // 블록이 없는 공중의 높이 (m).
        private const float k_arenaY = 500f;
        // 플레이어 자리. 다른 사람들이 보거나 듣지 못할 만큼 멀다.
        private static readonly Vector3 s_playerSpot = new Vector3(900f, k_arenaY, 900f);

        private struct Point
        {
            public Vector3 position;
            public float yaw;
            public int p0;
            public int p1;
            public int p2;
            public int p3;
        }

        private readonly List<string> m_problems = new List<string>();
        private readonly List<Point> m_points = new List<Point>();
        private int m_checks;
        private int m_nextInfoId;

        private int m_armedHuman;
        private int m_zombieHuman;
        private string m_pd1Path;

        private int m_messageSignals;
        private int m_endSignals;
        private bool m_endComplete;

        public override void _Ready()
        {
            AIController.Enabled = true;
            AIController.DrivePlayer = false;
            m_pd1Path = Path.Combine(Path.GetTempPath(), "godotxops_ai_check.pd1");

            EventManager.Instance.MessageShown += (id, text) => m_messageSignals++;
            EventManager.Instance.MissionEnded += complete =>
            {
                m_endSignals++;
                m_endComplete = complete;
            };

            if (!FindHumanTypes())
            {
                GD.Print("문제: 점검에 쓸 사람 종류(무장한 사람, 좀비)를 데이터에서 찾지 못함");
                GetTree().Quit(1);
                return;
            }

            // 소리가 AI 에게 전달되려면 시뮬레이션이 켜져 있어야 한다. 틱은 직접 돌린다.
            SimClock.TickEnabled = true;

            CheckSight();
            CheckHearing();
            CheckCaution();
            CheckLeadAim();
            CheckWeaponControl();
            CheckGrenade();
            CheckZombie();
            CheckPath();
            CheckEvents();
            CheckAutoJudge();
            CheckClone();
            CheckWallSight();

            SimClock.TickEnabled = false;
            MapLoader.UnloadPointData();
            File.Delete(m_pd1Path);
            File.Delete(Path.ChangeExtension(m_pd1Path, ".msg"));

            GD.Print($"AI 점검 {m_checks}항목 — 문제 {m_problems.Count}건");
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
        /// 점검에 쓸 사람 종류를 데이터에서 고른다: 총을 든 좀비 아닌 사람(AI 레벨 2 이상)과 좀비.
        /// </summary>
        /// <returns>둘 다 찾았으면 true.</returns>
        private bool FindHumanTypes()
        {
            HumanParameterData human = DataManager.Instance.HumanParameterData;
            WeaponParameterData weapon = DataManager.Instance.WeaponParameterData;
            m_armedHuman = -1;
            m_zombieHuman = -1;

            for (int i = 0; i < human.humanData.Count; i++)
            {
                HumanData data = human.humanData[i];
                if (data.typeIndex < 0 || data.typeIndex >= human.humanTypeData.Count) continue;

                bool zombie = human.humanTypeData[data.typeIndex].zombie;
                if (zombie && m_zombieHuman < 0) m_zombieHuman = i;

                int main = data.weaponIndex1;
                bool gun = main >= 0 && main < weapon.weaponData.Count
                    && main != weapon.weaponGeneralData.noneWeaponIndex
                    && main != weapon.weaponGeneralData.grenadeWeaponIndex
                    && !weapon.weaponGeneralData.caseWeaponIndex.Contains(main)
                    && weapon.weaponData[main].fireRate > 0f;
                if (!zombie && gun && data.aiIndex >= 2 && m_armedHuman < 0) m_armedHuman = i;
            }
            return m_armedHuman >= 0 && m_zombieHuman >= 0;
        }

        /// <summary>
        /// 만들 PD1 의 포인트 목록을 비우고 플레이어(식별번호 0, 팀 0)를 멀리 놓는다.
        /// </summary>
        private void BeginPoints()
        {
            m_points.Clear();
            m_nextInfoId = 200;
            AddHuman(m_armedHuman, 0, 0, s_playerSpot, 0f, 255);
        }

        /// <summary>
        /// 사람 포인트와 그 정보 포인트를 목록에 넣는다.
        /// </summary>
        /// <param name="humanIndex">사람 데이터 번호.</param>
        /// <param name="team">팀.</param>
        /// <param name="identifier">식별번호.</param>
        /// <param name="position">위치 (Godot 좌표).</param>
        /// <param name="yaw">방향 yaw (도).</param>
        /// <param name="pathStart">첫 경로의 식별번호.</param>
        private void AddHuman(int humanIndex, int team, int identifier, Vector3 position, float yaw, int pathStart)
        {
            int infoId = m_nextInfoId++;
            m_points.Add(new Point { position = position, yaw = yaw, p0 = MapLoader.PointHuman, p1 = infoId, p2 = pathStart, p3 = identifier });
            m_points.Add(new Point { position = position, p0 = MapLoader.PointHumanInfo, p1 = humanIndex, p2 = team, p3 = infoId });
        }

        private void AddPoint(int p0, int p1, int p2, int p3, Vector3 position, float yaw = 0f)
        {
            m_points.Add(new Point { position = position, yaw = yaw, p0 = p0, p1 = p1, p2 = p2, p3 = p3 });
        }

        /// <summary>
        /// 포인트 목록을 PD1 파일로 쓰고 로드한다. 좌표와 방향은 원본 형식으로 되돌려 쓴다.
        /// </summary>
        /// <param name="messages">같은 이름의 .msg 파일에 쓸 메시지. null 이면 쓰지 않는다.</param>
        /// <returns>로드에 성공했으면 true.</returns>
        private bool LoadPoints(string[] messages = null)
        {
            using (var writer = new BinaryWriter(File.Create(m_pd1Path)))
            {
                writer.Write((short)m_points.Count);
                foreach (Point point in m_points)
                {
                    writer.Write(-point.position.X / Coord.Scale);
                    writer.Write(point.position.Y / Coord.Scale);
                    writer.Write(point.position.Z / Coord.Scale);
                    writer.Write(Mathf.DegToRad(point.yaw - 180f));
                    writer.Write((byte)point.p0);
                    writer.Write((byte)point.p1);
                    writer.Write((byte)point.p2);
                    writer.Write((byte)point.p3);
                }
            }

            string msgPath = Path.ChangeExtension(m_pd1Path, ".msg");
            if (messages != null) File.WriteAllLines(msgPath, messages);
            else File.Delete(msgPath);

            GameRandom.Reseed(1u);
            return MapLoader.LoadPointData(m_pd1Path);
        }

        /// <summary>
        /// 한 사람의 AI 틱과 무기 틱을 한 번 돌린다. 이동 틱은 돌리지 않는다.
        /// </summary>
        /// <param name="human">대상.</param>
        private static void TickAI(Human human)
        {
            human.Brain.Tick(human.ConsumeThreatHeard());
            human.TickWeapon();
        }

        /// <summary>
        /// 조건이 될 때까지 한 사람의 AI 를 돌린다.
        /// </summary>
        /// <param name="human">대상.</param>
        /// <param name="maxTicks">최대 틱 수.</param>
        /// <param name="done">끝내는 조건.</param>
        /// <returns>조건이 된 틱 수. 끝까지 안 됐으면 −1.</returns>
        private static int TickUntil(Human human, int maxTicks, Func<bool> done)
        {
            for (int tick = 1; tick <= maxTicks; tick++)
            {
                TickAI(human);
                if (done()) return tick;
            }
            return -1;
        }

        private static Vector3 Arena(float x, float z)
        {
            return new Vector3(x, k_arenaY, z);
        }

        /// <summary>
        /// 시야: 정면의 적은 발견해 전투로 들어가 쏘고, 등 뒤의 적과 같은 팀은 발견하지 못한다. 적이 죽으면 경계로 돌아간다.
        /// </summary>
        private void CheckSight()
        {
            BeginPoints();
            // 정면 10 m 에 적.
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 2, Arena(0f, -10f), 180f, 255);
            // 등 뒤 10 m 에 적.
            AddHuman(m_armedHuman, 1, 3, Arena(200f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 4, Arena(200f, 10f), 0f, 255);
            // 정면에 같은 팀.
            AddHuman(m_armedHuman, 1, 5, Arena(400f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 1, 6, Arena(400f, -10f), 180f, 255);
            if (!LoadPoints())
            {
                Expect(false, "시야 점검용 포인트 로드 실패");
                return;
            }

            Human front = MapLoader.SearchHuman(1);
            Human frontEnemy = MapLoader.SearchHuman(2);
            Expect(front.Brain.Mode == AIBattleMode.Normal && front.Brain.Navi.Mode == AIMoveMode.Null, "경로 없는 사람의 초기 상태가 평상시·경로 없음이 아님");

            int toCaution = TickUntil(front, 600, () => front.Brain.Mode != AIBattleMode.Normal);
            Expect(toCaution > 0 && front.Brain.Mode == AIBattleMode.Caution, $"정면의 적을 보고 경계로 들어가지 않음 (틱 {toCaution}, 상태 {front.Brain.Mode})");

            int toAction = TickUntil(front, 10, () => front.Brain.Mode == AIBattleMode.Action);
            Expect(toAction > 0 && front.Brain.Enemy == frontEnemy, $"경계에서 전투로 들어가지 않음 (틱 {toAction})");

            int magazine = front.CurrentWeapon.Magazine;
            int toFire = TickUntil(front, 600, () => front.CurrentWeapon.Magazine < magazine || front.IsReloading);
            Expect(toFire > 0, "전투 중 600틱 동안 쏘지 않음");
            GD.Print($"시야: 발견까지 {toCaution}틱, 첫 발사까지 {toFire}틱");

            // 적이 죽으면 다음 틱에 전투가 끝난다.
            if (front.Brain.Mode != AIBattleMode.Action) TickUntil(front, 1200, () => front.Brain.Mode == AIBattleMode.Action);
            frontEnemy.ApplyDamage(frontEnemy.HP);
            TickAI(front);
            Expect(front.Brain.Mode == AIBattleMode.Caution && front.Brain.Enemy == null, $"적이 죽었는데 전투가 끝나지 않음 (상태 {front.Brain.Mode})");

            Human back = MapLoader.SearchHuman(3);
            TickUntil(back, 400, () => back.Brain.Mode != AIBattleMode.Normal);
            Expect(back.Brain.Mode == AIBattleMode.Normal, "등 뒤의 적을 발견함");

            Human ally = MapLoader.SearchHuman(5);
            TickUntil(ally, 400, () => ally.Brain.Mode != AIBattleMode.Normal);
            Expect(ally.Brain.Mode == AIBattleMode.Normal, "같은 팀을 적으로 봄");

            // 비전투 상태에서는 정면의 적도 찾지 않는다.
            Human passive = MapLoader.SearchHuman(4);
            passive.Controller.SetInput(new HumanInput { yaw = 180f, pitch = 0f });
            passive.Brain.SetNoFight(true);
            TickUntil(passive, 400, () => passive.Brain.Mode != AIBattleMode.Normal);
            Expect(passive.Brain.Mode == AIBattleMode.Normal, "비전투 상태인데 적을 발견함");
        }

        /// <summary>
        /// 청각과 피격: 총성·달리는 발소리를 들으면 경계로 들어간다. 같은 팀의 발소리, 걷는 소리, 먼 소리는 듣지 못한다. 맞으면 맞은 쪽을 돌아본다.
        /// </summary>
        private void CheckHearing()
        {
            HumanAIParameterData ai = DataManager.Instance.HumanParameterData.humanAIParameterData;

            BeginPoints();
            // 듣는 사람들은 모두 소리 나는 쪽을 등지고 있다.
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 2, Arena(0f, 8f), 0f, 255);
            AddHuman(m_armedHuman, 1, 3, Arena(200f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 4, Arena(200f, ai.aiHearFootstepForward - 0.5f), 0f, 255);
            AddHuman(m_armedHuman, 1, 5, Arena(200f, ai.aiHearFootstepForward * 2f), 0f, 255);
            AddHuman(m_armedHuman, 1, 6, Arena(400f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 7, Arena(400f, 8f), 0f, 255);
            if (!LoadPoints())
            {
                Expect(false, "청각 점검용 포인트 로드 실패");
                return;
            }

            Human listener = MapLoader.SearchHuman(1);
            Human shooter = MapLoader.SearchHuman(2);
            TickAI(listener);
            Expect(listener.Brain.Mode == AIBattleMode.Normal, "아무 소리도 없는데 경계함");
            bool fired = shooter.ShotWeapon();
            TickAI(listener);
            Expect(fired && listener.Brain.Mode == AIBattleMode.Caution, $"8 m 뒤의 총성을 듣고 경계하지 않음 (발사 {fired}, 상태 {listener.Brain.Mode})");
            Expect(listener.Brain.Enemy == null, "소리만 들었는데 표적이 생김");

            Human near = MapLoader.SearchHuman(3);
            Human runner = MapLoader.SearchHuman(4);
            Human allyRunner = MapLoader.SearchHuman(5);

            WorldSound.EmitFootstep(runner, FootstepKind.Walk);
            WorldSound.EmitFootstep(runner, FootstepKind.Jump);
            WorldSound.EmitFootstep(runner, FootstepKind.Landing);
            TickAI(near);
            Expect(near.Brain.Mode == AIBattleMode.Normal, "걷기·점프·착지 소리에 경계함");

            runner.Controller.Teleport(near.Controller.Position + new Vector3(0f, 0f, ai.aiHearFootstepForward + 2f));
            WorldSound.EmitFootstep(runner, FootstepKind.Forward);
            TickAI(near);
            Expect(near.Brain.Mode == AIBattleMode.Normal, "듣는 거리 밖의 달리는 소리에 경계함");

            runner.Controller.Teleport(near.Controller.Position + new Vector3(0f, 0f, ai.aiHearFootstepBack + 0.3f));
            WorldSound.EmitFootstep(runner, FootstepKind.Back);
            TickAI(near);
            Expect(near.Brain.Mode == AIBattleMode.Normal, "후진 달리기 소리를 전진 거리로 들음");

            allyRunner.Controller.Teleport(near.Controller.Position + new Vector3(0f, 0f, 1f));
            WorldSound.EmitFootstep(allyRunner, FootstepKind.Forward);
            TickAI(near);
            Expect(near.Brain.Mode == AIBattleMode.Normal, "같은 팀의 발소리에 경계함");

            runner.Controller.Teleport(near.Controller.Position + new Vector3(0f, 0f, ai.aiHearFootstepForward - 0.5f));
            WorldSound.EmitFootstep(runner, FootstepKind.Forward);
            TickAI(near);
            Expect(near.Brain.Mode == AIBattleMode.Caution, "적이 달리는 소리를 듣고 경계하지 않음");

            // 맞으면 쏜 쪽을 돌아본다. 적을 찾아 전투로 넘어가지 않게 비전투로 둔다.
            Human victim = MapLoader.SearchHuman(6);
            Human attacker = MapLoader.SearchHuman(7);
            victim.Brain.SetNoFight(true);
            Vector3 toAttacker = attacker.Controller.Position - victim.Controller.Position;
            float attackerYaw = Mathf.RadToDeg(Mathf.Atan2(toAttacker.X, -toAttacker.Z));
            victim.SetHitYaw(attackerYaw + 180f);
            TickAI(victim);
            Expect(victim.Brain.Mode == AIBattleMode.Caution, "맞았는데 경계하지 않음");
            int toFace = TickUntil(victim, 300, () => Mathf.Abs(Coord.DeltaAngle(victim.Controller.Yaw, attackerYaw)) < 5f);
            Expect(toFace > 0, $"맞은 쪽을 돌아보지 않음 (yaw {victim.Controller.Yaw:0.0}, 기대 {attackerYaw:0.0})");
            GD.Print($"청각: 맞은 쪽으로 도는 데 {toFace}틱");
        }

        /// <summary>
        /// 경계: 강제로 경계시키면 시간이 지나 평상시로 돌아오고, 경계 중에는 팔을 수평으로 든다. 경계 중 빈손이면 다른 슬롯의 무기를 든다.
        /// </summary>
        private void CheckCaution()
        {
            BeginPoints();
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 1, 2, Arena(200f, 0f), 0f, 255);
            if (!LoadPoints())
            {
                Expect(false, "경계 점검용 포인트 로드 실패");
                return;
            }

            Human human = MapLoader.SearchHuman(1);
            HumanAIParameterData ai = DataManager.Instance.HumanParameterData.humanAIParameterData;

            float restPitch = -DataManager.Instance.HumanParameterData.humanGeneralData.armAngleInitial;
            TickUntil(human, 60, () => false);
            Expect(Mathf.Abs(human.Controller.Pitch - restPitch) < 4f, $"평상시 팔 각도가 쉬는 각이 아님 ({human.Controller.Pitch:0.0}, 기대 {restPitch:0.0})");

            human.Brain.SetCautionMode();
            Expect(human.Brain.Mode == AIBattleMode.Caution && human.Brain.CautionCount == ai.aiCautionFrames, "강제 경계가 적용되지 않음");

            TickUntil(human, 60, () => false);
            Expect(human.Brain.Mode == AIBattleMode.Caution && Mathf.Abs(human.Controller.Pitch) < 3f, $"경계 중 팔이 수평이 아님 ({human.Controller.Pitch:0.0})");

            int toNormal = TickUntil(human, 2000, () => human.Brain.Mode == AIBattleMode.Normal);
            Expect(toNormal > 0, "경계가 끝나지 않음");
            // 160 에서 100 까지는 틱마다 줄고, 그 뒤는 1/50 확률이라 60틱보다는 걸려야 한다.
            Expect(toNormal < 0 || toNormal + 60 >= ai.aiCautionFrames - 100, $"경계가 너무 빨리 끝남 ({toNormal}틱)");
            GD.Print($"경계: 평상시 복귀까지 {toNormal + 60}틱");

            // 빈손으로 경계하면 탄이 있는 다른 슬롯으로 바꾼다.
            Human switcher = MapLoader.SearchHuman(2);
            int armedSlot = switcher.SelectWeapon;
            int otherSlot = (armedSlot + 1) % Human.WeaponSlotCount;
            int weaponIndex = switcher.CurrentWeapon.WeaponIndex;
            int none = DataManager.Instance.WeaponParameterData.weaponGeneralData.noneWeaponIndex;
            switcher.SetWeapon(otherSlot, none, 0, 0);
            TickUntil(switcher, 20, () => false);
            switcher.SetSelectWeapon(otherSlot);
            TickUntil(switcher, 20, () => false);
            Expect(switcher.SelectWeapon == otherSlot, "평상시인데 빈손에서 무기 슬롯으로 바꿈");
            switcher.Brain.SetCautionMode();
            TickUntil(switcher, 5, () => switcher.SelectWeapon == armedSlot);
            Expect(switcher.SelectWeapon == armedSlot && switcher.CurrentWeapon.WeaponIndex == weaponIndex, "경계 중 빈손인데 무기를 들지 않음");
        }

        /// <summary>
        /// 조준 예측: 가만히 있는 적은 정면으로, 옆으로 움직이는 적은 움직이는 쪽 앞을 겨눈다 (근거리 = 한 틱 이동량 × 1.5).
        /// </summary>
        private void CheckLeadAim()
        {
            const float distance = 10f;
            const float speed = 30f;

            BeginPoints();
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 2, Arena(0f, -distance), 180f, 255);
            AddHuman(m_armedHuman, 1, 3, Arena(200f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 4, Arena(200f, -distance), 180f, 255);
            if (!LoadPoints())
            {
                Expect(false, "조준 예측 점검용 포인트 로드 실패");
                return;
            }

            MapLoader.SearchHuman(4).Controller.AddKnockbackVector(Vector3.Right, speed);

            float still = AverageAimYaw(MapLoader.SearchHuman(1));
            float moving = AverageAimYaw(MapLoader.SearchHuman(3));
            float expected = Mathf.RadToDeg(Mathf.Atan2(speed * SimClock.FrameTime * 1.5f, distance));

            Expect(!float.IsNaN(still) && Mathf.Abs(still) < 2.5f, $"가만히 있는 적을 정면으로 겨누지 않음 (평균 yaw {still:0.00})");
            Expect(!float.IsNaN(moving) && Mathf.Abs(moving - expected) < 2.5f, $"움직이는 적을 앞질러 겨누지 않음 (평균 yaw {moving:0.00}, 기대 {expected:0.00})");
            GD.Print($"조준 예측: 정지 {still:0.00}°, 이동 {moving:0.00}° (기대 {expected:0.00}°)");
        }

        /// <summary>
        /// 전투에 들어간 뒤 조준이 자리 잡은 구간의 평균 yaw 를 구한다.
        /// </summary>
        /// <param name="human">대상.</param>
        /// <returns>평균 yaw (도). 전투에 들어가지 못했으면 NaN.</returns>
        private static float AverageAimYaw(Human human)
        {
            if (TickUntil(human, 800, () => human.Brain.Mode == AIBattleMode.Action) < 0) return float.NaN;

            TickUntil(human, 100, () => false);
            float sum = 0f;
            int count = 0;
            for (int tick = 0; tick < 200; tick++)
            {
                TickAI(human);
                if (human.Brain.Mode != AIBattleMode.Action) continue;
                sum += Coord.DeltaAngle(0f, human.Controller.Yaw);
                count++;
            }
            return count > 0 ? sum / count : float.NaN;
        }

        /// <summary>
        /// 무기 운용: 평상시에 탄창이 비면 바로 재장전하고, 남은 탄이 없으면 버린다.
        /// </summary>
        private void CheckWeaponControl()
        {
            BeginPoints();
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 1, 2, Arena(200f, 0f), 0f, 255);
            if (!LoadPoints())
            {
                Expect(false, "무기 운용 점검용 포인트 로드 실패");
                return;
            }

            Human reloader = MapLoader.SearchHuman(1);
            int weaponIndex = reloader.CurrentWeapon.WeaponIndex;
            int magazineSize = reloader.CurrentWeapon.Data.magazineSize;
            reloader.SetWeapon(reloader.SelectWeapon, weaponIndex, 0, magazineSize);
            reloader.Brain.Tick(false);
            Expect(reloader.IsReloading, "평상시에 탄창이 비었는데 재장전하지 않음");
            TickUntil(reloader, 400, () => !reloader.IsReloading);
            Expect(reloader.CurrentWeapon.Magazine == magazineSize, $"재장전 뒤 탄창이 차지 않음 ({reloader.CurrentWeapon.Magazine}/{magazineSize})");

            Human dropper = MapLoader.SearchHuman(2);
            int dropped = WeaponManager.Instance.CountActive();
            dropper.SetWeapon(dropper.SelectWeapon, weaponIndex, 0, 0);
            dropper.Brain.Tick(false);
            Expect(dropper.CurrentWeapon.IsNone && WeaponManager.Instance.CountActive() == dropped + 1, "탄이 하나도 없는 무기를 버리지 않음");
        }

        /// <summary>
        /// 수류탄: 수류탄을 든 채 전투에 들어가면 던지고, 수류탄이 남아 있는 동안에는 다른 슬롯의 총으로 바꾸지 않는다.
        /// </summary>
        private void CheckGrenade()
        {
            BeginPoints();
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 2, Arena(0f, -10f), 180f, 255);
            if (!LoadPoints())
            {
                Expect(false, "수류탄 점검용 포인트 로드 실패");
                return;
            }

            Human thrower = MapLoader.SearchHuman(1);
            int grenadeIndex = DataManager.Instance.WeaponParameterData.weaponGeneralData.grenadeWeaponIndex;
            int grenadeSlot = thrower.SelectWeapon;
            int gunSlot = (grenadeSlot + 1) % Human.WeaponSlotCount;
            thrower.SetWeapon(gunSlot, thrower.CurrentWeapon.WeaponIndex);
            thrower.SetWeapon(grenadeSlot, grenadeIndex);

            Weapon grenade = thrower.GetWeapon(grenadeSlot);
            int start = grenade.Magazine + grenade.Reserve;
            bool swapped = false;
            for (int tick = 0; tick < 600; tick++)
            {
                TickAI(thrower);
                Weapon held = thrower.GetWeapon(grenadeSlot);
                if (held.WeaponIndex != grenadeIndex || held.Magazine + held.Reserve == 0) break;
                if (thrower.SelectWeapon != grenadeSlot) swapped = true;
            }

            Weapon after = thrower.GetWeapon(grenadeSlot);
            int left = after.WeaponIndex == grenadeIndex ? after.Magazine + after.Reserve : 0;
            Expect(start > 0 && left < start, $"수류탄을 든 채 전투에 들어갔는데 던지지 않음 ({start} → {left}, 상태 {thrower.Brain.Mode})");
            Expect(!swapped, "수류탄이 남아 있는데 다른 무기로 바꿈");
            GD.Print($"수류탄: {start}개 중 {start - left}개 던짐, 중간 전환 {(swapped ? "있음" : "없음")}");
        }

        /// <summary>
        /// 좀비: 붙어 있는 적을 발견하면 근접 공격으로 데미지를 주고, 끌어당기고, 팔을 든다. 쏘지 않는다.
        /// </summary>
        private void CheckZombie()
        {
            BeginPoints();
            AddHuman(m_zombieHuman, 1, 1, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 0, 2, Arena(0f, -0.3f), 180f, 255);
            if (!LoadPoints())
            {
                Expect(false, "좀비 점검용 포인트 로드 실패");
                return;
            }

            Human zombie = MapLoader.SearchHuman(1);
            Human victim = MapLoader.SearchHuman(2);
            IntRange range = zombie.HumanTypeData.zombieMeleeDamageRange;
            float hpBefore = victim.HP;

            int toAction = TickUntil(zombie, 600, () => zombie.Brain.Mode == AIBattleMode.Action);
            Expect(toAction > 0, "좀비가 붙어 있는 적을 발견하지 못함");

            // 전투에 들어간 첫 틱에 공격 주기가 맞아 바로 한 번 때린다.
            TickAI(zombie);
            float damage = hpBefore - victim.HP;
            int weaponDamage = zombie.CurrentWeapon.IsNone ? 0 : (int)zombie.CurrentWeapon.Data.damage;
            Expect(damage >= range.min + weaponDamage && damage < range.max + weaponDamage,
                $"좀비 근접 데미지 {damage} (기대 {range.min + weaponDamage} 이상 {range.max + weaponDamage} 미만)");
            Expect(victim.Controller.MoveVelocity.Z > 0f, "좀비가 붙잡은 적을 끌어당기지 않음");
            Expect(victim.ConsumeHit(out _), "좀비에게 맞은 사람에게 피격 표시가 남지 않음");
            Expect(zombie.CurrentWeapon.IsNone == zombie.UnarmedArmDynamic, "전투 중인 맨손 좀비의 팔이 조준 방향을 따르지 않음");
            Expect(BulletManager.Instance.CountActive() == 0, "좀비가 총을 쏨");

            // 다음 공격은 한 주기 뒤다.
            float hpAfterFirst = victim.HP;
            TickUntil(zombie, 20, () => false);
            Expect(victim.HP == hpAfterFirst || !zombie.CurrentWeapon.IsNone, "좀비가 주기보다 빨리 다시 때림");
            TickUntil(zombie, 40, () => victim.HP < hpAfterFirst);
            Expect(victim.HP < hpAfterFirst, "좀비가 한 주기 뒤에 다시 때리지 않음");

            // 적이 죽어 전투가 끝나면 팔은 고정 자세로 튀지 않고 그 각도까지 내려온 뒤에 고정된다.
            if (!zombie.CurrentWeapon.IsNone) return;

            WeaponModelData model = zombie.CurrentWeapon.ModelData;
            float restPitch = Mathf.Min(-model.fixedRightArmAngle, DataManager.Instance.HumanParameterData.humanAIParameterData.aiTurnMaxPitchDeg);
            float raisedPitch = zombie.Controller.Pitch;
            victim.ApplyDamage(victim.HP + 1f);

            int toLeave = TickUntil(zombie, 60, () => zombie.Brain.Mode != AIBattleMode.Action);
            Expect(toLeave > 0, "적이 죽었는데 좀비의 전투가 끝나지 않음");
            Expect(zombie.UnarmedArmDynamic, $"전투가 끝난 틱에 맨손 팔이 고정 자세로 튐 (pitch {zombie.Controller.Pitch:0.0}, 고정 자세 {restPitch:0.0})");

            float maxStep = 0f;
            float previous = zombie.Controller.Pitch;
            int toFixed = -1;
            for (int tick = 1; tick <= 300; tick++)
            {
                TickAI(zombie);
                maxStep = Mathf.Max(maxStep, Mathf.Abs(zombie.Controller.Pitch - previous));
                previous = zombie.Controller.Pitch;
                if (!zombie.UnarmedArmDynamic)
                {
                    toFixed = tick;
                    break;
                }
            }
            Expect(toFixed > 1, $"맨손 팔이 내려오는 동안 조준 방향을 따르지 않음 ({toFixed}틱)");
            Expect(Mathf.Abs(zombie.Controller.Pitch - restPitch) < 1f, $"맨손 팔이 고정 자세에 닿기 전에 고정됨 (pitch {zombie.Controller.Pitch:0.0}, 고정 자세 {restPitch:0.0})");
            Expect(maxStep < 10f, $"맨손 팔이 내려오면서 한 틱에 {maxStep:0.0}° 움직임");
            GD.Print($"좀비: 전투 뒤 팔 {raisedPitch:0.0}° → {zombie.Controller.Pitch:0.0}° 내리는 데 {toFixed}틱, 한 틱 최대 {maxStep:0.0}°");
        }

        /// <summary>
        /// 경로: 포인트를 따라 넘어가고, 대기 포인트에서 멈추고, 랜덤 분기를 지나고, 같은 번호의 다른 종류 포인트가 앞에 있어도 경로가 이어진다.
        /// </summary>
        private void CheckPath()
        {
            Vector3 first = Arena(0f, -5f);
            Vector3 second = Arena(5f, -5f);

            BeginPoints();
            AddHuman(m_armedHuman, 1, 1, Arena(0f, 0f), 0f, 50);
            AddPoint(MapLoader.PointAIPath, 0, 51, 50, first);
            AddPoint(MapLoader.PointAIPath, 2, 52, 51, second, 90f);
            // 랜덤 분기로 시작하는 사람.
            AddHuman(m_armedHuman, 1, 2, Arena(200f, 0f), 0f, 60);
            AddPoint(MapLoader.PointRandomAIPath, 61, 62, 60, Arena(200f, 0f));
            AddPoint(MapLoader.PointAIPath, 1, 255, 61, Arena(200f, -5f));
            AddPoint(MapLoader.PointAIPath, 1, 255, 62, Arena(205f, 0f));
            // 첫 경로 번호(3)가 앞에 있는 사람의 식별번호와 겹친다. 경로는 종류별로 찾으므로 이어져야 한다.
            AddHuman(m_armedHuman, 1, 3, Arena(400f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 1, 4, Arena(400f, 5f), 0f, 3);
            AddPoint(MapLoader.PointAIPath, 0, 255, 3, Arena(400f, -5f));
            // 우선적 달리기와 5초 정지.
            AddHuman(m_armedHuman, 1, 5, Arena(600f, 0f), 0f, 70);
            AddPoint(MapLoader.PointAIPath, 7, 255, 70, Arena(600f, -5f));
            AddHuman(m_armedHuman, 1, 6, Arena(800f, 0f), 0f, 80);
            AddPoint(MapLoader.PointAIPath, 5, 81, 80, Arena(800f, 0f), 90f);
            AddPoint(MapLoader.PointAIPath, 1, 255, 81, Arena(800f, -5f));
            if (!LoadPoints())
            {
                Expect(false, "경로 점검용 포인트 로드 실패");
                return;
            }

            Human walker = MapLoader.SearchHuman(1);
            AIMoveNavi navi = walker.Brain.Navi;
            Expect(navi.Mode == AIMoveMode.Walk && navi.TargetPosition.DistanceTo(first) < 0.01f, $"첫 경로 포인트가 잡히지 않음 (모드 {navi.Mode})");

            // 목표가 정면이므로 걷기 입력이 들어간다.
            TickAI(walker);
            Expect((walker.Controller.MoveFlag & HumanMoveFlag.Walk) != 0, "걷기 경로인데 걷기 입력이 들어가지 않음");

            walker.Controller.Teleport(first);
            TickAI(walker);
            TickAI(walker);
            Expect(navi.Mode == AIMoveMode.Wait && navi.TargetPosition.DistanceTo(second) < 0.01f, $"도착 후 다음 포인트로 넘어가지 않음 (모드 {navi.Mode})");

            walker.Controller.Teleport(second);
            TickUntil(walker, 100, () => false);
            Expect(navi.Mode == AIMoveMode.Wait && navi.CurrentPoint.param3 == 51, "대기 포인트에서 머물지 않음");

            // 이벤트가 하듯 포인트를 걷기로 바꾸면 대기가 풀린다. 다음 포인트가 없으니 그 포인트에 남는다.
            navi.CurrentPoint.param1 = 0;
            TickAI(walker);
            Expect(navi.Mode == AIMoveMode.Walk, "걷기로 바뀐 포인트에서 대기가 풀리지 않음");

            Human random = MapLoader.SearchHuman(2);
            TickAI(random);
            int branch = random.Brain.Navi.CurrentPoint.param3;
            Expect((branch == 61 || branch == 62) && random.Brain.Navi.Mode == AIMoveMode.Run, $"랜덤 분기를 지나지 못함 (포인트 {branch}, 모드 {random.Brain.Navi.Mode})");
            TickAI(random);
            Expect((random.Controller.MoveFlag & HumanMoveFlag.Forward) != 0 || Mathf.Abs(Coord.DeltaAngle(random.Controller.Yaw, 90f)) < 90f,
                "달리기 경로인데 전진 입력도 회전도 없음");

            Human cut = MapLoader.SearchHuman(4);
            TickAI(cut);
            Expect(cut.Brain.Navi.Mode == AIMoveMode.Walk && cut.Brain.Navi.CurrentPoint.param0 == MapLoader.PointAIPath,
                $"같은 번호의 사람 포인트가 앞에 있으면 경로가 끊김 (모드 {cut.Brain.Navi.Mode})");

            Human run2 = MapLoader.SearchHuman(5);
            TickAI(run2);
            Expect(run2.Brain.Navi.Run2 && (run2.Controller.MoveFlag & HumanMoveFlag.Forward) != 0, "우선적 달리기 경로에서 달리지 않음");
            // 우선적 달리기는 소리에 경계하지 않는다.
            run2.NotifyThreatHeard();
            TickAI(run2);
            Expect(run2.Brain.Mode == AIBattleMode.Normal, "우선적 달리기 중에 소리로 경계함");

            // 5초 정지: 포인트 방향(90°)을 향한 뒤부터 센다.
            Human stopper = MapLoader.SearchHuman(6);
            int stopFrames = DataManager.Instance.HumanParameterData.humanAIParameterData.aiStop5SecFrames;
            int toFace = TickUntil(stopper, 400, () => Mathf.Abs(Coord.DeltaAngle(stopper.Controller.Yaw, 90f)) <= 2.5f);
            int toNext = TickUntil(stopper, stopFrames + 100, () => stopper.Brain.Navi.CurrentPoint.param3 == 81);
            Expect(toFace > 0 && toNext >= stopFrames - 5, $"5초 정지 포인트에서 방향을 맞추고 기다리지 않음 (방향 {toFace}틱, 대기 {toNext}틱, 기대 약 {stopFrames}틱)");
        }

        /// <summary>
        /// 이벤트: 시간 대기 → 메시지 → 걷기로 변경 → 사망 대기 → 팀 변경 → 도착 대기 → 미션 실패 순으로 한 줄이 진행되고, 다른 줄은 따로 진행된다.
        /// </summary>
        private void CheckEvents()
        {
            Vector3 arrival = Arena(700f, 700f);

            BeginPoints();
            AddHuman(m_armedHuman, 1, 2, Arena(0f, 0f), 0f, 255);
            AddHuman(m_armedHuman, 1, 3, Arena(200f, 0f), 0f, 255);
            AddPoint(MapLoader.PointAIPath, 2, 255, 51, Arena(0f, -5f));
            // 첫 줄 (시작 번호 156).
            AddPoint((int)EventType.WaitTime, 1, 100, 156, Vector3.Zero);
            AddPoint((int)EventType.Message, 0, 101, 100, Vector3.Zero);
            AddPoint((int)EventType.ChangeToWalk, 51, 102, 101, Vector3.Zero);
            AddPoint((int)EventType.WaitDeath, 2, 103, 102, Vector3.Zero);
            AddPoint((int)EventType.ChangeTeam, 3, 104, 103, Vector3.Zero);
            AddPoint((int)EventType.WaitArrival, 0, 105, 104, arrival);
            AddPoint((int)EventType.MissionFailed, 0, 0, 105, Vector3.Zero);
            // 둘째 줄 (시작 번호 146): 없는 소물은 부서진 것으로 보고 바로 메시지를 낸다. 범위 밖 번호라 표시는 바뀌지 않는다.
            AddPoint((int)EventType.WaitBreakObject, 77, 110, 146, Vector3.Zero);
            AddPoint((int)EventType.Message, 99, 111, 110, Vector3.Zero);
            // 셋째 줄 (시작 번호 136): 케이스를 들고 와야 진행된다.
            AddPoint((int)EventType.WaitCase, 0, 120, 136, s_playerSpot);
            AddPoint((int)EventType.Message, 1, 121, 120, Vector3.Zero);
            if (!LoadPoints(new[] { "first message", "second message" }))
            {
                Expect(false, "이벤트 점검용 포인트 로드 실패");
                return;
            }

            EventManager events = EventManager.Instance;
            int startCount = events.StartCount;
            m_messageSignals = 0;
            m_endSignals = 0;
            events.BeginMission();
            Expect(events.Running && events.Result == (int)MissionResult.InProgress && events.StartCount == startCount + 1, "미션 시작 상태가 아님");

            // 1초 대기는 33틱이다. 34번째 틱에 넘어간다.
            for (int i = 0; i < 33; i++) events.SimTick();
            Expect(events.MessageId == -1, "시간 대기가 33틱보다 일찍 끝남");
            events.SimTick();
            Expect(events.MessageId == 0 && events.MessageText == "first message" && m_messageSignals == 1,
                $"시간 대기 뒤 메시지가 표시되지 않음 (번호 {events.MessageId}, 시그널 {m_messageSignals})");
            Expect(MapLoader.GetPathPoint(51).param1 == 0, "걷기로 변경 이벤트가 경로 포인트를 바꾸지 않음");
            Expect(MapLoader.SearchHuman(3).Team == 1, "사망 대기를 건너뛰고 팀이 바뀜");

            for (int i = 0; i < 20; i++) events.SimTick();
            Expect(Mathf.IsEqualApprox(events.MessageAlpha, 1f), $"표시 중인 메시지의 투명도가 1 이 아님 ({events.MessageAlpha})");

            MapLoader.SearchHuman(2).SetDeadState(HumanDeadState.Falling);
            events.SimTick();
            Expect(MapLoader.SearchHuman(3).Team == 0, "대상이 죽었는데 팀 변경 이벤트가 실행되지 않음");
            Expect(events.Result == (int)MissionResult.InProgress, "도착 전에 미션이 끝남");

            // 케이스를 들면 셋째 줄이 진행돼 둘째 메시지가 나온다.
            Human player = MapLoader.Player;
            List<int> caseIndices = DataManager.Instance.WeaponParameterData.weaponGeneralData.caseWeaponIndex;
            if (caseIndices.Count > 0)
            {
                player.SetWeapon(0, caseIndices[0], 0, 0);
                events.SimTick();
                Expect(events.MessageId == 1 && m_messageSignals == 2, $"케이스를 들고 있는데 케이스 대기가 풀리지 않음 (번호 {events.MessageId})");
            }

            // 메시지는 5초(166틱) 뒤에 사라진다.
            for (int i = 0; i < 170; i++) events.SimTick();
            Expect(events.MessageId == -1 && events.MessageAlpha == 0f, "메시지가 5초 뒤에 사라지지 않음");

            player.Controller.Teleport(arrival + new Vector3(2f, 0f, 0f));
            events.SimTick();
            Expect(events.Result == (int)MissionResult.Failed && m_endSignals == 1 && !m_endComplete, $"도착 뒤 미션 실패 이벤트가 실행되지 않음 (결과 {events.Result})");

            int endTicks = events.EndTicks;
            events.SimTick();
            Expect(events.EndTicks == endTicks + 1, "미션이 끝난 뒤 틱을 세지 않음");

            MapLoader.UnloadPointData();
            Expect(!events.Running, "맵을 내렸는데 이벤트가 멈추지 않음");
        }

        /// <summary>
        /// 자동 판정: 적이 남아 있고 플레이어가 살아 있으면 진행, 플레이어가 죽으면 실패, 적이 모두 죽으면 클리어 (동시에 성립하면 클리어).
        /// </summary>
        private void CheckAutoJudge()
        {
            EventManager events = EventManager.Instance;

            for (int scenario = 0; scenario < 3; scenario++)
            {
                BeginPoints();
                AddHuman(m_armedHuman, 1, 2, Arena(0f, 0f), 0f, 255);
                AddHuman(m_armedHuman, 0, 3, Arena(200f, 0f), 0f, 255);
                if (!LoadPoints())
                {
                    Expect(false, "자동 판정 점검용 포인트 로드 실패");
                    return;
                }

                Human player = MapLoader.Player;
                Human enemy = MapLoader.SearchHuman(2);
                m_endSignals = 0;
                events.BeginMission();
                events.SimTick();
                Expect(events.Result == (int)MissionResult.InProgress, "적과 플레이어가 모두 살아 있는데 미션이 끝남");

                if (scenario != 1) enemy.ApplyDamage(enemy.HP);
                if (scenario != 0)
                {
                    player.ApplyDamage(player.HP);
                    player.SetDeadState(HumanDeadState.Falling);
                }
                events.SimTick();

                int expected = scenario == 1 ? (int)MissionResult.Failed : (int)MissionResult.Complete;
                string[] names = { "적 전멸", "플레이어 사망", "적 전멸과 플레이어 사망 동시" };
                Expect(events.Result == expected && m_endSignals == 1 && m_endComplete == (expected == (int)MissionResult.Complete),
                    $"{names[scenario]}: 결과 {events.Result} (기대 {expected}), 시그널 {m_endSignals}");
            }
        }

        /// <summary>
        /// 치트 F9: 복제한 사람은 종류·팀·무기가 같고, 따라오기는 대상 근처까지 달려가고, 제자리 경계는 그 자리에서 대기한다.
        /// </summary>
        private void CheckClone()
        {
            BeginPoints();
            if (!LoadPoints())
            {
                Expect(false, "복제 점검용 포인트 로드 실패");
                return;
            }

            Human player = MapLoader.Player;
            int count = MapLoader.HumanCount;

            Human follower = MapLoader.SpawnHumanClone(player);
            Expect(follower != null && MapLoader.HumanCount == count + 1, "복제가 만들어지지 않음");
            if (follower == null) return;

            Expect(follower.Team == player.Team && follower.HumanData == player.HumanData, "복제의 종류나 팀이 원본과 다름");
            Expect(follower.GetWeapon(0).WeaponIndex == player.GetWeapon(0).WeaponIndex
                && follower.GetWeapon(1).WeaponIndex == player.GetWeapon(1).WeaponIndex
                && follower.SelectWeapon == player.SelectWeapon, "복제의 무기가 원본과 다름");
            Expect(follower.Controller.Position.DistanceTo(player.Controller.Position + Coord.YawForward(player.Controller.Yaw) + Vector3.Up * 0.5f) < 0.01f,
                "복제가 원본의 정면 1 m, 위 0.5 m 에 놓이지 않음");

            follower.Brain.SetHoldTracking(player);
            Expect(follower.Brain.Navi.Mode == AIMoveMode.Tracking, "따라오기 복제의 이동 모드가 추적이 아님");

            // 대상이 멀어지면 그쪽으로 돌아 달린다.
            player.Controller.Teleport(follower.Controller.Position + new Vector3(10f, 0f, 0f));
            TickUntil(follower, 200, () => (follower.Controller.MoveFlag & HumanMoveFlag.Forward) != 0);
            Expect((follower.Controller.MoveFlag & HumanMoveFlag.Forward) != 0, "따라오기 복제가 멀어진 대상 쪽으로 달리지 않음");
            Expect(Mathf.Abs(Coord.DeltaAngle(follower.Controller.Yaw, 90f)) < 25f, $"따라오기 복제가 대상 쪽을 향하지 않음 (yaw {follower.Controller.Yaw:0.0})");

            Human guard = MapLoader.SpawnHumanClone(player);
            guard.Brain.SetHoldWait(guard.Controller.Position, guard.Controller.Yaw);
            TickUntil(guard, 50, () => false);
            Expect(guard.Brain.Navi.Mode == AIMoveMode.Wait && guard.Controller.MoveFlag == HumanMoveFlag.None, "제자리 경계 복제가 움직이려 함");
        }

        /// <summary>
        /// 벽: 실제 미션에서 벽을 찾아, 벽 뒤의 적은 발견하지 못하고 벽 앞의 적은 발견하는지 본다.
        /// </summary>
        private void CheckWallSight()
        {
            if (!MapLoader.LoadMissionData(0, false, 0)
                || !MapLoader.LoadBlockData(MapLoader.Instance.MissionBD1Path)
                || !MapLoader.LoadPointData(MapLoader.Instance.MissionPD1Path))
            {
                Expect(false, "미션 0 로드 실패");
                return;
            }
            GameRandom.Reseed(1u);

            IReadOnlyList<Human> humans = MapLoader.Humans;
            Human watcher = null;
            Human target = null;
            Vector3 origin = Vector3.Zero;
            float wallYaw = 0f;
            float wallDistance = 0f;

            foreach (Human human in humans)
            {
                Vector3 feet = human.Controller.Position;
                Vector3 eye = feet + Vector3.Up * human.CameraHeight;
                for (float yaw = 0f; yaw < 360f && watcher == null; yaw += 15f)
                {
                    if (!MapLoader.RaycastBlock(BlockLayer.Sight, eye, Coord.YawForward(yaw), 15f, out float distance) || distance < 4f) continue;

                    watcher = human;
                    origin = feet;
                    wallYaw = yaw;
                    wallDistance = distance;
                }
                if (watcher != null) break;
            }

            foreach (Human human in humans)
            {
                if (watcher != null && human != watcher && human.Team != watcher.Team) target = human;
            }

            if (watcher == null || target == null)
            {
                Expect(false, "미션 0 에서 벽이나 적을 찾지 못함");
                return;
            }

            // 다른 사람은 모두 멀리 치운다.
            int spot = 0;
            foreach (Human human in humans)
            {
                if (human != watcher && human != target) human.Controller.Teleport(Arena(500f + 100f * spot++, 500f));
            }

            Vector3 direction = Coord.YawForward(wallYaw);
            watcher.Controller.SetInput(new HumanInput { yaw = wallYaw, pitch = 0f });

            target.Controller.Teleport(origin + direction * (wallDistance + 2f));
            TickUntil(watcher, 400, () => watcher.Brain.Mode != AIBattleMode.Normal);
            Expect(watcher.Brain.Mode == AIBattleMode.Normal, $"벽({wallDistance:0.0} m) 뒤의 적을 발견함");

            target.Controller.Teleport(origin + direction * (wallDistance - 2f));
            int seen = TickUntil(watcher, 600, () => watcher.Brain.Mode != AIBattleMode.Normal);
            Expect(seen > 0, $"벽 앞({wallDistance - 2f:0.0} m)의 적을 발견하지 못함");
            GD.Print($"벽: 거리 {wallDistance:0.0} m, 벽 앞의 적 발견까지 {seen}틱");
        }
    }
}

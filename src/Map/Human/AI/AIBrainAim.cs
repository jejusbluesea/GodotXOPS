using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    // AIBrain 의 회전·팔 각도·적 탐색·시야 판정 담당 partial.
    public partial class AIBrain
    {
        // 맞은 방향이나 포인트 방향을 향했다고 보는 오차 (도). 원본 ai.cpp:407-417, 486-498.
        private const float k_faceTolerance = 2.5f;
        // 두리번거림을 시작할 확률의 분모: 경계 / 평상시 추적 대기 / 평상시 (원본 ai.cpp:426-434).
        private const int k_turnStartCaution = 20;
        private const int k_turnStartTracking = 65;
        private const int k_turnStartNormal = 85;
        // 두리번거림을 멈출 확률의 분모: 경계 / 평상시.
        private const int k_turnStopCaution = 20;
        private const int k_turnStopNormal = 18;
        // 대기 포인트에서 포인트가 가리키는 방향으로 돌기 시작할 확률의 분모 (원본 ai.cpp:447).
        private const int k_turnToPointChance = 80;
        // 경계 중 팔을 수평으로 맞출 때의 허용 오차, 평상시 팔 각도의 허용 폭 (도). 원본 ai.cpp:1278-1292 (±1°, −32° ~ −28°).
        private const float k_armCautionTolerance = 1f;
        private const float k_armRestTolerance = 2f;
        // 원거리에서 적을 봤을 때 확정할 확률의 분모 (원본 ai.cpp:1355).
        private const int k_longSearchChance = 4;
        // 원거리 교전 중 가까운 적을 다시 찾는 횟수와 시야각 (원본 ai.cpp:1366-1381 — 3회, 100° × 52°).
        private const int k_shortSearchLoops = 3;
        private const float k_shortSearchFovH = 100f;
        private const float k_shortSearchFovV = 52f;

        /// <summary>
        /// 한 지점을 향하는 yaw 를 구한다.
        /// </summary>
        /// <param name="direction">향할 방향 (Godot 좌표). 수평 성분만 쓴다.</param>
        /// <returns>yaw (도).</returns>
        private static float YawTo(Vector3 direction)
        {
            return Mathf.RadToDeg(Mathf.Atan2(direction.X, -direction.Z));
        }

        /// <summary>
        /// 한 지점을 향하는 pitch 를 구한다.
        /// </summary>
        /// <param name="direction">향할 방향 (Godot 좌표).</param>
        /// <returns>pitch (도, 아래 +).</returns>
        private static float PitchTo(Vector3 direction)
        {
            float horizontal = Mathf.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            return -Mathf.RadToDeg(Mathf.Atan2(direction.Y, horizontal));
        }

        /// <summary>
        /// 그 자리에서 주위를 둘러본다. 원본 AIcontrol::TurnSeen (ai.cpp:390-468).
        /// 맞아서 경계 중이면 맞은 방향을 향해 돌고, 그 밖에는 좌우 회전을 확률로 시작하고 멈춘다. 대기 포인트에서는 포인트 방향을 조금 더 본다.
        /// </summary>
        private void TurnSeen()
        {
            if (m_mode == AIBattleMode.Caution && m_faceCaution)
            {
                float delta = Coord.DeltaAngle(m_controller.Yaw, m_faceCautionYaw);
                if (delta > k_faceTolerance) m_turnRight = true;
                if (delta < -k_faceTolerance) m_turnLeft = true;
                if (Mathf.Abs(delta) <= k_faceTolerance) m_faceCaution = false;
                return;
            }

            if (m_mode == AIBattleMode.Action) return;

            int turnStart;
            int turnStop;
            if (m_mode == AIBattleMode.Caution)
            {
                turnStart = k_turnStartCaution;
                turnStop = k_turnStopCaution;
            }
            else
            {
                turnStart = m_nav.Mode == AIMoveMode.Tracking ? k_turnStartTracking : k_turnStartNormal;
                turnStop = k_turnStopNormal;
            }

            if (GetRand(turnStart) == 0) m_scanRight = true;
            if (GetRand(turnStart) == 0) m_scanLeft = true;

            if (m_mode == AIBattleMode.Normal && m_nav.Mode == AIMoveMode.Wait && GetRand(k_turnToPointChance) == 0)
            {
                float delta = Coord.DeltaAngle(m_controller.Yaw, m_nav.TargetLook);
                if (delta > 0f) m_scanRight = true;
                if (delta < 0f) m_scanLeft = true;
            }

            if (GetRand(turnStop) == 0) m_scanRight = false;
            if (GetRand(turnStop) == 0) m_scanLeft = false;

            if (m_scanRight) m_turnRight = true;
            if (m_scanLeft) m_turnLeft = true;
        }

        /// <summary>
        /// 포인트가 가리키는 방향을 계속 본다. 원본 AIcontrol::StopSeen (ai.cpp:471-501).
        /// </summary>
        /// <returns>그 방향을 향하고 있으면 true.</returns>
        private bool StopSeen()
        {
            m_scanLeft = false;
            m_scanRight = false;

            float delta = Coord.DeltaAngle(m_controller.Yaw, m_nav.TargetLook);
            if (delta > k_faceTolerance) m_turnRight = true;
            if (delta < -k_faceTolerance) m_turnLeft = true;
            return Mathf.Abs(delta) <= k_faceTolerance;
        }

        /// <summary>
        /// 팔(조준) 각도를 상태에 맞춘다. 원본 AIcontrol::ArmAngle (ai.cpp:1265-1294).
        /// 맨손은 계속 내리고, 경계 중에는 수평으로 겨누고, 평상시에는 팔이 쉬는 각(armAngleInitial) 근처에 둔다.
        /// </summary>
        private void ArmAngle()
        {
            m_turnUp = false;
            m_turnDown = false;

            float pitch = m_controller.Pitch;
            if (m_self.CurrentWeapon.IsNone)
            {
                m_turnDown = true;
            }
            else if (m_mode == AIBattleMode.Caution && m_cautionCnt > 0)
            {
                if (pitch > k_armCautionTolerance) m_turnUp = true;
                if (pitch < -k_armCautionTolerance) m_turnDown = true;
            }
            else
            {
                // 데이터는 팔 기준(아래가 음수)이라 시선 pitch(아래가 양수)로 부호를 뒤집는다.
                float rest = -GeneralData.armAngleInitial;
                if (pitch > rest + k_armRestTolerance) m_turnUp = true;
                if (pitch < rest - k_armRestTolerance) m_turnDown = true;
            }
        }

        /// <summary>
        /// 이번 틱의 이동·회전 의사를 사람에게 넣는다. 원본 AIObjectDriver::ControlObject (ai.cpp:2316-2371).
        /// 회전은 방향마다 각속도에 일정량을 더하고 매 틱 감쇠시켜 부드럽게 돈다. 넣은 이동 입력은 다음 틱의 이동이 소비한다.
        /// </summary>
        private void ApplyControl()
        {
            HumanAIParameterData ai = AIData;

            if (m_turnUp) m_addPitch -= ai.aiTurnRateDeg;
            if (m_turnDown) m_addPitch += ai.aiTurnRateDeg;
            if (m_turnLeft) m_addYaw -= ai.aiTurnRateDeg;
            if (m_turnRight) m_addYaw += ai.aiTurnRateDeg;

            // 이번 틱에 쏜 반동이 더해진 뒤의 조준각에서 출발한다.
            float yaw = m_controller.Yaw + m_addYaw;
            float pitch = Mathf.Clamp(m_controller.Pitch + m_addPitch, -ai.aiTurnMaxPitchDeg, ai.aiTurnMaxPitchDeg);

            var input = new HumanInput { moveFlag = m_moveIntent | m_combatMove, yaw = yaw, pitch = pitch };
            m_controller.SetInput(in input);

            m_addYaw *= ai.aiTurnDamping;
            m_addPitch *= ai.aiTurnDamping;
            if (Mathf.Abs(m_addYaw) < ai.aiTurnDeadzoneDeg) m_addYaw = 0f;
            if (Mathf.Abs(m_addPitch) < ai.aiTurnDeadzoneDeg) m_addPitch = 0f;
        }

        /// <summary>
        /// 적을 찾는다. 원본 AIcontrol::SearchEnemy (ai.cpp:1297-1363).
        /// 사람 목록에서 무작위로 한 명씩 뽑아 근거리(넓은 시야)와 원거리(좁은 시야)로 본다. 뽑는 범위가 실제 인원보다 넓어서 발견이 확률적으로 늦어진다.
        /// 탐색 횟수와 원거리 한계는 AI 레벨의 search 값과 무기 스코프에 따라 달라진다.
        /// 원거리에서 본 적은 1/4 확률로만 확정하지만, 확정하지 못해도 본 적은 기억에 남는다 (경계 중이면 다음 틱에 전투로 들어간다).
        /// </summary>
        /// <returns>0 = 못 찾음, 1 = 근거리에서 발견, 2 = 원거리에서 발견.</returns>
        private int SearchEnemy()
        {
            if (m_noFight) return 0;
            if (m_mode == AIBattleMode.Action) return 0;

            HumanAIData level = LevelData();
            HumanAIScopeData scope = ScopeData(m_self.CurrentWeapon);
            if (level == null || scope == null) return 0;

            HumanAIParameterData ai = AIData;
            bool caution = m_mode == AIBattleMode.Caution;

            int loops = level.search * ai.aiSearchLoopScale;
            float maxDistance;
            float nearFovH, nearFovV, longFovH, longFovV;
            if (!caution)
            {
                maxDistance = scope.aiAddSearchDistNormal + ai.aiSearchDistCoeffNormal * (level.search - 2) + ai.aiSearchDistBaseNormal;
                nearFovH = ai.aiSearchFovHNear;
                nearFovV = ai.aiSearchFovVNear;
                longFovH = ai.aiSearchFovHLong;
                longFovV = ai.aiSearchFovVLong;
            }
            else
            {
                // 경계 중에는 한 번(원본 4회 = 인원 배율 1 단위) 더 찾고, 더 멀리 더 넓게 본다.
                loops += ai.aiSearchLoopScale;
                maxDistance = scope.aiAddSearchDistCaution + ai.aiSearchDistCoeffCaution * (level.search - 2) + ai.aiSearchDistBaseCaution;
                nearFovH = ai.aiSearchFovHNearCaution;
                nearFovV = ai.aiSearchFovVNearCaution;
                longFovH = ai.aiSearchFovHLongCaution;
                longFovV = ai.aiSearchFovVLongCaution;
            }

            IReadOnlyList<Human> humans = MapLoader.Humans;
            int pool = Mathf.Max(humans.Count, ai.aiSearchPoolSize);

            for (int i = 0; i < loops; i++)
            {
                int index = GetRand(pool);
                Human target = index < humans.Count ? humans[index] : null;

                if (CheckLookEnemy(target, nearFovH, nearFovV, ai.aiShortAttackDist))
                {
                    m_longAttack = false;
                    return 1;
                }

                if (CheckLookEnemy(target, longFovH, longFovV, maxDistance) && GetRand(k_longSearchChance) == 0)
                {
                    m_longAttack = !m_nav.Run2;
                    return 2;
                }
            }
            return 0;
        }

        /// <summary>
        /// 원거리 교전 중에 가까운 적을 찾는다. 찾으면 그쪽으로 표적을 바꾸고 근거리 교전으로 넘어간다. 원본 AIcontrol::SearchShortEnemy (ai.cpp:1366-1381).
        /// </summary>
        /// <returns>찾았으면 true.</returns>
        private bool SearchShortEnemy()
        {
            IReadOnlyList<Human> humans = MapLoader.Humans;
            int pool = Mathf.Max(humans.Count, AIData.aiSearchPoolSize);

            for (int i = 0; i < k_shortSearchLoops; i++)
            {
                int index = GetRand(pool);
                Human target = index < humans.Count ? humans[index] : null;

                if (CheckLookEnemy(target, k_shortSearchFovH, k_shortSearchFovV, AIData.aiShortAttackDist))
                {
                    m_longAttack = false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 한 사람이 적이고 시야 안에 보이는지 판정한다. 보이면 그 사람을 표적으로 기억한다. 원본 AIcontrol::CheckLookEnemy (ai.cpp:1398-1444).
        /// 거리와 각도는 발 위치끼리 재고, 세로 시야는 팔 각도와 무관하게 수평을 기준으로 한다. 가리는 것은 블록만 본다 (사람과 소물은 가리지 않는다).
        /// </summary>
        /// <param name="target">볼 사람. null 이면 false.</param>
        /// <param name="fovH">가로 시야각 전체 (도).</param>
        /// <param name="fovV">세로 시야각 전체 (도).</param>
        /// <param name="maxDistance">볼 수 있는 최대 거리 (m).</param>
        /// <returns>보이면 true.</returns>
        private bool CheckLookEnemy(Human target, float fovH, float fovV, float maxDistance)
        {
            if (!IsValidHuman(target) || !target.Alive) return false;
            if (target.Team == m_self.Team) return false;

            Vector3 position = m_controller.Position;
            Vector3 toTarget = target.Controller.Position - position;
            float distanceSquared = toTarget.LengthSquared();
            if (distanceSquared > maxDistance * maxDistance) return false;

            float deltaYaw = Coord.DeltaAngle(m_controller.Yaw, YawTo(toTarget));
            float pitch = PitchTo(toTarget);
            if (Mathf.Abs(deltaYaw) >= fovH * 0.5f || Mathf.Abs(pitch) >= fovV * 0.5f) return false;

            float distance = Mathf.Sqrt(distanceSquared);
            if (distance > 0f)
            {
                Vector3 eye = position + Vector3.Up * m_controller.CameraHeight;
                if (MapLoader.RaycastBlock(eye, toTarget / distance, distance, out _)) return false;
            }

            m_enemy = target;
            return true;
        }
    }
}

using Godot;

namespace GodotXOPS
{
    // AIBrain 의 경로 이동(포인트 따라가기, 점프, 경로의 수류탄 투척) 담당 partial.
    public partial class AIBrain
    {
        // 목표 쪽으로 돌기 시작하는 각도 차 (도). 원본 ai.cpp:168-173.
        private const float k_turnTowardDeg = 0.5f;
        // 목표 방향과 이 각도 안으로 맞아야 전진한다: 걷기 / 달리기 / 추적 / 좀비 (도). 원본 ai.cpp:176-205.
        private const float k_forwardToleranceWalk = 6f;
        private const float k_forwardToleranceRun = 50f;
        private const float k_forwardToleranceTracking = 20f;
        private const float k_forwardToleranceZombie = 20f;
        // 이동 중 점프를 시도할 확률의 분모 (원본 ai.cpp:208).
        private const int k_jumpChance = 16;
        // 점프 판정에서 발밑 장애물을 보는 높이 (m). 원본 AI_CHECKJUMP_HEIGHT 0.3.
        private const float k_jumpCheckHeight = 0.03f;

        // 우선적 달리기로 전투 중 경로를 따를 때: 목표가 이 각도 안이면 전진, 이 각도 밖이면 후진, 그 사이 구간이면 옆걸음 (도). 원본 ai.cpp:242-255.
        private const float k_run2ForwardTolerance = 56f;
        private const float k_run2BackTolerance = 123.5f;
        private const float k_run2StrafeMin = 33f;
        private const float k_run2StrafeMax = 146f;

        // 경로의 수류탄 투척: 좌우·상하 오차가 모두 이 안이면 던진다 (도). 원본 ai.cpp:1247.
        private const float k_grenadeThrowTolerance = 1.5f;

        /// <summary>
        /// 경로를 따라 움직인다. 원본 AIcontrol::MovePath (ai.cpp:1489-1523).
        /// 목표에 닿기 전에는 그쪽으로 가고, 닿은 뒤에는 포인트 종류에 따라 둘러보거나, 정해진 방향을 보며 기다리거나, 다음 포인트로 넘어간다.
        /// 경로가 없으면 아무것도 하지 않는다.
        /// </summary>
        private void MovePath()
        {
            AIMoveMode mode = m_nav.Mode;
            if (mode == AIMoveMode.Grenade || mode == AIMoveMode.Random || mode == AIMoveMode.Null) return;

            if (!CheckTargetPos(false))
            {
                MoveTarget(false);
                return;
            }

            if (mode == AIMoveMode.Wait || mode == AIMoveMode.Tracking)
            {
                TurnSeen();
            }
            else if (mode == AIMoveMode.Stop5Sec)
            {
                if (m_waitCnt < AIData.aiStop5SecFrames)
                {
                    if (StopSeen()) m_waitCnt++;
                }
                else
                {
                    m_waitCnt = 0;
                    AdvancePath();
                }
            }
            else
            {
                m_waitCnt = 0;
                AdvancePath();
            }
        }

        /// <summary>
        /// 목표 지점에 닿았는지 본다 (수평 거리). 원본 AIcontrol::CheckTargetPos (ai.cpp:100-130).
        /// </summary>
        /// <param name="back">true 면 경로 목표가 아니라 경계를 시작한 자리를 본다.</param>
        /// <returns>닿았으면 true.</returns>
        private bool CheckTargetPos(bool back)
        {
            Vector3 target = back ? m_cautionBackPosition : m_nav.TargetPosition;
            bool tracking = !back && m_nav.Mode == AIMoveMode.Tracking;

            Vector3 toTarget = target - m_controller.Position;
            toTarget.Y = 0f;
            float arrival = tracking ? AIData.aiArrivalDistTracking : AIData.aiArrivalDistPath;
            return toTarget.LengthSquared() < arrival * arrival;
        }

        /// <summary>
        /// 목표 지점으로 돌면서 간다. 원본 AIcontrol::MoveTarget (ai.cpp:133-222).
        /// 목표 방향과 어느 정도 맞아야 전진하며, 그 허용각과 걷기·달리기는 이동 모드가 정한다. 좀비는 항상 걷는다.
        /// 원본에는 끼었을 때 좌우로 도는 처리도 있지만(ai.cpp:212-220), 조건이 되는 값(현재 이동 플래그, 누적 이동량)이 그 시점에 늘 0 이어서 실행되지 않으므로 옮기지 않았다.
        /// </summary>
        /// <param name="back">true 면 경계를 시작한 자리로 달려 돌아간다.</param>
        private void MoveTarget(bool back)
        {
            // 원본은 여기서 모든 이동·회전을 멈춘다 (ResetMode).
            m_scanLeft = false;
            m_scanRight = false;
            m_combatMove = HumanMoveFlag.None;
            m_moveIntent = HumanMoveFlag.None;
            m_turnLeft = m_turnRight = m_turnUp = m_turnDown = false;

            Vector3 target = back ? m_cautionBackPosition : m_nav.TargetPosition;
            AIMoveMode mode = back ? AIMoveMode.Run : m_nav.Mode;

            Vector3 toTarget = target - m_controller.Position;
            toTarget.Y = 0f;
            float delta = Coord.DeltaAngle(m_controller.Yaw, YawTo(toTarget));
            float absDelta = Mathf.Abs(delta);

            if (delta > k_turnTowardDeg) m_turnRight = true;
            if (delta < -k_turnTowardDeg) m_turnLeft = true;

            if (IsZombie())
            {
                if (absDelta < k_forwardToleranceZombie) m_moveIntent = HumanMoveFlag.Walk;
            }
            else if (mode == AIMoveMode.Run || mode == AIMoveMode.Run2)
            {
                if (absDelta < k_forwardToleranceRun) m_moveIntent = HumanMoveFlag.Forward;
            }
            else if (mode == AIMoveMode.Tracking)
            {
                if (absDelta < k_forwardToleranceTracking)
                {
                    float walkDistance = AIData.aiArrivalDistWalkTracking;
                    m_moveIntent = toTarget.LengthSquared() < walkDistance * walkDistance ? HumanMoveFlag.Walk : HumanMoveFlag.Forward;
                }
            }
            else
            {
                // 걷기, 대기, 5초 정지 포인트로는 걸어서 간다.
                if (absDelta < k_forwardToleranceWalk) m_moveIntent = HumanMoveFlag.Walk;
            }

            if (GetRand(k_jumpChance) == 0) MoveJump();
        }

        /// <summary>
        /// 우선적 달리기로 전투 중에 경로 목표로 간다. 원본 AIcontrol::MoveTarget2 (ai.cpp:225-272).
        /// 몸은 적을 겨눈 채(회전은 Action 이 한다) 목표가 있는 방향에 따라 전진·후진·옆걸음을 고른다.
        /// </summary>
        private void MoveTarget2()
        {
            Vector3 toTarget = m_nav.TargetPosition - m_controller.Position;
            toTarget.Y = 0f;
            float delta = Coord.DeltaAngle(m_controller.Yaw, YawTo(toTarget));
            float absDelta = Mathf.Abs(delta);

            if (absDelta < k_run2ForwardTolerance) m_moveIntent |= HumanMoveFlag.Forward;
            if (absDelta > k_run2BackTolerance) m_moveIntent |= HumanMoveFlag.Back;
            if (delta > k_run2StrafeMin && delta < k_run2StrafeMax) m_moveIntent |= HumanMoveFlag.Right;
            if (delta < -k_run2StrafeMin && delta > -k_run2StrafeMax) m_moveIntent |= HumanMoveFlag.Left;

            if (GetRand(k_jumpChance) == 0) MoveJump();
        }

        /// <summary>
        /// 가는 방향에 장애물이 있으면 점프한다. 원본 AIcontrol::MoveJump (ai.cpp:504-555).
        /// 직전 틱에 움직이고 있었을 때만 본다. 허리 높이의 앞쪽 점이 블록 안이거나, 발밑 바로 앞이 블록 안이거나, 그 자리에서 머리 높이까지 위로 블록이 있으면 점프한다.
        /// </summary>
        /// <returns>점프를 넣었으면 true.</returns>
        private bool MoveJump()
        {
            const HumanMoveFlag moving = HumanMoveFlag.Forward | HumanMoveFlag.Back | HumanMoveFlag.Left | HumanMoveFlag.Right | HumanMoveFlag.Walk;
            if ((m_controller.MoveFlagLt & moving) == 0) return false;

            // 진행 방향. 여러 방향이 겹치면 뒤에 확인한 쪽이 이긴다 (원본 순서: 걷기, 전진, 후진, 왼쪽, 오른쪽).
            HumanMoveFlag intent = m_moveIntent | m_combatMove;
            float yaw = m_controller.Yaw;
            Vector3 direction = Coord.YawForward(yaw);
            if ((intent & HumanMoveFlag.Back) != 0) direction = -Coord.YawForward(yaw);
            if ((intent & HumanMoveFlag.Left) != 0) direction = -Coord.YawRight(yaw);
            if ((intent & HumanMoveFlag.Right) != 0) direction = Coord.YawRight(yaw);

            Vector3 position = m_controller.Position;
            float checkDistance = AIData.aiJumpCheckDist;

            Vector3 waist = position + direction * (checkDistance + m_controller.MapRadius) + Vector3.Up * (m_controller.Height * 0.5f);
            Vector3 foot = position + direction * checkDistance + Vector3.Up * k_jumpCheckHeight;

            if (MapLoader.IsInsideBlock(BlockLayer.Human, waist)
                || MapLoader.IsInsideBlock(BlockLayer.Human, foot)
                || MapLoader.RaycastBlock(BlockLayer.Human, foot, Vector3.Up, m_controller.Height - k_jumpCheckHeight, out _))
            {
                m_moveIntent |= HumanMoveFlag.Jump;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 경로의 수류탄 투척 포인트를 처리한다. 원본 AIcontrol::ThrowGrenade (ai.cpp:1175-1262).
        /// 수류탄이 있으면 그것을 들고 포인트를 눈높이에서 직선으로 겨눠, 좌우·상하가 모두 맞으면 던진다. 없으면 그쪽을 향하기만 한다.
        /// </summary>
        /// <returns>0 = 아직 겨누는 중, 1 = 던졌다, 2 = 수류탄이 없다.</returns>
        private int ThrowGrenade()
        {
            int grenadeIndex = DataManager.Instance.WeaponParameterData.weaponGeneralData.grenadeWeaponIndex;
            int slot = -1;
            for (int i = 0; i < Human.WeaponSlotCount; i++)
            {
                if (m_self.GetWeapon(i).WeaponIndex == grenadeIndex)
                {
                    slot = i;
                    break;
                }
            }

            if (slot >= 0 && slot != m_self.SelectWeapon) m_self.SetSelectWeapon(slot);

            RawPointData point = m_nav.CurrentPoint;
            Vector3 eye = m_controller.Position + Vector3.Up * m_controller.CameraHeight;
            Vector3 toTarget = (point != null ? point.position : m_nav.TargetPosition) - eye;
            float deltaYaw = Coord.DeltaAngle(m_controller.Yaw, YawTo(toTarget));

            if (deltaYaw > 0f) m_turnRight = true;
            if (deltaYaw < 0f) m_turnLeft = true;

            if (slot < 0)
            {
                ArmAngle();
                return 2;
            }

            float deltaPitch = PitchTo(toTarget) - m_controller.Pitch;
            if (deltaPitch > 0f) m_turnDown = true;
            if (deltaPitch < 0f) m_turnUp = true;

            if (Mathf.Abs(deltaYaw) < k_grenadeThrowTolerance && Mathf.Abs(deltaPitch) < k_grenadeThrowTolerance)
            {
                // 연발이 아닌 무기로 직전 틱에 던졌으면 연달아 던지지 않는다.
                bool fullAuto = m_self.CurrentWeapon.Data.burstMode == WeaponBurstMode.FullAuto;
                if (!fullAuto && m_eventWeaponShot) return 0;

                if (m_self.ShotWeapon())
                {
                    m_eventWeaponShot = true;
                    return 1;
                }
            }
            return 0;
        }
    }
}

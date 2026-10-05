using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 원본 OpenXOPS human::ControlProcess / CollisionMap / CheckAndProcessDead 포팅. 엔진 물리를 쓰지 않고 원본 알고리즘으로 직접 계산한다.
    /// Human 이 소유하는 순수 클래스이며 SimClock 의 33.333fps 틱에서 이동·맵충돌·사망 상태머신을 진행한다.
    /// 논리 위치는 이 클래스가 들고, Human 노드의 transform 은 ApplyVisual 이 틱 사이를 보간해 시각 전용으로 갱신한다.
    /// 각도는 UnityXOPS 규약(도 단위, yaw 오른쪽 +, pitch 아래 +)으로 들고 Coord 로 Godot 방향·회전으로 바꾼다.
    /// </summary>
    public class HumanController : ISimTickable
    {
        // 원본 OpenXOPS object.h 상수 × 0.1 (미터)
        private const float k_climbHeight = 0.32f; // HUMAN_MAPCOLLISION_CLIMBHEIGHT
        private const float k_climbForwardDist = 0.2f; // 원본 dir*2.0f (오르기 전방 체크)
        private const float k_groundHeight = -0.05f; // HUMAN_MAPCOLLISION_GROUND_HEIGHT
        private const float k_groundR1 = 0.015f; // 플레이어 접지 반경 1
        private const float k_groundR2 = 0.05f; // 플레이어 접지 반경 2
        private const float k_groundR3 = 0.03f; // NPC 접지 반경
        private const float k_collisionAddSize = 0.001f; // COLLISION_ADDSIZE
        private const int k_moveYUpperCooldown = 8; // 경사 미끄러짐 후 점프/오르기 금지 프레임
        private const float k_checkMaxDist = 1.2f; // HUMAN_MAPCOLLISION_CHECK_MAXDIST — 브로드페이즈 반경 겸 1프레임 최대 이동 거리
        private const float k_headCheckOffset = 0.022f; // 머리 판정: 키 − 0.22
        private const float k_shoulderRayOffset = 0.2f; // 매몰 판정 레이 시작: 키 − 2.0
        private const float k_shoulderInsideOffset = 0.06f; // 매몰 판정 점: 키 − 0.6
        private const float k_moveCheckOffset = 0.3f; // HUMAN_MAPCOLLISION_CHECK_HEIGHT: 키 − 3.0 (어깨 정도)
        private const float k_embedPredictionTime = 0.33f; // 원본 move*11.0 (11프레임 뒤)
        private const float k_abnormalMoveMargin = 0.1f; // 원본 Dist − speed > 1.0
        private const float k_fallSubstep = 0.33f; // 원본 pos_y += move_y*0.33 (3회 반복, 합 0.99)
        // 착지 소리를 내는 최소 낙하 속도 (m/s). 원본 object.cpp:1790 — move_y < HUMAN_MAPCOLLISION_GROUND_HEIGHT(프레임당 −0.5).
        private const float k_landingSoundSpeed = -0.05f * SimClock.FrameRate;

        // 추가 충돌 플래그(MapLoader.AdjustCollision)가 켜진 미션에서만 쓰는 중심축 검사 높이 (원본 HUMAN_MAPCOLLISION_ADD_HEIGHT_A/B).
        private const float k_addHeightA = 0.9f;
        private const float k_addHeightB = 1.3f;

        // 오르기 최소 이동 속도 (원본: |move| > 0.2/frame = 0.666 m/s)
        private const float k_climbMinSpeed = 0.666f;
        // 경사 미끄러짐 속도 (원본 프레임당 법선 × 1.2 / −0.5 를 m/s 로 환산)
        private const float k_slideHorizontalSpeed = 4.0f;
        private const float k_slideVerticalSpeed = -1.667f;
        // 경사 미끄러짐 예측 시간 (원본: move*3.0 = 3프레임 뒤)
        private const float k_slidePredictionTime = 0.09f;
        // 치트(F5) 강제 상승 속도 — 원본 pos_y += 5.0/frame.
        private const float k_cheatRiseSpeed = 0.5f * SimClock.FrameRate;

        // 사망 상태머신 상수. HUMAN_DEADADDRY = 0.75°/frame² 를 시간 단위로 환산한 각가속도.
        private const float k_deadRotationAccel = 0.75f * SimClock.FrameRate * SimClock.FrameRate;
        private const float k_deadFlatLayPitch = 90f;
        private const float k_deadFreeFallEntryPitch = 135f;
        private const float k_deadPopupHeight = 0.1f; // 사망 진입 시 시체 함몰 방지 (원본 pos_y += 1.0)

        private readonly Human m_human;
        private readonly ControllerSizeData m_size;

        // 이번 틱에서 충돌 검사할 근처 블록 (원본 CheckBlockID[] 브로드페이즈 대응). 매 틱 갱신.
        private readonly List<Block> m_nearBlocks = new List<Block>(32);

        private float m_rotationX;
        private float m_armRotationY;
        private Vector3 m_position;
        private Vector3 m_prevPosition;
        private Vector3 m_moveVelocity;
        private HumanMoveFlag m_moveFlag;
        private HumanMoveFlag m_moveFlagLt;
        private int m_moveYUpper;
        private bool m_cheatRise;
        // 비행 모드 (디버그 콘솔의 flight). 켜져 있고 살아 있는 동안 평소의 이동·충돌 대신 TickFlight 가 돈다.
        private bool m_flight;
        // 접지 여부 — 원본 move_y_flag 의 반전. 스폰 직후 첫 틱 전까지는 접지로 간주한다.
        private bool m_grounded = true;
        // 이번 틱에 소리가 날 만큼 빠르게 착지했는지 (원본 move_y_landing).
        private bool m_landedHard;

        // 사망 회전: 각속도(deg/s), 각도(deg, + 앞으로 엎어짐 / − 뒤로 자빠짐), 방향(+1/−1).
        private float m_deadAddRy;
        private float m_deadPitchAngle;
        // 직전 틱의 사망 회전 각도. 화면에는 직전 틱과 현재 틱 사이를 보간해 보여 준다.
        private float m_prevDeadPitchAngle;
        private float m_deadDirection;

        public Vector3 Position => m_position;
        public float Yaw => m_rotationX;
        public float Pitch => m_armRotationY;
        public Vector3 MoveVelocity => m_moveVelocity;
        public HumanMoveFlag MoveFlag => m_moveFlag;
        public HumanMoveFlag MoveFlagLt => m_moveFlagLt;
        public bool Grounded => m_grounded;
        public bool Flight => m_flight;
        public float Height => m_size.height;
        public float CameraHeight => m_size.cameraHeight;
        public float MapRadius => m_size.mapRadius;
        public float HumanRadius => m_size.humanRadius;

        // 렌더용 시각 위치 — 직전 틱→현재 틱을 보간한 값.
        public Vector3 VisualPosition => m_prevPosition.Lerp(m_position, SimClock.InterpolationAlpha);

        // 원본 이동/맵충돌(O2) — 무기/총알/인간간충돌/AI판단보다 먼저.
        public int SimOrder => 10;

        /// <summary>
        /// 컨트롤러를 만든다. Human 의 종류 데이터가 확정된 뒤에 호출해야 체형 크기가 맞게 잡힌다.
        /// </summary>
        /// <param name="human">소유 Human.</param>
        /// <param name="position">스폰 위치 (발 기준, Godot 좌표).</param>
        /// <param name="yawDeg">스폰 방향 yaw (도).</param>
        public HumanController(Human human, Vector3 position, float yawDeg)
        {
            m_human = human;
            m_position = position;
            m_prevPosition = position;
            m_rotationX = yawDeg;

            HumanParameterData parameter = DataManager.Instance.HumanParameterData;
            List<ControllerSizeData> sizes = parameter.controllerSizeData;
            int sizeIndex = human.HumanTypeData != null ? human.HumanTypeData.controllerSizeIndex : 0;
            m_size = sizes.Count > 0 ? sizes[Mathf.Clamp(sizeIndex, 0, sizes.Count - 1)] : new ControllerSizeData();

            // 팔 pitch 초기값 — 원본 armrotation_y 초기값 −30°(아래). 팔 기준(음수=아래) → 시선 pitch 기준(양수=아래)으로 부호 반전.
            m_armRotationY = -parameter.humanGeneralData.armAngleInitial;
        }

        /// <summary>
        /// 이번 틱 입력을 주입한다. 조준각(yaw/pitch)은 덮어쓰고, 이동 플래그는 다음 틱이 소비할 때까지 OR 누적한다.
        /// 값을 클램프/정규화하지 않고 그대로 저장한다(그 책임은 호출측). input.weapon 은 여기서 소비하지 않는다.
        /// </summary>
        /// <param name="input">조준·이동 입력.</param>
        public void SetInput(in HumanInput input)
        {
            m_rotationX = input.yaw;
            m_armRotationY = input.pitch;
            m_moveFlag |= input.moveFlag;
        }

        /// <summary>
        /// 치트(F5) 강제 상승 여부를 설정한다. true 인 동안 매 틱 수직으로 관통 상승한다.
        /// </summary>
        /// <param name="active">상승 여부.</param>
        public void SetCheatRise(bool active)
        {
            m_cheatRise = active;
        }

        /// <summary>
        /// 비행 모드를 켜고 끈다 (디버그 콘솔의 flight). 원본에 없는 기능이다.
        /// 켜져 있으면 중력, 블록 충돌, 사람끼리 밀어내기, 낙하 데미지 없이 시선 방향으로 움직인다. 총알 판정은 그대로다.
        /// 날고 있는 동안은 접지한 것으로 치고, 끄면 평소의 이동으로 돌아가 떨어지는 동안 공중 상태가 된다.
        /// </summary>
        /// <param name="active">true 면 비행.</param>
        public void SetFlight(bool active)
        {
            if (m_flight == active) return;

            m_flight = active;
            if (active)
            {
                m_moveVelocity = Vector3.Zero;
            }
            else
            {
                // 비행을 끄면 보통 공중이다. 다음 이동 틱이 접지 여부를 다시 구할 때까지 공중으로 둔다 (그 사이의 무기 틱이 조준 오차에 쓴다).
                m_grounded = false;
            }
        }

        /// <summary>
        /// 조준각에 변화량을 더한다 (발사 반동 등).
        /// </summary>
        /// <param name="deltaYaw">yaw 변화량 (도).</param>
        /// <param name="deltaPitch">pitch 변화량 (도).</param>
        public void AddYawPitch(float deltaYaw, float deltaPitch)
        {
            m_rotationX += deltaYaw;
            m_armRotationY += deltaPitch;
        }

        /// <summary>
        /// 조준 방향으로 밀어내는 속도를 더한다. 원본 human::AddPosOrder (object.cpp:1025-1030) 대응. 다음 틱부터 감쇠된다.
        /// </summary>
        /// <param name="yawDeg">yaw (도).</param>
        /// <param name="pitchDeg">pitch (도).</param>
        /// <param name="speedMps">더할 속도 (m/s). 원본 1.0/frame ≈ 3.333 m/s.</param>
        public void AddKnockback(float yawDeg, float pitchDeg, float speedMps)
        {
            m_moveVelocity += Coord.AimDirection(yawDeg, pitchDeg) * speedMps;
        }

        /// <summary>
        /// 임의 방향으로 밀어내는 속도를 더한다 (폭발, 인간간 충돌).
        /// </summary>
        /// <param name="worldDirection">정규화된 방향.</param>
        /// <param name="speedMps">더할 속도 (m/s).</param>
        public void AddKnockbackVector(Vector3 worldDirection, float speedMps)
        {
            m_moveVelocity += worldDirection * speedMps;
        }

        /// <summary>
        /// 논리 위치를 직접 옮긴다 (스폰 재배치, 치트). 시각 보간도 그 자리에서 다시 시작한다.
        /// </summary>
        /// <param name="position">새 위치 (발 기준).</param>
        public void Teleport(Vector3 position)
        {
            m_position = position;
            m_prevPosition = position;
        }

        /// <summary>
        /// SimClock 33.333fps 틱 — 이동/맵충돌/사망 상태머신 1프레임.
        /// </summary>
        public void SimTick()
        {
            m_prevPosition = m_position;
            m_prevDeadPitchAngle = m_deadPitchAngle;

            // 완전히 고정된 시체는 더 계산하지 않는다 (원본 deadstate == 5 조기 반환).
            if (m_human.DeadState == HumanDeadState.Done) return;

            // 무기 입력과 카운터는 이동보다 먼저 처리한다 (원본: 입력 → human::ProcessObject 앞부분). 총알은 이동 전 위치에서 나간다.
            m_human.TickWeapon();

            // 사망 시 입력 플래그를 버린다. 틱 자체는 계속 돌려야 중력/지면/추락 한계가 시체에 적용된다.
            if (!m_human.Alive)
            {
                m_moveFlag = HumanMoveFlag.None;
            }

            Tick();
            TickDeadState();

            // 원본 human::ProcessObject 말미의 MotionCtrl->ProcessObject 대응 — 이번 틱 입력으로 다리 애니메이션/회전 갱신.
            m_human.HumanVisual?.TickLeg(SimClock.FrameTime, m_moveFlagLt, m_rotationX, m_human.Alive);
            m_human.HumanVisual?.TickArmReaction(m_human.ArmHeld);

            EmitFootsteps();
        }

        /// <summary>
        /// 이번 틱의 움직임에 맞는 발소리를 낸다. 원본 ObjectManager::Process 의 발소리 부분 (objectmanager.cpp:2771-2805).
        /// 종류는 이번 틱에 소비한 이동 입력으로 정하고 (걷기 → 전진 → 후진 → 좌우 순으로 먼저 걸리는 것), 점프와 착지는 따로 낸다.
        /// </summary>
        private void EmitFootsteps()
        {
            if (!m_human.Alive || m_human.HP <= 0f) return;
            // 날고 있으면 발이 땅에 닿지 않는다.
            if (m_flight) return;

            if ((m_moveFlagLt & HumanMoveFlag.Walk) != 0) WorldSound.EmitFootstep(m_human, FootstepKind.Walk);
            else if ((m_moveFlagLt & HumanMoveFlag.Forward) != 0) WorldSound.EmitFootstep(m_human, FootstepKind.Forward);
            else if ((m_moveFlagLt & HumanMoveFlag.Back) != 0) WorldSound.EmitFootstep(m_human, FootstepKind.Back);
            else if ((m_moveFlagLt & (HumanMoveFlag.Left | HumanMoveFlag.Right)) != 0) WorldSound.EmitFootstep(m_human, FootstepKind.Side);

            if (m_landedHard) WorldSound.EmitFootstep(m_human, FootstepKind.Landing);
            else if ((m_moveFlagLt & HumanMoveFlag.Jump) != 0) WorldSound.EmitFootstep(m_human, FootstepKind.Jump);
        }

        /// <summary>
        /// Human 노드의 transform 을 틱 사이 보간 위치와 현재 회전으로 갱신한다. 매 렌더 프레임 호출된다.
        /// </summary>
        public void ApplyVisual()
        {
            m_human.Position = VisualPosition;
            // 몸통 yaw × 사망 pitch 합성. 살아있을 때는 사망 pitch 가 0 이다.
            // 쓰러지는 각도는 틱마다 바뀌므로 위치처럼 틱 사이를 보간한다. 그러지 않으면 높은 프레임에서 33Hz 로 끊겨 보인다.
            float deadPitch = Mathf.Lerp(m_prevDeadPitchAngle, m_deadPitchAngle, SimClock.InterpolationAlpha);
            m_human.Rotation = Coord.FromUnityEuler(new Vector3(deadPitch, m_rotationX, 0f));

            if (m_human.Alive)
            {
                m_human.HumanVisual?.SetArmPitch(m_armRotationY);
            }
        }

        private void Tick()
        {
            HumanTypeData type = m_human.HumanTypeData;
            if (type == null) return;

            HumanControllerData ctrl = DataManager.Instance.HumanParameterData.humanControllerData;
            float dt = SimClock.FrameTime;
            bool player = m_human == MapLoader.Player;

            // 비행 모드는 살아 있는 동안만이다. 죽으면 평소대로 떨어진다.
            if (m_flight && m_human.Alive)
            {
                TickFlight(type, dt);
                return;
            }

            // 원본 ControlProcess: 가속을 더한 뒤 이번 입력을 MoveFlag_lt 로 넘기고 입력을 비운다.
            // 아래 점프·미끄러짐 판정이 읽는 MoveFlag_lt 는 그래서 "이번 틱" 입력이다.
            ApplyAcceleration(type, dt);
            m_moveFlagLt = m_moveFlag;
            m_moveFlag = HumanMoveFlag.None;

            Vector3 pos2 = m_position;
            Vector3 pos = pos2;

            // 0. 치트(F5) 강제 상승 — 원본 object.cpp:2013-2017. 충돌 계산의 백업 위치(pos2)도 함께 올려 수직 충돌이 되돌리지 못하게 한다.
            if (m_cheatRise)
            {
                m_moveVelocity.Y = 0f;
                float rise = k_cheatRiseSpeed * dt;
                pos.Y += rise;
                pos2.Y += rise;
            }

            // 1. 수평 이동 반영 후 감쇠 (원본: pos += move; move *= 0.5)
            pos.X += m_moveVelocity.X * dt;
            pos.Z += m_moveVelocity.Z * dt;
            float decay = Mathf.Exp(-type.attenuation * dt);
            m_moveVelocity.X *= decay;
            m_moveVelocity.Z *= decay;

            // 2. 이동 방향
            float dx = pos.X - pos2.X;
            float dz = pos.Z - pos2.Z;
            float speed = Mathf.Sqrt(dx * dx + dz * dz);
            float dirX = 0f;
            float dirZ = 0f;
            if (speed > 1e-6f)
            {
                dirX = dx / speed;
                dirZ = dz / speed;
            }

            float R = MapRadius;
            float H = Height;
            float waistY = H * 0.5f; // 원본 HUMAN_MAPCOLLISION_HEIGHT = 키의 절반
            float slopeLimit = Mathf.DegToRad(ctrl.controllerSlopeLimit);

            BuildNearBlocks(pos, H);
            List<Block> blocks = m_nearBlocks;

            if (speed > 0f || m_moveVelocity.Y != 0f)
            {
                // 3a. 머리
                for (int i = 0; i < blocks.Count; i++)
                {
                    var head = new Vector3(pos.X, pos.Y + H - k_headCheckOffset, pos.Z);
                    if (CollisionBlockScratch(blocks[i], ref pos, pos2, head, 0x01))
                    {
                        if (m_moveVelocity.Y > 0f) m_moveVelocity.Y = 0f;
                    }
                }

                // 3b. 발밑
                for (int i = 0; i < blocks.Count; i++)
                {
                    CollisionBlockScratch(blocks[i], ref pos, pos2, pos, 0x00);
                }

                // 3c. 허리 3점 — 진행 방향 앞, 그리고 원본이 쓰는 두 측면 점.
                // 원본은 (dist_z, dist_x) 와 (−dist_z, −dist_x) 순서인데, Godot 좌표는 원본의 X 가 뒤집혀 있어 같은 두 점이 반대 순서로 나온다.
                for (int i = 0; i < blocks.Count; i++)
                {
                    CollisionBlockScratch(blocks[i], ref pos, pos2,
                        new Vector3(pos.X + dirX * R, pos.Y + waistY, pos.Z + dirZ * R), 0x02);
                    CollisionBlockScratch(blocks[i], ref pos, pos2,
                        new Vector3(pos.X - dirZ * R, pos.Y + waistY, pos.Z - dirX * R), 0x02);
                    CollisionBlockScratch(blocks[i], ref pos, pos2,
                        new Vector3(pos.X + dirZ * R, pos.Y + waistY, pos.Z + dirX * R), 0x02);
                }

                // 3d. 추가 충돌 — 플래그가 켜진 미션에서만 (원본 AddCollisionFlag, 표준 맵 SCHOOL 용).
                if (MapLoader.Instance.AdjustCollision)
                {
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        CollisionBlockScratch(blocks[i], ref pos, pos2,
                            new Vector3(pos.X, pos.Y + k_addHeightA, pos.Z), 0x02);
                        CollisionBlockScratch(blocks[i], ref pos, pos2,
                            new Vector3(pos.X, pos.Y + k_addHeightB, pos.Z), 0x02);
                    }
                }

                // 3e. 낮은 턱 오르기 (원본 object.cpp:1607-1644)
                if ((Mathf.Abs(m_moveVelocity.X) > k_climbMinSpeed || Mathf.Abs(m_moveVelocity.Z) > k_climbMinSpeed)
                    && m_moveYUpper == 0)
                {
                    bool climb = false;
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        // 발끝만 블록에 묻혀 있고 그 위(오를 수 있는 높이)는 비어 있으면 오른다.
                        var toe = new Vector3(pos.X + dirX * k_climbForwardDist, pos.Y + k_collisionAddSize, pos.Z + dirZ * k_climbForwardDist);
                        var top = new Vector3(toe.X, toe.Y + k_climbHeight, toe.Z);
                        if (blocks[i].Contains(toe) && !blocks[i].Contains(top))
                        {
                            climb = true;

                            // 급경사 위에 서 있으면 오르지 않는다.
                            if (blocks[i].IntersectRay(pos, Vector3.Down, 0.12f, out int face, out _))
                            {
                                if (Mathf.Acos(Mathf.Clamp(blocks[i].faceNormals[face].Y, -1f, 1f)) > slopeLimit)
                                {
                                    climb = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (climb)
                    {
                        pos.Y += ctrl.controllerStepClimbSpeed * dt;
                        m_moveVelocity.Y *= 0.2f;
                    }
                }

                // 3f. 이동한 자리가 블록에 묻혔으면 이동을 무효로 한다 (원본 object.cpp:1647-1668)
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (blocks[i].IntersectRay(new Vector3(pos.X, pos.Y + H - k_shoulderRayOffset, pos.Z),
                        Vector3.Down, H - k_shoulderRayOffset * 2f, out _, out _))
                    {
                        pos.X = pos2.X;
                        pos.Z = pos2.Z;
                    }

                    float shoulderY = pos.Y + H - k_shoulderInsideOffset;
                    if (blocks[i].Contains(new Vector3(pos.X, shoulderY, pos.Z)))
                    {
                        // 예측 지점은 브로드페이즈 범위를 벗어날 수 있어 전체 블록으로 검사한다 (원본 CheckALLBlockInside).
                        var predicted = new Vector3(
                            pos.X + m_moveVelocity.X * k_embedPredictionTime,
                            shoulderY,
                            pos.Z + m_moveVelocity.Z * k_embedPredictionTime);
                        if (MapLoader.IsInsideBlock(predicted))
                        {
                            pos = pos2;
                            if (m_moveVelocity.Y > 0f) m_moveVelocity.Y = 0f;
                        }
                    }
                }
            }

            if (m_moveYUpper > 0) m_moveYUpper--;

            // 4. 낙하와 접지 판정 (프레임당 3회 나눠서)
            bool landed = false;
            bool alive = m_human.Alive;
            for (int ycnt = 0; ycnt < 3; ycnt++)
            {
                float ang = Mathf.Atan2(m_moveVelocity.Z, m_moveVelocity.X);
                float cos = Mathf.Cos(ang);
                float sin = Mathf.Sin(ang);

                pos.Y += m_moveVelocity.Y * dt * k_fallSubstep;
                float gy = pos.Y + k_groundHeight;

                if (!alive)
                {
                    // 시체: 발 한 점만 본다 (원본 deadstate 2, object.cpp:1320-1326).
                    if (AnyBlockContains(blocks, pos.X, gy, pos.Z))
                    {
                        landed = true;
                        break;
                    }
                }
                else if (player)
                {
                    // 플레이어: 4방향 점이 전부 블록 안일 때만 접지. 작은 반경과 큰 반경 두 번 본다.
                    if (AllFourGrounded(blocks, pos, gy, cos, sin, k_groundR1) || AllFourGrounded(blocks, pos, gy, cos, sin, k_groundR2))
                    {
                        landed = true;
                        break;
                    }
                }
                else
                {
                    // NPC: 바로 아래, 아니면 진행 방향으로 살짝 벗어난 점.
                    if (AnyBlockContains(blocks, pos.X, gy, pos.Z)
                        || AnyBlockContains(blocks, pos.X + cos * k_groundR3, gy, pos.Z + sin * k_groundR3))
                    {
                        landed = true;
                        break;
                    }
                }

                m_moveVelocity.Y -= ctrl.gravityAcceleration * dt * (1f / 3f);
                if (m_moveVelocity.Y < ctrl.fallMaxSpeed) m_moveVelocity.Y = ctrl.fallMaxSpeed;
            }

            m_grounded = landed;
            m_landedHard = landed && m_moveVelocity.Y < k_landingSoundSpeed;

            // 5. 접지 처리: 낙하 데미지, 점프, 급경사 미끄러짐
            if (landed)
            {
                // 낙하 데미지 — 임계 속도보다 빠르게 착지한 프레임 1회 (원본 object.cpp:1792-1797).
                if (alive && m_moveVelocity.Y < ctrl.fallMinSpeed)
                {
                    float scale = ctrl.fallDamageMax / Mathf.Abs(ctrl.fallMaxSpeed - ctrl.fallMinSpeed);
                    float damage = GameRandom.Gameplay.Range(0, ctrl.fallDamageRandomMax)
                                 + Mathf.Floor(scale * Mathf.Abs(m_moveVelocity.Y - ctrl.fallMinSpeed));
                    m_human.ApplyDamage(damage);
                }

                m_moveVelocity.Y = 0f;

                if ((m_moveFlagLt & HumanMoveFlag.Jump) != 0 && m_moveYUpper == 0)
                {
                    m_moveVelocity.Y = type.jumpSpeed;
                }

                // 가만히 서 있을 때는 4프레임에 1번꼴로 미끄러짐 판정을 건너뛴다 (원본 GetRand(4) == 0).
                bool slide = true;
                if (GameRandom.Gameplay.Range(0, 4) == 0)
                {
                    const HumanMoveFlag moving = HumanMoveFlag.Forward | HumanMoveFlag.Back | HumanMoveFlag.Left | HumanMoveFlag.Right | HumanMoveFlag.Walk;
                    if ((m_moveFlagLt & moving) == 0 && m_moveYUpper == 0) slide = false;
                }

                // 발밑 면을 찾아 급경사면 면을 따라 미끄러뜨린다.
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (!blocks[i].IntersectRay(new Vector3(pos.X, pos.Y + 0.25f, pos.Z), Vector3.Down, 0.35f, out int face, out _))
                    {
                        continue;
                    }

                    Vector3 n = blocks[i].faceNormals[face];
                    if (slide && Mathf.Acos(Mathf.Clamp(n.Y, -1f, 1f)) > slopeLimit)
                    {
                        m_moveVelocity = new Vector3(n.X * k_slideHorizontalSpeed, n.Y * k_slideVerticalSpeed, n.Z * k_slideHorizontalSpeed);

                        // 미끄러질 자리가 블록 안이면 그 방향 속도를 없앤다.
                        Vector3 predicted = pos + m_moveVelocity * k_slidePredictionTime;
                        for (int j = 0; j < blocks.Count; j++)
                        {
                            if (blocks[j].Contains(predicted))
                            {
                                m_moveVelocity.Y = 0f;
                                if (blocks[j].Contains(new Vector3(predicted.X, pos.Y, predicted.Z)))
                                {
                                    m_moveVelocity.X = 0f;
                                    m_moveVelocity.Z = 0f;
                                    break;
                                }
                            }
                        }

                        m_moveYUpper = k_moveYUpperCooldown;
                    }
                    break;
                }
            }

            // 6. 이동량이 명백히 이상하면 되돌린다 (원본 object.cpp:1856-1869)
            Vector3 moved = pos - pos2;
            float movedXZ = Mathf.Sqrt(moved.X * moved.X + moved.Z * moved.Z);
            float expectedXZ = Mathf.Sqrt(m_moveVelocity.X * m_moveVelocity.X + m_moveVelocity.Z * m_moveVelocity.Z) * 2f * dt;
            if (moved.Length() > k_checkMaxDist || movedXZ - expectedXZ > k_abnormalMoveMargin)
            {
                pos = pos2;
            }

            // 7. 플레이어만: 이전 위치와 새 위치 사이(어깨 높이)에 블록이 있으면 되돌린다 (원본 object.cpp:1871-1881)
            if (player)
            {
                Vector3 travel = pos - pos2;
                float travelLength = travel.Length();
                if (travelLength > 1e-6f
                    && MapLoader.RaycastBlock(pos2 + Vector3.Up * (H - k_moveCheckOffset), travel / travelLength, travelLength, out _))
                {
                    pos = pos2;
                }
            }

            // 8. 추락 한계: 원본 object.cpp:2083-2088. 위치를 한계에 묶고 사망 진입시킨다.
            bool clamped = false;
            if (pos.Y < ctrl.deadlineY)
            {
                pos.Y = ctrl.deadlineY;
                m_moveVelocity.Y = 0f;
                clamped = true;
            }

            if (m_human.Alive && (m_human.HP <= 0f || clamped))
            {
                EnterDeadState(ref pos);
            }

            m_position = pos;
        }

        /// <summary>
        /// Alive → Falling 사망 진입. 쓰러지는 방향을 정하고 시체 함몰 방지로 살짝 띄운다.
        /// </summary>
        /// <param name="pos">이번 틱의 위치. 띄운 높이가 반영된다.</param>
        private void EnterDeadState(ref Vector3 pos)
        {
            m_human.SetDeadState(HumanDeadState.Falling);
            m_human.OnDeath();

            // 원본 object.cpp:1213-1222 — 마지막 피격 방향과 본인 yaw 차이로 앞/뒤 분기.
            // 차이가 90° 미만(등 뒤에서 맞음)이면 앞으로 엎어지고, 그 외에는 뒤로 자빠진다.
            float deltaYaw = Coord.DeltaAngle(m_human.HitYaw, m_rotationX);
            m_deadDirection = Mathf.Abs(deltaYaw) < 90f ? 1f : -1f;
            m_deadAddRy = 0f;
            m_deadPitchAngle = 0f;

            pos.Y += k_deadPopupHeight;
        }

        /// <summary>
        /// 사망 상태머신 진행. 살아 있으면 아무것도 하지 않는다. 원본 object.cpp:1265-1377 deadstate 1/2/3 대응.
        /// Falling: 회전을 누적하며 머리 위치를 예측해 평지 안착(Settling) / 절벽 추락(HeadStuck) / 벽 걸림(LegSliding) 으로 분기.
        /// </summary>
        private void TickDeadState()
        {
            if (m_human.Alive) return;

            float dt = SimClock.FrameTime;
            float deadlineY = DataManager.Instance.HumanParameterData.humanControllerData.deadlineY;

            switch (m_human.DeadState)
            {
                case HumanDeadState.Falling:
                {
                    // 추락 한계 근처에서 이미 90° 이상이면 즉시 안착 (원본 object.cpp:1273-1279).
                    if (m_position.Y <= deadlineY + 1.0f && Mathf.Abs(m_deadPitchAngle) >= k_deadFlatLayPitch)
                    {
                        SettleFlat();
                        break;
                    }

                    if (Mathf.Abs(m_deadPitchAngle) >= k_deadFreeFallEntryPitch)
                    {
                        EnterHeadStuck();
                        break;
                    }

                    m_deadAddRy += k_deadRotationAccel * dt * m_deadDirection;
                    float predictedPitch = m_deadPitchAngle + m_deadAddRy * dt;

                    // 추락 한계 아래에서는 머리 충돌을 보지 않고 회전만 누적한다 (원본 object.cpp:1289-1291).
                    if (m_position.Y <= deadlineY)
                    {
                        m_deadPitchAngle = predictedPitch;
                        break;
                    }

                    if (Mathf.Abs(predictedPitch) >= k_deadFreeFallEntryPitch)
                    {
                        m_deadPitchAngle = predictedPitch;
                        EnterHeadStuck();
                        break;
                    }

                    if (MapLoader.IsInsideBlock(m_position + HeadOffset(predictedPitch)))
                    {
                        if (Mathf.Abs(predictedPitch) > k_deadFlatLayPitch)
                        {
                            // 지면에 머리가 닿음 → 90° 로 눕혀 안착.
                            SettleFlat();
                        }
                        else
                        {
                            // 벽에 머리가 걸림 → 다리를 뒤로 빼며 계속 쓰러진다.
                            m_human.SetDeadState(HumanDeadState.LegSliding);
                        }
                        break;
                    }

                    m_deadPitchAngle = predictedPitch;
                    break;
                }

                case HumanDeadState.HeadStuck:
                    // 회전은 멈추고, 낙하는 Tick 의 낙하 구간이 처리한다. 수직 속도가 0 이면 발이 닿은 것이다.
                    if (m_position.Y <= deadlineY || m_moveVelocity.Y == 0f)
                    {
                        m_human.SetDeadState(HumanDeadState.Settling);
                    }
                    break;

                case HumanDeadState.LegSliding:
                {
                    // 원본 deadstate 3 (object.cpp:1334-1377). 회전을 계속하면서 발을 뒤로 sin(회전량)×키 만큼 빼
                    // 머리가 벽 안으로 더 들어가지 않게 한다.
                    if (Mathf.Abs(m_deadPitchAngle) >= k_deadFlatLayPitch)
                    {
                        SettleFlat();
                        break;
                    }

                    m_deadAddRy += k_deadRotationAccel * dt * m_deadDirection;
                    float deltaPitch = m_deadAddRy * dt;
                    float predictedPitch = m_deadPitchAngle + deltaPitch;

                    Vector3 slide = -Coord.YawForward(m_rotationX) * (Mathf.Sin(Mathf.DegToRad(deltaPitch)) * Height);
                    Vector3 nextPosition = m_position + slide;

                    if (MapLoader.IsInsideBlock(nextPosition + Vector3.Up * 0.1f)
                        || MapLoader.IsInsideBlock(nextPosition + HeadOffset(predictedPitch)))
                    {
                        m_deadAddRy = 0f;
                        m_human.SetDeadState(HumanDeadState.Settling);
                        break;
                    }

                    m_position = nextPosition;
                    m_deadPitchAngle = predictedPitch;
                    break;
                }

                case HumanDeadState.Settling:
                    m_human.SetDeadState(HumanDeadState.Done);
                    break;
            }
        }

        private void SettleFlat()
        {
            m_deadPitchAngle = k_deadFlatLayPitch * m_deadDirection;
            m_deadAddRy = 0f;
            m_human.SetDeadState(HumanDeadState.Settling);
        }

        private void EnterHeadStuck()
        {
            m_human.SetDeadState(HumanDeadState.HeadStuck);
            m_moveVelocity.Y = 0f;
        }

        /// <summary>
        /// 사망 회전 각도에서 발 기준 머리 위치의 오프셋을 구한다. 원본 deadstate 1 머리 위치 예측 (object.cpp:1294-1296) 대응.
        /// </summary>
        /// <param name="pitchDeg">사망 회전 각도 (도, + 앞으로).</param>
        /// <returns>발에서 머리까지의 벡터.</returns>
        private Vector3 HeadOffset(float pitchDeg)
        {
            float rad = Mathf.DegToRad(pitchDeg);
            return Vector3.Up * (Height * Mathf.Cos(rad)) + Coord.YawForward(m_rotationX) * (Height * Mathf.Sin(rad));
        }

        /// <summary>
        /// 원본 human::CollisionBlockScratch 포팅. 판정 점이 이번 이동으로 블록 면을 뚫고 들어갔으면
        /// 면 법선에 따라 이동을 미끄러뜨린 위치로 pos 를 고친다.
        /// </summary>
        /// <param name="block">검사할 블록.</param>
        /// <param name="pos">현재 위치. 충돌 시 보정된다.</param>
        /// <param name="posOld">이번 틱 시작 위치.</param>
        /// <param name="check">판정할 점 (머리, 발, 허리 등).</param>
        /// <param name="mode">0x00: 통상, 0x01: Y 상승 금지, 0x02: Y 고정.</param>
        /// <returns>블록 면에 맞아 보정이 일어났으면 true.</returns>
        private static bool CollisionBlockScratch(Block block, ref Vector3 pos, Vector3 posOld, Vector3 check, int mode)
        {
            // 발밑(0x00)은 바닥 이음매에 걸리지 않게 판정 높이를 살짝 올린다 (원본 메모 참조).
            if (mode == 0x00) check.Y += k_collisionAddSize;

            Vector3 posBackup = pos;

            Vector3 v = pos - posOld;
            float dist = v.Length();
            if (dist < 1e-6f) return false;
            v /= dist;

            // 판정 점의 틱 시작 위치에서 이동 방향으로 쏜다. 경계 위에서 시작해도 면을 놓치지 않게 살짝 뒤에서 시작한다.
            const float k_rayStartMargin = 1e-4f;
            Vector3 rayStart = check - v * (dist + k_rayStartMargin);
            if (!block.IntersectRay(rayStart, v, dist + k_rayStartMargin, out int face, out _))
            {
                return false;
            }

            // 면과 이동 벡터가 이루는 각. 정면 충돌(π)이면 0(완전 정지), 스치듯(π/2)이면 1(이동 유지).
            Vector3 n = block.faceNormals[face];
            float dot = v.Dot(n);
            if (dot >= 0f) return false;

            float faceAngle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            float per = faceAngle > 1e-6f ? (Mathf.Pi / faceAngle - 1f) : 0f;

            // (이동 + 법선) 방향과 원래 이동 방향을 각도 비율로 섞는다.
            Vector3 v2 = v + n;
            if (v2.LengthSquared() > 1e-8f) v2 = v2.Normalized();
            Vector3 blended = v2 * (1f - per) + v * per;
            if (blended.LengthSquared() > 1e-8f) blended = blended.Normalized();

            // 수평으로 전혀 움직이지 않으면 법선을 쓴다.
            if (Mathf.Abs(blended.X) < 1e-6f && Mathf.Abs(blended.Z) < 1e-6f)
            {
                blended = n;
            }

            Vector3 newPos = blended * (per * dist) + posOld;

            // 보정한 위치가 여전히 블록 안이면 이동 무효.
            if (block.Contains(newPos)) newPos = posOld;

            if (mode == 0x01 && newPos.Y > posBackup.Y) newPos.Y = posBackup.Y;
            if (mode == 0x02) newPos.Y = posBackup.Y;

            pos = newPos;
            return true;
        }

        /// <summary>
        /// 원본 human::CollisionMap 의 CheckBlockID[] 프리필터 대응. 캐릭터 주변 상자에 걸치는 블록만 추린다.
        /// </summary>
        /// <param name="pos">현재 위치 (발 기준).</param>
        /// <param name="height">키 — 머리/어깨 판정 점을 범위에 넣기 위한 상단 확장.</param>
        private void BuildNearBlocks(Vector3 pos, float height)
        {
            m_nearBlocks.Clear();

            var margin = new Vector3(k_checkMaxDist, k_checkMaxDist, k_checkMaxDist);
            Vector3 lo = pos - margin;
            Vector3 hi = pos + margin + Vector3.Up * height;

            IReadOnlyList<Block> all = MapLoader.BlockColliders;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].OverlapsAABB(lo, hi)) m_nearBlocks.Add(all[i]);
            }
        }

        private static bool AnyBlockContains(List<Block> blocks, float x, float y, float z)
        {
            var point = new Vector3(x, y, z);
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].Contains(point)) return true;
            }
            return false;
        }

        /// <summary>
        /// 진행 방향 기준 앞·뒤·좌·우 네 점이 모두 블록 안인지 본다 (원본 플레이어 접지 판정).
        /// </summary>
        /// <param name="blocks">근처 블록.</param>
        /// <param name="pos">현재 위치.</param>
        /// <param name="y">판정 높이.</param>
        /// <param name="cos">진행 방향의 X 성분.</param>
        /// <param name="sin">진행 방향의 Z 성분.</param>
        /// <param name="radius">중심에서 각 점까지의 거리.</param>
        /// <returns>네 점 모두 블록 안이면 true.</returns>
        private static bool AllFourGrounded(List<Block> blocks, Vector3 pos, float y, float cos, float sin, float radius)
        {
            return AnyBlockContains(blocks, pos.X + cos * radius, y, pos.Z + sin * radius)
                && AnyBlockContains(blocks, pos.X - cos * radius, y, pos.Z - sin * radius)
                && AnyBlockContains(blocks, pos.X - sin * radius, y, pos.Z + cos * radius)
                && AnyBlockContains(blocks, pos.X + sin * radius, y, pos.Z - cos * radius);
        }

        /// <summary>
        /// 비행 모드의 한 틱 (디버그 콘솔의 flight, 원본에 없는 기능).
        /// 전진·후진은 시선 방향(위아래 포함)으로, 좌우는 수평 옆으로 움직인다. 점프 입력은 버린다. 블록을 통과하고 중력을 받지 않는다.
        /// 속도는 평지에서 달릴 때 수렴하는 속도와 같고, 걷기 입력을 함께 주면 걷는 속도가 된다.
        /// </summary>
        /// <param name="type">인간 종류 데이터 (가속도와 감쇠).</param>
        /// <param name="dt">틱 시간.</param>
        private void TickFlight(HumanTypeData type, float dt)
        {
            m_moveFlagLt = m_moveFlag & ~HumanMoveFlag.Jump;
            m_moveFlag = HumanMoveFlag.None;
            m_moveVelocity = Vector3.Zero;
            m_moveYUpper = 0;
            // 날고 있는 동안은 공중에 뜬 상태로 치지 않는다 (공중 조준 오차 가산이 붙지 않는다. 사용자 결정).
            m_grounded = true;
            m_landedHard = false;

            float forward = ((m_moveFlagLt & HumanMoveFlag.Forward) != 0 ? 1f : 0f) - ((m_moveFlagLt & HumanMoveFlag.Back) != 0 ? 1f : 0f);
            float right = ((m_moveFlagLt & HumanMoveFlag.Right) != 0 ? 1f : 0f) - ((m_moveFlagLt & HumanMoveFlag.Left) != 0 ? 1f : 0f);
            Vector3 direction = Coord.AimDirection(m_rotationX, m_armRotationY) * forward + Coord.YawRight(m_rotationX) * right;
            if (direction.LengthSquared() < 1e-6f) return;

            // 평소 이동은 틱마다 가속을 더하고 감쇠하므로 속도가 "가속 × dt ÷ (1 − 감쇠)"로 수렴한다. 그 값을 바로 쓴다.
            bool walk = (m_moveFlagLt & HumanMoveFlag.Walk) != 0;
            float accel = walk ? type.progressWalkAcceleration : type.progressRunAcceleration;
            float decay = Mathf.Exp(-type.attenuation * dt);
            float speed = decay < 1f ? accel * dt / (1f - decay) : accel * dt;

            m_position += direction.Normalized() * (speed * dt);
        }

        /// <summary>
        /// 원본 human::ControlProcess — 이동 플래그 조합에 따라 진행 방향과 가속도를 정해 속도에 더한다.
        /// </summary>
        /// <param name="type">인간 종류 데이터 (가속도).</param>
        /// <param name="dt">틱 시간.</param>
        private void ApplyAcceleration(HumanTypeData type, float dt)
        {
            const HumanMoveFlag directions = HumanMoveFlag.Forward | HumanMoveFlag.Back | HumanMoveFlag.Left | HumanMoveFlag.Right;
            const float k_invSqrt2 = 0.7071068f;

            // 정면 기준 (오른쪽, 앞) 성분.
            float right = 0f;
            float forward = 0f;
            float accel = 0f;

            if ((m_moveFlag & HumanMoveFlag.Walk) != 0)
            {
                forward = 1f;
                accel = type.progressWalkAcceleration;
            }
            else
            {
                float runForward = type.progressRunAcceleration;
                float runSide = type.sidewaysRunAcceleration;
                float runBack = type.regressRunAcceleration;

                switch (m_moveFlag & directions)
                {
                    case HumanMoveFlag.Forward:
                        forward = 1f; accel = runForward; break;
                    case HumanMoveFlag.Back:
                        forward = -1f; accel = runBack; break;
                    case HumanMoveFlag.Left:
                        right = -1f; accel = runSide; break;
                    case HumanMoveFlag.Right:
                        right = 1f; accel = runSide; break;
                    case HumanMoveFlag.Forward | HumanMoveFlag.Left:
                        right = -k_invSqrt2; forward = k_invSqrt2; accel = (runForward + runSide) * 0.5f; break;
                    case HumanMoveFlag.Forward | HumanMoveFlag.Right:
                        right = k_invSqrt2; forward = k_invSqrt2; accel = (runForward + runSide) * 0.5f; break;
                    case HumanMoveFlag.Back | HumanMoveFlag.Left:
                        right = -k_invSqrt2; forward = -k_invSqrt2; accel = runBack; break;
                    case HumanMoveFlag.Back | HumanMoveFlag.Right:
                        right = k_invSqrt2; forward = -k_invSqrt2; accel = runBack; break;
                }
            }

            if (accel <= 0f) return;

            Vector3 direction = Coord.YawForward(m_rotationX) * forward + Coord.YawRight(m_rotationX) * right;
            m_moveVelocity += direction * (accel * dt);
        }
    }
}

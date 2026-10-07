using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// AI 의 전투 상태. 원본 OpenXOPS AI_DEAD ~ AI_NORMAL (ai.h:196-199).
    /// </summary>
    public enum AIBattleMode
    {
        Dead,
        Action,
        Caution,
        Normal,
    }

    /// <summary>
    /// 사람 한 명의 AI. 원본 OpenXOPS AIcontrol (ai.cpp) 에 해당하며, 상태(평상시·경계·전투)에 따라 탐색·조준·사격·경로 이동을 정한다.
    /// 노드가 아닌 순수 클래스이고 Human 이 하나씩 갖는다. AIController 가 틱마다 Tick 을 한 번 부른다 (원본 한 프레임).
    /// 사람에게는 HumanController.SetInput(이동·조준)과 Human 의 무기 함수로만 손을 댄다.
    /// 회전·경로 이동 의사는 매 틱 새로 정하고, 두리번거림과 전투 중 회피 이동만 틱을 넘어 유지한다.
    /// 관심사별 partial: 이 파일(상태 전이) / AIBrainAim(회전·탐색·시야) / AIBrainCombat(사격·회피) / AIBrainNavigation(경로) / AIBrainWeapon(무기 운용) / AIBrainZombie(근접 공격).
    /// </summary>
    public partial class AIBrain
    {
        // 경계 남은 틱이 이 값 아래면 확률로 일찍 끝낸다 (원본 ai.cpp:1641 — 100 프레임, 1/50).
        private const int k_cautionRandomEndTicks = 100;
        private const int k_cautionRandomEndChance = 50;
        // 추적 중 경계: 대상과 수평으로 이만큼(m) 넘게 떨어지면 1/3 확률로 경계를 끝낸다 (원본 ai.cpp:1647-1657 — 25.0).
        private const float k_cautionTrackingLeaveDist = 2.5f;
        private const int k_cautionTrackingLeaveChance = 3;

        private readonly Human m_self;
        private readonly HumanController m_controller;
        private readonly AIMoveNavi m_nav = new AIMoveNavi();

        private AIBattleMode m_mode = AIBattleMode.Normal;
        private Human m_enemy;
        private bool m_noFight;
        private int m_cautionCnt;
        private int m_actionCnt;
        private int m_waitCnt;
        private bool m_longAttack;
        // 경로의 수류탄 투척을 직전 틱에 마쳤는지 (원본 EventWeaponShot). 연달아 던지는 것을 막는다.
        private bool m_eventWeaponShot;

        // 경계를 시작한 자리. 경계가 끝나면 여기로 돌아온 뒤 평상시가 된다 (원본 cautionback_posx/z).
        private Vector3 m_cautionBackPosition;
        // 맞은 방향을 향해 돌아보는 중인지와 그 방향 (원본 FaceCaution_flag / FaceCaution_rx).
        private bool m_faceCaution;
        private float m_faceCautionYaw;

        // 회전 속도 (도/틱). 원본 AIObjectDriver 의 addrx / addry.
        private float m_addYaw;
        private float m_addPitch;

        // 이번 틱의 회전·이동 의사. 매 틱 비우고 상태별 처리가 채운다.
        private bool m_turnLeft;
        private bool m_turnRight;
        private bool m_turnUp;
        private bool m_turnDown;
        private HumanMoveFlag m_moveIntent;

        // 틱을 넘어 유지되는 의사: 두리번거림과 전투 중 회피 이동.
        private bool m_scanLeft;
        private bool m_scanRight;
        private HumanMoveFlag m_combatMove;

        public Human Self => m_self;
        public AIBattleMode Mode => m_mode;
        public Human Enemy => m_enemy;
        public AIMoveNavi Navi => m_nav;
        public bool NoFight => m_noFight;
        public bool LongAttack => m_longAttack;
        public int CautionCount => m_cautionCnt;

        private static HumanAIParameterData AIData => DataManager.Instance.HumanParameterData.humanAIParameterData;
        private static HumanGeneralData GeneralData => DataManager.Instance.HumanParameterData.humanGeneralData;

        /// <summary>
        /// AI 를 만들고 첫 경로 포인트를 잡는다. 원본 AIcontrol::Init (ai.cpp:1766-1804).
        /// </summary>
        /// <param name="self">이 AI 가 움직일 사람. 컨트롤러와 무기가 준비된 뒤여야 한다.</param>
        public AIBrain(Human self)
        {
            m_self = self;
            m_controller = self.Controller;
            m_nav.Init(self.HumanParam);
            m_nav.Refresh();
        }

        /// <summary>
        /// 한 틱 진행. 원본 AIcontrol::Process (ai.cpp:1934-1985): 사망 확인 → 무기 들기 → 상태별 처리 → 이동·회전 적용 → 무기 운용.
        /// </summary>
        /// <param name="heard">이번 틱에 위협이 되는 소리를 들었는지. 평상시와 경계에서만 쓰인다.</param>
        public void Tick(bool heard)
        {
            if (m_self.HP <= 0f || !m_self.Alive)
            {
                m_mode = AIBattleMode.Dead;
                return;
            }

            // HP 가 있는데 사망 상태로 남아 있으면 되살아난 것이다.
            if (m_mode == AIBattleMode.Dead)
            {
                m_mode = AIBattleMode.Normal;
                m_nav.Refresh();
            }

            m_turnLeft = m_turnRight = m_turnUp = m_turnDown = false;
            m_moveIntent = HumanMoveFlag.None;

            if (m_mode == AIBattleMode.Action) CombatMoveCancel();

            if (m_mode == AIBattleMode.Action || m_mode == AIBattleMode.Caution) HaveWeapon();

            switch (m_mode)
            {
                case AIBattleMode.Action: ActionMain(); break;
                case AIBattleMode.Caution: CautionMain(heard); break;
                default: NormalMain(heard); break;
            }

            ApplyControl();
            ControlWeapon();

            // 맨손 팔은 전투 동작(좀비 공격, 항복) 중에만 조준 방향을 따른다. 그 밖에는 데이터의 고정 자세다.
            // 전투가 끝난 뒤에는 팔이 고정 자세까지 내려올 때까지 조준 방향을 계속 따르게 해서, 고정 자세로 한 틱 만에 튀지 않게 한다.
            bool unarmed = m_self.CurrentWeapon.IsNone;
            bool lowering = unarmed && m_self.UnarmedArmDynamic && m_controller.Pitch < UnarmedRestPitch() - k_unarmedRestTolerance;
            m_self.SetUnarmedArmDynamic(unarmed && (m_mode == AIBattleMode.Action || lowering));
        }

        /// <summary>
        /// 맨손 팔이 고정 자세일 때와 같은 모양이 되는 시선 pitch 를 구한다. 맨손의 팔은 평상시에 계속 내려가므로(ArmAngle) 이 각에 닿으면 고정 자세로 바꿔도 튀지 않는다.
        /// </summary>
        /// <returns>pitch (도, 아래 +). 맨손 모델의 팔이 고정이 아니면 음의 무한대(내릴 것이 없다).</returns>
        private float UnarmedRestPitch()
        {
            WeaponModelData model = m_self.CurrentWeapon.ModelData;
            if (model == null || (!model.fixRightArm && !model.fixLeftArm)) return float.NegativeInfinity;

            // 데이터는 팔 기준(아래가 음수)이라 시선 pitch(아래가 양수)로 부호를 뒤집는다. AI 가 내릴 수 있는 한계를 넘지 않게 한다.
            float fixedAngle = model.fixRightArm ? model.fixedRightArmAngle : model.fixedLeftArmAngle;
            return Mathf.Min(-fixedAngle, AIData.aiTurnMaxPitchDeg);
        }

        /// <summary>
        /// 평상시. 원본 AIcontrol::NormalMain (ai.cpp:1673-1763). 맞거나, 적을 보거나, 소리를 들으면 경계로 바뀌고 그 밖에는 경로를 따른다.
        /// 우선적 달리기 중에는 경계를 건너뛰고 적을 본 경우에만 곧바로 전투로 들어간다.
        /// 원본에는 아군 시체를 보고 경계하는 조건(CheckCorpse)도 있지만 넣지 않았다.
        /// </summary>
        /// <param name="heard">소리를 들었는지.</param>
        private void NormalMain(bool heard)
        {
            m_combatMove = HumanMoveFlag.None;
            m_nav.Refresh();
            m_enemy = null;

            bool hit = m_self.ConsumeHit(out float faceYaw);
            if (m_noFight) heard = false;

            if (m_nav.Mode == AIMoveMode.Random) AdvancePath();

            int throwResult = 0;
            if (m_nav.Mode == AIMoveMode.Grenade)
            {
                throwResult = ThrowGrenade();
                if (throwResult != 0) AdvancePath();
            }
            if (throwResult != 1) m_eventWeaponShot = false;

            if (m_nav.Mode == AIMoveMode.Run2)
            {
                if (SearchEnemy() != 0) m_mode = AIBattleMode.Action;
                else MovePath();
            }
            else if (hit)
            {
                EnterCaution(true, faceYaw);
            }
            else if (SearchEnemy() != 0 || heard)
            {
                EnterCaution(false, 0f);
            }
            else
            {
                MovePath();
            }

            if (m_nav.Mode != AIMoveMode.Grenade) ArmAngle();
        }

        /// <summary>
        /// 경계. 원본 AIcontrol::CautionMain (ai.cpp:1587-1669). 적을 찾으면 전투로, 맞거나 소리를 들으면 경계를 처음부터 다시 센다.
        /// 다 세면 경계를 시작한 자리로 돌아간 뒤 평상시가 된다. 그동안 주위를 둘러본다.
        /// </summary>
        /// <param name="heard">소리를 들었는지.</param>
        private void CautionMain(bool heard)
        {
            m_combatMove = HumanMoveFlag.None;

            bool hit = m_self.ConsumeHit(out float faceYaw);
            if (m_noFight) heard = false;

            if (IsValidHuman(m_enemy) || SearchEnemy() != 0)
            {
                m_mode = AIBattleMode.Action;
                m_actionCnt = 0;
            }
            else if (hit)
            {
                m_cautionCnt = AIData.aiCautionFrames;
                m_faceCaution = true;
                m_faceCautionYaw = faceYaw;
            }
            else if (heard)
            {
                m_cautionCnt = AIData.aiCautionFrames;
            }
            else if (m_cautionCnt == 0)
            {
                if (!CheckTargetPos(true))
                {
                    MoveTarget(true);
                }
                else
                {
                    m_mode = AIBattleMode.Normal;
                    m_faceCaution = false;

                    // 경계 대기 포인트에서는 경계가 끝날 때 다음 포인트로 넘어간다.
                    RawPointData point = m_nav.CurrentPoint;
                    if (point != null && point.param0 == MapLoader.PointAIPath && point.param1 == AIMoveNavi.PathParamWaitAlert)
                    {
                        AdvancePath();
                    }
                }
            }
            else if (m_cautionCnt < k_cautionRandomEndTicks && !m_faceCaution)
            {
                if (GetRand(k_cautionRandomEndChance) == 0) m_cautionCnt = 0;
            }
            else
            {
                m_cautionCnt--;
            }

            // 추적 중에 대상과 너무 멀어졌으면 확률로 경계를 끝내고 따라간다.
            if (m_nav.Mode == AIMoveMode.Tracking && GetRand(k_cautionTrackingLeaveChance) == 0 && GodotObject.IsInstanceValid(m_nav.TargetHuman))
            {
                Vector3 toTarget = m_nav.TargetHuman.Controller.Position - m_controller.Position;
                toTarget.Y = 0f;
                if (toTarget.LengthSquared() > k_cautionTrackingLeaveDist * k_cautionTrackingLeaveDist) m_cautionCnt = 0;
            }

            TurnSeen();
            ArmAngle();
        }

        /// <summary>
        /// 전투. 원본 AIcontrol::ActionMain (ai.cpp:1527-1583). 조준·사격한 뒤, 우선적 달리기면 경로를 계속 따르고 그 밖에는 원거리 교전 중 가까운 적을 다시 찾는다.
        /// 전투가 끝나면 경계로 (우선적 달리기는 평상시로) 돌아간다.
        /// </summary>
        private void ActionMain()
        {
            if (!IsValidHuman(m_enemy))
            {
                LeaveAction();
                return;
            }

            Action();

            if (m_nav.Mode == AIMoveMode.Random) AdvancePath();

            if (m_nav.Run2)
            {
                if (CheckTargetPos(false))
                {
                    // 지나온 포인트를 경계가 끝났을 때 돌아갈 자리로 삼는다.
                    m_cautionBackPosition = m_nav.TargetPosition;
                    AdvancePath();
                }
                else
                {
                    MoveTarget2();
                }
            }
            else if (m_longAttack)
            {
                SearchShortEnemy();
            }

            if (ActionCancel()) LeaveAction();
        }

        /// <summary>
        /// 평상시에서 경계로 들어간다. 지금 자리를 돌아올 자리로 기억한다.
        /// </summary>
        /// <param name="face">맞은 방향을 돌아볼지.</param>
        /// <param name="faceYaw">돌아볼 방향 yaw (도).</param>
        private void EnterCaution(bool face, float faceYaw)
        {
            m_mode = AIBattleMode.Caution;
            m_cautionCnt = AIData.aiCautionFrames;
            m_cautionBackPosition = m_controller.Position;
            m_faceCaution = face;
            m_faceCautionYaw = faceYaw;
        }

        /// <summary>
        /// 전투를 끝낸다. 우선적 달리기 중이면 평상시로, 그 밖에는 경계로 간다.
        /// </summary>
        private void LeaveAction()
        {
            m_enemy = null;
            m_combatMove = HumanMoveFlag.None;

            if (m_nav.Run2)
            {
                m_mode = AIBattleMode.Normal;
            }
            else
            {
                m_mode = AIBattleMode.Caution;
                m_cautionCnt = AIData.aiCautionFrames;
                m_faceCaution = false;
            }
        }

        /// <summary>
        /// 다음 경로 포인트로 넘어가고 이동 모드를 다시 읽는다.
        /// </summary>
        private void AdvancePath()
        {
            m_nav.Next();
            m_nav.Refresh();
        }

        /// <summary>
        /// 강제로 경계시킨다 (디버그 치트). 원본 AIcontrol::SetCautionMode (ai.cpp:1855-1868). 우선적 달리기 중이면 아무것도 하지 않는다.
        /// </summary>
        public void SetCautionMode()
        {
            if (m_nav.Run2 || m_mode == AIBattleMode.Dead) return;

            if (m_mode == AIBattleMode.Normal) m_cautionBackPosition = m_controller.Position;
            m_mode = AIBattleMode.Caution;
            m_cautionCnt = AIData.aiCautionFrames;
            m_faceCaution = false;
        }

        /// <summary>
        /// 비전투 여부를 정한다 (디버그 치트). 켜면 적을 찾지 않고 소리에도 반응하지 않는다. 원본 AIcontrol::SetNoFightFlag.
        /// </summary>
        /// <param name="value">true 면 싸우지 않는다.</param>
        public void SetNoFight(bool value)
        {
            m_noFight = value;
        }

        /// <summary>
        /// 경로와 무관하게 지정한 자리에서 대기하게 한다 (치트 F9 의 제자리 경계).
        /// </summary>
        /// <param name="position">대기할 위치.</param>
        /// <param name="yawDeg">선호하는 방향 yaw (도).</param>
        public void SetHoldWait(Vector3 position, float yawDeg)
        {
            m_nav.SetHoldWait(position, yawDeg);
        }

        /// <summary>
        /// 경로와 무관하게 지정한 사람을 따라다니게 한다 (치트 F9 의 따라오기).
        /// </summary>
        /// <param name="target">따라갈 사람.</param>
        public void SetHoldTracking(Human target)
        {
            m_nav.SetHoldTracking(target);
        }

        /// <summary>
        /// AI 레벨에 해당하는 파라미터를 얻는다.
        /// </summary>
        /// <returns>AI 레벨 데이터. 데이터가 비어 있으면 null.</returns>
        private HumanAIData LevelData()
        {
            return AIData.aiData.GetClamped(m_self.AILevel);
        }

        /// <summary>
        /// 무기의 스코프 종류에 해당하는 AI 파라미터를 얻는다. 스코프가 없는 무기와 맨손은 0 번, 스코프 무기는 scopeIndex + 1 번이다 (원본 scopemode).
        /// 에드온 스코프(10000 이상)는 같은 번호의 에드온 항목을 쓴다 (에드온 쪽에는 "스코프 없음" 항목이 없으므로 1 을 더하지 않는다).
        /// </summary>
        /// <param name="weapon">무기.</param>
        /// <returns>스코프별 AI 데이터. 데이터가 비어 있으면 null.</returns>
        private static HumanAIScopeData ScopeData(Weapon weapon)
        {
            int scopeIndex = weapon.Data.scopeIndex;
            int index = !weapon.Data.scope ? 0 : (scopeIndex >= DataList<HumanAIScopeData>.AddonBase ? scopeIndex : scopeIndex + 1);
            return AIData.aiScopeData.GetClamped(index);
        }

        private static bool IsValidHuman(Human human)
        {
            return human != null && GodotObject.IsInstanceValid(human);
        }

        /// <summary>
        /// 원본 GetRand: 0 이상 num 미만의 정수. AI 판단은 게임 결과를 바꾸므로 게임플레이 난수에서 뽑는다.
        /// </summary>
        /// <param name="num">상한 (제외). 0 이하면 0.</param>
        /// <returns>난수.</returns>
        private static int GetRand(int num)
        {
            return num <= 0 ? 0 : GameRandom.Gameplay.Range(0, num);
        }
    }
}

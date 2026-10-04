using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 게임 맵에 배치된 인간 캐릭터의 데이터와 시각 표현을 관리하는 노드.
    /// 이동·충돌은 HumanController, 모델은 HumanVisual 이 담당하고, 이 노드의 transform 은 시각 전용(틱 사이 보간)이다.
    /// </summary>
    public partial class Human : Node3D
    {
        // 시각 보간이 SimClock(-200) 뒤, 카메라(PlayerController) 앞에 돌도록 하는 처리 순서.
        public const int VisualProcessPriority = 50;

        private float m_hp;
        private int m_team;
        private HumanDeadState m_deadState = HumanDeadState.Alive;

        private HumanData m_humanData;
        private HumanTypeData m_humanTypeData;
        private HumanController m_controller;
        private HumanVisual m_humanVisual;
        private RawPointData m_humanParam;
        private RawPointData m_humanDataParam;
        private int m_identifier;

        // 원본 human::Hit_rx — 마지막 피격 yaw (도). 사망 진입 시 앞/뒤 쓰러짐 분기에 쓴다. 살아있는 동안 클리어되지 않는다.
        private float m_hitYaw;
        // 이번 구간 피격 여부 (원본 human::HitFlag). AI 반응과 플레이어 피격 연출이 소비한다.
        private bool m_hitPending;
        // 적 총성·총알 통과·폭발 등 위협 소리를 들었다는 신호. AI 가 매 틱 소비해 경계로 전환한다.
        private bool m_threatHeard;

        public float HP => m_hp;
        public int Team => m_team;
        public HumanDeadState DeadState => m_deadState;
        public bool Alive => m_deadState == HumanDeadState.Alive;
        public HumanData HumanData => m_humanData;
        public HumanTypeData HumanTypeData => m_humanTypeData;
        public HumanController Controller => m_controller;
        public HumanVisual HumanVisual => m_humanVisual;
        public RawPointData HumanParam => m_humanParam;
        public RawPointData HumanDataParam => m_humanDataParam;
        public int Identifier => m_identifier;
        public float HitYaw => m_hitYaw;
        public float CameraHeight => m_controller.CameraHeight;

        // AI 레벨 = HumanData.aiIndex (원본 HumanParameter.AIlevel).
        public int AILevel => m_humanData != null ? m_humanData.aiIndex : 0;
        // AI 경로 시작 웨이포인트 식별번호 — HUMAN 포인트의 param2(=원본 p3).
        public int PathStartId => m_humanParam != null ? m_humanParam.param2 : 0;

        /// <summary>
        /// 포인트 데이터와 파라미터로부터 인간 캐릭터를 생성 및 초기화한다. 트리에 추가한 뒤 호출한다.
        /// </summary>
        /// <param name="humanParam">인간 배치 포인트 데이터 (위치, 방향, 식별번호).</param>
        /// <param name="humanDataParam">인간 정보 포인트 데이터 (종류, 팀).</param>
        public void CreateHuman(RawPointData humanParam, RawPointData humanDataParam)
        {
            m_humanParam = humanParam;
            m_humanDataParam = humanDataParam;
            m_identifier = humanParam.param3;

            HumanParameterData parameter = DataManager.Instance.HumanParameterData;
            int humanIndex = humanDataParam.param1;
            if (humanIndex >= 0 && humanIndex < parameter.humanData.Count)
            {
                m_humanData = parameter.humanData[humanIndex];
                int typeIndex = m_humanData.typeIndex;
                if (typeIndex >= 0 && typeIndex < parameter.humanTypeData.Count)
                {
                    m_humanTypeData = parameter.humanTypeData[typeIndex];
                }
            }

            ProcessPriority = VisualProcessPriority;
            Position = humanParam.position;

            m_controller = new HumanController(this, humanParam.position, humanParam.look);

            m_humanVisual = new HumanVisual { Name = "Visual" };
            AddChild(m_humanVisual);
            m_humanVisual.CreateHumanVisual(this, m_humanData);
            m_humanVisual.ApplyArmModel(NoneWeaponModel(), false);

            m_hp = m_humanData != null ? m_humanData.hp : 0f;
            m_team = humanDataParam.param2;
            m_deadState = m_hp > 0f ? HumanDeadState.Alive : HumanDeadState.Done;

            SimClock.Register(m_controller);
            m_controller.ApplyVisual();
        }

        public override void _Process(double delta)
        {
            m_controller?.ApplyVisual();
        }

        public override void _ExitTree()
        {
            SimClock.Unregister(m_controller);
        }

        /// <summary>
        /// 맨손(무기 없음)의 무기 모델 데이터를 찾는다. 무기 장착을 옮기기 전까지 팔 자세를 정하는 데 쓴다.
        /// </summary>
        /// <returns>맨손 무기 모델 데이터. 데이터가 없으면 null.</returns>
        private static WeaponModelData NoneWeaponModel()
        {
            WeaponParameterData weapon = DataManager.Instance.WeaponParameterData;
            int noneIndex = weapon.weaponGeneralData.noneWeaponIndex;
            if (noneIndex < 0 || noneIndex >= weapon.weaponData.Count) return null;

            int modelIndex = weapon.weaponData[noneIndex].modelIndex;
            return modelIndex >= 0 && modelIndex < weapon.weaponModelData.Count ? weapon.weaponModelData[modelIndex] : null;
        }

        /// <summary>
        /// 미션 이벤트(팀 변경) — 대상 인물의 팀번호를 바꾼다. 원본 EventControl::SetTeamID.
        /// </summary>
        /// <param name="value">새 팀번호.</param>
        public void SetTeam(int value)
        {
            m_team = value;
        }

        /// <summary>
        /// 사망 상태를 설정한다. 전이 로직은 HumanController 가 호출한다.
        /// </summary>
        /// <param name="value">새 사망 상태.</param>
        public void SetDeadState(HumanDeadState value)
        {
            m_deadState = value;
        }

        /// <summary>
        /// 데미지 적용. HP 만 차감한다. 사망 진입(Falling)은 HumanController 가 다음 틱에 처리한다.
        /// 원본 OpenXOPS human::SubHP (object.cpp:1060-1080) 단순화 버전.
        /// </summary>
        /// <param name="damage">데미지. 0 이하는 무시.</param>
        public void ApplyDamage(float damage)
        {
            if (!Alive || damage <= 0f) return;

            m_hp -= damage;
            if (m_hp < 0f) m_hp = 0f;
        }

        /// <summary>
        /// 마지막 피격 방향을 기록한다. 총알 측이 명중 시 호출한다.
        /// </summary>
        /// <param name="yawDeg">총알 진행 방향의 yaw (도).</param>
        public void SetHitYaw(float yawDeg)
        {
            m_hitYaw = yawDeg;
            m_hitPending = true;
        }

        /// <summary>
        /// 피격 소비. 원본 CheckHit + SetHitFlag(공격자 방향).
        /// </summary>
        /// <param name="faceYawDeg">공격자를 바라보는 yaw (= 총알 진행방향 + 180°).</param>
        /// <returns>마지막 소비 이후 맞았으면 true.</returns>
        public bool ConsumeHit(out float faceYawDeg)
        {
            faceYawDeg = m_hitYaw + 180f;
            bool pending = m_hitPending;
            m_hitPending = false;
            return pending;
        }

        /// <summary>
        /// 위협 소리를 들었음을 기록한다. 음원 쪽이 청취 범위 안의 Human 에게 호출한다.
        /// </summary>
        public void NotifyThreatHeard()
        {
            m_threatHeard = true;
        }

        /// <summary>
        /// 위협 소리 청취 여부를 소비한다.
        /// </summary>
        /// <returns>마지막 소비 이후 들었으면 true.</returns>
        public bool ConsumeThreatHeard()
        {
            bool heard = m_threatHeard;
            m_threatHeard = false;
            return heard;
        }
    }
}

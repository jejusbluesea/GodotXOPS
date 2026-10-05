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
        private AIBrain m_brain;
        private HumanHitboxSizeData m_hitboxSize;
        private RawPointData m_humanParam;
        private RawPointData m_humanDataParam;
        private int m_identifier;

        // 원본 human::Hit_rx — 마지막 피격 yaw (도). 사망 진입 시 앞/뒤 쓰러짐 분기에 쓴다. 살아있는 동안 클리어되지 않는다.
        private float m_hitYaw;
        // 이번 구간 피격 여부 (원본 human::HitFlag). AI 반응과 플레이어 피격 연출이 소비한다.
        private bool m_hitPending;
        // 적 총성·총알 통과·폭발 등 위협 소리를 들었다는 신호. AI 가 매 틱 소비해 경계로 전환한다.
        private bool m_threatHeard;
        // 데미지를 받지 않는지 (원본 human::Invincible). 점검 도구가 켠다.
        private bool m_invincible;

        public float HP => m_hp;
        public int Team => m_team;
        public HumanDeadState DeadState => m_deadState;
        public bool Alive => m_deadState == HumanDeadState.Alive;
        public HumanData HumanData => m_humanData;
        public HumanTypeData HumanTypeData => m_humanTypeData;
        public HumanController Controller => m_controller;
        public HumanVisual HumanVisual => m_humanVisual;
        // 이 사람의 AI. 플레이어가 조작하는 동안에는 돌지 않고 상태만 남아 있다.
        public AIBrain Brain => m_brain;
        // 이 사람 체형의 총알 판정 원기둥 (머리·상반신·다리). 데이터가 없으면 null.
        public HumanHitboxSizeData HitboxSize => m_hitboxSize;
        public RawPointData HumanParam => m_humanParam;
        public RawPointData HumanDataParam => m_humanDataParam;
        public int Identifier => m_identifier;
        public float HitYaw => m_hitYaw;
        public bool Invincible => m_invincible;
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

            var hitboxSizes = parameter.humanHitboxSizeData;
            int hitboxIndex = m_humanTypeData != null ? m_humanTypeData.hitboxSizeIndex : 0;
            m_hitboxSize = hitboxSizes.Count > 0 ? hitboxSizes[Mathf.Clamp(hitboxIndex, 0, hitboxSizes.Count - 1)] : null;

            m_hp = m_humanData != null ? m_humanData.hp : 0f;
            m_team = humanDataParam.param2;
            m_deadState = m_hp > 0f ? HumanDeadState.Alive : HumanDeadState.Done;

            EquipInitialWeapons();

            m_brain = new AIBrain(this);

            SimClock.Register(m_controller);
            m_controller.ApplyVisual();
        }

        public override void _Process(double delta)
        {
            m_controller?.ApplyVisual();
            PlayPendingFireEffects((float)delta);
        }

        public override void _ExitTree()
        {
            SimClock.Unregister(m_controller);
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
        /// 무적 여부를 정한다. 무적이면 HP 가 줄지 않는다. 맞은 반응(조준 흐트러짐, 밀림, 피격 방향)은 그대로 받는다. 원본 human::SetInvincibleFlag.
        /// </summary>
        /// <param name="value">true 면 무적.</param>
        public void SetInvincible(bool value)
        {
            m_invincible = value;
        }

        /// <summary>
        /// 살아 있는 사람의 HP 를 사람 데이터의 처음 값으로 되돌린다 (디버그 콘솔의 treat, 원본 gamemain.cpp:4326-4343).
        /// </summary>
        /// <returns>되돌렸으면 true. 죽었거나 사람 데이터가 없으면 false.</returns>
        public bool RestoreHP()
        {
            if (!Alive || m_humanData == null) return false;

            m_hp = m_humanData.hp;
            return true;
        }

        /// <summary>
        /// 사망 상태를 설정한다. 전이 로직은 HumanController 가 호출한다.
        /// </summary>
        /// <param name="value">새 사망 상태.</param>
        public void SetDeadState(HumanDeadState value)
        {
            bool aliveChanged = (m_deadState == HumanDeadState.Alive) != (value == HumanDeadState.Alive);
            m_deadState = value;

            // 생사가 바뀌면 팔을 다시 붙인다. 죽으면 조준 방향을 따르던 맨손 팔이 고정 자세로 돌아간다.
            if (aliveChanged && m_weapons[0] != null) ApplyActiveWeaponVisual();
        }

        /// <summary>
        /// 데미지 적용. HP 만 차감한다. 사망 진입(Falling)은 HumanController 가 다음 틱에 처리한다.
        /// 원본 OpenXOPS human::SubHP (object.cpp:1060-1080) 단순화 버전.
        /// </summary>
        /// <param name="damage">데미지. 0 이하는 무시.</param>
        public void ApplyDamage(float damage)
        {
            if (!Alive || damage <= 0f || m_invincible) return;

            m_hp -= damage;
            if (m_hp < 0f) m_hp = 0f;
        }

        /// <summary>
        /// 총알에 맞은 데미지와 조준 흐트러짐을 적용한다. 원본 human::HitBulletHead / HitBulletUp / HitBulletLeg (object.cpp:1032-1061):
        /// 데미지 = (int)(위력 × 부위 배율) + 부위별 난수. 배율과 난수 범위는 사람 종류 데이터에서 온다.
        /// </summary>
        /// <param name="part">맞은 부위.</param>
        /// <param name="attacks">총알의 현재 위력.</param>
        /// <returns>난수를 뺀 기본 데미지. 혈흔이 튀는 양을 정하는 데 쓴다.</returns>
        public int HitBullet(HumanHitPart part, int attacks)
        {
            HumanGeneralData general = DataManager.Instance.HumanParameterData.humanGeneralData;
            float multiplier = 1f;
            IntRange randomAdd = default;
            int reaction;

            switch (part)
            {
                case HumanHitPart.Head:
                    reaction = general.headHitReaction;
                    if (m_humanTypeData != null)
                    {
                        multiplier = m_humanTypeData.headDamageMultiplier;
                        randomAdd = m_humanTypeData.headRandomAddDamage;
                    }
                    break;
                case HumanHitPart.Body:
                    reaction = general.bodyHitReaction;
                    if (m_humanTypeData != null)
                    {
                        multiplier = m_humanTypeData.bodyDamageMultiplier;
                        randomAdd = m_humanTypeData.bodyRandomAddDamage;
                    }
                    break;
                default:
                    reaction = general.legHitReaction;
                    if (m_humanTypeData != null)
                    {
                        multiplier = m_humanTypeData.legDamageMultiplier;
                        randomAdd = m_humanTypeData.legRandomAddDamage;
                    }
                    break;
            }

            int baseDamage = (int)(attacks * multiplier);
            ApplyDamage(baseDamage + GameRandom.Gameplay.Range(randomAdd.min, randomAdd.max));
            SetHitReaction(reaction);
            return baseDamage;
        }

        /// <summary>
        /// 폭발 데미지와 조준 흐트러짐을 적용한다. 원본 human::HitGrenadeExplosion (object.cpp:1074-1080).
        /// </summary>
        /// <param name="damage">데미지.</param>
        public void HitGrenadeExplosion(int damage)
        {
            ApplyDamage(damage);
            SetHitReaction(DataManager.Instance.HumanParameterData.humanGeneralData.grenadeHitReaction);
        }

        /// <summary>
        /// 좀비의 근접 공격 데미지와 조준 흐트러짐을 적용한다. 원본 human::HitZombieAttack (object.cpp:1063-1069).
        /// </summary>
        /// <param name="damage">데미지.</param>
        public void HitZombieAttack(int damage)
        {
            ApplyDamage(damage);
            SetHitReaction(DataManager.Instance.HumanParameterData.humanGeneralData.zombieHitReaction);
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

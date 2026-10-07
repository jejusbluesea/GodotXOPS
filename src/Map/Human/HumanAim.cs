using Godot;

namespace GodotXOPS
{
    // Human 의 조준 오차·피격 반응·스코프 담당 partial.
    public partial class Human
    {
        // 이동·점프·저체력에 따른 오차 (원본 StateGunsightErrorRange). 정수 단위, 1 = 0.15°.
        private int m_stateErrorRange;
        // 발사 반동과 피격으로 쌓이는 오차 (원본 ReactionGunsightErrorRange). 틱마다 줄어든다.
        private float m_reactionErrorRange;
        // 스코프 사용 여부 (원본 scopemode != 0).
        private bool m_scoping;
        // 맨손 팔을 조준 방향으로 움직일지 여부. AI 가 좀비 공격·항복 동작 중에만 켠다.
        private bool m_unarmedArmDynamic;

        // 지금 쏘면 적용될 조준 오차 (원본 human::GetGunsightErrorRange). 무기별 하한은 EffectiveErrorRange 가 적용한다.
        public int GunsightErrorRange => m_stateErrorRange + Mathf.RoundToInt(m_reactionErrorRange);
        public bool IsScoping => m_scoping;
        public bool UnarmedArmDynamic => m_unarmedArmDynamic;

        // 현재 무기의 스코프 데이터. 스코프가 없는 무기면 null.
        public ScopeData CurrentScopeData
        {
            get
            {
                WeaponData data = CurrentWeapon.Data;
                if (!data.scope) return null;

                DataList<ScopeData> list = DataManager.Instance.WeaponParameterData.scopeData;
                return list.Has(data.scopeIndex) ? list[data.scopeIndex] : null;
            }
        }

        // 스코프를 쓰는 동안에만 non-null.
        public ScopeData ActiveScope => m_scoping ? CurrentScopeData : null;

        /// <summary>
        /// 현재 무기로 지금 쏠 때의 실효 조준 오차. 탄도와 조준선 표시가 함께 쓴다.
        /// </summary>
        /// <returns>오차 (정수 단위, 1 = 0.15°).</returns>
        public int CurrentErrorRange()
        {
            return EffectiveErrorRange(CurrentWeapon.Data, GunsightErrorRange);
        }

        /// <summary>
        /// 스코프를 켜거나 끈다. 원본 ObjectManager::ChangeScopeMode + human::SetEnableScope (object.cpp:918-937):
        /// 스코프가 없는 무기이거나 재장전 중이면 켜지지 않는다.
        /// </summary>
        public void ToggleScope()
        {
            if (m_scoping)
            {
                m_scoping = false;
                return;
            }

            if (!Alive || m_reloadTicks > 0 || CurrentScopeData == null) return;
            m_scoping = true;
        }

        /// <summary>
        /// 스코프를 끈다. 원본 human::SetDisableScope.
        /// </summary>
        public void DisableScope()
        {
            m_scoping = false;
        }

        /// <summary>
        /// 피격으로 조준이 흐트러진 정도를 반영한다. 값은 부위별 데이터다 (원본 object.cpp:1039/1049/1059/1079 — 머리 15, 상반신 12, 다리 8, 폭발 10).
        /// 원본은 그대로 대입해서, 연사로 이미 더 크게 흐트러져 있던 조준이 맞는 순간 오히려 좋아진다. 그래서 더 큰 쪽을 남긴다.
        /// </summary>
        /// <param name="value">오차 값.</param>
        public void SetHitReaction(float value)
        {
            m_reactionErrorRange = Mathf.Max(m_reactionErrorRange, value);
        }

        /// <summary>
        /// 맨손 팔을 조준 방향으로 움직일지 정한다. 바뀔 때만 팔 모델을 다시 붙인다.
        /// </summary>
        /// <param name="value">true 면 조준 방향을 따른다.</param>
        public void SetUnarmedArmDynamic(bool value)
        {
            if (m_unarmedArmDynamic == value) return;

            m_unarmedArmDynamic = value;
            ApplyActiveWeaponVisual();
        }

        /// <summary>
        /// 조준 오차를 한 틱 갱신한다. 원본 human::GunsightErrorRange (object.cpp:1119-1157).
        /// 이동 오차는 대입이라 뒤에 평가된 조건이 이긴다 (걷기 → 전진 → 후진 → 좌우 → 공중). 저체력만 더한다.
        /// 원본처럼 이동 계산 전에 불려서, 이번 틱 입력과 직전 틱의 접지 상태를 본다.
        /// </summary>
        private void TickGunsightErrorRange()
        {
            WeaponAccuracyData accuracy = DataManager.Instance.WeaponParameterData.weaponAccuracyData;
            HumanMoveFlag flag = m_controller.MoveFlag;

            int state = 0;
            if ((flag & HumanMoveFlag.Walk) != 0) state = accuracy.walkAccuracyPenalty;
            if ((flag & HumanMoveFlag.Forward) != 0) state = accuracy.forwardAccuracyPenalty;
            if ((flag & HumanMoveFlag.Back) != 0) state = accuracy.backAccuracyPenalty;
            if ((flag & (HumanMoveFlag.Left | HumanMoveFlag.Right)) != 0) state = accuracy.strafeAccuracyPenalty;
            if (!m_controller.Grounded) state = accuracy.airborneAccuracyPenalty;
            if (m_hp < accuracy.injuryHpThreshold) state += accuracy.injuryAccuracyPenalty;
            m_stateErrorRange = state;

            Weapon weapon = CurrentWeapon;
            if (weapon.IsNone)
            {
                m_reactionErrorRange = 0f;
                return;
            }

            // 원본은 프레임당 1 씩 줄인다. 데이터는 초당 값(33.333)이다.
            m_reactionErrorRange -= accuracy.reactionRecoveryPerSecond * SimClock.FrameTime;
            m_reactionErrorRange = Mathf.Clamp(m_reactionErrorRange, 0f, weapon.Data.errorRange.max);
        }

        /// <summary>
        /// 무기별 하한을 적용한 오차를 구한다 (원본 objectmanager.cpp:1962-1964). 오차를 무시하는 무기는 0 이다.
        /// </summary>
        /// <param name="data">무기 데이터.</param>
        /// <param name="errorRange">사람의 조준 오차.</param>
        /// <returns>실효 오차.</returns>
        private static int EffectiveErrorRange(WeaponData data, int errorRange)
        {
            if (data.ignoreAimError) return 0;
            return Mathf.Max(errorRange, Mathf.RoundToInt(data.errorRange.min));
        }

    }
}

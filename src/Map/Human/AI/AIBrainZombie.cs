using Godot;

namespace GodotXOPS
{
    // AIBrain 의 좀비 근접 전투 담당 partial. 원본 Action 의 좀비 분기 (ai.cpp:720-776) 와 ObjectManager::CheckZombieAttack / HitZombieAttack (objectmanager.cpp:2405-2529).
    public partial class AIBrain
    {
        // 다가갈 때 적 방향과 맞아야 하는 각도, 달려들 때의 각도와 거리 (원본 ai.cpp:730-731 — 25°, 15°, 24.0).
        private const float k_zombieApproachTolerance = 25f;
        private const float k_zombieChargeTolerance = 15f;
        private const float k_zombieChargeDist = 2.4f;
        // 맨손 공격 주기(틱)와, 그 주기 안에서 달려드는 구간의 시작 (원본 actioncnt % 50, > 20).
        private const int k_zombieAttackPeriod = 50;
        private const int k_zombieChargeWindow = 20;
        // 붙잡기: 이 거리 안이고 눈높이 차가 이 값 안이면 적을 끌어당기고 시점을 흔든다 (원본 ai.cpp:748 — 9.0, 10.0).
        private const float k_zombieGrabDist = 0.9f;
        private const float k_zombieGrabHeight = 1.0f;
        // 끌어당기는 속도 (m/s). 원본 AddPosOrder(…, 0.5) = 프레임당 0.5.
        private const float k_zombiePullSpeed = 0.5f * Coord.Scale * SimClock.FrameRate;
        // 붙잡힌 적의 시점을 흔드는 양 (도). 좌우·상하 각각 −, +, 그대로 중 하나 (원본 ai.cpp:758-767).
        private const float k_zombieViewShake = 2f;
        // 공격 지점: 정면으로 이만큼 앞 (원본 2.0), 맨손 공격이 닿는 수평 반경 (원본 3.3), 공격 지점 높이에서 빼는 값 (원본 0.5).
        private const float k_zombieAttackOffset = 0.2f;
        private const float k_zombieAttackRadius = 0.33f;
        private const float k_zombieAttackHeightBias = 0.05f;
        // 공격음 볼륨. 원본은 사람 피격음을 그대로 쓴다 (MAX_SOUNDHITHUMAN).
        private const float k_zombieAttackVolume = 0.625f;

        // 좀비가 공격 자세로 드는 팔 각도 (시선 pitch 기준, 아래 +). 데이터는 팔 기준(아래가 음수)이다.
        private static float ZombieArmPitch => -AIData.aiZombieArmAngle;

        private bool IsZombie()
        {
            return m_self.HumanTypeData != null && m_self.HumanTypeData.zombie;
        }

        /// <summary>
        /// 좀비의 근접 공격이 닿는 수평 반경. 맨손이면 원본의 접촉 반경이고, 무기를 들었으면 min(사람 종류의 zombieMaxMeleeRange, 총알이 날아가는 거리)다.
        /// 쏠 수 없는 무기나 데이터가 0 이면 접촉 반경으로 돌아간다.
        /// </summary>
        /// <returns>반경 (m).</returns>
        private float ZombieMeleeReach()
        {
            Weapon weapon = m_self.CurrentWeapon;
            if (weapon.IsNone) return k_zombieAttackRadius;

            var bullets = DataManager.Instance.WeaponParameterData.bulletData;
            int bulletIndex = weapon.Data.bulletIndex;
            if (bulletIndex < 0 || bulletIndex >= bullets.Count) return k_zombieAttackRadius;

            float bulletRange = bullets[bulletIndex].lifetime * weapon.Data.bulletSpeed;
            float cap = m_self.HumanTypeData.zombieMaxMeleeRange;
            if (bulletRange <= 0f || cap <= 0f) return k_zombieAttackRadius;

            return Mathf.Min(cap, bulletRange);
        }

        /// <summary>
        /// 좀비의 공격 주기(틱). 무기에 발사 속도가 있으면 그 발사 간격이고, 맨손이나 쏠 수 없는 무기는 원본의 50 틱이다.
        /// </summary>
        /// <returns>주기 (틱, 1 이상).</returns>
        private int ZombieAttackPeriod()
        {
            float fireRate = m_self.CurrentWeapon.Data.fireRate;
            if (fireRate <= 0f) return k_zombieAttackPeriod;
            return Mathf.Max(1, Mathf.RoundToInt(SimClock.FrameRate / fireRate));
        }

        /// <summary>
        /// 좀비의 전투 한 틱: 적에게 다가가고, 가까우면 붙잡아 끌어당기고, 주기마다 근접 공격한다. 원본 ai.cpp:720-776.
        /// 맨손 좀비는 닿을 때까지 걸어가다 주기의 뒷부분에서 달려든다. 무기를 든 좀비는 공격이 닿는 거리에서 멈춰 서고 붙잡지 않는다.
        /// </summary>
        /// <param name="toTarget">내 눈에서 적의 눈까지의 벡터.</param>
        /// <param name="deltaYaw">적 방향과 내 정면의 각도 차 (도).</param>
        private void ZombieFight(Vector3 toTarget, float deltaYaw)
        {
            float distanceSquared = toTarget.LengthSquared();
            float reach = ZombieMeleeReach();
            bool armedStandoff = reach > k_zombieAttackRadius;
            float absYaw = Mathf.Abs(deltaYaw);

            if (absYaw <= k_zombieApproachTolerance)
            {
                if (armedStandoff)
                {
                    float horizontal = new Vector2(toTarget.X, toTarget.Z).Length();
                    bool hold = horizontal <= reach && ZombieAttackHeightOk(m_enemy);
                    if (!hold) m_moveIntent |= HumanMoveFlag.Forward;
                }
                else
                {
                    bool charge = absYaw <= k_zombieChargeTolerance
                        && distanceSquared < k_zombieChargeDist * k_zombieChargeDist
                        && m_actionCnt % k_zombieAttackPeriod > k_zombieChargeWindow;
                    m_moveIntent |= charge ? HumanMoveFlag.Forward : HumanMoveFlag.Walk;
                }
            }

            if (!armedStandoff && distanceSquared < k_zombieGrabDist * k_zombieGrabDist && Mathf.Abs(toTarget.Y) < k_zombieGrabHeight)
            {
                Vector3 pull = m_controller.Position - m_enemy.Controller.Position;
                pull.Y = 0f;
                if (pull.LengthSquared() > 0f)
                {
                    m_enemy.Controller.AddKnockbackVector(pull.Normalized(), k_zombiePullSpeed);
                }

                float shakeYaw = 0f;
                float shakePitch = 0f;
                switch (GetRand(3))
                {
                    case 0: shakeYaw = -k_zombieViewShake; break;
                    case 1: shakeYaw = k_zombieViewShake; break;
                }
                switch (GetRand(3))
                {
                    case 0: shakePitch = k_zombieViewShake; break;
                    case 1: shakePitch = -k_zombieViewShake; break;
                }
                m_enemy.Controller.AddYawPitch(shakeYaw, shakePitch);
            }

            if (m_actionCnt % ZombieAttackPeriod() == 0 && CheckZombieAttack(m_enemy, reach))
            {
                HitZombieAttack(m_enemy);
            }
        }

        /// <summary>
        /// 근접 공격이 닿는지 판정한다. 원본 ObjectManager::CheckZombieAttack (objectmanager.cpp:2405-2446).
        /// 공격 지점은 정면 앞의 한 점이고, 적이 그 점에서 수평 반경 안이면서 높이가 맞아야 한다.
        /// </summary>
        /// <param name="victim">공격받는 사람.</param>
        /// <param name="reach">수평 반경 (m).</param>
        /// <returns>닿으면 true.</returns>
        private bool CheckZombieAttack(Human victim, float reach)
        {
            if (!victim.Alive || victim.Team == m_self.Team) return false;

            Vector3 attackPoint = m_controller.Position + Coord.YawForward(m_controller.Yaw) * k_zombieAttackOffset;
            Vector3 victimPosition = victim.Controller.Position;
            float dx = attackPoint.X - victimPosition.X;
            float dz = attackPoint.Z - victimPosition.Z;
            if (dx * dx + dz * dz >= reach * reach) return false;

            return ZombieAttackHeightOk(victim);
        }

        /// <summary>
        /// 근접 공격의 높이 조건. 공격 지점 높이(내 발 + 눈높이 × 2 − 0.05)가 적의 눈높이부터 그 위로 키만큼의 구간에 들어야 한다 (원본 objectmanager.cpp:2433, 2440).
        /// </summary>
        /// <param name="victim">공격받는 사람.</param>
        /// <returns>높이가 맞으면 true.</returns>
        private bool ZombieAttackHeightOk(Human victim)
        {
            float attackY = m_controller.Position.Y + m_controller.CameraHeight * 2f - k_zombieAttackHeightBias;
            float victimY = victim.Controller.Position.Y + victim.Controller.CameraHeight;
            return attackY >= victimY && attackY <= victimY + victim.Controller.Height;
        }

        /// <summary>
        /// 근접 공격을 맞힌다: 데미지, 피격 방향, 혈흔, 공격음, 주변 AI 가 듣는 소리. 원본 ObjectManager::HitZombieAttack (objectmanager.cpp:2451-2529).
        /// 데미지는 사람 종류의 zombieMeleeDamageRange 이고, 무기를 든 좀비는 무기 데미지를 더한다.
        /// </summary>
        /// <param name="victim">공격받는 사람.</param>
        private void HitZombieAttack(Human victim)
        {
            HumanTypeData type = m_self.HumanTypeData;
            IntRange range = type.zombieMeleeDamageRange;
            int damage = GameRandom.Gameplay.Range(range.min, range.max);
            if (!m_self.CurrentWeapon.IsNone) damage += (int)m_self.CurrentWeapon.Data.damage;

            victim.HitZombieAttack(damage);
            victim.SetHitYaw(YawTo(victim.Controller.Position - m_controller.Position));

            Vector3 point = victim.Controller.Position + Vector3.Up * victim.Controller.CameraHeight;

            // 혈흔의 양은 원본처럼 최소 데미지 기준이다.
            if (EffectManager.Loaded && victim.HumanTypeData != null)
            {
                EffectManager.Instance.Play(victim.HumanTypeData.bloodEffectIndex, point, range.min);
            }
            if (SoundManager.Loaded && !string.IsNullOrEmpty(type.zombieAttackSound))
            {
                SoundManager.Instance.PlayAt(type.zombieAttackSound, point, k_zombieAttackVolume);
            }

            float hearDistance = AIData.aiHearHitHumanZombie;
            WorldSound.EmitPointSound(point, m_self.Team, hearDistance, hearDistance);
        }
    }
}

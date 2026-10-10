using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 풀에서 관리되는 발사체 하나. BulletData.useGravity 로 직선 탄과 수류탄 두 동작을 가른다 (원본 bullet / grenade 클래스).
    /// 노드가 아닌 순수 클래스다. 탄도와 판정은 BulletManager 가 틱마다 부르는 Tick 이 논리 위치로 계산하고,
    /// 화면의 모델은 BulletManager 가 직전 틱과 현재 틱 사이를 보간해 놓는다.
    /// 판정은 원본처럼 한 틱의 경로를 0.25 m 간격 점으로 나눠 점마다 사람(원기둥)·맵(블록 내부)을 검사한다. 엔진 물리는 쓰지 않는다.
    /// </summary>
    public class Bullet
    {
        // 한 틱 경로를 나누는 간격 (m). 원본 BULLET_SPEEDSCALE 2.5.
        private const float k_substep = 0.25f;
        // 사람을 관통한 뒤의 위력 배율 (원본 objectmanager.cpp:764/780/796). 곱한 뒤 정수로 자른다.
        private const float k_pierceAttenHead = 0.5f;
        private const float k_pierceAttenBody = 0.6f;
        private const float k_pierceAttenLeg = 0.7f;
        // 블록 안의 점을 하나 지날 때의 위력 배율 (원본 objectmanager.cpp:854).
        private const float k_pierceAttenWall = 0.6f;
        // 점 사이에 끼어 내부 판정에 걸리지 않은 얇은 블록을 지난 뒤의 위력 배율 (원본 objectmanager.cpp:870-875). 관통력은 줄지 않는다.
        private const float k_thinWallAttenPierce = 0.75f;
        private const float k_thinWallAttenStop = 0.55f;
        // 착탄 지점은 블록 면에서 이만큼 앞이다 (m). 원본 Dist − 1.0.
        private const float k_wallEntryMargin = 0.1f;
        // 명중한 사람을 총알 진행 방향으로 미는 속도 (m/s). 원본 AddPosOrder(brx, 0, 1.0) = 프레임당 1.0.
        private const float k_hitKnockbackSpeed = 0.1f * SimClock.FrameRate;

        // 수류탄: 틱마다 속도에 곱하는 감쇠 (원본 object.cpp:3010-3026).
        private const float k_grenadeDrag = 0.98f;
        // 수류탄 반사 감속 = −각도 × 0.2546 + 0.7 (원본 object.cpp:3000). 정면 충돌(π/2)이면 0.3, 스치면 0.7.
        private const float k_reflectAngleCoef = 0.2546f;
        private const float k_reflectBaseCoef = 0.7f;
        // 폭발 판정 점: 발 위 0.2 m 와 머리 아래 0.2 m (원본 objectmanager.cpp:1076/1088 — +2.0, HUMAN_HEIGHT−2.0).
        private const float k_explosionPointMargin = 0.2f;
        // 폭발에 맞은 사람의 혈흔 높이: 발 위 1.5 m (원본 objectmanager.cpp:1119 — hy + 15.0).
        private const float k_explosionBloodHeight = 1.5f;
        // 수류탄이 튈 때 소리를 내는 최소 속력 (틱당 m). 원본 objectmanager.cpp:2889 speed > 3.4.
        private const float k_grenadeBoundSoundMinSpeed = 0.34f;

        // 효과음 볼륨. 원본 볼륨 상수(폭발 120, 벽 착탄·바운드 95~100, 피격 75, 통과음 80)를 폭발 기준으로 나눈 값이다.
        private const float k_explosionVolume = 1f;
        private const float k_wallHitVolume = 0.8f;
        private const float k_humanHitVolume = 0.625f;
        private const float k_passingVolume = 0.667f;

        private BulletData m_data;
        private Human m_owner;
        private int m_team;
        private int m_attacks;
        private int m_penetration;
        // 맞은 사람의 방어구 / 헬멧 포인트를 데미지의 몇 배만큼 깎는지 (쏜 무기의 값).
        private float m_armorPointDecay;
        private float m_helmetPointDecay;
        private Vector3 m_position;
        private Vector3 m_prevPosition;
        private Vector3 m_direction;
        // 진행 방향의 yaw (도). 명중한 사람을 미는 방향과 피격 방향 기록에 쓴다.
        private float m_yaw;
        // 직선 탄의 틱당 이동 거리 (m).
        private float m_speedPerTick;
        // 수류탄의 속도 (틱당 m).
        private Vector3 m_velocity;
        private int m_tickCount;
        private int m_lifeTicks;
        private int m_armingTicks;
        private bool m_active;
        // 마지막으로 맞힌 사람. 같은 사람을 연달아 다시 맞히지 않게 한다 (원본 BulletObj_HumanIndex — 마지막 한 명만 기억).
        private Human m_lastHitHuman;
        private Vector3 m_visualOrigin;
        // 명중 통계 가중치 (단발 1, 산탄은 2 / 탄환 수).
        private float m_onTargetWeight;
        // 카메라 옆을 스치는 소리를 이미 냈는지. 한 발에 한 번만 낸다.
        private bool m_passingSoundDone;

        public bool IsActive => m_active;
        public BulletData Data => m_data;
        public bool UseGravity => m_data != null && m_data.useGravity;
        public int Attacks => m_attacks;
        public int Penetration => m_penetration;
        public Vector3 Position => m_position;
        public Vector3 Direction => m_direction;
        // 탄환 모델을 보이기 시작하는 기준점 (총구 위치).
        public Vector3 VisualOrigin => m_visualOrigin;
        // 수류탄의 현재 속력 (틱당 m).
        public float GrenadeSpeed => m_velocity.Length();

        /// <summary>
        /// 발사 상태로 초기화한다.
        /// </summary>
        /// <param name="data">탄환 데이터.</param>
        /// <param name="owner">쏜 사람.</param>
        /// <param name="team">쏜 사람의 팀.</param>
        /// <param name="attacks">위력.</param>
        /// <param name="penetration">관통력.</param>
        /// <param name="position">발사 위치.</param>
        /// <param name="yawDeg">발사 yaw (도).</param>
        /// <param name="pitchDeg">발사 pitch (도, 아래 +).</param>
        /// <param name="speedPerTick">틱당 이동 거리 (m).</param>
        /// <param name="visualOrigin">총구 위치.</param>
        /// <param name="onTargetWeight">명중 통계 가중치.</param>
        /// <param name="armorPointDecay">맞은 사람의 방어구 포인트를 데미지의 몇 배만큼 깎는지.</param>
        /// <param name="helmetPointDecay">맞은 사람의 헬멧 포인트를 데미지의 몇 배만큼 깎는지.</param>
        public void Spawn(BulletData data, Human owner, int team, int attacks, int penetration,
            Vector3 position, float yawDeg, float pitchDeg, float speedPerTick, Vector3 visualOrigin, float onTargetWeight,
            float armorPointDecay, float helmetPointDecay)
        {
            m_armorPointDecay = armorPointDecay;
            m_helmetPointDecay = helmetPointDecay;
            m_data = data;
            m_owner = owner;
            m_team = team;
            m_attacks = attacks;
            m_penetration = penetration;
            m_position = position;
            m_prevPosition = position;
            m_direction = Coord.AimDirection(yawDeg, pitchDeg);
            m_yaw = yawDeg;
            m_speedPerTick = speedPerTick;
            m_velocity = m_direction * speedPerTick;
            m_tickCount = 0;
            m_lifeTicks = Mathf.RoundToInt(data.lifetime * SimClock.FrameRate);
            m_armingTicks = Mathf.RoundToInt(data.armingDelay * SimClock.FrameRate);
            m_lastHitHuman = null;
            m_visualOrigin = visualOrigin;
            m_onTargetWeight = onTargetWeight;
            m_passingSoundDone = false;
            m_active = true;
        }

        /// <summary>
        /// 풀로 되돌린다.
        /// </summary>
        public void Deactivate()
        {
            m_active = false;
            m_owner = null;
            m_lastHitHuman = null;
        }

        /// <summary>
        /// 틱 사이를 보간한 시각 위치를 구한다.
        /// </summary>
        /// <param name="alpha">현재 틱 진행 비율 0~1.</param>
        /// <returns>시각 위치.</returns>
        public Vector3 VisualPosition(float alpha)
        {
            return m_prevPosition.Lerp(m_position, alpha);
        }

        /// <summary>
        /// 한 틱 진행한다.
        /// </summary>
        public void Tick()
        {
            if (!m_active) return;

            m_prevPosition = m_position;
            if (m_armingTicks > 0) m_armingTicks--;

            if (m_data.useGravity) TickGrenade();
            else TickStraight();

            if (m_active)
            {
                TickPassingSound();
                NotifyBulletPass();
            }
        }

        /// <summary>
        /// 직선 탄 한 틱. 판정을 먼저 하고 이동한다 (원본 ObjectManager::CollideBullet objectmanager.cpp:676-884 → bullet::ProcessObject object.cpp:2861-2879).
        /// 점 하나에서의 순서는 사람 → 소물 → 맵이다.
        /// </summary>
        private void TickStraight()
        {
            // 이번 틱 경로 위에 블록이 있는지. 0 없음 / 1 있으나 아직 내부 점에 안 걸림 / 2 내부 점에 걸림.
            int mapFlag = MapLoader.RaycastBlock(BlockLayer.Bullet, m_position, m_direction, m_speedPerTick, out float wallDist, out Block wallBlock, out int wallFace) ? 1 : 0;
            Vector3 wallEntry = m_position + m_direction * (wallDist - k_wallEntryMargin);
            Vector3 wallSurface = m_position + m_direction * wallDist;
            // 탄흔은 한 틱에 한 번만 남긴다. 관통탄은 같은 진입점으로 HitMap 을 여러 번 부른다.
            bool holeLeft = false;

            int steps = Mathf.RoundToInt(m_speedPerTick / k_substep);
            for (int step = 0; step < steps; step++)
            {
                if (m_penetration < 0)
                {
                    Deactivate();
                    return;
                }

                Vector3 point = m_position + m_direction * (k_substep * (step + 1));

                if (HitHumansAt(point)) return;

                if (m_penetration < 0)
                {
                    Deactivate();
                    return;
                }

                if (HitSmallObjectsAt(point)) return;

                if (mapFlag > 0 && MapLoader.IsInsideBlock(BlockLayer.Bullet, point))
                {
                    if (ExplodeOnTrigger(ExplosionTrigger.Block, wallEntry)) return;
                    HitMap(wallEntry, wallSurface, wallBlock, wallFace, !holeLeft);
                    holeLeft = true;

                    m_penetration--;
                    if (m_penetration >= 0) m_attacks = (int)(m_attacks * k_pierceAttenWall);
                    mapFlag = 2;
                }
            }

            // 경로 위에 블록이 있는데 내부 점에 한 번도 안 걸렸다 = 점 간격보다 얇은 블록.
            if (mapFlag == 1)
            {
                if (ExplodeOnTrigger(ExplosionTrigger.Block, wallEntry)) return;
                HitMap(wallEntry, wallSurface, wallBlock, wallFace, !holeLeft);
                m_attacks = (int)(m_attacks * (m_penetration > 0 ? k_thinWallAttenPierce : k_thinWallAttenStop));
            }

            if (m_tickCount > m_lifeTicks)
            {
                if (!ExplodeOnTrigger(ExplosionTrigger.Lifetime, m_position)) Deactivate();
                return;
            }

            m_position += m_direction * m_speedPerTick;
            m_tickCount++;
        }

        /// <summary>
        /// 수류탄 한 틱. 원본 grenade::ProcessObject (object.cpp:2963-3029):
        /// 이번 틱 이동 경로에 블록이 있으면 반사·감속만 하고 그 틱에는 움직이지 않는다. 없으면 이동한다. 그 뒤 감쇠와 중력을 적용한다.
        /// </summary>
        private void TickGrenade()
        {
            if (m_tickCount > m_lifeTicks)
            {
                if (!ExplodeOnTrigger(ExplosionTrigger.Lifetime, m_position)) Deactivate();
                return;
            }

            if (m_velocity == Vector3.Zero)
            {
                m_tickCount++;
                return;
            }

            float moveDist = m_velocity.Length();
            Vector3 direction = m_velocity / moveDist;

            if (MapLoader.RaycastBlock(BlockLayer.Bullet, m_position, direction, moveDist, out float hitDist, out Vector3 normal))
            {
                if (ExplodeOnTrigger(ExplosionTrigger.Block, m_position + direction * Mathf.Max(0f, hitDist - k_wallEntryMargin))) return;

                // 진입각: 면에 정면으로 박히면 π/2, 스치면 0 (원본 Collision::AngleVector, collision.cpp:862-883).
                float angle = -Mathf.Asin(Mathf.Clamp(direction.Dot(normal), -1f, 1f));
                float acceleration = -angle * k_reflectAngleCoef + k_reflectBaseCoef;
                Vector3 reflected = m_velocity - 2f * m_velocity.Dot(normal) * normal;
                m_velocity = reflected * acceleration;

                // 약하게 굴러가며 튀는 것은 소리를 내지 않는다.
                if (moveDist > k_grenadeBoundSoundMinSpeed && SoundManager.Loaded)
                {
                    SoundManager.Instance.PlayRandomAt(m_data.bounceSounds, m_position, k_wallHitVolume);
                }
            }
            else
            {
                // 사람에 닿으면 터지는 탄종만 경로 위의 사람을 검사한다. 보통 수류탄은 사람을 지나친다.
                if ((m_data.explosionTrigger & ExplosionTrigger.Human) != 0)
                {
                    int steps = Mathf.CeilToInt(moveDist / k_substep);
                    for (int step = 0; step < steps; step++)
                    {
                        Vector3 point = m_position + direction * Mathf.Min(moveDist, k_substep * (step + 1));
                        if (HitHumansAt(point)) return;
                    }
                }

                m_position += m_velocity;
            }

            float gravityPerTick = m_data.gravityScale * SimClock.FrameTime * SimClock.FrameTime;
            m_velocity.X *= k_grenadeDrag;
            m_velocity.Y = (m_velocity.Y - gravityPerTick) * k_grenadeDrag;
            m_velocity.Z *= k_grenadeDrag;
            m_tickCount++;
        }

        /// <summary>
        /// 점 하나에서 모든 사람의 머리·상반신·다리 원기둥을 검사하고, 맞으면 데미지와 관통 감쇠를 처리한다 (원본 objectmanager.cpp:731-801).
        /// 쏜 사람, 같은 팀, 죽은 사람, 방금 맞힌 사람은 건너뛴다.
        /// </summary>
        /// <param name="point">검사할 점.</param>
        /// <returns>사람에 닿아 폭발하거나 불발로 사라져 이 탄환의 처리가 끝났으면 true.</returns>
        private bool HitHumansAt(Vector3 point)
        {
            IReadOnlyList<Human> humans = MapLoader.Humans;
            for (int i = 0; i < humans.Count; i++)
            {
                Human human = humans[i];
                if (human == m_owner) continue;
                if (!human.Alive || human.HP <= 0f) continue;
                if (human == m_lastHitHuman) continue;
                if (human.Team == m_team) continue;

                HumanHitboxSizeData size = human.HitboxSize;
                if (size == null) continue;
                Vector3 humanPosition = human.Controller.Position;
                float humanYaw = human.Controller.Yaw;

                if (HumanHitbox.Contains(size.head, humanPosition, humanYaw, point))
                {
                    if (ExplodeOnTrigger(ExplosionTrigger.Human, point)) return true;
                    HitHuman(human, HumanHitPart.Head, k_pierceAttenHead, point);
                }
                if (HumanHitbox.Contains(size.body, humanPosition, humanYaw, point))
                {
                    if (ExplodeOnTrigger(ExplosionTrigger.Human, point)) return true;
                    HitHuman(human, HumanHitPart.Body, k_pierceAttenBody, point);
                }
                if (HumanHitbox.Contains(size.leg, humanPosition, humanYaw, point))
                {
                    if (ExplodeOnTrigger(ExplosionTrigger.Human, point)) return true;
                    HitHuman(human, HumanHitPart.Leg, k_pierceAttenLeg, point);
                }
            }
            return false;
        }

        /// <summary>
        /// 사람에 명중한 처리. 원본 ObjectManager::HitBulletHuman (objectmanager.cpp:910-992): 데미지, 밀림, 피격 방향, 혈흔, 피격음, 통계.
        /// </summary>
        /// <param name="human">맞은 사람.</param>
        /// <param name="part">맞은 부위.</param>
        /// <param name="attenuation">관통 뒤 위력 배율.</param>
        /// <param name="point">맞은 지점.</param>
        private void HitHuman(Human human, HumanHitPart part, float attenuation, Vector3 point)
        {
            float hpBefore = human.HP;
            int baseDamage = human.HitBullet(part, m_attacks, m_armorPointDecay, m_helmetPointDecay, out bool armored);
            human.Controller.AddKnockback(m_yaw, 0f, k_hitKnockbackSpeed);
            human.SetHitYaw(m_yaw);

            MapLoader.RecordHit(m_owner, part == HumanHitPart.Head, m_onTargetWeight);
            if (hpBefore > 0f && human.HP <= 0f) MapLoader.RecordKill(m_owner);

            // 혈흔은 데미지에 비례해 튄다 (원본 SetHumanBlood).
            if (EffectManager.Loaded) EffectManager.Instance.Play(m_data.humanHitEffectIndex, point, baseDamage);
            if (SoundManager.Loaded) SoundManager.Instance.PlayRandomAt(m_data.humanHitSounds, point, k_humanHitVolume);

            HumanAIParameterData ai = DataManager.Instance.HumanParameterData.humanAIParameterData;
            float hearDistance = part == HumanHitPart.Head ? ai.aiHearHitHumanHead : part == HumanHitPart.Body ? ai.aiHearHitHumanBody : ai.aiHearHitHumanLeg;
            WorldSound.EmitPointSound(point, m_team, hearDistance, hearDistance);

            m_lastHitHuman = human;
            m_attacks = (int)(m_attacks * attenuation);
            m_penetration--;
            // 방어구나 헬멧은 총알이 뚫고 나가는 것만 막는다 (원본에 없는 동작): 사람은 맞지만 뒤로 나가려면 관통력이 하나 더 든다.
            if (armored) m_penetration--;
        }

        /// <summary>
        /// 점 하나에서 모든 소물을 검사한다. 원본 objectmanager.cpp:810-843: 점이 소물의 판정 형상 안이면
        /// 소물에 위력 × 배율(0.25)의 데미지를 주고 총알 위력을 × 감쇠(0.7)로 줄인다. 관통력은 줄지 않는다.
        /// 사람과 달리 "이미 맞힌 소물"을 기억하지 않으므로, 형상을 지나는 점마다 다시 맞는다.
        /// </summary>
        /// <param name="point">검사할 점.</param>
        /// <returns>소물에 닿아 폭발하거나 불발로 사라져 이 탄환의 처리가 끝났으면 true.</returns>
        private bool HitSmallObjectsAt(Vector3 point)
        {
            IReadOnlyList<SmallObject> objects = MapLoader.SmallObjects;
            if (objects.Count == 0) return false;

            ObjectGeneralData general = DataManager.Instance.ObjectParameterData.objectGeneralData;
            for (int i = 0; i < objects.Count; i++)
            {
                SmallObject smallObject = objects[i];
                if (!smallObject.Contains(point)) continue;

                if (ExplodeOnTrigger(ExplosionTrigger.Object, point)) return true;

                smallObject.HitBullet(Mathf.FloorToInt(m_attacks * general.bulletDamageMultiplier));
                m_attacks = Mathf.FloorToInt(m_attacks * general.bulletPenetrationAttenuation);

                if (EffectManager.Loaded) EffectManager.Instance.Play(m_data.objectHitEffectIndex, point);
                float hearDistance = DataManager.Instance.HumanParameterData.humanAIParameterData.aiHearHitSmallObject;
                WorldSound.EmitPointSound(point, m_team, hearDistance, hearDistance);
            }
            return false;
        }

        /// <summary>
        /// 총알이 블록에 맞은 연출: 착탄 연기와 소리, 주변 AI 가 듣는 처리. 원본 ObjectManager::HitBulletMap (objectmanager.cpp:891-898).
        /// 이펙트·탄흔·소리는 맞은 면의 재질이 정한다. 재질 번호가 없는 맵(BD1)의 면은 0번 재질이고, 거기에 원본의 착탄 연기와 착탄음이 들어 있다.
        /// </summary>
        /// <param name="position">착탄 지점. 면에서 조금 앞이다.</param>
        /// <param name="surfacePoint">탄환이 면에 닿은 점. 이펙트의 데칼이 놓이는 자리다.</param>
        /// <param name="block">맞은 블록.</param>
        /// <param name="face">맞은 면 번호.</param>
        /// <param name="leaveHole">true 면 재질의 탄흔을 남긴다.</param>
        private void HitMap(Vector3 position, Vector3 surfacePoint, Block block, int face, bool leaveHole)
        {
            Vector3 normal = block.faceNormals[face];
            BlockMaterialData material = MapLoader.GetFaceMaterial(block, face);

            if (EffectManager.Loaded)
            {
                EffectManager.Instance.PlayOnSurface(material.hitEffect, position, surfacePoint, normal);
                if (leaveHole) EffectManager.Instance.PlayOnSurface(material.bulletHoleEffect, position, surfacePoint, normal, 0f, m_data.bulletHoleSize);
            }
            if (SoundManager.Loaded) SoundManager.Instance.PlayRandomAt(material.hitSounds, position, k_wallHitVolume);

            float hearDistance = DataManager.Instance.HumanParameterData.humanAIParameterData.aiHearBulletWallHitDist;
            WorldSound.EmitPointSound(position, m_team, hearDistance, hearDistance);
        }

        /// <summary>
        /// 다른 팀의 총알이 카메라 옆을 스쳐 지나가는 순간 통과음을 낸다. 원본 SoundManager::PassingBullet + CheckApproach (soundmanager.cpp:513, 626).
        /// 플레이어 팀의 총알은 소리를 내지 않는다. 통과음이 없는 탄종(수류탄)도 내지 않는다.
        /// </summary>
        private void TickPassingSound()
        {
            if (m_passingSoundDone || !SoundManager.Loaded) return;
            if (m_data.bulletPassingSounds == null || m_data.bulletPassingSounds.Count == 0) return;

            Human player = MapLoader.Player;
            if (player == null || m_team == player.Team) return;

            if (!IsClosestApproach(m_prevPosition, m_position, SoundManager.Instance.ListenerPosition, out Vector3 closest)) return;

            SoundManager.Instance.PlayRandomAt(m_data.bulletPassingSounds, closest, k_passingVolume);
            m_passingSoundDone = true;
        }

        /// <summary>
        /// 총알이 머리 옆을 스쳐 지나간 AI 에게 위협 신호를 남긴다. 팀을 가리지 않는다 (원본 GetWorldSound 의 BULLET, 거리 20).
        /// </summary>
        private void NotifyBulletPass()
        {
            if (!SimClock.TickEnabled) return;

            float maxDistance = DataManager.Instance.HumanParameterData.humanAIParameterData.aiHearBulletDist;
            Human player = MapLoader.Player;
            IReadOnlyList<Human> humans = MapLoader.Humans;

            for (int i = 0; i < humans.Count; i++)
            {
                Human human = humans[i];
                if (human == player || !human.Alive) continue;

                Vector3 head = human.Controller.Position + Vector3.Up * human.CameraHeight;
                if (IsClosestApproach(m_prevPosition, m_position, head, out Vector3 closest)
                    && (closest - head).LengthSquared() < maxDistance * maxDistance)
                {
                    human.NotifyThreatHeard();
                }
            }
        }

        /// <summary>
        /// 이번 틱이 듣는 위치에 가장 가까이 지나가는 틱인지 본다. 직전·현재·다음 위치의 거리를 비교한다 (원본 CheckApproach).
        /// </summary>
        /// <param name="previous">직전 틱 위치.</param>
        /// <param name="current">현재 위치.</param>
        /// <param name="listener">듣는 위치.</param>
        /// <param name="closest">경로 위에서 듣는 위치에 가장 가까운 점.</param>
        /// <returns>가장 가까이 지나가는 틱이면 true.</returns>
        private static bool IsClosestApproach(Vector3 previous, Vector3 current, Vector3 listener, out Vector3 closest)
        {
            closest = current;

            Vector3 move = current - previous;
            float d1 = (listener - previous).LengthSquared();
            float d2 = (listener - current).LengthSquared();
            float d3 = (listener - (current + move)).LengthSquared();
            if (!(d1 > d2 && d2 < d3)) return false;

            float length = move.Length();
            if (length > 1e-5f)
            {
                Vector3 direction = move / length;
                closest = current + direction * (listener - current).Dot(direction);
            }
            return true;
        }

        /// <summary>
        /// 이 탄종이 해당 조건에서 터지는 종류이면 터뜨린다. 안전 시간(armingDelay)이 남아 있으면 불발로 사라진다.
        /// </summary>
        /// <param name="trigger">일어난 조건.</param>
        /// <param name="position">폭발 위치.</param>
        /// <returns>터졌거나 불발로 사라졌으면 true. 이 조건에 반응하지 않는 탄종이면 false.</returns>
        private bool ExplodeOnTrigger(ExplosionTrigger trigger, Vector3 position)
        {
            if ((m_data.explosionTrigger & trigger) == 0) return false;

            if (trigger != ExplosionTrigger.Lifetime && m_armingTicks > 0)
            {
                Deactivate();
                return true;
            }

            m_position = position;
            Explode();
            return true;
        }

        /// <summary>
        /// 폭발. 원본 ObjectManager::GrenadeExplosion (objectmanager.cpp:1039-1230).
        /// 팀을 가리지 않고(쏜 사람 포함) 살아 있는 모든 사람의 발·머리 두 점에 거리 비례 데미지를 준다. 블록에 가려진 점은 데미지가 없다.
        /// 소물은 중심 한 점으로 같은 방식의 데미지를 받는다.
        /// </summary>
        private void Explode()
        {
            Vector3 origin = m_position;
            float radius = m_data.explosionRadius;
            BulletManager.NotifyExplosion(origin);

            if (SoundManager.Loaded) SoundManager.Instance.PlayAt(m_data.explosionSound, origin, k_explosionVolume);
            if (EffectManager.Loaded) EffectManager.Instance.Play(m_data.explosionEffectIndex, origin);
            float hearDistance = DataManager.Instance.HumanParameterData.humanAIParameterData.aiHearExplosionDist;
            WorldSound.EmitPointSound(origin, m_team, hearDistance, hearDistance);

            if (radius > 0f)
            {
                IReadOnlyList<Human> humans = MapLoader.Humans;
                for (int i = 0; i < humans.Count; i++)
                {
                    Human human = humans[i];
                    if (!human.Alive) continue;

                    Vector3 feet = human.Controller.Position;
                    float height = human.Controller.Height;
                    if ((feet - origin).Length() > radius + height) continue;

                    int damage = ExplosionDamage(origin, feet + Vector3.Up * k_explosionPointMargin, radius, m_data.humanExplosiveLegDamageMax)
                               + ExplosionDamage(origin, feet + Vector3.Up * (height - k_explosionPointMargin), radius, m_data.humanExplosiveHeadDamageMax);
                    if (damage <= 0) continue;

                    float hpBefore = human.HP;
                    human.HitGrenadeExplosion(damage);
                    PushByExplosion(human, origin, feet, radius);

                    // 수류탄은 킬만 통계에 넣는다 (원본 objectmanager.cpp:1113-1115). 혈흔은 튀지 않는 한 덩이만 낸다.
                    if (hpBefore > 0f && human.HP <= 0f) MapLoader.RecordKill(m_owner);
                    if (EffectManager.Loaded) EffectManager.Instance.Play(m_data.humanHitEffectIndex, feet + Vector3.Up * k_explosionBloodHeight);
                }

                // 소물 (원본 objectmanager.cpp:1171-1211).
                if (m_data.objectExplosiveDamageMax > 0f)
                {
                    IReadOnlyList<SmallObject> objects = MapLoader.SmallObjects;
                    for (int i = 0; i < objects.Count; i++)
                    {
                        SmallObject smallObject = objects[i];
                        if (smallObject.IsDestroyed) continue;

                        Vector3 toObject = smallObject.LogicPosition - origin;
                        float dist = toObject.Length();
                        if (dist > radius) continue;
                        if (dist > 1e-6f && MapLoader.RaycastBlock(BlockLayer.Sight, origin, toObject / dist, dist, out _)) continue;

                        smallObject.HitGrenadeExplosion((int)m_data.objectExplosiveDamageMax - (int)(m_data.objectExplosiveDamageMax / radius * dist));
                    }
                }
            }

            Deactivate();
        }

        /// <summary>
        /// 폭풍으로 사람을 민다. 폭발 지점에서 사람 쪽으로 밀고, 세기는 거리에 비례해 준다. 피격 방향도 같은 쪽이다.
        /// 폭발이 사람보다 위에 있으면 수평으로만 민다 (땅에 박히는 것을 막는다, 원본 objectmanager.cpp:1129-1135).
        /// 원본 식은 그 경우 수평 방향이 폭발 쪽으로 뒤집히는데, 따르지 않고 항상 멀어지게 한다.
        /// </summary>
        /// <param name="human">맞은 사람.</param>
        /// <param name="origin">폭발 위치.</param>
        /// <param name="feet">사람 위치 (발 기준).</param>
        /// <param name="radius">폭발 반경.</param>
        private void PushByExplosion(Human human, Vector3 origin, Vector3 feet, float radius)
        {
            Vector3 toHuman = feet - origin;
            float dist = toHuman.Length();
            human.SetHitYaw(Mathf.RadToDeg(Mathf.Atan2(toHuman.X, -toHuman.Z)));
            if (dist <= 1e-3f) return;

            Vector3 pushDirection = toHuman / dist;
            if (pushDirection.Y < 0f)
            {
                pushDirection.Y = 0f;
                float horizontal = pushDirection.Length();
                if (horizontal > 1e-3f) pushDirection /= horizontal;
            }

            float speed = m_data.explosionknockbackMax * SimClock.FrameRate * Mathf.Max(0f, 1f - dist / radius);
            if (speed > 0f) human.Controller.AddKnockbackVector(pushDirection, speed);
        }

        /// <summary>
        /// 폭발 지점에서 한 점까지의 데미지를 구한다. 원본 objectmanager.cpp:1078-1084: 최대 − (int)(최대 / 반경 × 거리), 블록에 가리면 0.
        /// </summary>
        /// <param name="origin">폭발 위치.</param>
        /// <param name="target">판정 점.</param>
        /// <param name="radius">폭발 반경.</param>
        /// <param name="maxDamage">거리 0 에서의 데미지.</param>
        /// <returns>데미지. 0 이상.</returns>
        private static int ExplosionDamage(Vector3 origin, Vector3 target, float radius, float maxDamage)
        {
            Vector3 toTarget = target - origin;
            float dist = toTarget.Length();
            if (dist > 1e-6f && MapLoader.RaycastBlock(BlockLayer.Sight, origin, toTarget / dist, dist, out _)) return 0;

            int damage = (int)maxDamage - (int)(maxDamage / radius * dist);
            return damage > 0 ? damage : 0;
        }
    }
}

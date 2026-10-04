using Godot;

namespace GodotXOPS
{
    // AIBrain 의 전투(조준·사격·회피 이동·전투 종료 판정) 담당 partial.
    public partial class AIBrain
    {
        // 근거리 교전에서 적의 이동을 앞질러 겨누는 양: 적이 한 틱에 움직이는 거리의 배수 (원본 ai.cpp:633).
        private const float k_leadShortTicks = 1.5f;
        // 원거리 교전에서는 거리에 비례한다: 수평 거리 1 m 당 틱 수 (원본 ai.cpp:639 — 원본 길이 단위당 0.12).
        private const float k_leadLongTicksPerMeter = 1.2f;
        // 수류탄은 근거리 기준 거리보다 먼 만큼 위를 겨눈다: 거리 차에 곱하는 비율, 근거리 / 원거리 (원본 ai.cpp:622-626).
        private const float k_grenadeRaiseShort = 0.12f;
        private const float k_grenadeRaiseLong = 0.4f;
        // 발사 허용각에 AI 레벨의 limitsError 를 곱해 더하는 양 (도), 근거리 / 원거리 (원본 ai.cpp:795, 807).
        private const float k_shotAngleLevelShort = 0.5f;
        private const float k_shotAngleLevelLong = 0.2f;
        // 우선적 달리기 중에는 달리면서 쏘므로 발사 허용각을 넓힌다 (원본 ai.cpp:797-799).
        private const float k_shotAngleRun2Scale = 1.5f;
        // 맨손일 때 물러서기 시작할 확률의 분모 (원본 ai.cpp:713, 307).
        private const int k_unarmedRetreatChance = 80;

        // 회피 이동을 시작할 확률의 분모: 전진 / 후진 / 좌우, 근거리와 원거리 (원본 ai.cpp:279-288).
        private const int k_moveStartForwardShort = 80;
        private const int k_moveStartBackShort = 90;
        private const int k_moveStartSideShort = 70;
        private const int k_moveStartForwardLong = 120;
        private const int k_moveStartBackLong = 150;
        private const int k_moveStartSideLong = 130;
        // 회피 이동을 멈출 확률의 분모: 전진 / 후진 / 좌우, 근거리와 원거리 (원본 ai.cpp:968-981).
        private const int k_moveStopForwardShort = 6;
        private const int k_moveStopBackShort = 6;
        private const int k_moveStopSideShort = 7;
        private const int k_moveStopForwardLong = 5;
        private const int k_moveStopBackLong = 4;
        private const int k_moveStopSideLong = 5;
        // 이동 중이 아닐 때 벽·낭떠러지를 확인할 확률의 분모 (원본 ai.cpp:313).
        private const int k_wallCheckChance = 3;
        // 낭떠러지 확인: 발밑에서 이만큼(m) 아래에 블록이 있는지 본다 (원본 posy − 1.0).
        private const float k_cliffProbeDepth = 0.1f;
        // 적이 너무 가까울 때 물러서기 시작할 확률의 분모 (원본 ai.cpp:382).
        private const int k_closeRetreatChance = 70;

        // 전투 중에 적이 아직 보이는지 확인할 확률의 분모와, 보이는 것과 무관하게 전투를 끝낼 확률의 분모. 근거리 / 원거리 (원본 ai.cpp:866-893).
        private const int k_cancelLookChanceShort = 40;
        private const int k_cancelForceChanceShort = 550;
        private const int k_cancelLookChanceLong = 30;
        private const int k_cancelForceChanceLong = 450;

        /// <summary>
        /// 적을 겨누고 쏜다. 원본 AIcontrol::Action (ai.cpp:559-838).
        /// 총은 적의 이동을 앞질러 겨누고, 수류탄은 거리에 따라 위를 겨눈다. 조준 오차가 허용각 안에 들면 AI 레벨의 확률로 발사한다.
        /// 맨손이면 쏘지 않고, 적이 무장했으면 팔을 든다 (항복). 좀비는 쏘지 않고 다가가 근접 공격한다.
        /// </summary>
        private void Action()
        {
            HumanAIData level = LevelData();
            if (level == null) return;

            Weapon weapon = m_self.CurrentWeapon;
            bool zombie = IsZombie();
            bool unarmed = weapon.IsNone;
            HumanAIParameterData ai = AIData;

            Vector3 position = m_controller.Position;
            Vector3 eye = position + Vector3.Up * m_controller.CameraHeight;
            Vector3 enemyPosition = m_enemy.Controller.Position;
            Vector3 target = enemyPosition + Vector3.Up * m_enemy.Controller.CameraHeight;

            if (!zombie)
            {
                float horizontal = new Vector2(enemyPosition.X - position.X, enemyPosition.Z - position.Z).Length();
                if (weapon.WeaponIndex == DataManager.Instance.WeaponParameterData.weaponGeneralData.grenadeWeaponIndex)
                {
                    target.Y += (horizontal - ai.aiShortAttackDist) * (m_longAttack ? k_grenadeRaiseLong : k_grenadeRaiseShort);
                }
                else
                {
                    Vector3 movePerTick = m_enemy.Controller.MoveVelocity * SimClock.FrameTime;
                    float scale = m_longAttack ? horizontal * k_leadLongTicksPerMeter : k_leadShortTicks;
                    target.X += movePerTick.X * scale;
                    target.Z += movePerTick.Z * scale;
                }
            }

            Vector3 toTarget = target - eye;
            float deltaYaw = Coord.DeltaAngle(m_controller.Yaw, YawTo(toTarget));
            float deltaPitch = PitchTo(toTarget) - m_controller.Pitch;
            float distanceSquared = toTarget.LengthSquared();

            // 원본은 여기서 조준 능력(aiming)으로 "방향을 바꿀 때인지" 정하는데, 값이 항상 1 이상이라 매 틱 조준한다.
            if (level.aiming + (m_longAttack ? 2 : 1) != 0)
            {
                if (deltaYaw > 0f) m_turnRight = true;
                if (deltaYaw < 0f) m_turnLeft = true;

                if (zombie)
                {
                    float armPitch = ZombieArmPitch;
                    if (m_controller.Pitch > armPitch) m_turnUp = true;
                    if (m_controller.Pitch < armPitch) m_turnDown = true;
                }
                else if (unarmed)
                {
                    if (m_enemy.CurrentWeapon.IsNone) m_turnDown = true;
                    else m_turnUp = true;
                }
                else
                {
                    if (deltaPitch > 0f) m_turnDown = true;
                    if (deltaPitch < 0f) m_turnUp = true;
                }
            }

            if (!zombie && unarmed && GetRand(k_unarmedRetreatChance) == 0)
            {
                m_combatMove |= HumanMoveFlag.Back;
            }

            if (zombie)
            {
                ZombieFight(toTarget, deltaYaw);
            }
            else
            {
                HumanAIScopeData scope = ScopeData(weapon);
                float shotAngle;
                if (!m_longAttack)
                {
                    shotAngle = scope.aiShotAngle + k_shotAngleLevelShort * level.limitsError;
                    if (m_nav.Run2) shotAngle *= k_shotAngleRun2Scale;
                }
                else
                {
                    shotAngle = scope.aiShotAngleLong + k_shotAngleLevelLong * level.limitsError;
                }

                if (Mathf.Abs(deltaYaw) + Mathf.Abs(deltaPitch) < shotAngle)
                {
                    if (GetRand(level.attack + (m_longAttack ? 1 : 0)) == 0) m_self.ShotWeapon();
                }
            }

            // 거리에 따라 근거리·원거리 교전을 바꾼다. 우선적 달리기 중에는 원거리로 가지 않는다.
            float shortDistanceSquared = ai.aiShortAttackDist * ai.aiShortAttackDist;
            if (distanceSquared < shortDistanceSquared) m_longAttack = false;
            if (distanceSquared > shortDistanceSquared && !m_nav.Run2) m_longAttack = true;

            if (!zombie) MoveRandom();

            m_actionCnt++;
        }

        /// <summary>
        /// 전투를 끝낼지 판정한다. 원본 AIcontrol::ActionCancel (ai.cpp:841-896).
        /// 적이 죽었거나 너무 멀면 끝낸다. 그 밖에는 가끔 적이 시야 안에 보이는지 확인하고, 낮은 확률로 그냥 끝내기도 한다.
        /// </summary>
        /// <returns>전투를 끝내야 하면 true.</returns>
        private bool ActionCancel()
        {
            if (m_noFight) return true;
            if (!m_enemy.Alive || m_enemy.HP <= 0f) return true;

            HumanAIParameterData ai = AIData;
            float cancel = ai.aiActionCancelDist;
            if ((m_enemy.Controller.Position - m_controller.Position).LengthSquared() > cancel * cancel) return true;

            int lookChance = m_longAttack ? k_cancelLookChanceLong : k_cancelLookChanceShort;
            int forceChance = m_longAttack ? k_cancelForceChanceLong : k_cancelForceChanceShort;

            if (GetRand(lookChance) == 0 && !CheckLookEnemy(m_enemy, ai.aiSearchFovHNear, ai.aiSearchFovVNear, cancel)) return true;
            if (GetRand(forceChance) == 0) return true;

            return false;
        }

        /// <summary>
        /// 전투 중 회피 이동을 확률로 멈춘다. 원본 AIcontrol::CancelMoveTurn (ai.cpp:948-1029) 의 전투 중 이동 부분.
        /// 시작 확률보다 멈출 확률이 훨씬 높아서 짧게 끊어 움직이게 된다. 우선적 달리기 중에는 전부 멈춘다 (이동은 경로가 정한다).
        /// </summary>
        private void CombatMoveCancel()
        {
            if (m_nav.Run2)
            {
                m_combatMove = HumanMoveFlag.None;
                return;
            }

            int forward = m_longAttack ? k_moveStopForwardLong : k_moveStopForwardShort;
            int back = m_longAttack ? k_moveStopBackLong : k_moveStopBackShort;
            int side = m_longAttack ? k_moveStopSideLong : k_moveStopSideShort;

            if (GetRand(forward) == 0) m_combatMove &= ~HumanMoveFlag.Forward;
            if (GetRand(back) == 0) m_combatMove &= ~HumanMoveFlag.Back;
            if (GetRand(side) == 0) m_combatMove &= ~HumanMoveFlag.Left;
            if (GetRand(side) == 0) m_combatMove &= ~HumanMoveFlag.Right;
        }

        /// <summary>
        /// 전투 중에 앞뒤좌우로 무작위 이동한다. 원본 AIcontrol::MoveRandom (ai.cpp:275-387).
        /// 가려는 쪽에 벽이 있거나 바닥이 없으면 반대로 바꾸고, 적이 너무 가까우면 전진을 멈춘다.
        /// </summary>
        private void MoveRandom()
        {
            const HumanMoveFlag anyMove = HumanMoveFlag.Forward | HumanMoveFlag.Back | HumanMoveFlag.Left | HumanMoveFlag.Right;

            int forwardStart = m_longAttack ? k_moveStartForwardLong : k_moveStartForwardShort;
            int backStart = m_longAttack ? k_moveStartBackLong : k_moveStartBackShort;
            int sideStart = m_longAttack ? k_moveStartSideLong : k_moveStartSideShort;

            if (GetRand(forwardStart) == 0) m_combatMove |= HumanMoveFlag.Forward;
            if (GetRand(backStart) == 0) m_combatMove |= HumanMoveFlag.Back;
            if (GetRand(sideStart) == 0) m_combatMove |= HumanMoveFlag.Left;
            if (GetRand(sideStart) == 0) m_combatMove |= HumanMoveFlag.Right;

            if (m_self.CurrentWeapon.IsNone && GetRand(k_unarmedRetreatChance) == 0)
            {
                m_combatMove |= HumanMoveFlag.Back;
            }

            if (GetRand(k_wallCheckChance) == 0 || (m_combatMove & anyMove) != 0)
            {
                float yaw = m_controller.Yaw;
                if (GetRand(2) == 0)
                {
                    Vector3 forward = Coord.YawForward(yaw);
                    if (BlockedOrCliff(forward))
                    {
                        m_combatMove &= ~HumanMoveFlag.Forward;
                        m_combatMove |= HumanMoveFlag.Back;
                    }
                    if (BlockedOrCliff(-forward))
                    {
                        m_combatMove &= ~HumanMoveFlag.Back;
                        m_combatMove |= HumanMoveFlag.Forward;
                    }
                }
                else
                {
                    Vector3 right = Coord.YawRight(yaw);
                    if (BlockedOrCliff(right))
                    {
                        m_combatMove &= ~HumanMoveFlag.Right;
                        m_combatMove |= HumanMoveFlag.Left;
                    }
                    if (BlockedOrCliff(-right))
                    {
                        m_combatMove &= ~HumanMoveFlag.Left;
                        m_combatMove |= HumanMoveFlag.Right;
                    }
                }
            }

            float retreat = AIData.aiCombatRetreatDist;
            if ((m_enemy.Controller.Position - m_controller.Position).LengthSquared() < retreat * retreat)
            {
                m_combatMove &= ~HumanMoveFlag.Forward;
                if (GetRand(k_closeRetreatChance) == 0) m_combatMove |= HumanMoveFlag.Back;
            }
        }

        /// <summary>
        /// 한 방향으로 움직이면 벽에 부딪히거나 떨어지는지 본다. 원본 MoveRandom 의 CheckALLBlockIntersectDummyRay 두 번 (ai.cpp:321-324):
        /// 허리 높이에서 그 방향으로 충돌 반경만큼 간 점과 그 절반 지점이 블록 안이면 벽이고, 발밑 조금 아래의 같은 두 점이 모두 블록 밖이면 낭떠러지다.
        /// </summary>
        /// <param name="direction">확인할 수평 방향 (정규화).</param>
        /// <returns>벽이나 낭떠러지가 있으면 true.</returns>
        private bool BlockedOrCliff(Vector3 direction)
        {
            Vector3 position = m_controller.Position;
            Vector3 reach = direction * m_controller.MapRadius;

            Vector3 waist = position + Vector3.Up * (m_controller.Height * 0.5f);
            if (MapLoader.IsInsideBlock(waist + reach) || MapLoader.IsInsideBlock(waist + reach * 0.5f)) return true;

            Vector3 ground = position + Vector3.Down * k_cliffProbeDepth;
            return !MapLoader.IsInsideBlock(ground + reach) && !MapLoader.IsInsideBlock(ground + reach * 0.5f);
        }
    }
}

namespace GodotXOPS
{
    // AIBrain 의 무기 운용(들기, 재장전, 교체, 버리기, 단발·연발 전환) 담당 partial.
    public partial class AIBrain
    {
        // 탄창이 비었을 때 대응을 시작할 확률의 분모: 평상시 / 경계 / 전투 (원본 ai.cpp:1096-1098).
        private const int k_emptyReactChanceNormal = 1;
        private const int k_emptyReactChanceCaution = 10;
        private const int k_emptyReactChanceAction = 8;

        /// <summary>
        /// 지금 슬롯이 맨손이거나 탄이 하나도 없으면, 탄이 있는 다른 슬롯의 무기를 든다. 경계와 전투 중에만 부른다. 원본 AIcontrol::HaveWeapon (ai.cpp:899-945).
        /// </summary>
        private void HaveWeapon()
        {
            Weapon current = m_self.CurrentWeapon;
            if (!current.IsNone && TotalBullets(current) > 0) return;

            int other = (m_self.SelectWeapon + 1) % Human.WeaponSlotCount;
            Weapon next = m_self.GetWeapon(other);
            if (!next.IsNone && TotalBullets(next) > 0) m_self.SetSelectWeapon(other);
        }

        /// <summary>
        /// 무기를 관리한다. 매 틱 부른다. 원본 AIcontrol::ControlWeapon (ai.cpp:1033-1170).
        /// 탄창이 비면 상태별 확률로 재장전하거나 다른 슬롯으로 바꾸고, 남은 탄이 없으면 버린다.
        /// 원본은 전투 중 수류탄을 들고 있으면 확률로 다른 무기로 바꾸는데 (ai.cpp:1059-1089), 던지기 전에 바꿔 버려 부자연스러워서 넣지 않는다.
        /// 단발·연발을 바꿀 수 있는 무기는 근거리에서 연발, 원거리에서 단발로 맞춘다. 케이스(임무 물품)는 건드리지 않는다.
        /// 원본은 여기서 매번 스코프를 해제하지만, AI 가 스코프를 쓸 수 있게 해제하지 않는다.
        /// </summary>
        private void ControlWeapon()
        {
            Weapon current = m_self.CurrentWeapon;
            if (current.IsNone) return;

            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (parameter.weaponGeneralData.caseWeaponIndex.Contains(current.WeaponIndex)) return;

            int other = (m_self.SelectWeapon + 1) % Human.WeaponSlotCount;

            if (current.Magazine == 0)
            {
                int react = m_mode == AIBattleMode.Normal ? k_emptyReactChanceNormal
                    : m_mode == AIBattleMode.Caution ? k_emptyReactChanceCaution
                    : k_emptyReactChanceAction;

                if (GetRand(react) == 0)
                {
                    // 재장전할 확률 = (under + 1) / ways. 평상시 1/1, 경계 4/5, 근거리 전투 3/4, 원거리 전투 2/3.
                    int ways;
                    int under;
                    if (m_mode == AIBattleMode.Normal)
                    {
                        ways = 1;
                        under = 0;
                    }
                    else if (m_mode == AIBattleMode.Caution)
                    {
                        ways = 5;
                        under = 3;
                    }
                    else if (!m_longAttack)
                    {
                        ways = 4;
                        under = 2;
                    }
                    else
                    {
                        ways = 3;
                        under = 1;
                    }

                    if (TotalBullets(current) == 0)
                    {
                        m_self.DropCurrentWeapon();
                        return;
                    }

                    if (GetRand(ways) <= under) m_self.ReloadWeapon();
                    else m_self.SetSelectWeapon(other);
                    return;
                }
            }

            SwitchBurstMode(current, parameter);
        }

        /// <summary>
        /// 단발·연발을 바꿀 수 있는 무기를 교전 거리에 맞춘다. 근거리에서 단발이면 연발로, 원거리에서 연발이면 단발로 바꾼다. 원본 ai.cpp:1144-1167.
        /// </summary>
        /// <param name="current">지금 든 무기.</param>
        /// <param name="parameter">무기 데이터.</param>
        private void SwitchBurstMode(Weapon current, WeaponParameterData parameter)
        {
            HumanWeaponAction action = HumanWeaponAction.SwitchNext;
            int targetIndex = current.Data.nextWeaponIndex;
            if (!parameter.weaponData.Has(targetIndex))
            {
                action = HumanWeaponAction.SwitchPrevious;
                targetIndex = current.Data.previousWeaponIndex;
            }
            if (!parameter.weaponData.Has(targetIndex)) return;

            bool semiNow = current.Data.burstMode == WeaponBurstMode.SemiAuto;
            bool semiNext = parameter.weaponData[targetIndex].burstMode == WeaponBurstMode.SemiAuto;

            if (!m_longAttack ? (semiNow && !semiNext) : (!semiNow && semiNext))
            {
                m_self.ApplyWeaponAction(action);
            }
        }

        /// <summary>
        /// 무기에 남은 탄 전부 (장전된 탄 + 예비 탄). 원본 weapon 의 nbs.
        /// </summary>
        /// <param name="weapon">무기.</param>
        /// <returns>탄 수.</returns>
        private static int TotalBullets(Weapon weapon)
        {
            return weapon.Magazine + weapon.Reserve;
        }
    }
}

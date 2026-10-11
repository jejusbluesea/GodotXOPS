using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    // AIBrain 의 무기 운용(들기, 재장전, 교체, 버리기, 단발·연발 전환) 담당 partial.
    public partial class AIBrain
    {
        // 탄창이 비었을 때 대응을 시작할 확률의 분모: 평상시 / 경계 / 전투 (원본 ai.cpp:1096-1098).
        private const int k_emptyReactChanceNormal = 1;
        private const int k_emptyReactChanceCaution = 10;
        private const int k_emptyReactChanceAction = 8;
        // 탄이 남은 다른 종류를 찾을 때 다음(이전) 종류를 몇 번까지 따라가는지. 종류들이 고리로 이어져 있어도 여기서 멈춘다.
        private const int k_typeSearchHops = 8;

        /// <summary>
        /// 지금 슬롯이 맨손이거나 탄이 하나도 없으면, 탄이 있는 다른 슬롯의 무기를 든다. 경계와 전투 중에만 부른다. 원본 AIcontrol::HaveWeapon (ai.cpp:899-945).
        /// </summary>
        private void HaveWeapon()
        {
            Weapon current = m_self.CurrentWeapon;
            if (!current.IsNone && !current.OutOfAmmo) return;

            int other = (m_self.SelectWeapon + 1) % Human.WeaponSlotCount;
            bool otherUsable = SlotUsable(m_self.GetWeapon(other));
            // 지금 종류의 탄은 없어도 전환하면 쓸 탄이 있는 무기는 들고 있는다 (ControlWeapon 이 전환한다).
            // 순서가 음수인 종류의 탄만 남았으면 다른 슬롯의 무기가 먼저다.
            if (!current.IsNone && FindLoadedType(current, !otherUsable, out _)) return;

            if (otherUsable) m_self.SetSelectWeapon(other);
        }

        /// <summary>
        /// 그 슬롯의 무기가 쓸 수 있는 상태인지 본다: 맨손이 아니고, 지금 종류에 탄이 있거나 전환하면 쓸 탄이 있다.
        /// </summary>
        /// <param name="weapon">슬롯의 무기.</param>
        /// <returns>쓸 수 있으면 true.</returns>
        private static bool SlotUsable(Weapon weapon)
        {
            return !weapon.IsNone && (!weapon.OutOfAmmo || FindLoadedType(weapon, true, out _));
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

            // 종류마다 순서나 쓰는 거리가 적힌 무기는 전투 중에 그 값으로 종류를 고른다. 그런 값이 없는 무기(원본 전부)는 이 분기를 타지 않는다.
            bool typeChoice = TypeChoiceActive(current);
            if (typeChoice && m_mode == AIBattleMode.Action && m_enemy != null && ChooseType(current, other)) return;

            // 탄을 쓰지 않는 무기(근접 무기)는 탄창이 비었다고 재장전하거나 버리지 않는다.
            if (current.MagazineEmpty)
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

                    if (current.OutOfAmmo)
                    {
                        // 탄 묶음이 다른 종류(총에 달린 유탄 발사기 등)에 탄이 남아 있으면 버리지 않고 그쪽으로 전환한다. 원본의 무기는 묶음이 하나라 늘 버린다.
                        // 순서가 음수인 종류의 탄만 남았고 다른 슬롯에 쓸 무기가 있으면, 이 무기를 버리지 않고 그 슬롯으로 넘어간다.
                        bool otherUsable = SlotUsable(m_self.GetWeapon(other));
                        if (FindLoadedType(current, !otherUsable, out HumanWeaponAction toLoaded)) m_self.ApplyWeaponAction(toLoaded);
                        else if (otherUsable && FindLoadedType(current, true, out _)) m_self.SetSelectWeapon(other);
                        else m_self.DropCurrentWeapon();
                        return;
                    }

                    if (GetRand(ways) <= under) m_self.ReloadWeapon();
                    else m_self.SetSelectWeapon(other);
                    return;
                }
            }

            // 순서나 거리로 종류를 고르는 무기에는 원본의 단발·연발 규칙을 쓰지 않는다 (둘이 서로 되돌린다).
            if (!typeChoice) SwitchBurstMode(current, parameter);
        }

        /// <summary>
        /// 지금 든 무기와 종류 전환으로 갈 수 있는 종류들을 s_types 에 모은다. 맨 앞이 지금 종류이고, 다음 쪽, 이전 쪽 순서다.
        /// </summary>
        /// <param name="current">지금 든 무기.</param>
        private static void CollectTypes(Weapon current)
        {
            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            s_types.Clear();
            s_types.Add((current.WeaponIndex, HumanWeaponAction.None));
            foreach (HumanWeaponAction direction in s_typeDirections)
            {
                int index = current.WeaponIndex;
                for (int hop = 0; hop < k_typeSearchHops; hop++)
                {
                    if (!list.Has(index)) break;
                    int next = direction == HumanWeaponAction.SwitchNext ? list[index].nextWeaponIndex : list[index].previousWeaponIndex;
                    if (!list.Has(next) || next == current.WeaponIndex) break;

                    index = next;
                    bool known = false;
                    foreach ((int Index, HumanWeaponAction Action) entry in s_types) known |= entry.Index == index;
                    if (known) break;
                    s_types.Add((index, direction));
                }
            }
        }

        /// <summary>
        /// 이 무기의 종류들 가운데 순서(aiPriority)나 쓰는 거리(aiMinRange / aiMaxRange)가 적힌 것이 있는지 본다. 지금 종류는 그 한 자루의 값을 본다.
        /// </summary>
        /// <param name="current">지금 든 무기.</param>
        /// <returns>있으면 true.</returns>
        private static bool TypeChoiceActive(Weapon current)
        {
            if (current.AiPriority != 0 || current.AiMinRange > 0f || current.AiMaxRange > 0f) return true;

            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            CollectTypes(current);
            for (int i = 1; i < s_types.Count; i++)
            {
                WeaponData data = list[s_types[i].Index];
                if (data.aiPriority != 0 || data.aiMinRange > 0f || data.aiMaxRange > 0f) return true;
            }
            return false;
        }

        /// <summary>
        /// 전투 중에 어느 종류를 들지 고른다 (원본에 없다). 탄이 있고 적까지의 거리에서 쓸 수 있는 종류 가운데 순서가 가장 큰 것을 든다. 같으면 지금 종류다.
        /// "다른 슬롯의 무기로 바꾸기"가 순서 0 이다: 0 이상인 종류에 탄이 남아 있는 동안은 이 무기를 들고, 음수인 종류만 남았으면 다른 슬롯이 먼저다.
        /// 0 이상인 종류에 탄은 있는데 전부 거리 밖이면 바꾸지 않는다 (다가가거나 물러나는 것은 Action 이 한다).
        /// </summary>
        /// <param name="current">지금 든 무기.</param>
        /// <param name="otherSlot">다른 슬롯의 번호.</param>
        /// <returns>종류나 슬롯을 바꿨으면 true.</returns>
        private bool ChooseType(Weapon current, int otherSlot)
        {
            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            Vector3 eye = m_controller.Position + Vector3.Up * m_controller.CameraHeight;
            Vector3 enemyEye = m_enemy.Controller.Position + Vector3.Up * m_enemy.Controller.CameraHeight;
            float range = (enemyEye - eye).Length();

            CollectTypes(current);
            int best = -1;
            int bestPriority = int.MinValue;
            bool loadedNonNegative = false;
            for (int i = 0; i < s_types.Count; i++)
            {
                int index = s_types[i].Index;
                bool isCurrent = i == 0;
                if (isCurrent ? current.OutOfAmmo : !current.TypeHasAmmo(index)) continue;

                int priority = isCurrent ? current.AiPriority : list[index].aiPriority;
                if (priority >= 0) loadedNonNegative = true;

                float minRange = isCurrent ? current.AiMinRange : list[index].aiMinRange;
                float maxRange = isCurrent ? current.AiMaxRange : list[index].aiMaxRange;
                if ((maxRange > 0f && range > maxRange) || (minRange > 0f && range < minRange)) continue;

                if (priority > bestPriority)
                {
                    best = i;
                    bestPriority = priority;
                }
            }

            if (best >= 0 && bestPriority >= 0) return SwitchToType(best);
            if (loadedNonNegative) return false;

            if (SlotUsable(m_self.GetWeapon(otherSlot)))
            {
                m_self.SetSelectWeapon(otherSlot);
                return true;
            }
            return best >= 0 && SwitchToType(best);
        }

        /// <summary>
        /// s_types 의 그 종류 쪽으로 한 칸 전환한다.
        /// </summary>
        /// <param name="typeEntry">s_types 에서의 자리. 0 은 지금 종류다.</param>
        /// <returns>전환 입력을 넣었으면 true.</returns>
        private bool SwitchToType(int typeEntry)
        {
            if (typeEntry <= 0) return false;

            m_self.ApplyWeaponAction(s_types[typeEntry].Action);
            return true;
        }

        /// <summary>
        /// 종류 전환(다음 / 이전)으로 갈 수 있는 종류 가운데 쓸 탄이 있는 것을 찾는다. 다음 쪽을 먼저, 그다음 이전 쪽을 본다.
        /// 한 번에 한 칸씩 전환하므로 돌려주는 것은 그쪽으로 가는 첫 전환이다 (사이의 종류에 탄이 없어도 다음 틱에 같은 쪽으로 이어 간다).
        /// </summary>
        /// <param name="current">지금 든 무기.</param>
        /// <param name="allowNegative">false 면 순서(aiPriority)가 음수인 종류는 찾지 않는다 (다른 슬롯에 쓸 무기가 있을 때).</param>
        /// <param name="action">그 종류 쪽으로 가는 전환 입력. 못 찾았으면 None.</param>
        /// <returns>찾았으면 true.</returns>
        private static bool FindLoadedType(Weapon current, bool allowNegative, out HumanWeaponAction action)
        {
            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            foreach (HumanWeaponAction direction in s_typeDirections)
            {
                int index = current.WeaponIndex;
                for (int hop = 0; hop < k_typeSearchHops; hop++)
                {
                    if (!list.Has(index)) break;
                    int next = direction == HumanWeaponAction.SwitchNext ? list[index].nextWeaponIndex : list[index].previousWeaponIndex;
                    if (!list.Has(next) || next == current.WeaponIndex) break;

                    index = next;
                    if ((allowNegative || list[index].aiPriority >= 0) && current.TypeHasAmmo(index))
                    {
                        action = direction;
                        return true;
                    }
                }
            }
            action = HumanWeaponAction.None;
            return false;
        }

        private static readonly HumanWeaponAction[] s_typeDirections = { HumanWeaponAction.SwitchNext, HumanWeaponAction.SwitchPrevious };
        // CollectTypes 가 채우는 종류 목록 (무기 번호, 지금 종류에서 그쪽으로 가는 전환 입력). AI 는 한 틱에 한 명씩 차례로 돌므로 같이 쓴다.
        private static readonly List<(int Index, HumanWeaponAction Action)> s_types = new List<(int Index, HumanWeaponAction Action)>();

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
    }
}

using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 무기 한 자루의 종류와 탄약 상태. 사람의 장착 슬롯과 (7단계의) 떨어진 무기가 같은 클래스를 쓴다.
    /// 노드가 아닌 순수 클래스이며 모델 표시는 WeaponVisual 이 맡는다.
    /// 발사 간격·재장전·전환 카운터는 원본처럼 사람 쪽(Human)이 갖는다. 원본 OpenXOPS weapon 클래스의 탄약 부분에 해당한다.
    /// </summary>
    public class Weapon
    {
        // 조준 오차 정수 1 단위 = 0.15°. 원본 objectmanager.cpp:1980 DegreeToRadian(0.15f).
        public const float ErrorRangeUnitDegrees = 0.15f;

        // 사람 종류 데이터가 없을 때 쓰는 초기 탄약 배수. 원본 TOTAL_WEAPON_AUTOBULLET.
        public const int DefaultAutoBulletMultiplier = 3;

        private static readonly WeaponData s_emptyData = new WeaponData { name = string.Empty, scopeIndex = -1, previousWeaponIndex = -1, nextWeaponIndex = -1 };

        private int m_weaponIndex;
        private WeaponData m_data = s_emptyData;
        private WeaponModelData m_modelData;
        private int m_magazine;
        private int m_reserve;
        // 지난 재장전 뒤에 쏜 발 수. 재장전할 때 탄피를 내는 무기(ShellEjectMode.OnReload)가 몇 개를 낼지 정하는 데 쓴다.
        private int m_spentShells;
        // 지금 쓰지 않는 묶음(ammoGroup)의 탄. 종류 전환으로 다른 묶음의 무기가 됐을 때 접어 둔 것이다. 그런 전환을 한 적이 없으면 null.
        private Dictionary<int, StoredAmmo> m_storedAmmo;

        /// <summary>
        /// 접어 둔 한 묶음의 탄.
        /// </summary>
        private struct StoredAmmo
        {
            public int magazine;
            public int reserve;
        }

        public int WeaponIndex => m_weaponIndex;
        public WeaponData Data => m_data;
        public WeaponModelData ModelData => m_modelData;
        // 장전된 탄 (원본 Loadbullets).
        public int Magazine => m_magazine;
        // 예비 탄 (원본 bullets − Loadbullets).
        public int Reserve => m_reserve;
        // 쏠 때 탄을 쓰는 무기인지 (WeaponData.consumeAmmo).
        public bool UsesAmmo => m_data.consumeAmmo;
        // 탄창이 비어서 재장전이나 교체가 필요한지 / 남은 탄이 하나도 없는지. 탄을 쓰지 않는 무기는 둘 다 늘 false 다.
        public bool MagazineEmpty => m_data.consumeAmmo && m_magazine == 0;
        public bool OutOfAmmo => m_data.consumeAmmo && m_magazine + m_reserve == 0;
        // AI 가 이 무기를 쓰는 거리 (m, 0 이면 제한 없음). 처음 값은 무기 데이터의 것이고 이 한 자루의 값이다.
        public float AiMaxRange { get; private set; }
        public float AiMinRange { get; private set; }
        // AI 가 지금 종류를 고르는 순서 (WeaponData.aiPriority). 처음 값은 무기 데이터의 것이고 이 한 자루의 값이다.
        public int AiPriority { get; private set; }
        // 맨손 여부. 원본에서 weapon[selectweapon] == NULL 인 상태에 해당한다.
        public bool IsNone => m_weaponIndex == DataManager.Instance.WeaponParameterData.weaponGeneralData.noneWeaponIndex;

        /// <summary>
        /// 무기 종류와 탄약을 (다시) 설정한다. 인덱스가 범위 밖이면 맨손으로 폴백하므로 슬롯이 비는 일은 없다.
        /// </summary>
        /// <param name="weaponIndex">WeaponParameterData.weaponData 인덱스.</param>
        /// <param name="magazine">장전된 탄. 음수면 장탄수만큼 가득.</param>
        /// <param name="reserve">예비 탄. 음수면 장탄수 × (기본 배수 − 1).</param>
        /// <param name="clampMagazine">true 면 장전된 탄을 장탄수 이하로 자른다. 치트 무기 변경처럼 탄약을 그대로 넘길 때는 false.</param>
        public void Configure(int weaponIndex, int magazine = -1, int reserve = -1, bool clampMagazine = true)
        {
            ApplyType(weaponIndex);
            // 새 무기다. 접어 둔 다른 묶음의 탄은 없다.
            m_storedAmmo = null;

            int magazineSize = m_data.magazineSize;
            if (magazine < 0) magazine = magazineSize;
            if (reserve < 0) reserve = magazineSize * (DefaultAutoBulletMultiplier - 1);
            SetLoadedAmmo(clampMagazine ? Mathf.Clamp(magazine, 0, magazineSize) : magazine, reserve);
        }

        /// <summary>
        /// 같은 무기의 종류만 바꾼다 (Z / X 의 종류 전환). 이 한 자루가 갖고 있던 상태는 남는다.
        /// 바꿀 종류의 탄 묶음(ammoGroup)이 지금과 같으면 탄을 그대로 넘긴다 (원본 human::ChangeWeaponID: 단발 ↔ 연발).
        /// 다르면 지금 탄을 접어 두고 그 묶음의 탄을 꺼낸다. 그 묶음을 처음 쓰는 것이면 그 종류의 새 무기처럼 채운다.
        /// </summary>
        /// <param name="weaponIndex">바꿀 무기 인덱스. 범위 밖이면 맨손.</param>
        /// <param name="freshReserve">그 묶음을 처음 쓸 때 줄 예비 탄 (장탄수 × (사람 종류의 배수 − 1)).</param>
        public void SwitchType(int weaponIndex, int freshReserve)
        {
            int oldGroup = m_data.ammoGroup;
            int magazine = m_magazine;
            int reserve = m_reserve;

            ApplyType(weaponIndex);

            int newGroup = m_data.ammoGroup;
            if (newGroup != oldGroup)
            {
                m_storedAmmo ??= new Dictionary<int, StoredAmmo>();
                m_storedAmmo[oldGroup] = new StoredAmmo { magazine = magazine, reserve = reserve };
                if (m_storedAmmo.Remove(newGroup, out StoredAmmo stored))
                {
                    magazine = stored.magazine;
                    reserve = stored.reserve;
                }
                else
                {
                    magazine = m_data.magazineSize;
                    reserve = Mathf.Max(0, freshReserve);
                }
            }
            SetLoadedAmmo(magazine, reserve);
        }

        /// <summary>
        /// 다른 무기의 상태를 통째로 옮겨 온다 (종류, 탄, 접어 둔 다른 묶음의 탄, AI 가 쓰는 거리). 버린 무기를 맵에 놓을 때와 주울 때 쓴다:
        /// 무기 한 자루가 갖는 상태는 버렸다 주워도 그대로여야 한다.
        /// </summary>
        /// <param name="source">옮겨 올 무기.</param>
        public void CopyFrom(Weapon source)
        {
            m_weaponIndex = source.m_weaponIndex;
            m_data = source.m_data;
            m_modelData = source.m_modelData;
            m_magazine = source.m_magazine;
            m_reserve = source.m_reserve;
            m_spentShells = source.m_spentShells;
            m_storedAmmo = source.m_storedAmmo != null ? new Dictionary<int, StoredAmmo>(source.m_storedAmmo) : null;
            AiMinRange = source.AiMinRange;
            AiMaxRange = source.AiMaxRange;
            AiPriority = source.AiPriority;
        }

        /// <summary>
        /// 접어 둔 다른 묶음의 탄을 읽는다. 지금 쓰는 묶음은 Magazine / Reserve 다.
        /// </summary>
        /// <param name="group">묶음 번호 (WeaponData.ammoGroup).</param>
        /// <param name="magazine">장전된 탄.</param>
        /// <param name="reserve">예비 탄.</param>
        /// <returns>그 묶음의 탄을 접어 둔 것이 있으면 true.</returns>
        public bool TryGetStoredAmmo(int group, out int magazine, out int reserve)
        {
            magazine = 0;
            reserve = 0;
            if (m_storedAmmo == null || !m_storedAmmo.TryGetValue(group, out StoredAmmo stored)) return false;

            magazine = stored.magazine;
            reserve = stored.reserve;
            return true;
        }

        /// <summary>
        /// 이 무기를 그 종류로 전환하면 쓸 탄이 있는지 본다 (Z / X 로 이어진 다른 종류를 AI 가 고를 때 쓴다).
        /// 탄을 쓰지 않는 종류는 늘 있다. 지금과 같은 묶음이면 지금 탄, 접어 둔 묶음이면 그 탄, 아직 꺼낸 적 없는 묶음이면 새로 채워질 탄을 본다.
        /// </summary>
        /// <param name="weaponIndex">전환할 무기 인덱스.</param>
        /// <returns>쓸 탄이 있으면 true. 없는 번호면 false.</returns>
        public bool TypeHasAmmo(int weaponIndex)
        {
            DataList<WeaponData> list = DataManager.Instance.WeaponParameterData.weaponData;
            if (!list.Has(weaponIndex)) return false;

            WeaponData data = list[weaponIndex];
            if (data.pelletCount <= 0) return false;
            if (!data.consumeAmmo) return true;
            if (data.ammoGroup == m_data.ammoGroup) return m_magazine + m_reserve > 0;
            if (TryGetStoredAmmo(data.ammoGroup, out int magazine, out int reserve)) return magazine + reserve > 0;
            return data.magazineSize > 0;
        }

        /// <summary>
        /// 지금 쓰는 묶음의 탄을 직접 정한다. 무기의 다른 상태는 그대로다.
        /// </summary>
        /// <param name="magazine">장전된 탄 (0 이상).</param>
        /// <param name="reserve">예비 탄 (0 이상).</param>
        public void SetAmmo(int magazine, int reserve)
        {
            SetLoadedAmmo(Mathf.Max(0, magazine), Mathf.Max(0, reserve));
        }

        /// <summary>
        /// 무기 종류와 그 종류의 데이터를 정한다. 탄은 건드리지 않는다. AI 가 쓰는 거리는 그 종류의 데이터 값으로 돌아간다.
        /// </summary>
        /// <param name="weaponIndex">무기 인덱스. 범위 밖이면 맨손.</param>
        private void ApplyType(int weaponIndex)
        {
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (!parameter.weaponData.Has(weaponIndex))
            {
                weaponIndex = parameter.weaponGeneralData.noneWeaponIndex;
            }

            m_weaponIndex = weaponIndex;
            m_data = parameter.weaponData.Has(weaponIndex) ? parameter.weaponData[weaponIndex] : s_emptyData;

            int modelIndex = m_data.modelIndex;
            m_modelData = parameter.weaponModelData.Has(modelIndex) ? parameter.weaponModelData[modelIndex] : null;
            SetAiRange(m_data.aiMinRange, m_data.aiMaxRange);
            AiPriority = m_data.aiPriority;
        }

        /// <summary>
        /// AI 가 지금 종류를 고르는 순서를 정한다. 종류를 (다시) 설정하거나 전환하면 무기 데이터의 값으로 돌아간다.
        /// </summary>
        /// <param name="priority">순서. 클수록 먼저 고른다. 음수면 다른 슬롯의 무기보다 뒤다.</param>
        public void SetAiPriority(int priority)
        {
            AiPriority = priority;
        }

        /// <summary>
        /// 지금 쓰는 묶음의 탄을 정한다.
        /// </summary>
        /// <param name="magazine">장전된 탄.</param>
        /// <param name="reserve">예비 탄.</param>
        private void SetLoadedAmmo(int magazine, int reserve)
        {
            m_magazine = magazine;
            m_reserve = reserve;
            // 탄창에서 비어 있는 만큼을 쏜 것으로 친다 (주운 무기, 종류를 바꾼 무기).
            int magazineSize = m_data.magazineSize;
            m_spentShells = Mathf.Clamp(magazineSize - m_magazine, 0, magazineSize);
        }

        /// <summary>
        /// AI 가 이 무기를 쓰는 거리를 정한다. 종류를 (다시) 설정하면 무기 데이터의 값으로 돌아간다.
        /// </summary>
        /// <param name="minRange">이보다 가까우면 쓰지 않는 거리 (m). 0 이하면 제한 없음.</param>
        /// <param name="maxRange">이보다 멀면 쓰지 않는 거리 (m). 0 이하면 제한 없음.</param>
        public void SetAiRange(float minRange, float maxRange)
        {
            AiMinRange = float.IsFinite(minRange) ? Mathf.Max(0f, minRange) : 0f;
            AiMaxRange = float.IsFinite(maxRange) ? Mathf.Max(0f, maxRange) : 0f;
        }

        /// <summary>
        /// 지금 쏠 수 있는 탄이 장전돼 있는지 본다 (ConsumeShot 이 성공할 조건). 탄을 쓰지 않는 무기는 탄창과 무관하다.
        /// </summary>
        /// <returns>쏠 수 있으면 true.</returns>
        public bool CanConsumeShot()
        {
            return m_data.pelletCount > 0 && (!m_data.consumeAmmo || m_magazine > 0);
        }

        /// <summary>
        /// 예비 탄을 더한다 (이벤트의 무한 탄약).
        /// </summary>
        /// <param name="count">더할 탄 수.</param>
        public void AddReserve(int count)
        {
            if (count > 0) m_reserve += count;
        }

        /// <summary>
        /// 한 발 쏠 탄약을 소비한다. 원본 weapon::Shot (object.cpp:2325-2366).
        /// 자동 재장전 방식(수류탄)은 쏜 직후 예비 탄에서 바로 채우고, 예비 탄도 없으면 소진된 것으로 알린다.
        /// </summary>
        /// <param name="depleted">자동 재장전 무기가 탄을 다 써서 무기째 사라져야 하면 true.</param>
        /// <param name="autoReloaded">이 발사로 자동 재장전이 일어났으면 true.</param>
        /// <returns>탄약을 소비했으면 true. 장전된 탄이 없거나 쏠 수 없는 무기면 false.</returns>
        public bool ConsumeShot(out bool depleted, out bool autoReloaded)
        {
            depleted = false;
            autoReloaded = false;
            if (m_data.pelletCount <= 0) return false;
            // 탄을 쓰지 않는 무기는 탄창을 건드리지 않고 쏜다.
            if (!m_data.consumeAmmo) return true;
            if (m_magazine <= 0) return false;

            m_magazine--;
            m_spentShells++;

            if (m_magazine <= 0 && m_data.reloadStyle == WeaponReloadStyle.AutoReload)
            {
                if (m_reserve > 0)
                {
                    RunReload();
                    autoReloaded = true;
                }
                else if (m_data.discardAfterAutoReloadIfNoAmmo)
                {
                    depleted = true;
                }
            }
            return true;
        }

        /// <summary>
        /// 재장전을 시작할 수 있는지 본다. 원본 weapon::StartReload (object.cpp:2371-2379) 는 예비 탄이 없을 때만 막는다.
        /// 탄창이 가득 찬 경우도 막는다. 원본은 허용해서 남은 탄을 통째로 버리게 된다.
        /// 자동 재장전 무기는 직접 재장전할 수 없다 (쏘면 저절로 채워진다). 탄을 쓰지 않는 무기도 재장전하지 않는다.
        /// </summary>
        /// <returns>재장전할 수 있으면 true.</returns>
        public bool CanReload()
        {
            if (!m_data.consumeAmmo) return false;
            if (m_data.reloadStyle == WeaponReloadStyle.AutoReload) return false;
            return m_data.magazineSize > 0 && m_reserve > 0 && m_magazine < m_data.magazineSize;
        }

        // 한 발씩 장전하는 무기인지.
        public bool ShellByShell => m_data.reloadStyle == WeaponReloadStyle.ShellByShellReload;

        /// <summary>
        /// 예비 탄에서 한 발을 탄창에 넣는다 (한 발씩 장전하는 무기). 한 발을 넣을 시간이 지날 때마다 사람이 호출한다.
        /// </summary>
        /// <returns>더 넣을 수 있으면 true. 탄창이 찼거나 예비 탄이 떨어졌으면 false.</returns>
        public bool LoadShell()
        {
            if (m_reserve > 0 && m_magazine < m_data.magazineSize)
            {
                m_magazine++;
                m_reserve--;
            }
            return m_reserve > 0 && m_magazine < m_data.magazineSize;
        }

        /// <summary>
        /// 재장전할 때 한꺼번에 나올 탄피의 수를 돌려주고, 쏜 발 수를 0 으로 되돌린다. 재장전이 시작될 때마다 부른다.
        /// 남은 탄을 두는 방식(남기고 재장전, 한 발씩 장전)은 지난 재장전 뒤에 쏜 만큼, 그 밖에는 장탄수만큼이다.
        /// </summary>
        /// <returns>탄피 수.</returns>
        public int TakeReloadShells()
        {
            int spent = Mathf.Min(m_spentShells, m_data.magazineSize);
            m_spentShells = 0;
            bool retains = m_data.reloadStyle == WeaponReloadStyle.RetainAndReload || m_data.reloadStyle == WeaponReloadStyle.ShellByShellReload;
            return retains ? spent : m_data.magazineSize;
        }

        /// <summary>
        /// 탄창을 채운다. 재장전 시간이 끝난 틱에 사람이 호출한다. 원본 weapon::RunReload (object.cpp:2383-2408) 는 남은 탄을 버리는 방식 하나뿐이고,
        /// 남은 탄을 유지하는 방식은 데이터(reloadStyle)로 고를 수 있게 추가된 것이다.
        /// </summary>
        public void RunReload()
        {
            if (m_reserve <= 0) return;
            int magazineSize = m_data.magazineSize;

            if (m_data.reloadStyle == WeaponReloadStyle.DiscardAndReload)
            {
                // 탄창에 남은 탄은 버리고 예비 탄에서 새로 채운다.
                int load = Mathf.Min(m_reserve, magazineSize);
                m_magazine = load;
                m_reserve -= load;
            }
            else
            {
                // 남은 탄은 두고 모자란 만큼만 채운다.
                int load = Mathf.Min(m_reserve, Mathf.Max(0, magazineSize - m_magazine));
                m_magazine += load;
                m_reserve -= load;
            }
        }

        /// <summary>
        /// 치트(F6) — 예비 탄에 장탄수만큼 더한다. 원본 ObjectManager::CheatAddBullet (objectmanager.cpp:2285).
        /// </summary>
        public void CheatAddMagazine()
        {
            if (m_data.magazineSize <= 0) return;
            m_reserve += m_data.magazineSize;
        }
    }
}

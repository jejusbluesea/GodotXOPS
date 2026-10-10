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

        public int WeaponIndex => m_weaponIndex;
        public WeaponData Data => m_data;
        public WeaponModelData ModelData => m_modelData;
        // 장전된 탄 (원본 Loadbullets).
        public int Magazine => m_magazine;
        // 예비 탄 (원본 bullets − Loadbullets).
        public int Reserve => m_reserve;
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
            WeaponParameterData parameter = DataManager.Instance.WeaponParameterData;
            if (!parameter.weaponData.Has(weaponIndex))
            {
                weaponIndex = parameter.weaponGeneralData.noneWeaponIndex;
            }

            m_weaponIndex = weaponIndex;
            m_data = parameter.weaponData.Has(weaponIndex) ? parameter.weaponData[weaponIndex] : s_emptyData;

            int modelIndex = m_data.modelIndex;
            m_modelData = parameter.weaponModelData.Has(modelIndex) ? parameter.weaponModelData[modelIndex] : null;

            int magazineSize = m_data.magazineSize;
            if (magazine < 0) magazine = magazineSize;
            if (reserve < 0) reserve = magazineSize * (DefaultAutoBulletMultiplier - 1);
            m_magazine = clampMagazine ? Mathf.Clamp(magazine, 0, magazineSize) : magazine;
            m_reserve = reserve;
            // 탄창에서 비어 있는 만큼을 쏜 것으로 친다 (주운 무기, 종류를 바꾼 무기).
            m_spentShells = Mathf.Clamp(magazineSize - m_magazine, 0, magazineSize);
        }

        /// <summary>
        /// 지금 쏠 수 있는 탄이 장전돼 있는지 본다 (ConsumeShot 이 성공할 조건).
        /// </summary>
        /// <returns>쏠 수 있으면 true.</returns>
        public bool CanConsumeShot()
        {
            return m_magazine > 0 && m_data.pelletCount > 0;
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
            if (m_magazine <= 0) return false;
            if (m_data.pelletCount <= 0) return false;

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
        /// 자동 재장전 무기는 직접 재장전할 수 없다 (쏘면 저절로 채워진다).
        /// </summary>
        /// <returns>재장전할 수 있으면 true.</returns>
        public bool CanReload()
        {
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

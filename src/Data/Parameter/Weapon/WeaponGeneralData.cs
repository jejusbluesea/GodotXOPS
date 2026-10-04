using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 무기 시스템 전역에서 쓰는 특수 무기 인덱스와 공통 스케일을 담는 컨테이너 클래스.
    /// 정확도는 WeaponAccuracyData, 떨어진 무기 낙하는 WeaponDropPhysicsData 로 분리했다.
    /// </summary>
    public class WeaponGeneralData
    {
        public int noneWeaponIndex;
        public List<int> caseWeaponIndex = new List<int>();
        public int grenadeWeaponIndex;
        public float weaponScale;
    }
}

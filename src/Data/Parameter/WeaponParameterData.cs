using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 무기 파라미터(공용, 정확도, 낙하 물리, 데이터, 탄환, 스코프, 모델)를 담는 컨테이너 클래스.
    /// </summary>
    public class WeaponParameterData
    {
        // 각 필드는 기본값으로 초기화한다. 섹션 파일 로드가 실패(부재/빈/깨짐)해도 해당 필드는
        // 빈 기본 인스턴스로 남아 다운스트림 NullReference를 막는다(OverwriteFromJson 참조).
        public WeaponGeneralData weaponGeneralData = new WeaponGeneralData();
        public WeaponAccuracyData weaponAccuracyData = new WeaponAccuracyData();
        public WeaponDropPhysicsData weaponDropPhysicsData = new WeaponDropPhysicsData();
        public DataList<WeaponData> weaponData = new DataList<WeaponData>();
        public DataList<BulletData> bulletData = new DataList<BulletData>();
        public DataList<ScopeData> scopeData = new DataList<ScopeData>();
        public DataList<WeaponModelData> weaponModelData = new DataList<WeaponModelData>();
    }
}

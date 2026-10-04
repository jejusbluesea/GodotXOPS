namespace GodotXOPS
{
    /// <summary>
    /// 모든 오브젝트에 적용되는 공통 파라미터를 담는 컨테이너 클래스.
    /// </summary>
    public class ObjectGeneralData
    {
        public float modelScale;

        // 총알 명중 시 소물 데미지 = floor(attacks × bulletDamageMultiplier), 명중 후 잔여 attacks ×= bulletPenetrationAttenuation.
        // 원본 objectmanager.cpp:835/839 (0.25 / 0.7). 총알은 서브스텝마다 소물 충돌 구체를 반복 피격하므로 한 발이 통과 두께만큼 여러 번 데미지를 준다.
        public float bulletDamageMultiplier;
        public float bulletPenetrationAttenuation;
        public int addonObjectIndex;
    }
}

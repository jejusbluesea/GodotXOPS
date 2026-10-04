namespace GodotXOPS
{
    /// <summary>
    /// Human 의 월드 상호작용 범위 파라미터를 담는 컨테이너 클래스. 현재는 떨어진 무기 자동 줍기 판정 범위.
    /// </summary>
    public class HumanInteractionData
    {
        // 떨어진 무기 자동 줍기 판정 볼륨 (원본 HUMAN_PICKUPWEAPON_R/L/H). 슬롯이 맨손일 때 매 프레임 검사.
        public FloatRange weaponPickupVerticalRange; // 수직 밴드: 무기 높이 − Human 높이 (min~max)
        public float weaponPickupRadius; // 수평(XZ) 반경
    }
}

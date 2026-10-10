namespace GodotXOPS
{
    /// <summary>
    /// 인간 캐릭터의 이름, 체력, 모델, 무기, AI 파라미터 정보를 담는 컨테이너 클래스.
    /// </summary>
    public class HumanData
    {
        public string name;
        public float hp;
        // 방어구 포인트. 몸통과 다리에 총알을 맞을 때마다 깎이고, 0 이면 방어구가 없는 것이다.
        public float armor;
        // 방어구가 있는 동안 몸통과 다리에 받는 총알 데미지를 줄이는 비율 (0 에서 1. 0 이면 줄이지 않는다).
        public float armorDamageDecrease;
        // 헬멧 포인트. 머리에 총알을 맞을 때마다 깎이고, 0 이면 헬멧이 없는 것이다.
        public float helmet;
        // 헬멧이 있는 동안 머리에 받는 총알 데미지를 줄이는 비율 (0 에서 1).
        public float helmetDamageDecrease;
        public int modelIndex;
        public int weaponIndex0;
        public int weaponIndex1;
        public int aiIndex;
        public int typeIndex;
    }
}

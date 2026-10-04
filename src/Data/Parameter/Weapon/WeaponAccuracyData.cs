namespace GodotXOPS
{
    /// <summary>
    /// 무기 종류와 무관하게 모든 사격에 공통 적용되는 조준 오차 파라미터를 담는 컨테이너 클래스.
    /// 페널티 값은 무기별 errorRange 와 같은 정수 스케일이다.
    /// </summary>
    public class WeaponAccuracyData
    {
        public int walkAccuracyPenalty;
        public int forwardAccuracyPenalty;
        public int backAccuracyPenalty;
        public int strafeAccuracyPenalty;
        public int airborneAccuracyPenalty;
        public int injuryAccuracyPenalty;
        public int injuryHpThreshold;
        public float reactionRecoveryPerSecond;
    }
}

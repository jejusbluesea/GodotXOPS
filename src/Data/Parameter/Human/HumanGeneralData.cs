namespace GodotXOPS
{
    /// <summary>
    /// 모든 인간 캐릭터에 적용되는 공통 스케일·높이, 팔 각도, 피격 반응을 담는 컨테이너 클래스.
    /// </summary>
    public class HumanGeneralData
    {
        public float humanBodyScale;
        public float humanArmScale;
        public float humanLegScale;
        public float humanBodyHeight;
        public float humanArmHeight;
        public float humanLegHeight;

        public float armAngleReloading;
        // 무기 든 평상시 팔 pitch 초기값 (원본 armrotation_y init, object.cpp:265 DegreeToRadian(-30) = 아래로 30°).
        // arm-space(음수=아래) 원본값. HumanController 에서 카메라 pitch space(양수=아래)로 부호 반전해 적용.
        public float armAngleInitial;

        public int headHitReaction;
        public int bodyHitReaction;
        public int legHitReaction;
        public int zombieHitReaction;
        public int grenadeHitReaction;
    }
}

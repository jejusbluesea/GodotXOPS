namespace GodotXOPS
{
    /// <summary>
    /// 인간 이동 물리(계단·경사·접지)와 중력·낙하·시체 파라미터를 담는 컨테이너 클래스.
    /// 체형 크기(height/radius)는 타입별로 다를 수 있어 ControllerSizeData 로 분리했다(HumanTypeData.controllerSizeIndex).
    /// </summary>
    public class HumanControllerData
    {
        public float controllerStepOffset;
        public float controllerStepClimbSpeed;
        public float controllerSlopeLimit;
        public float controllerGroundProbeRadius;

        public float gravityAcceleration;
        public float fallMinSpeed;
        public float fallMaxSpeed;
        public int fallDamageMax; // 종단속도 착지 시 데미지 (원본 HUMAN_DAMAGE_MAXFALL 120)
        public int fallDamageRandomMax; // 가산 랜덤 데미지 상한 exclusive — Random.Range(0, N) (원본 GetRand(6) = 0~5)
        public float deadlineY;
        public float deadBodyFallAngularSpeed;
        public bool deadBodyCollision;
    }
}

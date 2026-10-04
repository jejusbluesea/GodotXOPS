namespace GodotXOPS
{
    /// <summary>
    /// 바닥에 떨어진 무기의 낙하 물리 파라미터를 담는 컨테이너 클래스.
    /// 장착 중인 무기는 사람을 따라다니므로 이 값들의 영향을 받지 않는다.
    /// </summary>
    public class WeaponDropPhysicsData
    {
        public float gravity;
        public float terminalVelocityY;
        public float horizontalDampingPerSec;
        public float horizontalStopThreshold;
        public float groundCollisionMargin;
        public float deadlineY;
        public float dropoffHorizontalSpeed;
    }
}

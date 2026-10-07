namespace GodotXOPS
{
    /// <summary>
    /// 모든 블록 재질이 공유하는 전역 설정을 담는 데이터 클래스.
    /// </summary>
    public class BlockMaterialGeneralData
    {
        // 발소리 볼륨 (0 에서 1 사이). 소리의 거리 감쇠는 다른 효과음과 같아서, 볼륨이 작을수록 가까이에서만 들린다.
        public float footstepWalkVolume = 0.15f;
        public float footstepRunVolume = 0.3f;
        public float footstepLandingVolume = 0.4f;
    }
}

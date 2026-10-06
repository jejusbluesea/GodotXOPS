namespace GodotXOPS
{
    /// <summary>
    /// 모든 이펙트가 공유하는 전역 설정(벽 혈흔 프리셋, 데칼을 띄우는 거리, 풀 크기)을 담는 데이터 클래스.
    /// </summary>
    public class EffectGeneralData
    {
        // CollideMap 혈흔 입자가 Block 에 닿을 때 생성할 벽 데칼 이펙트 인덱스 (원본 AddMapEffect 의 하드코딩 대응). 보통 WallBlood.
        public int wallBloodEffectIndex;
        // 빌보드가 아닌 이펙트(데칼)를 블록 면에서 띄우는 거리 (m). 면과 겹쳐 떨리는 것을 막는다.
        public float decalSurfaceOffset = 0.05f;
        // 시작할 때 만들어 두는 이펙트 자리 수 (원본 MAX_EFFECT 256).
        public int poolInitialSize = 256;
        // 자리가 다 찼을 때 한 번에 늘리는 수. 0 이하이면 늘리지 않는다 (원본처럼 가득 차면 버린다).
        public int poolGrowStep = 64;
        // 늘릴 수 있는 한계. 0 이하이면 한계 없음.
        public int poolMaxSize;
    }
}

namespace GodotXOPS
{
    /// <summary>
    /// 메인메뉴 배경에서 사용되는 데모 맵의 BD1, PD1 경로와 스카이 인덱스를 담는 데이터 클래스.
    /// </summary>
    public class DemoData
    {
        // 경로는 SafePath.Combine 에 그대로 들어가 null 이면 예외가 나므로 빈 문자열이 기본이다
        // (빈 경로면 SafePath가 null을 돌려주고 LoadBlockData/LoadPointData가 로그와 함께 걸러낸다).
        public string bd1Path = string.Empty;
        public string pd1Path = string.Empty;
        public int skyIndex;
    }
}

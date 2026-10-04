namespace GodotXOPS
{
    /// <summary>
    /// JSON에서 로드되는 공식 미션의 경로 및 설정 데이터를 담는 클래스.
    /// </summary>
    public class OfficialMissionData : IMissionData
    {
        // 경로는 SafePath.Combine 에 그대로 들어가 null 이면 예외가 나므로 빈 문자열이 기본이다(DemoData 와 동일).
        public string name = string.Empty;
        public string fullname = string.Empty;
        public string bd1Path = string.Empty;
        public string pd1Path = string.Empty;
        public string txtPath = string.Empty;
        public bool adjustCollision;
        public bool darkScreen;

        [System.Text.Json.Serialization.JsonIgnore]
        public string Name => name;
    }
}

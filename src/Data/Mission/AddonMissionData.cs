namespace GodotXOPS
{
    /// <summary>
    /// 어드온 미션의 이름과 .mif 파일 경로를 담는 데이터 클래스.
    /// </summary>
    public class AddonMissionData : IMissionData
    {
        public string name;
        public string mifPath;

        [System.Text.Json.Serialization.JsonIgnore]
        public string Name => name;
    }
}

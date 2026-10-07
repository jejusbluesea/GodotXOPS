namespace GodotXOPS
{
    /// <summary>
    /// 어드온 미션의 이름과 미션 파일 경로를 담는 데이터 클래스. 미션 파일은 원본 MIF(.mif)이거나 확장 미션 파일(.mif2)이다.
    /// </summary>
    public class AddonMissionData : IMissionData
    {
        public string name;
        public string mifPath;

        [System.Text.Json.Serialization.JsonIgnore]
        public string Name => name;
    }
}

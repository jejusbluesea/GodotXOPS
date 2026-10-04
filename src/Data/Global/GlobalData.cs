namespace GodotXOPS
{
    /// <summary>
    /// GodotXOPS의 전역 데이터, 버전 및 기타 정보를 담는 클래스.
    /// </summary>
    public class GlobalData
    {
        // 전부 UI 로 그대로 넘어가 문자열로 이어 붙여진다. null 이 섞이지 않도록
        // 파일이 없거나 깨졌을 때도 빈 문자열이 되도록 초기화한다("데이터가 없으면 빈 문자열" 계약).
        public string productName = string.Empty;
        public string companyName = string.Empty;
        public string licenseType = string.Empty;
        public string licenseName = string.Empty;
        public string[] licenseLines = new string[0];
        public string versionMajor = string.Empty;
        public string versionMinor = string.Empty;
        public string versionPatch = string.Empty;

        // 표시용 버전 문자열(major.minor.patch). [JsonIgnore] 로 직렬화에서 제외한다.
        [System.Text.Json.Serialization.JsonIgnore]
        public string Version => $"{versionMajor}.{versionMinor}.{versionPatch}";
    }
}

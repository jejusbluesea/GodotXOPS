using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 화면 스크립트 하나의 등록 파일 (JSON, godotdata/ui/ 에 둔다). 화면 하나를 SafeGDScript(.sgd) 하나가 맡는다.
    /// 등록된 화면은 기본 화면 대신 그 스크립트가 그린다.
    /// </summary>
    public class UiPackData
    {
        // 맡을 화면의 이름 (씬 이름과 같다. 예: "maingame").
        public string screen = string.Empty;
        // 스크립트 파일 경로 (exe 폴더 기준, 확장자 .sgd).
        public string scriptPath = string.Empty;
        // 스크립트가 번호로 가리키는 이미지 파일들 (exe 폴더 기준). 스크립트는 파일 경로를 받지 않는다.
        public List<string> images = new List<string>();
    }
}

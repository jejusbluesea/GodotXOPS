using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 스크립트 이벤트 묶음 하나의 등록 파일 (JSON). 묶음 하나가 SafeGDScript(.sgd) 하나이고, 이벤트마다 그 안의 함수 하나를 맡는다.
    /// 설치형 묶음은 godotdata/event/ 의 JSON 이고 미션 전용 묶음은 MIF2 의 addonEventDataPath 가 가리킨다.
    /// </summary>
    public class EventPackData
    {
        // 스크립트 파일 경로 (exe 폴더 기준, 확장자 .sgd).
        public string scriptPath = string.Empty;
        public List<EventDefinitionData> events = new List<EventDefinitionData>();
    }
}

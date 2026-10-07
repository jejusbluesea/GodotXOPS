using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 스크립트 이벤트 하나의 등록 정보. 게임은 이것으로 함수를 찾고 파라미터를 채우며, 맵 에디터는 이것으로 입력 칸을 그린다.
    /// </summary>
    public class EventDefinitionData
    {
        // 포인트 종류 번호. 설치형은 20 에서 9999 사이, 미션 전용은 10000 이상.
        public int type;
        // 에디터에 보이는 이름.
        public string name = string.Empty;
        // 스크립트 안의 함수 이름. 줄이 이 포인트에 있는 동안 틱마다 함수(p, state)로 불린다.
        public string function = string.Empty;
        public List<EventSlotData> parameters = new List<EventSlotData>();
        // 출구. 함수가 돌려준 번호의 출구가 가리키는 칸의 값이 다음 포인트의 식별번호다. 비워 두면 출구 하나(p3)다.
        public List<EventSlotData> exits = new List<EventSlotData>();
    }
}

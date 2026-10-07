namespace GodotXOPS
{
    /// <summary>
    /// 파라미터나 출구 하나가 포인트의 어느 칸에 있는지.
    /// </summary>
    public class EventSlotData
    {
        // 스크립트가 받는 사전의 키 (파라미터), 에디터에 보이는 이름 (출구).
        public string name = string.Empty;
        // 칸: "p2"(원본 P2), "p3"(원본 P3), "e0" 부터는 PD2 의 추가 파라미터.
        public string slot = string.Empty;
        // 값의 형식: "int", "float", "bool". 그 밖의 값("human", "message" 등)은 에디터용 표시이고 정수로 읽는다. p2 와 p3 은 늘 정수다.
        public string kind = "int";
    }
}

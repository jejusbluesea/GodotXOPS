namespace GodotXOPS
{
    /// <summary>
    /// 이벤트 한 줄의 진행 상태. 줄마다 따로 진행되고 미션을 시작할 때 새로 만든다.
    /// </summary>
    public sealed class EventLine
    {
        // 줄 번호 (0 부터).
        public readonly int Index;
        // 지금 처리할 포인트의 식별번호.
        public int Cursor;
        // 시간 대기 이벤트가 센 틱 수 (원본 waitcnt). 대기가 끝나면 0 으로 돌아간다.
        public int WaitCount;
        // 멈춘 줄은 진행하지 않는다. 스크립트가 멈추거나(stop_line) 스크립트 이벤트가 실패했을 때 켜진다.
        public bool Stopped;
        // 스크립트 이벤트가 줄마다 받는 저장 칸. 줄이 다른 포인트로 넘어가면 비운다. 스크립트 이벤트를 만나기 전에는 null.
        public Godot.Collections.Dictionary State;

        /// <summary>
        /// 줄을 만든다.
        /// </summary>
        /// <param name="index">줄 번호.</param>
        /// <param name="cursor">시작 포인트의 식별번호.</param>
        public EventLine(int index, int cursor)
        {
            Index = index;
            Cursor = cursor;
        }

        /// <summary>
        /// 다른 포인트로 넘어간다. 저장 칸을 비운다.
        /// </summary>
        /// <param name="cursor">다음 포인트의 식별번호.</param>
        public void MoveTo(int cursor)
        {
            Cursor = cursor;
            State?.Clear();
        }
    }
}

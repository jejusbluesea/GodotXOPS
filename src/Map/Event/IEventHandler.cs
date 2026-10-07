namespace GodotXOPS
{
    /// <summary>
    /// 이벤트 포인트 한 종류를 처리하는 것. EventManager 는 이 인터페이스만 알고, 내장 이벤트(10~19)와 스크립트 이벤트가 각각 구현한다.
    /// </summary>
    public interface IEventHandler
    {
        // 기다림. 줄은 이 포인트에 머물고 다음 틱에 다시 불린다.
        public const int Wait = -1;
        // 처리에 실패했다. 줄은 여기서 멈추고 다시 불리지 않는다.
        public const int Failed = -2;

        /// <summary>
        /// 줄이 이 포인트에 있는 동안 틱마다 불린다.
        /// </summary>
        /// <param name="events">이벤트 매니저 (미션 종료, 메시지).</param>
        /// <param name="line">이 포인트를 처리 중인 줄.</param>
        /// <param name="point">처리할 포인트.</param>
        /// <returns>나갈 출구의 번호 (0 이상). 기다리면 Wait, 실패했으면 Failed.</returns>
        int Tick(EventManager events, EventLine line, RawPointData point);

        /// <summary>
        /// 출구가 가리키는 다음 포인트의 식별번호를 구한다.
        /// </summary>
        /// <param name="point">지금 포인트.</param>
        /// <param name="exit">Tick 이 돌려준 출구 번호.</param>
        /// <param name="next">다음 포인트의 식별번호.</param>
        /// <returns>그런 출구가 있으면 true.</returns>
        bool TryGetNext(RawPointData point, int exit, out int next);
    }
}

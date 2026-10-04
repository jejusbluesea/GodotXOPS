namespace GodotXOPS
{
    /// <summary>
    /// PD1 이벤트 포인트의 종류 (param0, 원본 P1). 원본 OpenXOPS event.cpp:288-345.
    /// 10·11 은 미션을 끝내고, 14·18·19 는 바로 실행하고 다음으로 넘어가며, 12·13·15·16·17 은 조건이 될 때까지 기다린다.
    /// </summary>
    public enum EventType
    {
        MissionComplete = 10,
        MissionFailed = 11,
        // 대상(param1)이 죽을 때까지 기다린다.
        WaitDeath = 12,
        // 대상(param1)이 이 포인트 근처에 올 때까지 기다린다.
        WaitArrival = 13,
        // 경로 포인트(param1)의 이동 모드를 걷기로 바꾼다. 거기서 대기하던 사람이 다시 움직인다.
        ChangeToWalk = 14,
        // 소물(param1)이 부서질 때까지 기다린다.
        WaitBreakObject = 15,
        // 대상(param1)이 케이스를 든 채 이 포인트 근처에 올 때까지 기다린다.
        WaitCase = 16,
        // param1 초 동안 기다린다.
        WaitTime = 17,
        // 메시지(param1)를 표시한다.
        Message = 18,
        // 대상(param1)의 팀을 0 으로 바꾼다.
        ChangeTeam = 19,
    }

    /// <summary>
    /// 미션 결과.
    /// </summary>
    public enum MissionResult
    {
        InProgress = 0,
        Complete = 1,
        Failed = 2,
    }
}

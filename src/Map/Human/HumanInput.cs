using System;

namespace GodotXOPS
{
    /// <summary>
    /// 캐릭터 이동 입력 플래그. 원본 OpenXOPS MOVEFLAG_* 대응.
    /// </summary>
    [Flags]
    public enum HumanMoveFlag
    {
        None = 0,
        Forward = 1 << 0,
        Back = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        Walk = 1 << 4,
        Jump = 1 << 5,
    }

    /// <summary>
    /// 이번 틱 무기 액션 의도. 직접 호출 대신 플래그로 표현해 Human 이 한 곳에서 소비한다.
    /// 소비 순서는 원본 입력 처리 순서(슬롯선택 → 버림 → 무기ID전환 → 재장전 → 발사)를 따른다.
    /// </summary>
    [Flags]
    public enum HumanWeaponAction
    {
        None = 0,
        SelectFirst = 1 << 0,
        SelectSecond = 1 << 1,
        Drop = 1 << 2,
        SwitchPrevious = 1 << 3,
        SwitchNext = 1 << 4,
        Reload = 1 << 5,
        Fire = 1 << 6,
    }

    /// <summary>
    /// 한 틱 분량의 캐릭터 입력. PlayerController(사람)·AI 가 채우고, 이동/조준은 HumanController 가,
    /// 무기 액션은 Human 이 소비하는 단일 입력 표면. 조준은 절대각(yaw/pitch, 도), 이동은 방향 플래그.
    /// 각도 규약은 UnityXOPS 와 같다: yaw 는 오른쪽으로 돌수록 +, pitch 는 아래를 볼수록 +.
    /// </summary>
    public struct HumanInput
    {
        public HumanMoveFlag moveFlag;
        public float yaw;
        public float pitch;
        public HumanWeaponAction weapon;
    }

    /// <summary>
    /// 사망 상태머신. 원본 OpenXOPS human::deadstate (object.cpp:1208-1389) 정수값과 동일.
    /// 0 Alive 정상 / 1 Falling 쓰러지기 시작 / 2 HeadStuck 머리 박힘+자유낙하 /
    /// 3 LegSliding 다리 미끄러뜨리기 / 4 Settling 1프레임 정지 / 5 Done 완전 고정.
    /// </summary>
    public enum HumanDeadState
    {
        Alive = 0,
        Falling = 1,
        HeadStuck = 2,
        LegSliding = 3,
        Settling = 4,
        Done = 5,
    }
}

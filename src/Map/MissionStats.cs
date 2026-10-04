using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 플레이어 한 명의 미션 통계 — 플레이 시간, 발사, 명중, 킬, 헤드샷. 원본 OpenXOPS GameInfo (gamemain.h:102-112) 에 해당한다.
    /// 결과 화면이 읽는다. MapLoader 가 갖고 있어 씬이 바뀌어도 유지되고, 사람을 로드할 때 초기화된다.
    /// 플레이 시간은 원본처럼 틱 수로 센다.
    /// </summary>
    public class MissionStats : ISimTickable
    {
        // 미션 경과 틱 수 (원본 framecnt).
        public int PlayTicks;
        // 발사 횟수 — 총을 한 번 쏠 때마다 +1 (산탄의 탄환 수와 무관, 수류탄 제외). 원본 gamemain.cpp:2240.
        public int Fire;
        // 명중 가중 합. 단발은 1, 산탄은 탄환당 2 / 탄환 수 (전탄 명중 = 2). 원본 objectmanager.cpp:975.
        public float OnTarget;
        // 킬 수 — 이번 타격으로 HP 가 0 이하가 되면 +1 (총알, 수류탄 모두). 원본 objectmanager.cpp:978, 1115.
        public int Kill;
        // 헤드샷 수 — 머리 명중마다 +1 (킬과 무관, 수류탄 제외). 원본 objectmanager.cpp:976.
        public int Headshot;

        // 미션 경과 시간 (초).
        public float PlayTime => PlayTicks * SimClock.FrameTime;
        // 표시용 명중 수. 원본 gamemain.cpp:4777 (int)floor(ontarget).
        public int OnTargetInt => Mathf.FloorToInt(OnTarget);
        // 명중률(%). 산탄이 전부 맞으면 100% 를 넘을 수 있다 (원본과 같다). 원본 gamemain.cpp:4779-4781.
        public float AccuracyPercent => Fire > 0 ? (float)OnTargetInt / Fire * 100f : 0f;

        // 원본 미션 판정/이벤트 순서.
        public int SimOrder => 300;

        public void SimTick()
        {
            PlayTicks++;
        }

        /// <summary>
        /// 모든 값을 0 으로 되돌린다.
        /// </summary>
        public void Reset()
        {
            PlayTicks = 0;
            Fire = 0;
            OnTarget = 0f;
            Kill = 0;
            Headshot = 0;
        }
    }
}

using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// SimClock 이 33.333fps 로 호출하는 시뮬레이션 대상. SimOrder 로 한 틱 안의 실행 순서를 정한다(원본 프레임 루프 순서 재현).
    /// 원본 순서: 사람(무기 입력·이동·맵충돌, 10) → 떨어진 무기(30) → 총알(40) → 인간간충돌(100) → AI판단(200) → 미션판정/이벤트(300).
    /// </summary>
    public interface ISimTickable
    {
        // 틱 실행 순서 (오름차순, 낮을수록 먼저).
        int SimOrder { get; }

        /// <summary>
        /// 시뮬레이션 1틱 진행 (원본 33.333fps 1프레임 분량).
        /// </summary>
        void SimTick();
    }

    /// <summary>
    /// 게임플레이 33.333fps 단일 틱을 발행하는 시뮬레이션 시계. 원본 OpenXOPS 는 프레임당 1회 고정 케이던스로 전체 로직을 돈다.
    /// 등록된 ISimTickable 을 SimOrder 오름차순으로 한 틱에 1회씩 호출한다. 렌더레이트와 분리해 프레임레이트 독립성을 확보한다.
    /// 매 렌더 프레임에서 실시간을 누산해 FrameTime 마다 1틱을 발행하고, InterpolationAlpha 로 시각 보간에 쓸 진행 비율을 알려 준다.
    /// TickEnabled 가 true 인 동안(메인게임/데모)에만 틱을 발행한다.
    /// </summary>
    public partial class SimClock : Singleton<SimClock>
    {
        // 원본 GAMEFPS. 게임플레이 프레임 단위 환산의 단일 소스 — 하드코딩 33.333 대신 이 값을 참조한다.
        public const float FrameRate = 33.3333f;
        // 한 틱 시간(초) = 1/FrameRate.
        public const float FrameTime = 1f / FrameRate;

        // 프레임 폭주 방지 (긴 프레임에 누산이 몰려도 최대 4틱만 따라잡음).
        private const int k_maxCatchup = 4;

        // 입력 확정(InputManager) 다음, 다른 모든 노드보다 먼저 돈다.
        private const int k_processPriority = -200;

        private static readonly List<ISimTickable> s_tickables = new List<ISimTickable>();

        private float m_accum;

        // 틱 발행 여부. 브리핑/결과 같은 정지 화면에서는 false 로 둬 중력·AI 를 모두 멈춘다.
        public static bool TickEnabled { get; set; }

        // 게임 정지 중에도 도는 틱 대상의 첫 순서. 미션 판정·이벤트(300)부터는 멈추지 않는다 (이벤트가 멈춰 버리면 정지를 풀 수 없다).
        public const int PausedOrderFrom = 300;

        private static bool s_worldPaused;
        // 정지가 풀린 뒤 세계의 첫 틱이 돌 때까지 true. 그동안 세계의 시각 보간을 멈춘 자리에 붙여 둔다.
        private static bool s_worldHeld;
        private static float s_alpha = 1f;

        // 게임 정지 (이벤트 Pause World). 켜져 있으면 PausedOrderFrom 보다 앞선 틱 대상(블록, 사람, 무기, 소물, 총알, 충돌, AI)이 돌지 않는다.
        public static bool WorldPaused
        {
            get => s_worldPaused;
            set
            {
                s_worldPaused = value;
                if (value) s_worldHeld = true;
            }
        }

        // 다음 틱까지의 진행 비율 0~1. 렌더레이트 소비자가 직전 틱→현재 틱 사이를 이 값으로 보간한다.
        // 게임 정지 중에는 1 이다 (세계의 틱이 돌지 않으므로 그대로 두면 직전 틱과 지금 틱 사이를 되풀이해 오간다).
        public static float InterpolationAlpha => s_worldHeld ? 1f : s_alpha;
        // 게임 정지와 무관한 진행 비율. 정지 중에도 움직이는 것(이벤트의 카메라, 화면 암전, 레터박스)이 쓴다.
        public static float EventAlpha => s_alpha;

        /// <summary>
        /// 시뮬레이션 틱 대상을 등록한다. SimOrder 오름차순 정렬 삽입 — 낮은 값이 먼저 호출된다. 중복 등록은 무시.
        /// </summary>
        /// <param name="tickable">등록할 대상.</param>
        public static void Register(ISimTickable tickable)
        {
            if (tickable == null || s_tickables.Contains(tickable)) return;

            int i = 0;
            while (i < s_tickables.Count && s_tickables[i].SimOrder <= tickable.SimOrder) i++;
            s_tickables.Insert(i, tickable);
        }

        /// <summary>
        /// 시뮬레이션 틱 대상을 해제한다.
        /// </summary>
        /// <param name="tickable">해제할 대상.</param>
        public static void Unregister(ISimTickable tickable)
        {
            if (tickable == null) return;
            s_tickables.Remove(tickable);
        }

        public override void _Ready()
        {
            ProcessPriority = k_processPriority;
        }

        public override void _Process(double delta)
        {
            // 정지 중에는 누산조차 하지 않는다(재개 시 버스트 방지). alpha=1 — 시각이 직전 틱으로 되감기지 않고 현재 논리 위치에 멈추도록.
            if (!TickEnabled)
            {
                m_accum = 0f;
                s_alpha = 1f;
                return;
            }

            m_accum += (float)delta;
            int guard = 0;
            while (m_accum >= FrameTime && guard++ < k_maxCatchup)
            {
                m_accum -= FrameTime;
                Step();
            }
            if (m_accum > FrameTime) m_accum = 0f;
            s_alpha = m_accum / FrameTime;
        }

        /// <summary>
        /// 한 시뮬레이션 틱 — 등록된 모든 대상을 SimOrder 순으로 1회씩 진행한다. 점검 도구가 프레임과 무관하게 틱을 돌릴 때도 쓴다.
        /// </summary>
        public static void Step()
        {
            // 틱 도중 등록/해제(스폰·사망)가 일어나도 순회가 깨지지 않게 사본을 돈다.
            ISimTickable[] snapshot = s_tickables.ToArray();
            // 정지 여부는 틱의 처음에 정한다. 이번 틱의 이벤트가 정지를 켜거나 꺼도 다음 틱부터 듣는다.
            bool paused = s_worldPaused;
            if (!paused) s_worldHeld = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (paused && snapshot[i].SimOrder < PausedOrderFrom) continue;
                snapshot[i].SimTick();
            }
        }
    }
}

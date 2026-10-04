using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 모든 사람의 AI 를 틱마다 한 번씩 돌린다. 원본 OpenXOPS maingame::Process 의 AI 루프 (gamemain.cpp:2546-2555).
    /// 노드가 아닌 순수 클래스이고 MapLoader 가 사람을 로드할 때 SimClock 에 등록한다.
    /// 사람 이동(10)·총알(40)·인간간 충돌(100) 뒤에 돌며, AI 가 넣은 이동 입력은 다음 틱의 이동이 소비한다 (원본의 한 프레임 지연).
    /// 플레이어가 조작하는 사람은 건너뛴다. 조작 대상이 바뀌면(치트 F8) 그 틱부터 새 대상을 건너뛰고 옛 대상은 AI 가 이어받는다.
    /// </summary>
    public class AIController : ISimTickable
    {
        // AI 전체를 돌릴지 (원본 AIstop 의 반대). 점검 도구와 디버그 치트가 끈다.
        public static bool Enabled { get; set; } = true;
        // 플레이어가 조작하는 사람도 AI 가 움직일지 (원본 PlayerAI). 조작자가 없는 메뉴 데모 화면에서 켠다.
        public static bool DrivePlayer { get; set; }

        // 원본 AI 판단 — 인간간 충돌 뒤, 미션 판정·이벤트 앞.
        public int SimOrder => 200;

        public void SimTick()
        {
            IReadOnlyList<Human> humans = MapLoader.Humans;
            Human player = DrivePlayer ? null : MapLoader.Player;

            // 틱 도중 사람이 추가돼도(치트 F9) 이번 틱에는 처음 있던 사람까지만 돈다.
            int count = humans.Count;
            for (int i = 0; i < count; i++)
            {
                Human human = humans[i];

                // 소리 신호는 듣는 쪽이 쓰든 말든 매 틱 비운다. 원본의 소리 목록은 한 프레임만 유지된다.
                bool heard = human.ConsumeThreatHeard();
                if (!Enabled || human == player || human.Brain == null) continue;

                human.Brain.Tick(heard);
            }
        }

        /// <summary>
        /// 모든 AI 의 비전투 여부를 정한다 (디버그 치트). 원본 콘솔 명령 (gamemain.cpp:4517-4531).
        /// </summary>
        /// <param name="value">true 면 아무도 싸우지 않는다.</param>
        public static void SetNoFightAll(bool value)
        {
            foreach (Human human in MapLoader.Humans)
            {
                human.Brain?.SetNoFight(value);
            }
        }

        /// <summary>
        /// 모든 AI 를 강제로 경계시킨다 (디버그 치트). 원본 콘솔 명령 (gamemain.cpp:4534-4540).
        /// </summary>
        public static void SetCautionAll()
        {
            foreach (Human human in MapLoader.Humans)
            {
                human.Brain?.SetCautionMode();
            }
        }
    }
}

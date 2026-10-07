using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 미션 이벤트와 클리어·실패 판정. 원본 OpenXOPS EventControl (event.cpp) 과 maingame::Process 의 판정·이벤트 부분 (gamemain.cpp:2559-2616),
    /// ObjectManager::CheckGameOverorComplete (objectmanager.cpp:2561-2602) 에 해당한다.
    /// 이벤트는 여러 줄이 따로 진행된다 (PD1 은 세 줄, PD2 는 파일이 정한 만큼). 줄마다 지금 처리할 포인트의 식별번호를 들고, 포인트의 param2(원본 p3)가 가리키는 번호로 넘어간다.
    /// 포인트의 종류별 처리는 처리기(IEventHandler)가 한다. 이 클래스는 줄을 진행시키고 처리기가 돌려준 출구로 넘어가기만 한다.
    /// UI(GDScript)가 쓰는 창구이기도 하다. 메시지와 미션 종료를 시그널로 알리고, 현재 값은 프로퍼티로 읽는다.
    /// BeginMission 을 부른 뒤에만 돌고, 맵을 내리면 멈춘다.
    /// </summary>
    public partial class EventManager : Singleton<EventManager>, ISimTickable
    {
        // 메시지가 새로 표시될 때.
        [Signal]
        public delegate void MessageShownEventHandler(int id, string text);

        // 미션이 끝났을 때. complete 가 true 면 클리어, false 면 실패.
        [Signal]
        public delegate void MissionEndedEventHandler(bool complete);

        // 원본 형식(PD1)에서 한 틱에 한 줄이 처리하는 최대 포인트 수 (원본 TOTAL_EVENTFRAMESTEP). 확장 형식(PD2)에는 이 제한이 없다.
        private const int k_legacyMaxFrameSteps = 6;
        // 메시지를 표시하는 시간과 나타나고 사라지는 시간 (초). 원본 TOTAL_EVENTENT_SHOWMESSEC 5.0, gamemain.cpp:3143-3144 의 0.2.
        private const float k_messageSeconds = 5.0f;
        private const float k_messageFadeSeconds = 0.2f;
        // 원본 형식(PD1)의 최대 메시지 수 (원본 MAX_POINTMESSAGES). 확장 형식(PD2)에는 이 제한이 없다.
        private const int k_legacyMaxMessages = 16;

        // 이벤트 줄들. 줄 수는 미션을 시작할 때 포인트 데이터에서 받는다.
        private EventLine[] m_lines = System.Array.Empty<EventLine>();
        // 포인트 종류 번호 → 처리기.
        private readonly Dictionary<int, IEventHandler> m_handlers = new Dictionary<int, IEventHandler>();
        // 한 줄이 이번 틱에 이미 처리한 포인트의 식별번호. 확장 형식에서 바로 넘어가는 이벤트끼리 고리를 이뤘을 때 틱이 끝나지 않는 것을 막는다.
        private readonly HashSet<int> m_visited = new HashSet<int>();

        private bool m_running;
        private MissionResult m_result = MissionResult.InProgress;
        private int m_endTicks;
        private int m_messageId = -1;
        private int m_messageCnt;
        private int m_startCount;
        private bool m_linesPaused;

        // 이벤트와 판정이 돌고 있는지.
        public bool Running => m_running;
        // 미션 결과 (MissionResult 값: 0 진행 중, 1 클리어, 2 실패).
        public int Result => (int)m_result;
        // 미션이 끝난 뒤 지난 틱 수 (원본 end_framecnt). 끝나는 연출의 시간 기준으로 쓴다.
        public int EndTicks => m_endTicks;
        // 표시 중인 메시지 번호. 없으면 −1.
        public int MessageId => m_messageId;
        public string MessageText => m_messageId >= 0 ? MapLoader.GetMessageText(m_messageId) : string.Empty;
        // 이벤트 줄들을 멈출지 (디버그 콘솔의 estop, 원본 gamemain.cpp:4598-4608). 자동 판정과 메시지 시간은 계속 돈다. 미션을 시작하면 풀린다.
        public bool LinesPaused
        {
            get => m_linesPaused;
            set => m_linesPaused = value;
        }
        // BeginMission 이 불린 횟수. 값이 바뀌면 미션이 처음부터 다시 시작된 것이다.
        public int StartCount => m_startCount;

        // 표시 중인 메시지의 투명도 0~1. 처음과 끝의 0.2초 동안 선형으로 나타나고 사라진다 (원본 gamemain.cpp:3139-3146).
        public float MessageAlpha
        {
            get
            {
                if (m_messageId < 0) return 0f;

                float fade = k_messageFadeSeconds * SimClock.FrameRate;
                float total = k_messageSeconds * SimClock.FrameRate;
                if (m_messageCnt < fade) return Mathf.Clamp(m_messageCnt / fade, 0f, 1f);
                if (m_messageCnt > total - fade) return Mathf.Clamp((total - m_messageCnt) / fade, 0f, 1f);
                return 1f;
            }
        }

        // 원본 미션 판정·이벤트 순서 — AI 판단 뒤.
        public int SimOrder => 300;

        public override void _Ready()
        {
            var builtin = new BuiltinEventHandler();
            for (int type = MapLoader.PointEventFirst; type <= MapLoader.PointEventLast; type++)
            {
                m_handlers[type] = builtin;
            }
            SimClock.Register(this);
        }

        public override void _ExitTree()
        {
            SimClock.Unregister(this);
            base._ExitTree();
        }

        /// <summary>
        /// 미션을 처음부터 시작한다. 이벤트 줄들을 포인트 데이터의 시작 번호(MapLoader.EventEntryIds)로 되돌리고 결과와 메시지를 비운다. 맵과 사람을 로드한 뒤 부른다.
        /// </summary>
        public void BeginMission()
        {
            IReadOnlyList<int> entryIds = MapLoader.EventEntryIds;
            m_lines = new EventLine[entryIds.Count];
            for (int i = 0; i < m_lines.Length; i++)
            {
                m_lines[i] = new EventLine(i, entryIds[i]);
            }
            ResetHud();
            m_variables.Clear();
            m_autoJudge = true;
            m_missionTicks = 0;
            m_result = MissionResult.InProgress;
            m_endTicks = 0;
            m_messageId = -1;
            m_messageCnt = 0;
            m_linesPaused = false;
            m_running = true;
            m_startCount++;
        }

        /// <summary>
        /// 이벤트와 판정을 멈춘다. 결과와 메시지는 그대로 남는다.
        /// </summary>
        public void StopMission()
        {
            m_running = false;
            ResetHud();
        }

        /// <summary>
        /// 한 틱 진행. 원본 순서와 같다: 자동 판정 → 이벤트 줄들 → 메시지 시간.
        /// 미션이 끝난 뒤에는 판정과 이벤트를 멈추고 메시지 시간과 종료 후 틱만 센다.
        /// </summary>
        public void SimTick()
        {
            if (!m_running) return;

            LatchInteract();
            if (m_result == MissionResult.InProgress)
            {
                m_missionTicks++;
                MissionResult judged = m_autoJudge ? CheckGameOverOrComplete() : MissionResult.InProgress;
                if (judged != MissionResult.InProgress) EndMission(judged);
            }
            else
            {
                m_endTicks++;
            }

            foreach (EventLine line in m_lines)
            {
                if (m_result != MissionResult.InProgress || m_linesPaused) break;
                ProcessLine(line);
            }

            TickHud();

            if (m_messageId != -1 && m_messageCnt < (int)(k_messageSeconds * SimClock.FrameRate))
            {
                m_messageCnt++;
            }
            else
            {
                m_messageId = -1;
                m_messageCnt = 0;
            }
        }

        /// <summary>
        /// 이벤트 한 줄을 진행한다. 원본 EventControl::ProcessEventPoint (event.cpp:260-353).
        /// 기다리는 포인트를 만나거나, 다음 포인트가 없거나, 한 틱의 처리 한도에 닿으면 멈춘다.
        /// 한도는 원본 형식(PD1)에서만 6개다 (원본과 같은 틱에 같은 이벤트가 일어나게 한다).
        /// 확장 형식(PD2)은 기다리는 포인트를 만날 때까지 한 틱에 다 처리하고, 이번 틱에 이미 지난 포인트로 돌아오면 다음 틱으로 넘긴다.
        /// 처리기가 실패했거나 없는 출구를 돌려주면 그 줄만 멈춘다.
        /// </summary>
        /// <param name="line">진행할 줄.</param>
        private void ProcessLine(EventLine line)
        {
            if (line.Stopped) return;

            bool extended = MapLoader.PointDataExtended;
            m_visited.Clear();

            for (int step = 0; extended || step < k_legacyMaxFrameSteps; step++)
            {
                RawPointData point = MapLoader.GetEventPoint(line.Cursor);
                if (point == null) return;
                if (extended && !m_visited.Add(line.Cursor)) return;
                if (!m_handlers.TryGetValue(point.param0, out IEventHandler handler)) return;

                int exit = handler.Tick(this, line, point);
                // 처리기가 미션을 끝냈거나 이 줄을 멈췄거나 다른 포인트로 보냈으면(start_line) 여기서 끝낸다.
                if (exit == IEventHandler.Wait || m_result != MissionResult.InProgress || line.Stopped || line.Cursor != point.param3) return;
                if (exit == IEventHandler.Failed || !handler.TryGetNext(point, exit, out int next))
                {
                    line.Stopped = true;
                    return;
                }

                line.MoveTo(next);
            }
        }

        /// <summary>
        /// 메시지를 표시한다 (원본 event.cpp:330-337). 범위 밖 번호면 표시 중인 메시지는 그대로 두고 표시 시간만 처음부터 다시 센다.
        /// 원본 형식(PD1)에서는 16개까지만 받는다.
        /// </summary>
        /// <param name="id">메시지 번호.</param>
        public void ShowMessage(int id)
        {
            if (id >= 0 && (MapLoader.PointDataExtended || id < k_legacyMaxMessages)) m_messageId = id;
            m_messageCnt = 0;
            if (m_messageId >= 0) EmitSignal(SignalName.MessageShown, m_messageId, MessageText);
        }

        /// <summary>
        /// 진행 중인 미션을 강제로 끝낸다 (디버그 콘솔의 comp / fail, 원본 gamemain.cpp:4583-4596).
        /// </summary>
        /// <param name="complete">true 면 클리어, false 면 실패.</param>
        /// <returns>끝냈으면 true. 미션이 돌고 있지 않거나 이미 끝났으면 false.</returns>
        public bool ForceEnd(bool complete)
        {
            if (!m_running || m_result != MissionResult.InProgress) return false;

            EndMission(complete ? MissionResult.Complete : MissionResult.Failed);
            return true;
        }

        /// <summary>
        /// 이벤트 한 줄이 지금 기다리는 포인트의 식별번호 (디버그 콘솔의 event).
        /// </summary>
        /// <param name="line">줄 번호 (0 부터).</param>
        /// <returns>식별번호. 줄 번호가 범위 밖이면 −1.</returns>
        public int LineCursor(int line)
        {
            return line >= 0 && line < m_lines.Length ? m_lines[line].Cursor : -1;
        }

        // 이벤트 줄 수.
        public int LineCount => m_lines.Length;

        /// <summary>
        /// 미션을 끝낸다.
        /// </summary>
        /// <param name="result">결과.</param>
        internal void EndMission(MissionResult result)
        {
            m_result = result;
            m_endTicks = 1;
            // 미션이 끝나면 이벤트 줄이 더 돌지 않아 스크립트가 글자를 지울 기회가 없다.
            ResetHud();
            EmitSignal(SignalName.MissionEnded, result == MissionResult.Complete);
        }

        /// <summary>
        /// 이벤트와 무관한 자동 판정. 원본 ObjectManager::CheckGameOverorComplete (objectmanager.cpp:2561-2602).
        /// 플레이어와 다른 팀의 HP 합이 0 이면 클리어, 플레이어가 죽었으면 실패다. 둘 다 성립하면 클리어가 이긴다.
        /// </summary>
        /// <returns>판정 결과.</returns>
        private static MissionResult CheckGameOverOrComplete()
        {
            Human player = MapLoader.Player;
            if (player == null) return MissionResult.InProgress;

            float enemyHp = 0f;
            foreach (Human human in MapLoader.Humans)
            {
                if (human.Team != player.Team && human.HP > 0f) enemyHp += human.HP;
            }
            if (enemyHp <= 0f) return MissionResult.Complete;

            return player.Alive ? MissionResult.InProgress : MissionResult.Failed;
        }
    }
}

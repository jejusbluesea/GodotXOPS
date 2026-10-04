using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// AI 의 이동 모드. 원본 OpenXOPS AIcontrol 의 AI_WALK ~ AI_NULL (ai.h:201-209).
    /// </summary>
    public enum AIMoveMode
    {
        Walk,
        Run,
        Wait,
        Stop5Sec,
        Tracking,
        Grenade,
        // 우선적 달리기 — 경계를 건너뛰고, 전투 중에도 경로를 따라 움직인다.
        Run2,
        // 랜덤 분기 포인트 위에 있다. 다음 틱에 둘 중 하나로 넘어간다.
        Random,
        // 경로 없음.
        Null,
    }

    /// <summary>
    /// 사람 한 명의 경로 추종 상태. 원본 OpenXOPS AIMoveNavi (ai.cpp:1987-2248).
    /// 지금 서 있는 포인트 하나를 들고, 그 포인트의 param2(원본 p3)가 가리키는 식별번호로 다음 포인트를 찾는다.
    /// 이동 모드와 목표 위치는 Refresh 를 부를 때만 갱신된다. 원본처럼 평상시에는 매 틱, 경계·전투 중에는 갱신하지 않는다.
    /// </summary>
    public class AIMoveNavi
    {
        // 경로 포인트의 param1(원본 p2) 값 중 "경계 대기". 이동 모드는 Wait 와 같고, 경계가 끝날 때 다음 포인트로 넘어간다.
        public const int PathParamWaitAlert = 4;

        // 지금 서 있는 포인트. 처음에는 사람 자신의 배치 포인트다 (원본 path_pointid).
        private RawPointData m_current;
        private AIMoveMode m_mode = AIMoveMode.Null;
        private bool m_hold;
        private Human m_target;
        private Vector3 m_targetPosition;
        private float m_targetLook;

        public AIMoveMode Mode => m_mode;
        public bool Run2 => m_mode == AIMoveMode.Run2;
        // 추적 대상. 한 번 잡으면 죽어도 바뀌지 않는다.
        public Human TargetHuman => m_target;
        // 목표 지점. 높이는 쓰지 않는다 (수류탄 투척만 포인트 높이를 직접 읽는다).
        public Vector3 TargetPosition => m_targetPosition;
        // 목표 지점에서 선호하는 방향 yaw (도).
        public float TargetLook => m_targetLook;
        public RawPointData CurrentPoint => m_current;

        /// <summary>
        /// 사람의 배치 포인트에서 출발해 첫 경로 포인트로 넘어간다. 원본 AIMoveNavi::Init.
        /// </summary>
        /// <param name="humanPoint">사람의 배치 포인트. param2 가 첫 경로의 식별번호다.</param>
        public void Init(RawPointData humanPoint)
        {
            m_mode = AIMoveMode.Null;
            m_hold = false;
            m_target = null;
            m_current = humanPoint;
            Next();
        }

        /// <summary>
        /// 지금 포인트에서 이동 모드와 목표 위치를 다시 읽는다. 원본 AIMoveNavi::MovePathNowState (ai.cpp:2031-2105).
        /// 추적 모드는 대상의 현재 위치를 목표로 삼으므로 매 틱 불러야 한다.
        /// 이벤트가 포인트의 param1 을 바꾸면(걷기로 변경) 다음 호출 때 반영된다.
        /// </summary>
        public void Refresh()
        {
            if (m_hold)
            {
                if (m_mode == AIMoveMode.Tracking && GodotObject.IsInstanceValid(m_target))
                {
                    m_targetPosition = m_target.Controller.Position;
                    m_targetLook = m_target.Controller.Yaw;
                }
                return;
            }

            if (m_current == null)
            {
                m_mode = AIMoveMode.Null;
                return;
            }

            if (m_current.param0 == MapLoader.PointRandomAIPath)
            {
                m_mode = AIMoveMode.Random;
                return;
            }

            // 아직 사람의 배치 포인트에 있으면 (첫 경로를 찾지 못했으면) 경로가 없는 것이다.
            if (m_current.param0 != MapLoader.PointAIPath)
            {
                m_mode = AIMoveMode.Null;
                return;
            }

            // 0~7 밖의 값이면 이전 모드를 그대로 둔다 (원본 switch 의 default).
            switch (m_current.param1)
            {
                case 0: m_mode = AIMoveMode.Walk; break;
                case 1: m_mode = AIMoveMode.Run; break;
                case 2: m_mode = AIMoveMode.Wait; break;
                case 3: m_mode = AIMoveMode.Tracking; break;
                case PathParamWaitAlert: m_mode = AIMoveMode.Wait; break;
                case 5: m_mode = AIMoveMode.Stop5Sec; break;
                case 6: m_mode = AIMoveMode.Grenade; break;
                case 7: m_mode = AIMoveMode.Run2; break;
            }

            if (m_mode == AIMoveMode.Tracking)
            {
                // 추적 대상은 포인트의 param2 가 가리키는 식별번호의 사람이다. 찾지 못하면 목표 위치는 이전 값으로 남는다.
                if (!GodotObject.IsInstanceValid(m_target)) m_target = MapLoader.SearchHuman(m_current.param2);
                if (!GodotObject.IsInstanceValid(m_target)) return;

                m_targetPosition = m_target.Controller.Position;
                m_targetLook = m_target.Controller.Yaw;
            }
            else
            {
                m_targetPosition = m_current.position;
                m_targetLook = m_current.look;
            }
        }

        /// <summary>
        /// 다음 포인트로 넘어간다. 원본 AIMoveNavi::MovePathNextState (ai.cpp:2109-2139).
        /// 랜덤 분기 포인트에서는 param1 과 param2 중 하나를 반반 확률로 고른다. 다음 포인트가 없으면 지금 포인트에 머문다.
        /// </summary>
        /// <returns>다음 포인트로 넘어갔으면 true.</returns>
        public bool Next()
        {
            if (m_current == null)
            {
                m_mode = AIMoveMode.Null;
                return false;
            }

            int nextId = m_current.param2;
            if (m_current.param0 == MapLoader.PointRandomAIPath)
            {
                nextId = GameRandom.Gameplay.Range(0, 2) == 0 ? m_current.param1 : m_current.param2;
                m_mode = AIMoveMode.Random;
            }

            RawPointData next = MapLoader.GetPathPoint(nextId);
            if (next == null) return false;

            m_current = next;
            return true;
        }

        /// <summary>
        /// 경로와 무관하게 지정한 자리에서 대기하게 한다 (치트 F9). 원본 AIMoveNavi::SetHoldWait.
        /// </summary>
        /// <param name="position">대기할 위치.</param>
        /// <param name="yawDeg">선호하는 방향 yaw (도).</param>
        public void SetHoldWait(Vector3 position, float yawDeg)
        {
            m_mode = AIMoveMode.Wait;
            m_hold = true;
            m_targetPosition = position;
            m_targetLook = yawDeg;
        }

        /// <summary>
        /// 경로와 무관하게 지정한 사람을 따라다니게 한다 (치트 F9). 원본 AIMoveNavi::SetHoldTracking.
        /// </summary>
        /// <param name="target">따라갈 사람.</param>
        public void SetHoldTracking(Human target)
        {
            if (target == null) return;

            m_mode = AIMoveMode.Tracking;
            m_hold = true;
            m_target = target;
            m_targetPosition = target.Controller.Position;
            m_targetLook = target.Controller.Yaw;
        }
    }
}

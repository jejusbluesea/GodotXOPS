using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    // Human 의 구역 담당 partial: 발 위치로 들어 있는 구역을 매 틱 구하고, 구역의 데미지·중력·AI 시야·적대를 이 사람에게 건다.
    // 구역이 없는 맵에서는 아무것도 하지 않고, 값은 전부 원본과 같은 동작이다 (중력 배율 1, 시야 배율 1, 늘 적대).
    public partial class Human
    {
        private readonly List<int> m_areaTags = new List<int>();
        private int m_topArea = -1;
        // 구역 데미지의 소수점 아래 (정수로 쌓일 때만 HP 를 뺀다).
        private float m_areaDamageCarry;
        // 이 사람이 지금 구역에서 적대하게 된 팀 ("공격받으면 적대" 규칙). 구역이 바뀌면 잊는다.
        private readonly HashSet<int> m_provokedTeams = new HashSet<int>();

        // 들어 있는 구역 태그 (오름차순). 마지막 틱 기준.
        public IReadOnlyList<int> AreaTags => m_areaTags;
        // 들어 있는 구역 가운데 번호가 가장 큰 태그. 없으면 -1.
        public int TopArea => m_topArea;
        // 구역이 거는 중력의 배율. 구역 밖이면 1.
        public float AreaGravityScale => MapLoader.GetAreaState(m_topArea)?.gravityScale ?? 1f;
        // 구역이 거는 AI 시야 거리의 배율. 구역 밖이면 1.
        public float AreaSightScale => MapLoader.GetAreaState(m_topArea)?.aiSightScale ?? 1f;

        /// <summary>
        /// 한 틱의 구역 처리: 발 위치로 구역을 다시 구하고, 구역의 데미지를 준다. MapLoader 의 AreaTicker 가 사람 틱보다 먼저 부른다.
        /// 데미지는 들어 있는 구역들의 초당 데미지를 더해 틱마다 쌓고, 정수가 될 때만 뺀다 (무적은 막고 방어구는 거치지 않는다).
        /// </summary>
        public void TickArea()
        {
            int previousTop = m_topArea;
            MapLoader.AreaTagsAt(Controller.Position, m_areaTags);
            m_topArea = m_areaTags.Count > 0 ? m_areaTags[m_areaTags.Count - 1] : -1;
            if (m_topArea != previousTop) m_provokedTeams.Clear();

            float damagePerSecond = 0f;
            foreach (int tag in m_areaTags) damagePerSecond += MapLoader.GetAreaState(tag).damagePerSecond;
            if (!Alive || damagePerSecond <= 0f)
            {
                m_areaDamageCarry = 0f;
                return;
            }

            m_areaDamageCarry += damagePerSecond / SimClock.FrameRate;
            float whole = Mathf.Floor(m_areaDamageCarry);
            if (whole < 1f) return;

            m_areaDamageCarry -= whole;
            ApplyDamage(whole);
        }

        /// <summary>
        /// 그 팀에게 공격받았음을 기억한다 ("공격받으면 적대" 규칙). 구역 안에 있을 때만 기억하고 구역이 바뀌면 잊는다.
        /// </summary>
        /// <param name="team">공격한 사람의 팀.</param>
        public void MarkProvoked(int team)
        {
            if (m_topArea >= 0) m_provokedTeams.Add(team);
        }

        /// <summary>
        /// 이 사람(AI)이 상대를 적으로 보는지. 같은 팀은 적이 아니고, 구역 밖이나 규칙이 없는 팀끼리는 늘 적이다 (원본).
        /// 구역의 규칙은 이 사람이 들어 있는 구역(번호가 가장 큰 태그)의 것이다. AI 의 판단에만 쓰고 데미지와는 무관하다.
        /// </summary>
        /// <param name="target">상대.</param>
        /// <returns>적이면 true.</returns>
        public bool IsHostileTo(Human target)
        {
            if (target == null || target.Team == Team) return false;

            AreaState area = MapLoader.GetAreaState(m_topArea);
            if (area == null || !area.TryGetHostility(Team, target.Team, out AreaHostility mode)) return true;

            switch (mode)
            {
                case AreaHostility.Never:
                    return false;
                case AreaHostility.Provoked:
                    // 무기를 든 상대를 보면 그 팀을 적으로 기억한다.
                    if (!target.CurrentWeapon.IsNone) m_provokedTeams.Add(target.Team);
                    return m_provokedTeams.Contains(target.Team);
                default:
                    return true;
            }
        }
    }
}

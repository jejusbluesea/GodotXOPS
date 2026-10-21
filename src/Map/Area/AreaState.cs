using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 구역 태그 하나의 미션 중의 성질. 맵을 로드할 때 맵 데이터 파일의 처음 값으로 만들고, 이벤트가 바꾼다.
    /// </summary>
    public class AreaState
    {
        public string name = string.Empty;
        public float damagePerSecond;
        public float gravityScale = 1f;
        public float aiSightScale = 1f;
        public float soundReverb;
        public bool blockWeather;
        public int weatherIndex = -1;
        public readonly List<AreaHostilityRule> hostility = new List<AreaHostilityRule>();

        /// <summary>
        /// 태그 데이터의 처음 값으로 만든다 (적대 규칙은 복사한다).
        /// </summary>
        /// <param name="data">태그 데이터. null 이면 기본값.</param>
        /// <returns>새 상태.</returns>
        public static AreaState From(AreaTagData data)
        {
            var state = new AreaState();
            if (data == null) return state;

            state.name = data.name ?? string.Empty;
            state.damagePerSecond = data.damagePerSecond;
            state.gravityScale = data.gravityScale;
            state.aiSightScale = data.aiSightScale;
            state.soundReverb = data.soundReverb;
            state.blockWeather = data.blockWeather;
            state.weatherIndex = data.weatherIndex;
            foreach (AreaHostilityRule rule in data.hostility)
            {
                if (rule != null) state.hostility.Add(new AreaHostilityRule { team = rule.team, targetTeam = rule.targetTeam, mode = rule.mode });
            }
            return state;
        }

        /// <summary>
        /// team 의 AI 가 targetTeam 을 어떻게 대하는지. 같은 짝의 규칙이 여럿이면 뒤의 것이다.
        /// </summary>
        /// <param name="team">보는 팀.</param>
        /// <param name="targetTeam">상대 팀.</param>
        /// <param name="mode">규칙의 방식.</param>
        /// <returns>규칙이 있으면 true.</returns>
        public bool TryGetHostility(int team, int targetTeam, out AreaHostility mode)
        {
            mode = AreaHostility.Always;
            bool found = false;
            foreach (AreaHostilityRule rule in hostility)
            {
                if (rule.team != team || rule.targetTeam != targetTeam) continue;
                mode = rule.mode;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 적대 규칙 하나를 정한다. 같은 짝의 규칙이 있으면 바꾸고 없으면 더한다.
        /// </summary>
        /// <param name="team">보는 팀.</param>
        /// <param name="targetTeam">상대 팀.</param>
        /// <param name="mode">방식.</param>
        public void SetHostility(int team, int targetTeam, AreaHostility mode)
        {
            hostility.RemoveAll(rule => rule.team == team && rule.targetTeam == targetTeam);
            hostility.Add(new AreaHostilityRule { team = team, targetTeam = targetTeam, mode = mode });
        }
    }
}

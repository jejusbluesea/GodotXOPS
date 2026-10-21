using System.Collections.Generic;

namespace GodotXOPS
{
    // EventApi 의 구역 담당 partial: 사람이 어느 구역에 있는지 묻고, 구역의 성질과 적대 규칙을 미션 중에 바꾼다.
    // 바꾼 성질은 맵을 다시 로드하면(미션 재시작) 맵 데이터 파일의 처음 값으로 돌아간다.
    public sealed partial class EventApi
    {
        // set_area 가 받는 성질의 이름.
        private const string k_areaDamage = "damage";
        private const string k_areaGravity = "gravity";
        private const string k_areaAiSight = "ai_sight";
        private const string k_areaReverb = "reverb";
        private const string k_areaBlockWeather = "block_weather";
        private const string k_areaWeather = "weather";

        /// <summary>
        /// 사람이 그 구역 태그 안에 있는지 (발 위치, 마지막 틱 기준. 겹친 구역의 어느 것이든).
        /// </summary>
        /// <param name="index">사람의 인덱스.</param>
        /// <param name="tag">구역 태그 번호.</param>
        /// <returns>안이면 true. 없는 사람이면 false.</returns>
        private static bool InArea(int index, int tag)
        {
            Human human = MapLoader.GetHuman(index);
            if (human == null) return false;

            IReadOnlyList<int> tags = human.AreaTags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == tag) return true;
            }
            return false;
        }

        /// <summary>
        /// 그 구역 태그 안의 살아 있는 사람 수.
        /// </summary>
        /// <param name="tag">구역 태그 번호.</param>
        /// <param name="team">팀. 음수면 모든 팀.</param>
        /// <returns>사람 수.</returns>
        private static int AreaCount(int tag, int team)
        {
            int count = 0;
            for (int i = 0; i < MapLoader.HumanCount; i++)
            {
                Human human = MapLoader.GetHuman(i);
                if (human == null || !human.Alive || (team >= 0 && human.Team != team)) continue;
                if (InArea(i, tag)) count++;
            }
            return count;
        }

        /// <summary>
        /// 구역 태그의 지금 성질.
        /// </summary>
        /// <param name="tag">구역 태그 번호.</param>
        /// <returns>사전: exists, name, damage, gravity, ai_sight, reverb, block_weather, weather.</returns>
        private static Godot.Collections.Dictionary AreaInfo(int tag)
        {
            AreaState state = MapLoader.GetAreaState(tag);
            if (state == null) return new Godot.Collections.Dictionary { ["exists"] = false };

            return new Godot.Collections.Dictionary
            {
                ["exists"] = true,
                ["name"] = state.name,
                [k_areaDamage] = state.damagePerSecond,
                [k_areaGravity] = state.gravityScale,
                [k_areaAiSight] = state.aiSightScale,
                [k_areaReverb] = state.soundReverb,
                [k_areaBlockWeather] = state.blockWeather,
                [k_areaWeather] = state.weatherIndex,
            };
        }

        /// <summary>
        /// 구역 태그의 성질 하나를 바꾼다. 값은 확인한다: 데미지와 시야는 0 이상, 울림은 0 에서 1 사이, 날씨 막기는 0 이 아니면 참, 날씨는 음수면 -1(바깥 날씨).
        /// </summary>
        /// <param name="tag">구역 태그 번호.</param>
        /// <param name="key">성질의 이름 (damage, gravity, ai_sight, reverb, block_weather, weather).</param>
        /// <param name="value">값.</param>
        /// <returns>바꿨으면 true. 없는 태그, 모르는 이름, 수가 아닌 값이면 false.</returns>
        private static bool SetArea(int tag, string key, float value)
        {
            AreaState state = MapLoader.GetAreaState(tag);
            if (state == null || !float.IsFinite(value)) return false;

            switch (key)
            {
                case k_areaDamage:
                    state.damagePerSecond = System.Math.Max(0f, value);
                    return true;
                case k_areaGravity:
                    state.gravityScale = value;
                    return true;
                case k_areaAiSight:
                    state.aiSightScale = System.Math.Max(0f, value);
                    return true;
                case k_areaReverb:
                    state.soundReverb = System.Math.Clamp(value, 0f, 1f);
                    return true;
                case k_areaBlockWeather:
                    state.blockWeather = value != 0f;
                    return true;
                case k_areaWeather:
                    state.weatherIndex = value < 0f ? -1 : (int)value;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 구역 태그의 적대 규칙 하나를 정한다 (같은 짝이 있으면 바꾼다).
        /// </summary>
        /// <param name="tag">구역 태그 번호.</param>
        /// <param name="team">보는 팀.</param>
        /// <param name="targetTeam">상대 팀.</param>
        /// <param name="mode">0 늘 적대, 1 적대 안 함, 2 무기를 든 상대를 보거나 공격받으면 적대.</param>
        /// <returns>정했으면 true. 없는 태그나 모르는 방식이면 false.</returns>
        private static bool SetAreaHostility(int tag, int team, int targetTeam, int mode)
        {
            AreaState state = MapLoader.GetAreaState(tag);
            if (state == null || mode < (int)AreaHostility.Always || mode > (int)AreaHostility.Provoked) return false;

            state.SetHostility(team, targetTeam, (AreaHostility)mode);
            return true;
        }
    }
}

using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 소리가 난 것을 AI 에게 알리는 창구. 원본 OpenXOPS SoundManager::GetWorldSound 의 AI 인지 부분에 해당한다.
    /// 소리가 나면 듣는 범위 안의 플레이어가 아닌 사람에게 위협 신호(Human.NotifyThreatHeard)를 남기고, AI 가 틱마다 그것을 소비해 경계로 바뀐다.
    /// 실제 소리 재생은 SoundManager 가 따로 한다. 듣는 거리는 HumanAIParameterData 의 aiHear* 값이다.
    /// </summary>
    public static class WorldSound
    {
        /// <summary>
        /// 한 지점에서 난 소리를 듣는 범위 안의 사람들에게 알린다. 듣는 위치는 각자의 눈높이다.
        /// </summary>
        /// <param name="position">소리가 난 위치.</param>
        /// <param name="sourceTeam">소리를 낸 쪽의 팀.</param>
        /// <param name="enemyDistance">다른 팀이 듣는 거리.</param>
        /// <param name="allyDistance">같은 팀이 듣는 거리. 0 이하이면 같은 팀은 듣지 못한다.</param>
        public static void EmitPointSound(Vector3 position, int sourceTeam, float enemyDistance, float allyDistance)
        {
            if (!SimClock.TickEnabled) return;

            IReadOnlyList<Human> humans = MapLoader.Humans;
            Human player = MapLoader.Player;

            for (int i = 0; i < humans.Count; i++)
            {
                Human human = humans[i];
                if (human == player || !human.Alive) continue;

                float maxDistance = human.Team == sourceTeam ? allyDistance : enemyDistance;
                if (maxDistance <= 0f) continue;

                Vector3 head = human.Controller.Position + Vector3.Up * human.CameraHeight;
                if ((head - position).LengthSquared() < maxDistance * maxDistance) human.NotifyThreatHeard();
            }
        }
    }
}

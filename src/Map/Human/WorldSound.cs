using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 발소리 종류. 원본 SoundManager::SetFootsteps 의 MoveMode (걷기 0, 전진 1, 후진 2, 좌우 3, 점프 4, 착지 5).
    /// </summary>
    public enum FootstepKind
    {
        Walk,
        Forward,
        Back,
        Side,
        Jump,
        Landing,
    }

    /// <summary>
    /// 소리가 난 것을 AI 에게 알리는 창구. 원본 OpenXOPS SoundManager::GetWorldSound 의 AI 인지 부분에 해당한다.
    /// 소리가 나면 듣는 범위 안의 플레이어가 아닌 사람에게 위협 신호(Human.NotifyThreatHeard)를 남기고, AI 가 틱마다 그것을 소비해 경계로 바뀐다.
    /// 실제 소리 재생은 SoundManager 가 따로 한다. 듣는 거리는 HumanAIParameterData 의 aiHear* 값이다.
    /// </summary>
    public static class WorldSound
    {
        /// <summary>
        /// 발소리를 낸다. 원본 SoundManager::SetFootsteps (soundmanager.cpp:246-268) 에 해당하며, 움직이는 사람마다 매 틱 불린다.
        /// 원본은 발소리 WAV 를 재생하지 않는다 (PlaySound 의 FOOTSTEPS_* 분기가 비어 있다). 발소리를 넣으려면 이 함수에서 SoundManager 를 부르면 된다.
        /// 그때 필요한 재료는 사람(위치, 발밑 블록)과 종류로 충분하다.
        /// AI 는 다른 팀이 달리는 소리만 듣는다. 걷기·점프·착지는 듣지 못한다 (soundmanager.cpp:348-365).
        /// </summary>
        /// <param name="source">발소리를 낸 사람.</param>
        /// <param name="kind">발소리 종류.</param>
        public static void EmitFootstep(Human source, FootstepKind kind)
        {
            HumanAIParameterData ai = DataManager.Instance.HumanParameterData.humanAIParameterData;
            float distance;
            switch (kind)
            {
                case FootstepKind.Forward: distance = ai.aiHearFootstepForward; break;
                case FootstepKind.Back: distance = ai.aiHearFootstepBack; break;
                case FootstepKind.Side: distance = ai.aiHearFootstepSide; break;
                default: return;
            }

            EmitPointSound(source.Controller.Position, source.Team, distance, 0f);
        }

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

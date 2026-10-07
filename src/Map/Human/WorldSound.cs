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
        // 발밑의 면을 찾는 레이: 발 위 0.25 m 에서 아래로 0.5 m. 얕은 계단이나 경사에서도 밟은 면을 잡는다.
        private const float k_footRayHeight = 0.25f;
        private const float k_footRayLength = 0.5f;

        /// <summary>
        /// 발소리를 재생한다. 소리는 발밑 블록 면의 재질(BlockMaterialData)에서 가져온다. 재질 번호가 없는 맵(BD1)은 0번 재질이고, 기본 데이터의 0번에는 발소리가 없다.
        /// 걷기와 달리기는 발이 땅에 닿는 틱에, 착지는 착지한 틱에 호출된다. 원본에는 없는 기능이고 판정에 영향이 없다.
        /// </summary>
        /// <param name="source">발소리를 낸 사람.</param>
        /// <param name="kind">발소리 종류. 점프는 소리가 없다.</param>
        public static void PlayFootstep(Human source, FootstepKind kind)
        {
            if (!SoundManager.Loaded || kind == FootstepKind.Jump) return;

            Vector3 position = source.Controller.Position;
            // 들리지 않는 거리의 발소리는 재생기를 잡기 전에 버린다 (재생기가 64개뿐이다).
            if (!SoundManager.Instance.IsAudible(position)) return;

            Vector3 origin = position + Vector3.Up * k_footRayHeight;
            if (!MapLoader.RaycastBlock(BlockLayer.Human, origin, Vector3.Down, k_footRayLength, out _, out Block block, out int face)) return;

            BlockMaterialData material = MapLoader.GetFaceMaterial(block, face);

            BlockMaterialGeneralData general = DataManager.Instance.BlockMaterialParameterData.blockMaterialGeneralData;
            switch (kind)
            {
                case FootstepKind.Walk:
                    SoundManager.Instance.PlayRandomAt(material.footstepWalk, position, general.footstepWalkVolume);
                    break;
                case FootstepKind.Landing:
                    SoundManager.Instance.PlayRandomAt(material.footstepLanding, position, general.footstepLandingVolume);
                    break;
                default:
                    SoundManager.Instance.PlayRandomAt(material.footstepRun, position, general.footstepRunVolume);
                    break;
            }
        }

        /// <summary>
        /// 발소리를 낸다. 원본 SoundManager::SetFootsteps (soundmanager.cpp:246-268) 에 해당하며, 움직이는 사람마다 매 틱 불린다.
        /// 원본은 발소리 WAV 를 재생하지 않는다 (PlaySound 의 FOOTSTEPS_* 분기가 비어 있다). 여기서는 AI 가 듣는 신호만 내고, 들리는 소리는 PlayFootstep 이 따로 낸다.
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

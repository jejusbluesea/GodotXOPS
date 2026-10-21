using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS
{
    // MapLoader 의 구역 담당 partial: BD2 의 구역 블록과 맵 데이터 파일의 구역 태그를 들고, 점이 어느 구역에 있는지 알려 준다.
    // 구역은 블록 데이터와 함께 붙고 떨어지며 (BD2 만. BD1 에는 없다), 미션을 다시 시작하면 태그의 성질이 처음 값으로 돌아간다.
    // 판정은 사람의 발 위치로 하고, 구역의 효과(데미지, 중력, AI 의 시야, 적대)는 틱에서 정한다 (AreaTicker, SimOrder 7).
    public partial class MapLoader
    {
        private readonly List<AreaVolume> m_areas = new List<AreaVolume>();
        private readonly List<AreaState> m_areaStates = new List<AreaState>();
        private readonly AreaTicker m_areaTicker = new AreaTicker();

        // 구역 블록 수와 구역 태그 수.
        public static int AreaVolumeCount => Instance.m_areas.Count;
        public static int AreaTagCount => Instance.m_areaStates.Count;

        /// <summary>
        /// 구역의 효과를 사람에게 매 틱 거는 것. 사람(10)보다 먼저 돌아 이번 틱의 이동이 구역의 중력을 쓴다.
        /// </summary>
        private sealed class AreaTicker : ISimTickable
        {
            public int SimOrder => 7;

            public void SimTick()
            {
                foreach (Human human in Humans)
                {
                    human.TickArea();
                }
            }
        }

        /// <summary>
        /// BD2 의 구역 블록과 맵 데이터 파일의 구역 태그로 구역을 만든다. 그 전의 구역은 지운다. 블록 데이터를 로드할 때 부른다.
        /// 태그 목록에 없는 번호를 가리키는 구역 블록은 경고하고 뺀다.
        /// </summary>
        /// <param name="file">BD2 의 내용.</param>
        /// <param name="data">맵 데이터.</param>
        private static void LoadAreas(BD2File file, BlockMapData data)
        {
            MapLoader loader = Instance;
            loader.ClearAreas();

            foreach (AreaTagData tag in data.areaTagData) loader.m_areaStates.Add(AreaState.From(tag));

            int skipped = 0;
            foreach (BD2Area area in file.areas)
            {
                if (area.tag < 0 || area.tag >= loader.m_areaStates.Count)
                {
                    skipped++;
                    continue;
                }

                var volume = new AreaVolume { vertices = area.vertices, tag = area.tag };
                ComputeFacePlanes(area.vertices, volume.faceNormals, volume.faceCenters);
                volume.boundsMin = area.vertices[0];
                volume.boundsMax = area.vertices[0];
                foreach (Vector3 vertex in area.vertices)
                {
                    volume.boundsMin = volume.boundsMin.Min(vertex);
                    volume.boundsMax = volume.boundsMax.Max(vertex);
                }
                loader.m_areas.Add(volume);
            }
            if (skipped > 0) Debugger.LogWarning($"{skipped} area block(s) point to an area tag that the map data does not have.", nameof(MapLoader));

            if (loader.m_areas.Count > 0) SimClock.Register(loader.m_areaTicker);
        }

        /// <summary>
        /// 구역을 전부 지운다 (블록 데이터를 내릴 때).
        /// </summary>
        private void ClearAreas()
        {
            m_areas.Clear();
            m_areaStates.Clear();
            SimClock.Unregister(m_areaTicker);
        }

        /// <summary>
        /// 점이 들어 있는 구역 태그들을 오름차순으로 모은다.
        /// </summary>
        /// <param name="point">월드 좌표.</param>
        /// <param name="tags">태그 번호를 받을 목록. 먼저 비운다.</param>
        public static void AreaTagsAt(Vector3 point, List<int> tags)
        {
            tags.Clear();
            foreach (AreaVolume area in Instance.m_areas)
            {
                if (!tags.Contains(area.tag) && area.Contains(point)) tags.Add(area.tag);
            }
            tags.Sort();
        }

        /// <summary>
        /// 점이 들어 있는 구역 가운데 번호가 가장 큰 태그. 구역이 겹치면 이 태그의 성질을 쓴다 (데미지만 더한다).
        /// </summary>
        /// <param name="point">월드 좌표.</param>
        /// <returns>태그 번호. 어느 구역에도 없으면 -1.</returns>
        public static int TopAreaAt(Vector3 point)
        {
            int top = -1;
            foreach (AreaVolume area in Instance.m_areas)
            {
                if (area.tag > top && area.Contains(point)) top = area.tag;
            }
            return top;
        }

        /// <summary>
        /// 구역 태그의 지금 성질.
        /// </summary>
        /// <param name="tag">태그 번호.</param>
        /// <returns>성질. 없는 번호면 null.</returns>
        public static AreaState GetAreaState(int tag)
        {
            List<AreaState> states = Instance.m_areaStates;
            return tag >= 0 && tag < states.Count ? states[tag] : null;
        }

        /// <summary>
        /// 이름으로 구역 태그를 찾는다 (대소문자 무시).
        /// </summary>
        /// <param name="name">태그 이름.</param>
        /// <returns>태그 번호. 없으면 -1.</returns>
        public static int FindAreaTag(string name)
        {
            List<AreaState> states = Instance.m_areaStates;
            for (int i = 0; i < states.Count; i++)
            {
                if (string.Equals(states[i].name, name, System.StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 그 자리에서 날씨(비, 눈)를 막는지. 겹치면 번호가 가장 큰 태그의 값이다. 날씨의 연출이 카메라 자리로 묻는다.
        /// </summary>
        /// <param name="point">월드 좌표.</param>
        /// <returns>막으면 true.</returns>
        public static bool WeatherBlockedAt(Vector3 point)
        {
            if (!Loaded) return false;

            int tag = TopAreaAt(point);
            return tag >= 0 && Instance.m_areaStates[tag].blockWeather;
        }

        /// <summary>
        /// 그 자리에서 소리에 거는 울림. 겹치면 번호가 가장 큰 태그의 값이다. 소리가 듣는 자리(카메라)로 묻는다.
        /// </summary>
        /// <param name="point">월드 좌표.</param>
        /// <returns>0 에서 1 사이. 구역 밖이면 0.</returns>
        public static float ReverbAt(Vector3 point)
        {
            if (!Loaded) return 0f;

            int tag = TopAreaAt(point);
            return tag >= 0 ? Mathf.Clamp(Instance.m_areaStates[tag].soundReverb, 0f, 1f) : 0f;
        }

        /// <summary>
        /// 사람이 다른 사람에게 공격받았음을 알린다. 구역의 "공격받으면 적대" 규칙이 쓴다.
        /// </summary>
        /// <param name="victim">맞은 사람.</param>
        /// <param name="attacker">공격한 사람. null 이면 무시.</param>
        public static void NotifyAttacked(Human victim, Human attacker)
        {
            if (victim == null || attacker == null || victim == attacker) return;
            victim.MarkProvoked(attacker.Team);
        }
    }
}

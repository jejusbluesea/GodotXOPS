using System.Collections.Generic;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 번호로 가리키는 데이터 목록 (무기, 사람, 오브젝트, 이펙트 등). 기본 데이터 뒤에 맵팩과 미션이 들고 온 에드온 데이터를 붙일 수 있다.
    /// 번호 10000 미만은 기본 데이터(이 목록 자체), 10000 에서 19999 는 맵팩 데이터의 (번호 − 10000) 번째, 20000 이상은 미션 데이터의 (번호 − 20000) 번째다.
    /// 그래서 기본 목록이 늘어도 에드온의 번호가 밀리지 않고, 없는 번호를 엉뚱한 항목으로 읽지 않는다.
    /// JSON 으로는 보통의 배열로 읽고 쓴다 (에드온 쪽은 포함되지 않는다).
    /// 번호로 꺼낼 때는 반드시 이 형식 그대로 써야 한다. List 로 받아서 인덱싱하면 에드온 번호를 처리하지 못한다.
    /// </summary>
    /// <typeparam name="T">항목의 형식.</typeparam>
    public class DataList<T> : List<T>
    {
        // 맵팩 데이터가 시작하는 번호. 기본 목록은 이 개수 미만이어야 한다.
        public const int PackBase = 10000;
        // 미션(MIF2) 데이터가 시작하는 번호. 맵팩 데이터는 이 번호까지만 가리킬 수 있다.
        public const int MissionBase = 20000;

        private List<T> m_pack;
        private List<T> m_mission;

        // 맵팩 항목 수. 없으면 0.
        public int PackCount => m_pack?.Count ?? 0;
        // 미션 항목 수. 없으면 0.
        public int MissionCount => m_mission?.Count ?? 0;

        /// <summary>
        /// 번호가 에드온(맵팩이나 미션)의 번호인지 알려 준다.
        /// </summary>
        /// <param name="index">번호.</param>
        /// <returns>10000 이상이면 true.</returns>
        public static bool IsAddon(int index)
        {
            return index >= PackBase;
        }

        /// <summary>
        /// 번호로 항목을 꺼내거나 바꾼다. 10000 이상은 에드온 항목이다. 없는 번호면 예외가 나므로 먼저 Has 로 확인하거나 TryGet 을 쓴다.
        /// </summary>
        /// <param name="index">번호.</param>
        public new T this[int index]
        {
            get
            {
                if (index >= MissionBase) return m_mission[index - MissionBase];
                if (index >= PackBase) return m_pack[index - PackBase];
                return base[index];
            }
            set
            {
                if (index >= MissionBase) m_mission[index - MissionBase] = value;
                else if (index >= PackBase) m_pack[index - PackBase] = value;
                else base[index] = value;
            }
        }

        /// <summary>
        /// 그 번호의 항목이 있는지 알려 준다.
        /// </summary>
        /// <param name="index">번호.</param>
        /// <returns>기본·맵팩·미션 데이터에 있으면 true. 음수와 각 층의 항목 수를 넘는 번호는 false.</returns>
        public bool Has(int index)
        {
            if (index < 0) return false;
            if (index < PackBase) return index < Count;
            if (index < MissionBase) return index - PackBase < PackCount;
            return index - MissionBase < MissionCount;
        }

        /// <summary>
        /// 번호로 항목을 꺼낸다.
        /// </summary>
        /// <param name="index">번호.</param>
        /// <param name="value">꺼낸 항목. 없으면 기본값.</param>
        /// <returns>있었으면 true.</returns>
        public bool TryGet(int index, out T value)
        {
            if (Has(index))
            {
                value = this[index];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// 번호로 항목을 꺼내되, 없는 번호면 기본 데이터의 범위로 잘라서 꺼낸다 (AI 레벨, 몸 크기처럼 범위를 벗어난 값을 가까운 끝으로 보는 목록용).
        /// </summary>
        /// <param name="index">번호.</param>
        /// <returns>항목. 기본 데이터가 비어 있고 에드온에도 없으면 기본값.</returns>
        public T GetClamped(int index)
        {
            if (Has(index)) return this[index];
            return Count > 0 ? base[Mathf.Clamp(index, 0, Count - 1)] : default;
        }

        /// <summary>
        /// 한 번호의 다음(또는 이전) 번호를 구한다. 기본 데이터 → 맵팩 → 미션 → 다시 기본 데이터의 순서로 돈다.
        /// </summary>
        /// <param name="index">지금 번호.</param>
        /// <param name="direction">+1 이면 다음, −1 이면 이전.</param>
        /// <returns>이웃한 번호. 항목이 하나도 없으면 받은 번호 그대로.</returns>
        public int Neighbor(int index, int direction)
        {
            int total = Count + PackCount + MissionCount;
            if (total == 0) return index;

            int position;
            if (index >= MissionBase) position = Count + PackCount + (index - MissionBase);
            else if (index >= PackBase) position = Count + (index - PackBase);
            else position = index;

            position = ((position + direction) % total + total) % total;
            if (position < Count) return position;
            if (position < Count + PackCount) return PackBase + (position - Count);
            return MissionBase + (position - Count - PackCount);
        }

        /// <summary>
        /// 맵팩 데이터를 붙인다. 전에 붙인 것은 대체된다. 20000 번부터는 미션의 번호이므로 그 뒤의 항목은 가리킬 수 없다.
        /// </summary>
        /// <param name="pack">맵팩 항목들. null 이거나 비어 있으면 맵팩 데이터가 없는 상태가 된다.</param>
        public void SetPack(List<T> pack)
        {
            m_pack = pack != null && pack.Count > 0 ? new List<T>(pack) : null;
        }

        /// <summary>
        /// 맵팩 데이터를 뗀다.
        /// </summary>
        public void ClearPack()
        {
            m_pack = null;
        }

        /// <summary>
        /// 미션 데이터를 붙인다. 전에 붙인 것은 대체된다.
        /// </summary>
        /// <param name="mission">미션 항목들. null 이거나 비어 있으면 미션 데이터가 없는 상태가 된다.</param>
        public void SetMission(List<T> mission)
        {
            m_mission = mission != null && mission.Count > 0 ? new List<T>(mission) : null;
        }

        /// <summary>
        /// 미션 데이터를 뗀다.
        /// </summary>
        public void ClearMission()
        {
            m_mission = null;
        }
    }
}

namespace GodotXOPS
{
    /// <summary>
    /// 시드 기반 결정론적 의사난수 생성기. 같은 시드는 언제나 같은 수열을 만든다(플랫폼·프레임레이트 무관).
    /// 전역 난수와 달리 인스턴스별로 독립 스트림이라 서로 다른 용도의 난수가 소비 순서를 오염시키지 않는다.
    /// 시드를 SplitMix 로 확산해 내부 상태를 채운 뒤 xorshift128(Marsaglia)로 스텝한다.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint m_x;
        private uint m_y;
        private uint m_z;
        private uint m_w;

        /// <summary>주어진 시드로 초기화한다.</summary>
        /// <param name="seed">시드값. 같은 값이면 같은 수열.</param>
        public DeterministicRandom(uint seed) => SetSeed(seed);

        /// <summary>시드를 다시 설정해 수열을 처음으로 되돌린다.</summary>
        /// <param name="seed">시드값. 같은 값이면 같은 수열.</param>
        public void SetSeed(uint seed)
        {
            // SplitMix64 로 4개 상태워드를 서로 다른 값으로 채운다 — 인접 시드가 비슷한 초기 수열을 내지 않도록 확산.
            ulong s = seed;
            m_x = SplitMix(ref s);
            m_y = SplitMix(ref s);
            m_z = SplitMix(ref s);
            m_w = SplitMix(ref s);
            if ((m_x | m_y | m_z | m_w) == 0u) m_x = 0x9E3779B9u; // 전 상태 0 은 xorshift 고정점 → 회피
        }

        /// <summary>[0,1) 실수 난수(24비트 정밀).</summary>
        /// <returns>0 이상 1 미만.</returns>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>
        /// [minInclusive, maxExclusive) 정수 난수. 상한 제외.
        /// max ≤ min 이면 min 을 반환한다.
        /// </summary>
        /// <param name="minInclusive">하한(포함).</param>
        /// <param name="maxExclusive">상한(제외).</param>
        /// <returns>[min, max) 범위 정수.</returns>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span); // 작은 span 에선 modulo 편향 무시할 수준
        }

        /// <summary>
        /// [min, max) 실수 난수.
        /// 연속 분포라 정확히 상한에 닿을 확률은 사실상 0 이다.
        /// </summary>
        /// <param name="min">하한.</param>
        /// <param name="max">상한.</param>
        /// <returns>[min, max) 범위 실수.</returns>
        public float Range(float min, float max) => min + NextFloat() * (max - min);

        // xorshift128 — 다음 32비트 상태를 만들고 반환한다.
        private uint NextUInt()
        {
            uint t = m_x ^ (m_x << 11);
            m_x = m_y;
            m_y = m_z;
            m_z = m_w;
            m_w = m_w ^ (m_w >> 19) ^ (t ^ (t >> 8));
            return m_w;
        }

        // SplitMix64 한 스텝 — 시드 확산용. state 를 전진시키고 32비트로 접어 반환한다.
        private static uint SplitMix(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15ul;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ul;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBul;
            return (uint)(z ^ (z >> 31));
        }
    }
}

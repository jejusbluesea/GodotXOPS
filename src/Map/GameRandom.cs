namespace GodotXOPS
{
    /// <summary>
    /// 게임플레이용/연출용 난수 스트림을 분리 보관하는 정적 진입점.
    /// 시드 없는 전역 난수 하나를 모두가 공유하면, 렌더레이트로 뽑는 연출 난수(파편·이펙트)가
    /// 프레임레이트마다 다른 개수만큼 소비돼 그 뒤에 게임플레이(산탄·AI)가 뽑는 값까지 바꿔버린다(연출이 결과를 오염).
    /// 두 스트림을 독립시켜 연출 난수 소비가 게임플레이 수열을 밀지 않게 한다 — 프레임레이트 독립성의 핵심.
    ///  · Gameplay: 산탄·조준오차·반동·AI판단·낙하데미지·랜덤 무기 스폰 등 게임 결과에 영향. 원칙상 33.333Hz 틱에서만 뽑는다.
    ///  · Visual  : 파편·이펙트 변형·사운드 선택 등 순수 연출. 렌더레이트에서 뽑아도 무해.
    /// 미션 시작마다 엔트로피로 재시드해 시도마다 달라진다.
    /// 시드를 고정하면(<see cref="Reseed(uint)"/>) 리플레이 디버깅·멀티 결정론에 쓸 수 있다(멀티는 서버가 시드를 배포).
    /// </summary>
    public static class GameRandom
    {
        private const uint k_visualSalt = 0x9E3779B9u; // 두 스트림이 같은 마스터 시드에서도 다른 수열을 갖게 하는 분리용 상수

        public static DeterministicRandom Gameplay { get; } = new DeterministicRandom(0u);
        public static DeterministicRandom Visual { get; } = new DeterministicRandom(0u);

        // 마지막으로 두 스트림을 초기화한 마스터 시드. 엔트로피 시작한 판도 이 값을 로그/리플레이 헤더에 기록하면 재현할 수 있다.
        public static uint LastSeed { get; private set; }

        // 부팅 시점 1회 — 데모 씬처럼 BeginMission 전에도 세션마다 달라지도록 엔트로피 시드로 시작한다.
        static GameRandom() => Reseed(BootSeed());

        /// <summary>
        /// 두 스트림을 주어진 마스터 시드로 재설정한다. 같은 시드 → 두 스트림 모두 동일 수열(리플레이/멀티 결정론).
        /// </summary>
        /// <param name="masterSeed">마스터 시드. Gameplay 는 이 값, Visual 은 salt 를 xor 한 값으로 독립 파생.</param>
        public static void Reseed(uint masterSeed)
        {
            LastSeed = masterSeed;
            Gameplay.SetSeed(masterSeed);
            Visual.SetSeed(masterSeed ^ k_visualSalt);
        }

        /// <summary>
        /// 엔트로피로 두 스트림을 재설정한다 — 미션 시작 시 호출해 시도마다 난수 흐름이 달라지게 한다.
        /// </summary>
        public static void ReseedEntropy() => Reseed(BootSeed());

        // 시계 기반 엔트로피 시드. 결정론이 필요하면 Reseed(고정값)으로 덮어쓴다.
        private static uint BootSeed() =>
            unchecked((uint)System.Environment.TickCount ^ (uint)System.DateTime.UtcNow.Ticks);
    }
}

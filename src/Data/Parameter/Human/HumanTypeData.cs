namespace GodotXOPS
{
    public class HumanTypeData
    {
        // 인간 타입 (0: Human, 1: Robot, 2: Zombie)

        public int controllerSizeIndex; // ControllerSizeData 인덱스 — 이 타입의 체형(height/radius)
        public int hitboxSizeIndex; // HumanHitboxSizeData 인덱스 — 이 타입의 히트박스 형상

        public float progressRunAcceleration;
        public float sidewaysRunAcceleration;
        public float regressRunAcceleration;
        public float progressWalkAcceleration;
        public float attenuation;
        public float jumpSpeed;

        public float headDamageMultiplier;
        public float bodyDamageMultiplier;
        public float legDamageMultiplier;
        public int maxFallDamage;
        public IntRange headRandomAddDamage;
        public IntRange bodyRandomAddDamage;
        public IntRange legRandomAddDamage;

        public int bloodEffectIndex;
        public float bloodEffectThreshold;
        public int hitEffectIndex;
        public int deathEffectIndex;
        public bool bloodAttachesToMap;

        public bool canPickupWeapon;

        public bool zombie;
        public IntRange zombieMeleeDamageRange;
        // 무장 좀비 근접 리치 상한(m, JSON 값은 이미 스케일 적용됨). 실효 리치 = min(이 값, 총알 lifetime×bulletSpeed). 맨손 좀비엔 미적용.
        public float zombieMaxMeleeRange;
        // 좀비 근접 공격음. 원본은 일반 피탄음(HIT_HUMAN_ZOMBIE = human被弾음)과 동일 사운드 재사용 (soundmanager.cpp:608).
        public string zombieAttackSound;

        public int autoBulletMultiplier;

        // 체력 재생 (원본에 없다). 켜져 있으면 HP 가 마지막으로 준 뒤 regenerationDelay 초가 지나고부터
        // 초당 regenerationRate 씩, 사람 데이터의 처음 HP × regenerationMaxRatio(0 에서 1)까지 채운다.
        public bool regeneration;
        public float regenerationDelay = 5f;
        public float regenerationRate = 10f;
        public float regenerationMaxRatio = 1f;
    }
}

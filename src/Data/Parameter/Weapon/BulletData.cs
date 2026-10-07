using Godot;
using System.Collections.Generic;
using System;

namespace GodotXOPS
{
    /// <summary>
    /// 탄환의 폭발 트리거 조건. Flags 비트 조합으로 데이터에서 자유 설정.
    /// 0=None(폭발 안 함), 1=Lifetime(시한), 2=Block(맵 명중), 4=Human(사람 명중), 8=Object(소품 명중).
    /// 예: 수류탄=1(Lifetime), RPG=15(전부).
    /// </summary>
    [Flags]
    public enum ExplosionTrigger
    {
        None = 0,
        Lifetime = 1 << 0,
        Block = 1 << 1,
        Human = 1 << 2,
        Object = 1 << 3,
    }

    /// <summary>
    /// 탄환의 메시, 텍스처, 중력, 폭발, 음향 파라미터를 담는 컨테이너 클래스.
    /// </summary>
    public class BulletData
    {
        public string name;
        public string texturePath;
        public string modelPath;
        public Vector3 modelPosition;
        public Vector3 modelRotation;
        public Vector3 modelScale;
        // 총알 visual 을 머즐에서 이 거리(m)만큼 멀어질 때까지 숨긴다 — 3인칭에서 총알이 사수 머리를 관통하는 것처럼 보이는 현상 방지(원본엔 없는 연출). 0 이면 처음부터 표시.
        // 머리(스폰점)→머즐 오프셋보다 커야 게이트가 유효하다(작으면 스폰 즉시 보여 관통이 남음). 보통 총알 모델의 가장 긴 치수 + 여유.
        public float bulletBoundAdjust;
        public bool useGravity;
        public float gravityScale;
        public ExplosionTrigger explosionTrigger;
        public float armingDelay;
        public float explosionRadius;
        public float humanExplosiveHeadDamageMax;
        public float humanExplosiveLegDamageMax;
        public float objectExplosiveDamageMax;
        public float explosionknockbackMax;
        public string explosionSound;
        public int explosionEffectIndex;
        // 재질이 남기는 탄흔(BlockMaterialData.bulletHoleEffect)의 크기 배율. 블록에 맞았을 때의 이펙트와 소리는 탄환이 아니라 맞은 면의 재질이 정한다.
        public float bulletHoleSize = 1f;
        public int humanHitEffectIndex;
        public int objectHitEffectIndex;
        public List<string> bounceSounds; // 중력을 받는 탄(수류탄)이 블록에 튕길 때의 소리 — 리스트 랜덤 선택. 직선 탄은 쓰지 않는다.
        public List<string> humanHitSounds; // 사람 피격음 — 리스트에서 균등 랜덤 선택. 하나만 넣으면 그것만 재생.
        public List<string> bulletPassingSounds; // 총알이 카메라 근처 통과 시 hyu 음 — 리스트 랜덤 선택. 비어있으면(GRENADE 등) 재생 안 함.
        public float lifetime;
    }
}

using Godot;
using System;
using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 단일 이펙트 프리셋. 여러 emitter 의 컴포지트로 구성된다 (예: 폭발 = mflash 1 + smoke 4).
    /// 단순 효과(머즐 플래시 등)는 emitters 길이 1 로 표현.
    /// </summary>
    public class EffectData
    {
        public string name;
        public List<EffectEmitter> emitters = new List<EffectEmitter>();
    }

    /// <summary>
    /// 한 번의 원본 OpenXOPS AddEffect 호출에 대응하는 sub-emitter 정의.
    /// 트리거 시 호출자가 좌표만 넘기고, 그 외 시각/물리 파라미터는 모두 이쪽에서 결정.
    /// </summary>
    public class EffectEmitter
    {
        public int textureIndex; // EffectGeneralData.texturePaths 인덱스
        public EffectFlags flags; // 빌보드/맵 충돌 동작 플래그
        public int spawnCount; // 같은 emitter 를 N 번 발사 (랜덤 시드만 다름)
        // >0 이면 개수 = floor(트리거값 × 이 값) — spawnCount 무시. 원본 혈흔 분사 damage/10 대응(0.1).
        // 트리거값(데미지)이 0 이면 0개 → 폭발 혈흔(flowing=false)은 메인만, 분사 없음.
        public float countPerTrigger;

        public Vector3 positionOffset; // 트리거 좌표 기준
        public Vector3 positionRandomRange; // ±range each axis

        public Vector3 velocity;
        public Vector3 velocityRandomRange;
        public float gravityY; // m/s² (음수=낙하). 원본 addmove_y 대응

        public float rotationDeg;
        public float rotationRandomRange; // ±deg
        public float rotationRateDeg; // deg/sec
        public float rotationRateRandomRange; // ±deg/sec

        public float size;
        public float sizeRandomRange;
        public float sizeRate; // size/sec (수명 동안 선형 변화)

        public float alpha; // 0~1
        public float alphaRate; // /sec (페이드아웃은 음수)
        public float brightness; // 0~1 (원본 0~255 정규화)
        public float brightnessRate; // /sec

        public float lifetime; // sec
    }

    /// <summary>
    /// 이펙트 동작 플래그. 원본 OpenXOPS settype (object.h:429-431) 비트 조합과 동일.
    /// 원본은 단일 알파 블렌딩만 사용하므로 블렌드 모드는 없음.
    /// </summary>
    [Flags]
    public enum EffectFlags
    {
        None = 0,
        NoBillboard = 1 << 0, // 빌보드 X — 벽 부착 데칼용
        CollideMap = 1 << 1, // 맵 충돌 검사 — 충돌 시 NoBillboard 데칼이 자동 생성됨 (혈흔)
    }
}

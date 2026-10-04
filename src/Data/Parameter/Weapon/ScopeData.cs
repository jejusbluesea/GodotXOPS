using Godot;
using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 스코프의 시야 배율, 정확도, 반동 조정, 텍스처, 조준선 정보를 담는 컨테이너 클래스.
    /// </summary>
    public class ScopeData
    {
        public float fovDegrees;
        public FloatRange errorRangeAdjust;
        public FloatRange recoilAimVerticalAdjust;
        public FloatRange recoilAimHorizontalAdjust;
        public string texturePath;
        public float textureAspect;
        // true 면 스코프 사용 중 WeaponData.crosshair 가 true 여도 동적 크로스헤어를 숨긴다 (스코프 자체 레티클로 대체).
        // 원본 OpenXOPS ScopeGunsight != 0 (간이/정밀) 에 대응 — 크로스헤어는 ScopeGunsight==0(비스코프)에서만 표시 (gamemain.cpp:3175).
        public bool hideCrosshair;
        public List<ScopeLine> lines;
    }

    /// <summary>
    /// 스코프의 조준선을 정의하는 선분 정보를 담는 컨테이너 클래스.
    /// </summary>
    public class ScopeLine
    {
        public Vector2 start;
        public Vector2 end;
        public Color color;
        public float width;
    }
}

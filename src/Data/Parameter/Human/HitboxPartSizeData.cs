using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 히트박스 한 부위(캡슐)의 위치·회전·크기. rotationEuler 로 캡슐(로컬 Y축)을 눕혀
    /// 비휴머노이드 체형(개 몸통 등)도 표현한다.
    /// </summary>
    public class HitboxPartSizeData
    {
        public Vector3 position; // Human 로컬 기준 부위 중심 위치
        public Vector3 rotationEuler; // 부위 회전(오일러 deg)
        public float height; // 캡슐 높이
        public float radius; // 캡슐 반경
    }
}

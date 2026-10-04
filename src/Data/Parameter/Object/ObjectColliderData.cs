using Godot;
using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 오브젝트의 충돌 판정 형상 목록을 담는 컨테이너 클래스.
    /// </summary>
    public class ObjectColliderData
    {
        // 섹션 파일이 깨지거나 항목에 shapes 키가 없어도 소비자(ObjectCollider)가 null 역참조하지 않도록 미리 초기화한다.
        public List<ColliderShape> shapes = new List<ColliderShape>();
    }

    /// <summary>
    /// 개별 충돌 형상 (구/박스/캡슐) 정의.
    /// size 해석: Box = (x, y, z) 전체 크기 / Sphere = x (반지름) / Capsule = x (반지름), y (높이), z (방향 0=X 1=Y 2=Z)
    /// </summary>
    public class ColliderShape
    {
        public ColliderShapeType type;
        public Vector3 center;
        public Vector3 size;
    }

    /// <summary>
    /// 충돌 형상 타입 열거형.
    /// </summary>
    public enum ColliderShapeType
    {
        Sphere,
        Box,
        Capsule
    }
}

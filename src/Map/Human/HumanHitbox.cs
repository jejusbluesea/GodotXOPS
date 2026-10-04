using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 총알이 맞는 신체 부위. 원본 OpenXOPS HitBulletHuman 의 Hit_id (0 머리, 1 상반신, 2 다리).
    /// </summary>
    public enum HumanHitPart
    {
        Head = 0,
        Body = 1,
        Leg = 2,
    }

    /// <summary>
    /// 총알-사람 판정. 엔진 물리를 쓰지 않고 원본처럼 "점이 수직 원기둥 안에 있는가"를 직접 계산한다.
    /// 원본 objectmanager.cpp:755-800 은 총알 경로 위의 점마다 머리·상반신·다리 원기둥을 차례로 검사한다.
    /// 원기둥 크기는 HumanHitboxSizeData(부위 중심 위치, 회전, 높이, 반지름)에서 온다. 회전이 없는 부위는 원본과 같은 수직 원기둥이고,
    /// 회전(rotationEuler)을 준 부위는 사람의 몸 방향을 따라 도는 기울어진 원기둥이 된다 (네발 체형의 몸통 등).
    /// </summary>
    public static class HumanHitbox
    {
        /// <summary>
        /// 점이 부위 원기둥 안에 있는지 본다. 원본 CollideCylinderInside (collision.cpp:1246-1272):
        /// 수평으로는 원 안(경계 포함), 수직으로는 밑면과 윗면 사이(경계 포함)다.
        /// </summary>
        /// <param name="part">부위 크기 데이터. null 이면 맞지 않는다.</param>
        /// <param name="humanPosition">사람의 논리 위치 (발 기준).</param>
        /// <param name="humanYawDeg">사람의 몸 방향 yaw (도). 중심이 축에서 벗어났거나 회전한 부위에만 영향을 준다.</param>
        /// <param name="point">검사할 점.</param>
        /// <returns>안에 있으면 true.</returns>
        public static bool Contains(HitboxPartSizeData part, Vector3 humanPosition, float humanYawDeg, Vector3 point)
        {
            if (part == null) return false;

            Vector3 local;
            if (part.rotationEuler == Vector3.Zero && part.position.X == 0f && part.position.Z == 0f)
            {
                // 몸 중심축 위의 수직 원기둥은 몸 방향과 무관하다. 원본과 같은 계산이다.
                local = point - (humanPosition + Vector3.Up * part.position.Y);
            }
            else
            {
                // 점을 부위의 로컬 공간(원기둥 축 = Y)으로 옮긴다.
                Basis body = Basis.FromEuler(Coord.FromUnityEuler(new Vector3(0f, humanYawDeg, 0f)));
                Basis rotation = body * Basis.FromEuler(Coord.FromUnityEuler(part.rotationEuler));
                Vector3 center = humanPosition + body * Coord.FromUnity(part.position);
                local = rotation.Inverse() * (point - center);
            }

            if (local.X * local.X + local.Z * local.Z > part.radius * part.radius) return false;

            float bottom = -part.height * 0.5f;
            return bottom <= local.Y && local.Y <= bottom + part.height;
        }

        /// <summary>
        /// 부위에 해당하는 크기 데이터를 고른다.
        /// </summary>
        /// <param name="size">한 체형의 히트박스 데이터.</param>
        /// <param name="part">부위.</param>
        /// <returns>부위 크기 데이터. 없으면 null.</returns>
        public static HitboxPartSizeData PartOf(HumanHitboxSizeData size, HumanHitPart part)
        {
            if (size == null) return null;

            switch (part)
            {
                case HumanHitPart.Head: return size.head;
                case HumanHitPart.Body: return size.body;
                default: return size.leg;
            }
        }
    }
}

using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 좌표계 변환의 단일 창구. 변환은 반드시 여기를 거치고, 다른 곳에서 축 부호를 직접 뒤집지 않는다.
    /// OpenXOPS(DirectX, 왼손) → UnityXOPS(왼손) = (-x, y, -z) × 0.1 이고, Godot(오른손, 전방 -Z)은 거기서 Z만 뒤집는다.
    /// 결과적으로 Godot = OpenXOPS 의 (-x, y, z) × 0.1 이며, 화면에 보이는 모습은 원본과 같다(삼각형 와인딩 유지).
    /// </summary>
    public static class Coord
    {
        // OpenXOPS 길이 단위 → Godot 미터.
        public const float Scale = 0.1f;

        /// <summary>
        /// OpenXOPS 원본 좌표(맵 파일의 위치 값 등)를 Godot 좌표로 바꾼다.
        /// </summary>
        /// <param name="x">원본 X.</param>
        /// <param name="y">원본 Y.</param>
        /// <param name="z">원본 Z.</param>
        /// <returns>Godot 좌표.</returns>
        public static Vector3 FromXops(float x, float y, float z)
        {
            return new Vector3(-x * Scale, y * Scale, z * Scale);
        }

        /// <summary>
        /// OpenXOPS 원본 공간의 방향 벡터(광원 방향 등)를 Godot 공간으로 바꾼다. 축척은 적용하지 않는다.
        /// </summary>
        /// <param name="x">원본 X.</param>
        /// <param name="y">원본 Y.</param>
        /// <param name="z">원본 Z.</param>
        /// <returns>Godot 공간의 방향 벡터.</returns>
        public static Vector3 DirectionFromXops(float x, float y, float z)
        {
            return new Vector3(-x, y, z);
        }

        /// <summary>
        /// UnityXOPS 공간의 위치·방향·오프셋(godotdata JSON 값, 모델 정점)을 Godot 좌표로 바꾼다. 축척은 건드리지 않는다.
        /// </summary>
        /// <param name="unity">UnityXOPS 공간의 벡터.</param>
        /// <returns>Godot 공간의 벡터.</returns>
        public static Vector3 FromUnity(Vector3 unity)
        {
            return new Vector3(unity.X, unity.Y, -unity.Z);
        }

        /// <summary>
        /// UnityXOPS 오일러 각(도 단위)을 Godot 오일러 각(라디안, YXZ 순서)으로 바꾼다.
        /// Z를 뒤집는 거울 변환이라 X·Y축 회전은 부호가 반대가 되고 Z축 회전은 그대로다. 회전 합성 순서는 두 엔진이 같다.
        /// </summary>
        /// <param name="unityDegrees">UnityXOPS 오일러 각 (도).</param>
        /// <returns>Node3D.Rotation 에 그대로 넣을 수 있는 Godot 오일러 각 (라디안).</returns>
        public static Vector3 FromUnityEuler(Vector3 unityDegrees)
        {
            return new Vector3(
                -Mathf.DegToRad(unityDegrees.X),
                -Mathf.DegToRad(unityDegrees.Y),
                Mathf.DegToRad(unityDegrees.Z));
        }

        /// <summary>
        /// 캐릭터 yaw(UnityXOPS 규약: 도 단위, 0 = 원본 정면, 오른쪽으로 돌수록 +)가 가리키는 수평 정면 방향을 Godot 공간으로 구한다.
        /// </summary>
        /// <param name="yawDeg">yaw (도).</param>
        /// <returns>길이 1 의 수평 방향.</returns>
        public static Vector3 YawForward(float yawDeg)
        {
            float rad = Mathf.DegToRad(yawDeg);
            return new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
        }

        /// <summary>
        /// 캐릭터 yaw 기준 수평 오른쪽 방향을 Godot 공간으로 구한다.
        /// </summary>
        /// <param name="yawDeg">yaw (도).</param>
        /// <returns>길이 1 의 수평 방향.</returns>
        public static Vector3 YawRight(float yawDeg)
        {
            float rad = Mathf.DegToRad(yawDeg);
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }

        /// <summary>
        /// yaw 와 pitch(UnityXOPS 규약: 도 단위, 아래를 볼수록 +)가 가리키는 조준 방향을 Godot 공간으로 구한다.
        /// </summary>
        /// <param name="yawDeg">yaw (도).</param>
        /// <param name="pitchDeg">pitch (도).</param>
        /// <returns>길이 1 의 방향.</returns>
        public static Vector3 AimDirection(float yawDeg, float pitchDeg)
        {
            float pitch = Mathf.DegToRad(pitchDeg);
            return YawForward(yawDeg) * Mathf.Cos(pitch) + Vector3.Down * Mathf.Sin(pitch);
        }

        /// <summary>
        /// 두 각도(도)의 최단 차이 (to − from) 를 -180 ~ 180 범위로 구한다.
        /// </summary>
        /// <param name="fromDeg">시작 각도.</param>
        /// <param name="toDeg">끝 각도.</param>
        /// <returns>최단 회전량 (도).</returns>
        public static float DeltaAngle(float fromDeg, float toDeg)
        {
            float delta = Mathf.PosMod(toDeg - fromDeg, 360f);
            return delta > 180f ? delta - 360f : delta;
        }

        /// <summary>
        /// 각도(도)를 최단 경로로 보간한다.
        /// </summary>
        /// <param name="fromDeg">시작 각도.</param>
        /// <param name="toDeg">끝 각도.</param>
        /// <param name="t">보간 비율 0~1.</param>
        /// <returns>보간된 각도 (도).</returns>
        public static float LerpAngle(float fromDeg, float toDeg, float t)
        {
            return fromDeg + DeltaAngle(fromDeg, toDeg) * Mathf.Clamp(t, 0f, 1f);
        }
    }
}
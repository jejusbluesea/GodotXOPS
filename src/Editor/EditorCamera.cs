using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터의 3D 시점. 블렌더처럼 한 점(중심)을 두고 그 둘레를 돈다: 돌리기, 옮기기, 확대·축소, 정면·측면·위 시점, 원근과 직교 전환.
    /// 좁은 곳을 위해 날아다니기(카메라 자리에서 돌고 움직이기)도 함께 둔다. 어느 쪽으로 움직여도 같은 상태(중심, 거리, 각도)를 고친다.
    /// 각도는 UnityXOPS 규약이다 (도, yaw 는 오른쪽으로 돌수록 +, pitch 는 아래를 볼수록 +).
    /// </summary>
    public sealed class EditorCamera
    {
        private const float k_pitchLimit = 89.9f;
        // 가운데 버튼으로 돌릴 때 마우스 1픽셀이 돌리는 각도 (도).
        private const float k_orbitDegreesPerPixel = 0.4f;
        // 휠 한 칸이 거리에 곱하는 비율.
        private const float k_zoomStep = 1.15f;
        private const float k_minDistance = 0.3f;
        private const float k_maxDistance = 2000f;
        // 날아다니는 속도 (m/s)와 가속 배율.
        private const float k_flySpeed = 8f;
        private const float k_flyFastMultiplier = 4f;
        // 선택한 것으로 시점을 맞출 때 물러나는 거리의 하한 (m).
        private const float k_focusMinDistance = 4f;
        // 직교 시점에서 카메라를 중심에서 이만큼(m) 더 물려 둔다. 직교에서는 거리가 크기만 정하므로, 물려 두지 않으면 중심보다 가까운 것이 카메라 뒤로 넘어가 잘린다.
        private const float k_orthographicBackoff = 1000f;

        private readonly Camera3D m_camera;
        private Vector3 m_pivot;
        private float m_distance = 20f;
        private float m_yaw;
        private float m_pitch = 25f;
        private bool m_orthographic;

        public Camera3D Camera => m_camera;
        public bool Orthographic => m_orthographic;
        public Vector3 Pivot => m_pivot;
        // 중심에서 카메라까지의 거리 (m).
        public float Distance => m_distance;

        /// <summary>
        /// 시점을 만든다.
        /// </summary>
        /// <param name="camera">움직일 카메라 노드.</param>
        public EditorCamera(Camera3D camera)
        {
            m_camera = camera;
            Apply();
        }

        /// <summary>
        /// 중심의 둘레를 돈다 (가운데 버튼 끌기).
        /// </summary>
        /// <param name="pixels">마우스가 움직인 양 (픽셀).</param>
        public void Orbit(Vector2 pixels)
        {
            m_yaw += pixels.X * k_orbitDegreesPerPixel;
            m_pitch = Mathf.Clamp(m_pitch + pixels.Y * k_orbitDegreesPerPixel, -k_pitchLimit, k_pitchLimit);
            Apply();
        }

        /// <summary>
        /// 화면과 나란히 옮긴다 (Shift + 가운데 버튼 끌기). 중심에 있는 것이 마우스를 따라오는 만큼 움직인다.
        /// </summary>
        /// <param name="pixels">마우스가 움직인 양 (픽셀).</param>
        public void Pan(Vector2 pixels)
        {
            float perPixel = ViewHeight() / Mathf.Max(1f, m_camera.GetViewport().GetVisibleRect().Size.Y);
            Basis basis = m_camera.GlobalBasis;
            m_pivot += (-basis.X * pixels.X + basis.Y * pixels.Y) * perPixel;
            Apply();
        }

        /// <summary>
        /// 중심으로 다가가거나 물러난다 (휠).
        /// </summary>
        /// <param name="steps">휠 칸 수. 양수면 다가간다.</param>
        public void Zoom(float steps)
        {
            m_distance = Mathf.Clamp(m_distance / Mathf.Pow(k_zoomStep, steps), k_minDistance, k_maxDistance);
            Apply();
        }

        /// <summary>
        /// 정해진 방향에서 보게 한다 (정면, 측면, 위).
        /// </summary>
        /// <param name="yaw">yaw (도).</param>
        /// <param name="pitch">pitch (도).</param>
        public void SetAngles(float yaw, float pitch)
        {
            m_yaw = yaw;
            m_pitch = Mathf.Clamp(pitch, -k_pitchLimit, k_pitchLimit);
            Apply();
        }

        /// <summary>
        /// 카메라를 한 자리에 놓고 한 방향을 보게 한다. 중심은 그 앞으로 지금 거리만큼 떨어진 곳이 된다.
        /// </summary>
        /// <param name="position">카메라 위치.</param>
        /// <param name="yaw">yaw (도).</param>
        /// <param name="pitch">pitch (도).</param>
        public void SetPose(Vector3 position, float yaw, float pitch)
        {
            m_yaw = yaw;
            m_pitch = Mathf.Clamp(pitch, -k_pitchLimit, k_pitchLimit);
            m_pivot = position + Coord.AimDirection(m_yaw, m_pitch) * m_distance;
            Apply();
        }

        /// <summary>
        /// 원근이나 직교로 정한다.
        /// </summary>
        /// <param name="orthographic">true 면 직교.</param>
        public void SetOrthographic(bool orthographic)
        {
            if (m_orthographic == orthographic) return;

            m_orthographic = orthographic;
            Apply();
        }

        /// <summary>
        /// 원근과 직교를 바꾼다.
        /// </summary>
        public void ToggleOrthographic()
        {
            m_orthographic = !m_orthographic;
            Apply();
        }

        /// <summary>
        /// 한 범위가 화면에 들어오도록 중심과 거리를 맞춘다. 보는 방향은 그대로다.
        /// </summary>
        /// <param name="center">범위의 가운데.</param>
        /// <param name="radius">범위의 반지름 (m).</param>
        public void Focus(Vector3 center, float radius)
        {
            m_pivot = center;
            float halfFov = Mathf.DegToRad(m_camera.Fov * 0.5f);
            m_distance = Mathf.Clamp(Mathf.Max(k_focusMinDistance, radius / Mathf.Tan(halfFov) * 1.2f), k_minDistance, k_maxDistance);
            Apply();
        }

        /// <summary>
        /// 날아다니기 (오른쪽 버튼을 누르고 있는 동안). 카메라 자리에서 돌고, 보는 방향 기준으로 움직인다.
        /// </summary>
        /// <param name="look">마우스가 움직인 양에 감도를 곱한 값 (도). X 는 yaw, Y 는 위로 볼수록 +.</param>
        /// <param name="move">이동 입력. X 는 오른쪽, Y 는 앞쪽.</param>
        /// <param name="vertical">위아래 입력 (+ 위).</param>
        /// <param name="fast">가속 여부.</param>
        /// <param name="delta">프레임 시간 (초).</param>
        public void Fly(Vector2 look, Vector2 move, float vertical, bool fast, float delta)
        {
            // 카메라 자리를 지키며 돌려면 중심을 카메라 앞으로 다시 잡아야 한다. 직교에서는 카메라 노드가 뒤로 물러나 있으므로 상태에서 자리를 구한다.
            Vector3 position = m_pivot - Coord.AimDirection(m_yaw, m_pitch) * m_distance;
            m_yaw += look.X;
            m_pitch = Mathf.Clamp(m_pitch - look.Y, -k_pitchLimit, k_pitchLimit);

            Vector3 forward = Coord.AimDirection(m_yaw, m_pitch);
            Vector3 right = Coord.YawRight(m_yaw);
            float speed = k_flySpeed * (fast ? k_flyFastMultiplier : 1f);
            position += (right * move.X + forward * move.Y + Vector3.Up * vertical) * speed * delta;

            m_pivot = position + forward * m_distance;
            Apply();
        }

        /// <summary>
        /// 화면 세로에 들어오는 월드 길이 (중심의 거리에서). 직교 시점의 크기와 옮기기의 배율에 쓴다.
        /// </summary>
        /// <returns>길이 (m).</returns>
        private float ViewHeight()
        {
            return 2f * m_distance * Mathf.Tan(Mathf.DegToRad(m_camera.Fov * 0.5f));
        }

        /// <summary>
        /// 상태를 카메라 노드에 적용한다.
        /// </summary>
        private void Apply()
        {
            m_camera.Rotation = Coord.FromUnityEuler(new Vector3(m_pitch, m_yaw, 0f));
            m_camera.Position = m_pivot - Coord.AimDirection(m_yaw, m_pitch) * (m_distance + (m_orthographic ? k_orthographicBackoff : 0f));
            if (m_orthographic)
            {
                m_camera.Projection = Camera3D.ProjectionType.Orthogonal;
                m_camera.Size = ViewHeight();
            }
            else
            {
                m_camera.Projection = Camera3D.ProjectionType.Perspective;
            }
        }
    }
}

using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 포인트 사이의 연결을 3D 화면에 화살표 선으로 그린다: 이벤트의 출구가 가리키는 다음 이벤트, AI 경로의 다음 경로, 사람의 첫 경로.
    /// 무엇을 잇는지는 에디터가 정하고 (Begin → Add ... → End), 여기서는 그리기만 한다.
    /// </summary>
    public partial class LinkOverlay : Node3D
    {
        // 화살촉의 길이 (m)와 벌어진 정도 (길이에 대한 비율).
        private const float k_headLength = 0.35f;
        private const float k_headSpread = 0.35f;
        // 화살촉을 받는 쪽 표식에서 이만큼(m) 앞에 둔다 (표식에 묻히지 않게).
        private const float k_headGap = 0.25f;

        private ImmediateMesh m_mesh;
        private StandardMaterial3D m_material;
        private bool m_xray;
        private bool m_open;

        public override void _Ready()
        {
            m_mesh = new ImmediateMesh();
            AddChild(new MeshInstance3D { Name = "Lines", Mesh = m_mesh });
            // 표식과 같은 이유로 반투명 패스에서 그린다.
            m_material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = m_xray,
            };
        }

        /// <summary>
        /// X-RAY 를 켜거나 끈다. 켜면 선이 블록에 가려지지 않는다.
        /// </summary>
        /// <param name="enabled">켤지.</param>
        public void SetXray(bool enabled)
        {
            m_xray = enabled;
            if (m_material != null) m_material.NoDepthTest = enabled;
        }

        /// <summary>
        /// 그리던 것을 지우고 새로 그리기 시작한다.
        /// </summary>
        public void Begin()
        {
            m_mesh.ClearSurfaces();
            m_open = false;
        }

        /// <summary>
        /// 화살표 선 하나를 더한다.
        /// </summary>
        /// <param name="from">시작점.</param>
        /// <param name="to">끝점 (화살촉이 가리키는 쪽).</param>
        /// <param name="color">색.</param>
        public void Add(Vector3 from, Vector3 to, Color color)
        {
            Vector3 span = to - from;
            float length = span.Length();
            if (length < 1e-4f) return;

            if (!m_open)
            {
                m_mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, m_material);
                m_open = true;
            }

            Vector3 direction = span / length;
            Vector3 tip = length > k_headGap * 2f ? to - direction * k_headGap : to;
            Vector3 side = direction.Cross(Vector3.Up);
            if (side.LengthSquared() < 1e-6f) side = direction.Cross(Vector3.Right);
            side = side.Normalized() * (k_headLength * k_headSpread);
            Vector3 back = tip - direction * Mathf.Min(k_headLength, length * 0.5f);

            Line(from, tip, color);
            Line(tip, back + side, color);
            Line(tip, back - side, color);
        }

        /// <summary>
        /// 그리기를 끝낸다.
        /// </summary>
        public void End()
        {
            if (m_open) m_mesh.SurfaceEnd();
            m_open = false;
        }

        /// <summary>
        /// 선분 하나를 더한다.
        /// </summary>
        /// <param name="a">한 끝.</param>
        /// <param name="b">다른 끝.</param>
        /// <param name="color">색.</param>
        private void Line(Vector3 a, Vector3 b, Color color)
        {
            m_mesh.SurfaceSetColor(color);
            m_mesh.SurfaceAddVertex(a);
            m_mesh.SurfaceSetColor(color);
            m_mesh.SurfaceAddVertex(b);
        }
    }
}

using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터의 3D 화면에 그리는 격자. 직교 시점에서는 보는 방향에 가장 가까운 축에 수직인 면 위에, 블록 위로 겹쳐 그린다.
    /// 원근 시점에서는 켰을 때만 높이 0 의 바닥에 그리고 블록에 가려진다. 간격은 에디터의 격자 단위이고, 선이 너무 촘촘해지면 10배씩 성기게 한다.
    /// </summary>
    public partial class EditorGrid : MeshInstance3D
    {
        // 직교 시점에서 선 사이가 이만큼(픽셀)보다 좁아지면 간격을 10배로 늘린다.
        private const float k_minLinePixels = 8f;
        // 이 수마다 한 번씩 굵은(진한) 선을 긋는다.
        private const int k_majorEvery = 10;
        // 원근 시점의 바닥 격자: 중심에서 뻗는 거리(시점 거리의 배수와 하한, m)와 한 방향의 선 수의 상한.
        private const float k_floorReachFactor = 3f;
        private const float k_floorMinReach = 10f;
        private const int k_floorMaxLines = 120;
        private static readonly Color s_minorColor = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color s_majorColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color[] s_axisColors =
        {
            new Color(1f, 0.35f, 0.35f, 0.7f), new Color(0.45f, 1f, 0.45f, 0.7f), new Color(0.4f, 0.6f, 1f, 0.7f),
        };

        private readonly ImmediateMesh m_mesh = new ImmediateMesh();
        private StandardMaterial3D m_overlayMaterial;
        private StandardMaterial3D m_floorMaterial;
        private Transform3D m_lastTransform;
        private Vector2 m_lastScreen;
        private float m_lastSize;
        private float m_lastGridSize;
        private int m_lastFlags = -1;

        // 지금 그린 선의 수, 선 사이의 간격 (m), 격자가 놓인 면에 수직인 축 (0 X, 1 Y, 2 Z). 점검용.
        public int LineCount { get; private set; }
        public float Step { get; private set; }
        public int PlaneAxis { get; private set; }

        public override void _Ready()
        {
            Mesh = m_mesh;
            m_overlayMaterial = CreateMaterial(true);
            m_floorMaterial = CreateMaterial(false);
        }

        /// <summary>
        /// 시점에 맞춰 격자를 다시 그린다. 시점과 설정이 지난번과 같으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="view">에디터의 시점.</param>
        /// <param name="gridSize">격자 단위 (m).</param>
        /// <param name="show">직교 시점에서 격자를 그릴지.</param>
        /// <param name="floor">원근 시점에서 바닥 격자를 그릴지.</param>
        public void Refresh(EditorCamera view, float gridSize, bool show, bool floor)
        {
            Camera3D camera = view.Camera;
            Vector2 screen = camera.GetViewport().GetVisibleRect().Size;
            int flags = (view.Orthographic ? 1 : 0) | (show ? 2 : 0) | (floor ? 4 : 0);
            if (flags == m_lastFlags && camera.GlobalTransform == m_lastTransform && screen == m_lastScreen && camera.Size == m_lastSize && gridSize == m_lastGridSize) return;

            m_lastFlags = flags;
            m_lastTransform = camera.GlobalTransform;
            m_lastScreen = screen;
            m_lastSize = camera.Size;
            m_lastGridSize = gridSize;

            m_mesh.ClearSurfaces();
            LineCount = 0;
            if (gridSize <= 0f || screen.Y < 1f) return;

            if (view.Orthographic)
            {
                if (!show) return;

                float pixelsPerMeter = screen.Y / Mathf.Max(camera.Size, 1e-4f);
                float step = gridSize;
                while (step * pixelsPerMeter < k_minLinePixels) step *= k_majorEvery;
                int normal = (int)(-camera.GlobalBasis.Z).Abs().MaxAxisIndex();
                // 화면을 비스듬히 봐도 덮도록 화면 대각선의 절반만큼 뻗는다.
                Draw(m_overlayMaterial, normal, view.Pivot, 0.5f * screen.Length() / pixelsPerMeter, step);
            }
            else if (floor)
            {
                float reach = Mathf.Max(k_floorMinReach, view.Distance * k_floorReachFactor);
                float step = gridSize;
                while (2f * reach / step > k_floorMaxLines) step *= k_majorEvery;
                Draw(m_floorMaterial, 1, new Vector3(view.Pivot.X, 0f, view.Pivot.Z), reach, step);
            }
        }

        /// <summary>
        /// 한 축에 수직인 면 위에 격자를 그린다.
        /// </summary>
        /// <param name="material">선의 머티리얼.</param>
        /// <param name="normal">면에 수직인 축 (0 X, 1 Y, 2 Z).</param>
        /// <param name="center">격자의 가운데. 면은 이 점을 지난다.</param>
        /// <param name="reach">가운데에서 뻗는 거리 (m).</param>
        /// <param name="step">선 사이의 간격 (m).</param>
        private void Draw(StandardMaterial3D material, int normal, Vector3 center, float reach, float step)
        {
            PlaneAxis = normal;
            Step = step;
            int first = (normal + 1) % 3;
            int second = (normal + 2) % 3;

            m_mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
            int lines = 0;
            foreach ((int along, int across) in new[] { (first, second), (second, first) })
            {
                int from = Mathf.CeilToInt((center[across] - reach) / step);
                int to = Mathf.FloorToInt((center[across] + reach) / step);
                for (int i = from; i <= to; i++)
                {
                    // 원점을 지나는 선은 그 선이 뻗는 축의 색이다.
                    Color color = i == 0 ? s_axisColors[along] : (i % k_majorEvery == 0 ? s_majorColor : s_minorColor);
                    Vector3 start = center;
                    start[across] = i * step;
                    Vector3 end = start;
                    start[along] = center[along] - reach;
                    end[along] = center[along] + reach;
                    m_mesh.SurfaceSetColor(color);
                    m_mesh.SurfaceAddVertex(start);
                    m_mesh.SurfaceSetColor(color);
                    m_mesh.SurfaceAddVertex(end);
                    lines++;
                }
            }
            if (lines == 0)
            {
                // 빈 표면은 끝낼 수 없다. 보이지 않는 선 하나를 넣는다.
                m_mesh.SurfaceSetColor(Colors.Transparent);
                m_mesh.SurfaceAddVertex(center);
                m_mesh.SurfaceSetColor(Colors.Transparent);
                m_mesh.SurfaceAddVertex(center);
            }
            m_mesh.SurfaceEnd();
            LineCount = lines;
        }

        /// <summary>
        /// 격자 선의 머티리얼을 만든다. 조명과 안개를 받지 않고 꼭짓점의 색을 쓴다.
        /// </summary>
        /// <param name="overlay">true 면 블록 위로 겹쳐 그린다 (깊이 검사를 끈다).</param>
        /// <returns>머티리얼.</returns>
        private static StandardMaterial3D CreateMaterial(bool overlay)
        {
            return new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = overlay,
            };
        }
    }
}

using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록 편집 화면의 덧그림: 모든 블록의 모서리 선, 꼭짓점 점, 선택한 꼭짓점·모서리·면·블록의 강조.
    /// 블록의 면 자체는 게임의 로더가 그린 메시이고, 이 덧그림은 문서의 내용(BD2)에서 바로 그리므로 옮기는 도중의 모양도 보여 준다.
    /// </summary>
    public partial class BlockOverlay : Node3D
    {
        // 블록의 모서리 12개 (정점 번호 쌍). 정점 0~3 이 한 면, 4~7 이 맞은편 면이고 같은 순서로 이어진다.
        public static readonly int[][] Edges =
        {
            new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 },
            new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 },
            new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 },
        };

        private static readonly Color s_wireColor = new Color(1f, 0.25f, 0.9f);
        private static readonly Color s_vertexColor = new Color(1f, 1f, 1f);
        private static readonly Color s_selectedColor = new Color(1f, 0.9f, 0.1f);
        private static readonly Color s_selectedFaceColor = new Color(1f, 0.9f, 0.1f, 0.35f);
        // 꼭짓점 점의 크기 (픽셀).
        private const float k_vertexPointSize = 5f;
        private const float k_selectedPointSize = 9f;

        private ImmediateMesh m_mesh;
        private StandardMaterial3D m_lineMaterial;
        private StandardMaterial3D m_pointMaterial;
        private StandardMaterial3D m_selectedPointMaterial;
        private StandardMaterial3D m_faceMaterial;
        private bool m_xray;

        public override void _Ready()
        {
            m_mesh = new ImmediateMesh();
            AddChild(new MeshInstance3D { Name = "Lines", Mesh = m_mesh });
            m_lineMaterial = CreateMaterial(0f);
            m_pointMaterial = CreateMaterial(k_vertexPointSize);
            m_selectedPointMaterial = CreateMaterial(k_selectedPointSize);
            m_faceMaterial = CreateMaterial(0f);
            m_faceMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }

        /// <summary>
        /// X-RAY 를 켜거나 끈다. 켜면 덧그림이 블록에 가려지지 않는다.
        /// </summary>
        /// <param name="enabled">켤지.</param>
        public void SetXray(bool enabled)
        {
            m_xray = enabled;
            foreach (StandardMaterial3D material in new[] { m_lineMaterial, m_pointMaterial, m_selectedPointMaterial, m_faceMaterial })
            {
                if (material != null) material.NoDepthTest = enabled;
            }
        }

        /// <summary>
        /// 덧그림을 다시 그린다.
        /// </summary>
        /// <param name="blocks">문서의 블록들.</param>
        /// <param name="mode">선택 단위. 꼭짓점 단위일 때만 모든 꼭짓점에 점을 찍는다.</param>
        /// <param name="selection">선택한 것들의 키 (블록 번호 × ElementStride + 요소 번호).</param>
        public void Redraw(IReadOnlyList<BD2Block> blocks, BlockElement mode, ICollection<int> selection)
        {
            m_mesh.ClearSurfaces();
            if (blocks.Count == 0) return;

            m_mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, m_lineMaterial);
            for (int b = 0; b < blocks.Count; b++)
            {
                Vector3[] vertices = blocks[b].vertices;
                bool wholeBlock = mode == BlockElement.Block && selection.Contains(b * XopsEditor.ElementStride);
                for (int e = 0; e < Edges.Length; e++)
                {
                    bool selected = wholeBlock || (mode == BlockElement.Edge && selection.Contains(b * XopsEditor.ElementStride + e));
                    m_mesh.SurfaceSetColor(selected ? s_selectedColor : s_wireColor);
                    m_mesh.SurfaceAddVertex(vertices[Edges[e][0]]);
                    m_mesh.SurfaceSetColor(selected ? s_selectedColor : s_wireColor);
                    m_mesh.SurfaceAddVertex(vertices[Edges[e][1]]);
                }
            }
            m_mesh.SurfaceEnd();

            if (mode == BlockElement.Vertex)
            {
                m_mesh.SurfaceBegin(Mesh.PrimitiveType.Points, m_pointMaterial);
                for (int b = 0; b < blocks.Count; b++)
                {
                    foreach (Vector3 vertex in blocks[b].vertices)
                    {
                        m_mesh.SurfaceSetColor(s_vertexColor);
                        m_mesh.SurfaceAddVertex(vertex);
                    }
                }
                m_mesh.SurfaceEnd();

                if (selection.Count > 0)
                {
                    m_mesh.SurfaceBegin(Mesh.PrimitiveType.Points, m_selectedPointMaterial);
                    foreach (int key in selection)
                    {
                        m_mesh.SurfaceSetColor(s_selectedColor);
                        m_mesh.SurfaceAddVertex(blocks[key / XopsEditor.ElementStride].vertices[key % XopsEditor.ElementStride]);
                    }
                    m_mesh.SurfaceEnd();
                }
            }

            if (mode == BlockElement.Face && selection.Count > 0)
            {
                IReadOnlyList<int[]> faces = MapLoader.BlockFaceVertices;
                m_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, m_faceMaterial);
                foreach (int key in selection)
                {
                    Vector3[] vertices = blocks[key / XopsEditor.ElementStride].vertices;
                    int[] face = faces[key % XopsEditor.ElementStride];
                    foreach (int corner in new[] { 0, 1, 2, 0, 2, 3 })
                    {
                        m_mesh.SurfaceSetColor(s_selectedFaceColor);
                        m_mesh.SurfaceAddVertex(vertices[face[corner]]);
                    }
                }
                m_mesh.SurfaceEnd();
            }
        }

        /// <summary>
        /// 정점 색을 그대로 쓰는 단색 머티리얼을 만든다. 표식과 같은 이유로 반투명 패스에서 그린다.
        /// </summary>
        /// <param name="pointSize">점으로 그릴 때의 크기 (픽셀). 0 이면 점 크기를 쓰지 않는다.</param>
        /// <returns>머티리얼.</returns>
        private StandardMaterial3D CreateMaterial(float pointSize)
        {
            return new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = m_xray,
                UsePointSize = pointSize > 0f,
                PointSize = pointSize > 0f ? pointSize : 1f,
            };
        }
    }
}

using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 구역 블록을 반투명한 상자로 그리는 덧그림. 태그마다 색이 다르고 고른 태그는 진하게 그린다. 문서의 내용(BD2 의 구역)에서 바로 그린다.
    /// </summary>
    public partial class AreaOverlay : Node3D
    {
        // 면 여섯 개 (꼭짓점 번호). 블록과 같은 배치다.
        private static readonly int[][] s_faces =
        {
            new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
            new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
        };
        private const float k_faceAlpha = 0.12f;
        private const float k_selectedFaceAlpha = 0.3f;

        private ImmediateMesh m_mesh;
        private StandardMaterial3D m_material;

        public override void _Ready()
        {
            m_mesh = new ImmediateMesh();
            m_material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
            };
            AddChild(new MeshInstance3D { Name = "Areas", Mesh = m_mesh });
        }

        /// <summary>
        /// 태그 번호의 색. 이웃한 번호끼리 색이 멀어지게 황금비로 색상을 돌린다.
        /// </summary>
        /// <param name="tag">태그 번호.</param>
        /// <param name="alpha">진하기.</param>
        /// <returns>색.</returns>
        public static Color TagColor(int tag, float alpha)
        {
            return Color.FromHsv(Mathf.PosMod(tag * 0.618034f, 1f), 0.75f, 1f, alpha);
        }

        /// <summary>
        /// 구역 블록들을 다시 그린다.
        /// </summary>
        /// <param name="areas">문서의 구역 블록들.</param>
        /// <param name="selectedTag">진하게 그릴 태그. 없으면 -1.</param>
        public void Redraw(List<BD2Area> areas, int selectedTag)
        {
            m_mesh.ClearSurfaces();
            if (areas.Count == 0) return;

            m_mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, m_material);
            foreach (BD2Area area in areas)
            {
                Color color = TagColor(area.tag, area.tag == selectedTag ? k_selectedFaceAlpha : k_faceAlpha);
                foreach (int[] face in s_faces)
                {
                    foreach (int i in new[] { 0, 1, 2, 0, 2, 3 })
                    {
                        m_mesh.SurfaceSetColor(color);
                        m_mesh.SurfaceAddVertex(area.vertices[face[i]]);
                    }
                }
            }
            m_mesh.SurfaceEnd();
        }
    }
}

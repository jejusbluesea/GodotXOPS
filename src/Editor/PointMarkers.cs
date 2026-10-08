using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 포인트들을 3D 화면에 표식으로 그리고, 화면의 한 점이나 사각형으로 표식을 고른다.
    /// 종류마다 색과 모양이 다르고, 방향이 있는 종류에는 화살표가, 모든 표식에는 식별번호가 붙는다. 선택한 표식은 흰 테두리 상자로 감싼다.
    /// 모델을 만들 수 있는 포인트(사람, 무기, 소물)는 상자 대신 그 모델을 보여 준다 (ModelFactory, ShowModels). 화살표와 식별번호는 그대로 붙는다.
    /// 표식은 보여 주기만 한다. 값은 MapDocument 의 포인트가 갖고, 바뀌면 Rebuild 로 다시 맞춘다.
    /// </summary>
    public partial class PointMarkers : Node3D
    {
        // 표식의 크기 (m).
        private static readonly Vector3 s_humanSize = new Vector3(0.4f, 1.8f, 0.4f);
        private static readonly Vector3 s_itemSize = new Vector3(0.35f, 0.2f, 0.35f);
        private static readonly Vector3 s_nodeSize = new Vector3(0.3f, 0.3f, 0.3f);
        // 방향 화살표: 표식의 가운데에서 앞으로 뻗는 막대의 길이와 굵기 (m).
        private const float k_arrowLength = 0.8f;
        private const float k_arrowThickness = 0.06f;
        // 식별번호 글자: 표식 위로 띄우는 높이 (m)와 글자 크기.
        private const float k_labelGap = 0.25f;
        private const float k_labelPixelSize = 0.004f;
        private const int k_labelFontSize = 48;
        private const int k_labelOutline = 12;
        // 선택한 표식을 감싸는 테두리 상자가 표식보다 얼마나 큰지 (m).
        private const float k_selectionMargin = 0.12f;
        // 클릭으로 고를 때 표식이 화면에서 이 거리(픽셀) 안에 있어야 한다.
        private const float k_pickRadiusPixels = 14f;
        // 가려졌는지 볼 때 표식 바로 앞에서 레이를 멈춘다 (m). 표식이 놓인 바닥이나 벽에 레이가 닿아 가려진 것으로 치지 않게 한다.
        private const float k_occlusionSlack = 0.05f;

        private readonly List<Node3D> m_nodes = new List<Node3D>();
        private readonly List<MeshInstance3D> m_highlights = new List<MeshInstance3D>();
        // 표식마다의 방향 화살표와 식별번호 글자. 방향이 없는 종류의 화살표는 null 이다.
        private readonly List<MeshInstance3D> m_arrows = new List<MeshInstance3D>();
        private readonly List<Label3D> m_labels = new List<Label3D>();
        // 표식마다의 상자와 모델. 모델이 없는 표식의 모델은 null 이다.
        private readonly List<MeshInstance3D> m_boxes = new List<MeshInstance3D>();
        private readonly List<Node3D> m_models = new List<Node3D>();
        private bool m_xray;

        // 포인트 하나의 모델을 만드는 함수 (yaw 0 기준으로 조립한 노드. 방향은 표식이 돌린다). 모델이 없는 포인트면 null 을 돌려준다.
        public System.Func<PD2Point, Node3D> ModelFactory;
        // 모델을 보여 줄지. 바꾼 뒤에는 Rebuild 를 부른다.
        public bool ShowModels = true;
        private readonly Dictionary<Color, StandardMaterial3D> m_materials = new Dictionary<Color, StandardMaterial3D>();
        private IReadOnlyList<PD2Point> m_points;
        private StandardMaterial3D m_highlightMaterial;
        // 모델이 있는 표식의 선택 표시는 모델을 가리지 않게 모서리 선으로만 그린다.
        private StandardMaterial3D m_outlineMaterial;
        private static readonly Color s_outlineColor = new Color(1f, 1f, 1f, 0.9f);

        /// <summary>
        /// 표식을 전부 다시 만든다. 선택 표시는 모두 꺼진 채로 만들어진다.
        /// </summary>
        /// <param name="points">포인트 목록 (파일 순서).</param>
        public void Rebuild(IReadOnlyList<PD2Point> points)
        {
            foreach (Node3D node in m_nodes)
            {
                RemoveChild(node);
                node.Free();
            }
            m_nodes.Clear();
            m_highlights.Clear();
            m_arrows.Clear();
            m_labels.Clear();
            m_boxes.Clear();
            m_models.Clear();
            m_points = points;

            m_highlightMaterial ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1f, 1f, 1f, 0.4f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            };

            m_outlineMaterial ??= new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = s_outlineColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            };

            foreach (PD2Point point in points)
            {
                Node3D node = CreateMarker(point, out MeshInstance3D highlight);
                AddChild(node);
                m_nodes.Add(node);
                m_highlights.Add(highlight);
            }
        }

        /// <summary>
        /// 선택한 포인트들에 테두리를 두르고 나머지의 테두리를 지운다.
        /// </summary>
        /// <param name="selection">선택한 포인트 번호들.</param>
        public void SetSelection(ICollection<int> selection)
        {
            for (int i = 0; i < m_highlights.Count; i++)
            {
                m_highlights[i].Visible = selection.Contains(i);
            }
        }

        /// <summary>
        /// X-RAY 를 켜거나 끈다. 켜면 표식이 블록에 가려지지 않고 전부 보인다 (깊이 검사를 끈다).
        /// </summary>
        /// <param name="enabled">켤지.</param>
        public void SetXray(bool enabled)
        {
            m_xray = enabled;
            // 모델은 블록에 가려지므로, X-RAY 에서는 가려진 것도 보이게 상자를 함께 그린다.
            for (int i = 0; i < m_boxes.Count; i++)
            {
                m_boxes[i].Visible = enabled || m_models[i] == null;
            }
            foreach (StandardMaterial3D material in m_materials.Values)
            {
                material.NoDepthTest = enabled;
            }
            foreach (Label3D label in m_labels)
            {
                label.NoDepthTest = enabled;
            }
        }

        /// <summary>
        /// 포인트 하나의 위치나 방향이 바뀌었을 때 그 표식을 다시 맞춘다 (옮기는 도중의 미리 보기). 종류나 식별번호가 바뀌었으면 Rebuild 를 쓴다.
        /// </summary>
        /// <param name="index">포인트 번호.</param>
        public void Refresh(int index)
        {
            if (m_points == null || index < 0 || index >= m_nodes.Count) return;

            PD2Point point = m_points[index];
            m_nodes[index].Position = point.position;
            if (m_models[index] != null) m_models[index].Rotation = ModelRotation(point);
            MeshInstance3D arrow = m_arrows[index];
            if (arrow == null) return;

            Vector3 forward = Coord.YawForward(point.direction);
            float height = ShapeSize(PointTypeInfo.Get(point.type).Shape).Y * 0.5f;
            arrow.Position = Vector3.Up * height + forward * (k_arrowLength * 0.5f);
            arrow.Basis = Basis.LookingAt(forward, Vector3.Up);
        }

        /// <summary>
        /// 포인트의 표식 가운데 (바닥이 아니라 몸통의 가운데).
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <returns>월드 좌표.</returns>
        public static Vector3 Center(PD2Point point)
        {
            return point.position + Vector3.Up * (ShapeSize(PointTypeInfo.Get(point.type).Shape).Y * 0.5f);
        }

        /// <summary>
        /// 포인트의 모델 노드.
        /// </summary>
        /// <param name="index">포인트 번호.</param>
        /// <returns>모델. 모델을 보여 주지 않는 포인트면 null.</returns>
        public Node3D ModelOf(int index)
        {
            return index >= 0 && index < m_models.Count ? m_models[index] : null;
        }

        /// <summary>
        /// 포인트의 상자가 보이는지.
        /// </summary>
        /// <param name="index">포인트 번호.</param>
        /// <returns>보이면 true.</returns>
        public bool BoxVisible(int index)
        {
            return index >= 0 && index < m_boxes.Count && m_boxes[index].Visible;
        }

        /// <summary>
        /// 포인트의 방향대로 모델을 돌리는 회전. PD2 의 방향은 그 자리에 놓이는 것의 yaw 다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <returns>오일러 각 (라디안).</returns>
        private static Vector3 ModelRotation(PD2Point point)
        {
            return Coord.FromUnityEuler(new Vector3(0f, point.direction, 0f));
        }

        /// <summary>
        /// 화면의 한 점에서 가장 가까운 표식을 찾는다. 표식의 가운데를 화면에 투영해 거리를 재고, 같은 자리에 겹쳐 있으면 카메라에 가까운 것을 고른다.
        /// </summary>
        /// <param name="camera">카메라.</param>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <param name="xray">true 면 블록에 가려진 표식도 고른다.</param>
        /// <returns>포인트 번호. 가까운 표식이 없으면 −1.</returns>
        public int Pick(Camera3D camera, Vector2 screenPosition, bool xray)
        {
            if (m_points == null) return -1;

            int best = -1;
            float bestDepth = float.MaxValue;
            for (int i = 0; i < m_points.Count; i++)
            {
                Vector3 center = Center(m_points[i]);
                if (camera.IsPositionBehind(center)) continue;
                if (camera.UnprojectPosition(center).DistanceTo(screenPosition) > k_pickRadiusPixels) continue;
                if (!xray && Occluded(camera, center)) continue;

                float depth = camera.GlobalPosition.DistanceSquaredTo(center);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 화면의 사각형 안에 가운데가 들어 있는 표식을 모두 찾는다.
        /// </summary>
        /// <param name="camera">카메라.</param>
        /// <param name="rect">화면 사각형 (픽셀).</param>
        /// <param name="xray">true 면 블록에 가려진 표식도 고른다.</param>
        /// <param name="result">찾은 포인트 번호를 더할 목록.</param>
        public void PickRect(Camera3D camera, Rect2 rect, bool xray, List<int> result)
        {
            if (m_points == null) return;

            for (int i = 0; i < m_points.Count; i++)
            {
                Vector3 center = Center(m_points[i]);
                if (camera.IsPositionBehind(center)) continue;
                if (!rect.HasPoint(camera.UnprojectPosition(center))) continue;
                if (!xray && Occluded(camera, center)) continue;

                result.Add(i);
            }
        }

        /// <summary>
        /// 카메라에서 한 점이 블록에 가려 보이지 않는지 본다. 직교 시점에서도 맞도록 그 점이 찍히는 화면 자리에서 레이를 쏜다.
        /// </summary>
        /// <param name="camera">카메라.</param>
        /// <param name="target">볼 점.</param>
        /// <returns>블록이 사이에 있으면 true.</returns>
        private static bool Occluded(Camera3D camera, Vector3 target)
        {
            Vector3 origin = camera.ProjectRayOrigin(camera.UnprojectPosition(target));
            Vector3 toTarget = target - origin;
            float distance = toTarget.Length() - k_occlusionSlack;
            if (distance <= 0f) return false;

            return MapLoader.RaycastBlock(BlockLayer.Sight, origin, toTarget.Normalized(), distance, out _);
        }

        /// <summary>
        /// 표식 하나를 만든다. 원점이 포인트의 위치(바닥)이고 몸통은 그 위로 선다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <param name="highlight">선택 표시용 테두리 상자 (감춰진 채로 만든다).</param>
        /// <returns>표식 노드.</returns>
        private Node3D CreateMarker(PD2Point point, out MeshInstance3D highlight)
        {
            PointTypeInfo.Info info = PointTypeInfo.Get(point.type);
            Vector3 size = ShapeSize(info.Shape);
            StandardMaterial3D material = GetMaterial(info.Color);

            var root = new Node3D { Position = point.position };
            Node3D model = ShowModels ? ModelFactory?.Invoke(point) : null;
            var box = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                MaterialOverride = material,
                Position = Vector3.Up * (size.Y * 0.5f),
                Visible = model == null || m_xray,
            };
            root.AddChild(box);
            m_boxes.Add(box);
            m_models.Add(model);

            MeshInstance3D arrow = null;
            if (info.Shape != PointTypeInfo.Shape.Node)
            {
                // PD2 의 방향은 그 자리에 놓이는 것의 yaw 다. 화살표를 몸통 가운데에서 그 방향으로 뻗는다.
                Vector3 forward = Coord.YawForward(point.direction);
                arrow = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(k_arrowThickness, k_arrowThickness, k_arrowLength) },
                    MaterialOverride = material,
                    Position = Vector3.Up * (size.Y * 0.5f) + forward * (k_arrowLength * 0.5f),
                    Basis = Basis.LookingAt(forward, Vector3.Up),
                };
                root.AddChild(arrow);
            }
            m_arrows.Add(arrow);

            var label = new Label3D
            {
                Text = point.id.ToString(),
                Position = Vector3.Up * (size.Y + k_labelGap),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = k_labelPixelSize,
                FontSize = k_labelFontSize,
                OutlineSize = k_labelOutline,
                Modulate = info.Color,
                Shaded = false,
                NoDepthTest = m_xray,
            };
            root.AddChild(label);
            m_labels.Add(label);

            highlight = new MeshInstance3D
            {
                Mesh = model != null ? BoxOutline(size + Vector3.One * k_selectionMargin) : new BoxMesh { Size = size + Vector3.One * k_selectionMargin },
                MaterialOverride = model != null ? m_outlineMaterial : m_highlightMaterial,
                Position = Vector3.Up * (size.Y * 0.5f),
                Visible = false,
            };
            root.AddChild(highlight);
            if (model != null)
            {
                model.Rotation = ModelRotation(point);
                root.AddChild(model);
            }
            return root;
        }

        /// <summary>
        /// 상자의 모서리 12개를 선으로 그린 메시를 만든다 (가운데가 원점).
        /// </summary>
        /// <param name="size">가로, 높이, 세로 (m).</param>
        /// <returns>선 메시.</returns>
        private static ImmediateMesh BoxOutline(Vector3 size)
        {
            Vector3 half = size * 0.5f;
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            for (int corner = 0; corner < 8; corner++)
            {
                var from = new Vector3((corner & 1) != 0 ? half.X : -half.X, (corner & 2) != 0 ? half.Y : -half.Y, (corner & 4) != 0 ? half.Z : -half.Z);
                foreach (int bit in new[] { 1, 2, 4 })
                {
                    if ((corner & bit) != 0) continue;
                    Vector3 to = from;
                    if (bit == 1) to.X = half.X;
                    else if (bit == 2) to.Y = half.Y;
                    else to.Z = half.Z;
                    mesh.SurfaceAddVertex(from);
                    mesh.SurfaceAddVertex(to);
                }
            }
            mesh.SurfaceEnd();
            return mesh;
        }

        /// <summary>
        /// 표식 모양의 크기.
        /// </summary>
        /// <param name="shape">모양.</param>
        /// <returns>가로, 높이, 세로 (m).</returns>
        private static Vector3 ShapeSize(PointTypeInfo.Shape shape)
        {
            switch (shape)
            {
                case PointTypeInfo.Shape.Human: return s_humanSize;
                case PointTypeInfo.Shape.Item: return s_itemSize;
                default: return s_nodeSize;
            }
        }

        /// <summary>
        /// 색 하나의 표식 머티리얼. 같은 색은 함께 쓴다. 조명과 안개를 받지 않는 단색이다.
        /// 반투명 패스로 그린다 (불투명 패스의 단색 머티리얼은 이 프로젝트의 렌더 설정에서 거의 검게 나온다). 깊이는 쓰게 해서 표식끼리 앞뒤가 맞는다.
        /// </summary>
        /// <param name="color">색.</param>
        /// <returns>머티리얼.</returns>
        private StandardMaterial3D GetMaterial(Color color)
        {
            if (!m_materials.TryGetValue(color, out StandardMaterial3D material))
            {
                material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = color,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
                    NoDepthTest = m_xray,
                };
                m_materials[color] = material;
            }
            return material;
        }
    }
}

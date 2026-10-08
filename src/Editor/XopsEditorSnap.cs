using System;
using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 옮길 때 붙는 대상. 여러 개를 함께 켤 수 있다.
        /// </summary>
        [Flags]
        private enum SnapTarget
        {
            None = 0,
            // 격자점. 돌리기와 크기 바꾸기에서는 정해진 단계로 끊는다.
            Grid = 1,
            // 블록의 꼭짓점.
            Vertex = 2,
            // 블록의 모서리 위에서 마우스에 가장 가까운 점.
            Edge = 4,
            // 모서리의 가운데.
            EdgeCenter = 8,
            // 마우스 아래의 블록 면 위.
            Face = 16,
            // 면의 가운데.
            FaceCenter = 32,
        }

        // 꼭짓점·가운데·모서리에 붙을 때, 화면에서 마우스와 이 거리(픽셀) 안에 있어야 한다.
        private const float k_snapRadiusPixels = 14f;
        // 메뉴에 나오는 순서와 이름.
        private static readonly (SnapTarget Target, string Name)[] s_snapTargets =
        {
            (SnapTarget.Grid, "Grid"), (SnapTarget.Vertex, "Vertex"), (SnapTarget.Edge, "Edge"),
            (SnapTarget.EdgeCenter, "Edge center"), (SnapTarget.Face, "Face"), (SnapTarget.FaceCenter, "Face center"),
        };

        // 스냅을 켰는지와 대상. 모드마다 따로 기억한다 (0 포인트 모드, 1 블록 모드). 포인트는 바닥에 놓는 일이 많고 블록은 꼭짓점을 맞추는 일이 많다.
        private readonly bool[] m_snapOn = { true, true };
        private readonly SnapTarget[] m_snapTargets = { SnapTarget.Face, SnapTarget.Vertex };
        private CheckBox m_snapButton;
        private MenuButton m_snapMenu;
        // 겹친 꼭짓점·모서리를 눌렀을 때 어느 블록의 것인지 늘 물을지 (Ctrl + 클릭과 같다).
        private bool m_askOverlap;
        private CheckBox m_overlapButton;
        // 변형하는 것들 가운데 대상에 맞춰지는 것의 순번 (m_transformStarts 의 번호). 변형을 시작할 때 마우스에 가장 가까운 것이다.
        private int m_transformAnchor;
        private readonly HashSet<int> m_snapExcluded = new HashSet<int>();

        private int SnapSlot => m_editMode == EditMode.Block ? 1 : 0;

        /// <summary>
        /// 위쪽 도구 줄에 스냅의 켜기 상자와 대상 메뉴를 만든다.
        /// </summary>
        /// <param name="row">도구 줄.</param>
        private void BuildSnapControls(HBoxContainer row)
        {
            m_snapButton = new CheckBox { Text = "Snap", FocusMode = Control.FocusModeEnum.None, TooltipText = "Snap while moving. Hold Ctrl to turn it the other way for a moment. Remembered separately for point mode and block mode" };
            m_snapButton.Toggled += SetSnapEnabled;
            row.AddChild(m_snapButton);

            m_snapMenu = new MenuButton { Flat = false, FocusMode = Control.FocusModeEnum.None, TooltipText = "What to snap to. Several can be on: the one nearest to the mouse wins, then an edge, then the face under the mouse, then the grid" };
            PopupMenu popup = m_snapMenu.GetPopup();
            popup.HideOnCheckableItemSelection = false;
            foreach ((SnapTarget target, string name) in s_snapTargets)
            {
                popup.AddCheckItem(name, (int)target);
            }
            popup.IdPressed += id => SetSnap((SnapTarget)(int)id, !m_snapTargets[SnapSlot].HasFlag((SnapTarget)(int)id));
            row.AddChild(m_snapMenu);
            SyncSnapButtons();
        }

        /// <summary>
        /// 지금 모드의 스냅을 켜거나 끈다.
        /// </summary>
        /// <param name="enabled">켤지.</param>
        private void SetSnapEnabled(bool enabled)
        {
            m_snapOn[SnapSlot] = enabled;
            SyncSnapButtons();
            if (Transforming) UpdateTransform();
        }

        /// <summary>
        /// 지금 모드의 스냅 대상 하나를 켜거나 끈다. 대상을 켜면 스냅도 켠다.
        /// </summary>
        /// <param name="target">대상.</param>
        /// <param name="enabled">켤지.</param>
        private void SetSnap(SnapTarget target, bool enabled)
        {
            int slot = SnapSlot;
            m_snapTargets[slot] = enabled ? m_snapTargets[slot] | target : m_snapTargets[slot] & ~target;
            if (enabled) m_snapOn[slot] = true;
            SyncSnapButtons();
            if (Transforming) UpdateTransform();
        }

        /// <summary>
        /// 지금 듣는 스냅 대상. Ctrl 을 누르고 있으면 켜고 끈 상태가 잠깐 뒤집힌다.
        /// </summary>
        /// <returns>대상들. 스냅이 듣지 않으면 None.</returns>
        private SnapTarget ActiveSnap()
        {
            int slot = SnapSlot;
            return m_snapOn[slot] != Input.IsKeyPressed(Key.Ctrl) ? m_snapTargets[slot] : SnapTarget.None;
        }

        /// <summary>
        /// 스냅의 켜기 상자와 대상 메뉴를 지금 모드의 상태에 맞춘다.
        /// </summary>
        private void SyncSnapButtons()
        {
            if (m_snapButton == null) return;

            int slot = SnapSlot;
            m_snapButton.SetPressedNoSignal(m_snapOn[slot]);
            PopupMenu popup = m_snapMenu.GetPopup();
            var names = new List<string>();
            foreach ((SnapTarget target, string name) in s_snapTargets)
            {
                bool on = m_snapTargets[slot].HasFlag(target);
                popup.SetItemChecked(popup.GetItemIndex((int)target), on);
                if (on) names.Add(name);
            }
            m_snapMenu.Text = names.Count == 0 ? "Snap to: nothing" : (names.Count <= 2 ? string.Join(", ", names) : $"{names[0]} +{names.Count - 1}");
            if (m_overlapButton != null)
            {
                m_overlapButton.Visible = m_editMode == EditMode.Block;
                m_overlapButton.SetPressedNoSignal(m_askOverlap);
            }
        }

        /// <summary>
        /// 변형을 시작할 때, 변형하는 것들 가운데 마우스에 가장 가까운 것을 고른다. 스냅 대상에 맞춰지는 것이 이것이고 나머지는 같은 만큼 따라간다.
        /// </summary>
        private void ChooseTransformAnchor()
        {
            Camera3D camera = m_view.Camera;
            m_transformAnchor = 0;
            float best = float.MaxValue;
            for (int i = 0; i < m_transformStarts.Length; i++)
            {
                if (camera.IsPositionBehind(m_transformStarts[i])) continue;
                float distance = camera.UnprojectPosition(m_transformStarts[i]).DistanceSquaredTo(m_transformStartMouse);
                if (distance >= best) continue;
                best = distance;
                m_transformAnchor = i;
            }
        }

        /// <summary>
        /// 마우스 아래에서 붙을 자리를 찾는다. 꼭짓점·모서리의 가운데·면의 가운데 가운데 화면에서 가장 가까운 것이 먼저이고,
        /// 없으면 모서리 위의 가장 가까운 점, 그것도 없으면 마우스 아래의 면이다. 옮기고 있는 블록은 대상에서 뺀다.
        /// X-RAY 가 꺼져 있으면 블록에 가려진 자리에는 붙지 않는다.
        /// </summary>
        /// <param name="mouse">화면 좌표 (픽셀).</param>
        /// <param name="targets">찾을 대상들 (Grid 는 여기서 보지 않는다).</param>
        /// <param name="point">붙을 자리.</param>
        /// <param name="kind">찾은 대상의 종류.</param>
        /// <returns>찾았으면 true.</returns>
        private bool FindSnapPoint(Vector2 mouse, SnapTarget targets, out Vector3 point, out SnapTarget kind)
        {
            point = Vector3.Zero;
            kind = SnapTarget.None;
            Camera3D camera = m_view.Camera;
            List<BD2Block> blocks = m_document.Blocks.blocks;
            IReadOnlyList<int[]> faces = MapLoader.BlockFaceVertices;

            m_snapExcluded.Clear();
            if (Transforming && m_transformingBlocks)
            {
                foreach (int index in m_transformBlockIndices) m_snapExcluded.Add(index);
            }

            float best = k_snapRadiusPixels;
            float bestDepth = float.MaxValue;
            void Consider(Vector3 candidate, SnapTarget candidateKind, ref Vector3 found, ref SnapTarget foundKind)
            {
                if (camera.IsPositionBehind(candidate)) return;
                float distance = camera.UnprojectPosition(candidate).DistanceTo(mouse);
                if (distance > k_snapRadiusPixels) return;

                // 화면에서 더 가까운 것을 고르고, 거의 같은 자리(겹쳐 찍힌 것)면 카메라에 가까운 것을 고른다.
                float depth = camera.GlobalPosition.DistanceSquaredTo(candidate);
                bool better = foundKind == SnapTarget.None || distance < best - k_pickTiePixels || (Mathf.Abs(distance - best) <= k_pickTiePixels && depth < bestDepth);
                if (!better || (!m_xray && SnapHidden(candidate))) return;

                best = distance;
                bestDepth = depth;
                found = candidate;
                foundKind = candidateKind;
            }

            if ((targets & (SnapTarget.Vertex | SnapTarget.EdgeCenter | SnapTarget.FaceCenter)) != 0)
            {
                for (int b = 0; b < blocks.Count; b++)
                {
                    if (m_snapExcluded.Contains(b)) continue;

                    Vector3[] vertices = blocks[b].vertices;
                    if (targets.HasFlag(SnapTarget.Vertex))
                    {
                        foreach (Vector3 vertex in vertices) Consider(vertex, SnapTarget.Vertex, ref point, ref kind);
                    }
                    if (targets.HasFlag(SnapTarget.EdgeCenter))
                    {
                        foreach (int[] edge in BlockOverlay.Edges) Consider((vertices[edge[0]] + vertices[edge[1]]) * 0.5f, SnapTarget.EdgeCenter, ref point, ref kind);
                    }
                    if (targets.HasFlag(SnapTarget.FaceCenter))
                    {
                        foreach (int[] face in faces) Consider((vertices[face[0]] + vertices[face[1]] + vertices[face[2]] + vertices[face[3]]) * 0.25f, SnapTarget.FaceCenter, ref point, ref kind);
                    }
                }
                if (kind != SnapTarget.None) return true;
            }

            Vector3 origin = camera.ProjectRayOrigin(mouse);
            Vector3 direction = camera.ProjectRayNormal(mouse);
            if (targets.HasFlag(SnapTarget.Edge))
            {
                for (int b = 0; b < blocks.Count; b++)
                {
                    if (m_snapExcluded.Contains(b)) continue;

                    Vector3[] vertices = blocks[b].vertices;
                    foreach (int[] edge in BlockOverlay.Edges)
                    {
                        Consider(ClosestOnSegmentToRay(vertices[edge[0]], vertices[edge[1]], origin, direction), SnapTarget.Edge, ref point, ref kind);
                    }
                }
                if (kind != SnapTarget.None) return true;
            }

            if (targets.HasFlag(SnapTarget.Face))
            {
                // 포인트는 사람이 설 수 있는 면에 놓는다 (게임의 판정). 블록은 문서의 면에서 직접 찾는다: 옮기는 중인 블록을 빼야 하고, 화면의 블록은 옮기기 전의 모양이다.
                bool hit = Transforming && m_transformingBlocks
                    ? RayHitsFaces(origin, direction, m_snapExcluded, out point)
                    : SurfaceUnder(mouse, out point);
                if (hit)
                {
                    kind = SnapTarget.Face;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 선분 위에서 레이에 가장 가까운 점을 구한다.
        /// </summary>
        /// <param name="from">선분의 한 끝.</param>
        /// <param name="to">선분의 다른 끝.</param>
        /// <param name="origin">레이의 시작점.</param>
        /// <param name="direction">레이의 방향 (정규화).</param>
        /// <returns>선분 위의 점.</returns>
        private static Vector3 ClosestOnSegmentToRay(Vector3 from, Vector3 to, Vector3 origin, Vector3 direction)
        {
            Vector3 along = to - from;
            Vector3 offset = from - origin;
            float a = along.Dot(along);
            float b = along.Dot(direction);
            float d = along.Dot(offset);
            float e = direction.Dot(offset);
            float denominator = a - b * b;
            // 레이와 나란한 선분은 어느 점이나 같은 거리다. 가운데를 쓴다.
            float t = Mathf.Abs(denominator) < 1e-8f ? 0.5f : Mathf.Clamp((b * e - d) / denominator, 0f, 1f);
            return from + along * t;
        }

        /// <summary>
        /// 레이가 문서의 블록 면과 처음 만나는 자리를 찾는다.
        /// </summary>
        /// <param name="origin">레이의 시작점.</param>
        /// <param name="direction">레이의 방향 (정규화).</param>
        /// <param name="excluded">건너뛸 블록의 번호들.</param>
        /// <param name="hit">만난 자리.</param>
        /// <returns>만났으면 true.</returns>
        private bool RayHitsFaces(Vector3 origin, Vector3 direction, ISet<int> excluded, out Vector3 hit)
        {
            bool found = PickFace(origin, direction, excluded, out float distance) >= 0;
            hit = origin + direction * distance;
            return found;
        }

        /// <summary>
        /// 붙을 자리가 블록에 가려 보이지 않는지 본다. 옮기고 있는 블록은 가리지 않는 것으로 친다.
        /// </summary>
        /// <param name="target">볼 점.</param>
        /// <returns>가려졌으면 true.</returns>
        private bool SnapHidden(Vector3 target)
        {
            Camera3D camera = m_view.Camera;
            Vector3 origin = camera.ProjectRayOrigin(camera.UnprojectPosition(target));
            Vector3 toTarget = target - origin;
            float reach = toTarget.Length() - k_blockOcclusionSlack;
            if (reach <= 0f) return false;

            return PickFace(origin, toTarget.Normalized(), m_snapExcluded, out float distance) >= 0 && distance < reach;
        }
    }
}

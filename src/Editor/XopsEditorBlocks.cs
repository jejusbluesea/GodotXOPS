using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 무엇을 편집하는 중인지. 선택과 변형은 지금 모드의 것에만 듣는다.
        /// </summary>
        private enum EditMode
        {
            Point,
            Block,
            // 미션 파일의 설정. 3D 화면 대신 칸들이 있는 화면이 보인다.
            Mission,
            // 데이터 파일(godotdata 와 에드온의 JSON). 3D 화면 대신 파일 목록과 내용의 트리가 보인다.
            Asset,
        }

        // 블록 요소의 키: 블록 번호 × ElementStride + 요소 번호 (꼭짓점 0~7, 모서리 0~11, 면 0~5, 블록 단위는 0).
        public const int ElementStride = 16;

        // 클릭으로 고를 때 꼭짓점이나 모서리가 화면에서 이 거리(픽셀) 안에 있어야 한다.
        private const float k_blockPickRadiusPixels = 10f;
        // 화면 거리가 이만큼(픽셀)도 차이 나지 않으면 겹친 것으로 보고 카메라에 가까운 쪽을 고른다.
        private const float k_pickTiePixels = 0.5f;
        // 두 꼭짓점을 같은 자리로 보는 거리 (m).
        private const float k_coincidentDistance = 1e-3f;
        // 가려졌는지 볼 때 대상 바로 앞에서 레이를 멈추는 거리 (m). 대상이 놓인 면 자체에 레이가 닿아 가려진 것으로 치지 않게 한다.
        private const float k_blockOcclusionSlack = 0.05f;
        // 새 블록의 한 변 (m).
        private const float k_newBlockSize = 1f;
        // 겹친 것 가운데 하나를 고르는 메뉴에서 "전부"의 항목 번호.
        private const int k_coincidentAll = -1;

        private static readonly int[] s_elementCounts = { BD2Block.VertexCount, 12, BD2Block.FaceCount, 1 };
        // 새 블록의 꼭짓점 자리 (가운데 기준, 한 변이 1 일 때). BD1 과 같은 순서다: 0~3 이 윗면, 4~7 이 아랫면.
        private static readonly Vector3[] s_boxCorners =
        {
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
        };
        private static readonly Vector2[] s_faceUVs = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

        private EditMode m_editMode = EditMode.Point;
        private BlockElement m_blockElement = BlockElement.Vertex;
        private readonly SortedSet<int> m_blockSelection = new SortedSet<int>();
        private BlockOverlay m_blockOverlay;
        // 저장한 뒤로 블록이 바뀌었는지.
        private bool m_blockDirty;
        private PopupMenu m_coincidentMenu;
        private readonly List<int> m_coincidentKeys = new List<int>();
        private bool m_coincidentExtend;

        /// <summary>
        /// 편집 모드를 바꾼다 (Tab). 포인트 모드에서는 포인트만, 블록 모드에서는 블록의 꼭짓점·모서리·면·블록만 선택된다.
        /// </summary>
        /// <param name="mode">새 모드.</param>
        private void SetEditMode(EditMode mode)
        {
            if (Transforming) CancelTransform();
            CancelPick();
            // 미션이나 에셋 모드에서 고친 데이터가 포인트의 모델에 보이게 한다.
            bool backToView = m_editMode >= EditMode.Mission && mode < EditMode.Mission;
            m_editMode = mode;
            if (backToView && m_markers != null && m_markers.ShowModels) RebuildPoints();
            if (m_missionPanel != null) m_missionPanel.Visible = mode == EditMode.Mission;
            if (m_assetPanel != null) m_assetPanel.Visible = mode == EditMode.Asset;
            if (mode == EditMode.Mission) ApplyMission();
            if (mode == EditMode.Asset) RefreshAssetList();
            SyncModeButtons();
            RedrawBlocks();
            RedrawLinks();
            UpdateDetails();
            switch (mode)
            {
                case EditMode.Block: SetMessage("Block mode: 1 vertex, 2 edge, 3 face, 4 block"); break;
                case EditMode.Mission: SetMessage("Mission mode: settings of the mission file (MIF2)"); break;
                case EditMode.Asset: SetMessage("Asset mode: data files (godotdata and addon JSON)"); break;
                default: SetMessage("Point mode"); break;
            }
        }

        /// <summary>
        /// 블록의 선택 단위를 바꾼다 (1 꼭짓점, 2 모서리, 3 면, 4 블록). 단위가 바뀌면 선택을 푼다.
        /// </summary>
        /// <param name="element">새 단위.</param>
        private void SetBlockElement(BlockElement element)
        {
            if (Transforming) CancelTransform();
            if (m_blockElement != element) m_blockSelection.Clear();
            m_blockElement = element;
            SyncModeButtons();
            RedrawBlocks();
            UpdateDetails();
        }

        /// <summary>
        /// 블록의 덧그림(모서리 선, 꼭짓점, 선택 강조)을 문서의 지금 내용으로 다시 그린다. 포인트 모드에서는 감춘다.
        /// </summary>
        private void RedrawBlocks()
        {
            if (m_blockOverlay == null) return;

            m_blockOverlay.Visible = m_editMode == EditMode.Block;
            if (m_blockOverlay.Visible) m_blockOverlay.Redraw(m_document.Blocks.blocks, m_blockElement, m_blockSelection);
        }

        /// <summary>
        /// 문서의 블록으로 화면의 블록 메시와 판정을 다시 만든다 (블록을 고친 뒤). 없어진 블록을 가리키던 선택은 버린다.
        /// </summary>
        private void RebuildBlocks()
        {
            int count = m_document.Blocks.blocks.Count;
            m_blockSelection.RemoveWhere(key => key / ElementStride >= count);
            MapLoader.LoadBlockData(m_document.Blocks, m_document.Textures);
            MapLoader.ClearFog();
            RedrawBlocks();
            UpdateDetails();
        }

        /// <summary>
        /// 요소 하나를 이루는 꼭짓점들을 모은다.
        /// </summary>
        /// <param name="element">요소의 단위.</param>
        /// <param name="key">요소의 키.</param>
        /// <param name="vertexKeys">꼭짓점 키(블록 번호 × ElementStride + 꼭짓점 번호)를 더할 집합.</param>
        private static void CollectVertices(BlockElement element, int key, ISet<int> vertexKeys)
        {
            int blockBase = key / ElementStride * ElementStride;
            int index = key % ElementStride;
            switch (element)
            {
                case BlockElement.Vertex:
                    vertexKeys.Add(key);
                    break;
                case BlockElement.Edge:
                    vertexKeys.Add(blockBase + BlockOverlay.Edges[index][0]);
                    vertexKeys.Add(blockBase + BlockOverlay.Edges[index][1]);
                    break;
                case BlockElement.Face:
                    foreach (int vertex in MapLoader.BlockFaceVertices[index])
                    {
                        vertexKeys.Add(blockBase + vertex);
                    }
                    break;
                default:
                    for (int vertex = 0; vertex < BD2Block.VertexCount; vertex++)
                    {
                        vertexKeys.Add(blockBase + vertex);
                    }
                    break;
            }
        }

        /// <summary>
        /// 요소의 가운데 (꼭짓점은 그 자리, 모서리·면·블록은 꼭짓점들의 평균).
        /// </summary>
        /// <param name="element">요소의 단위.</param>
        /// <param name="key">요소의 키.</param>
        /// <returns>월드 좌표.</returns>
        private Vector3 ElementCenter(BlockElement element, int key)
        {
            var vertexKeys = new SortedSet<int>();
            CollectVertices(element, key, vertexKeys);
            Vector3 sum = Vector3.Zero;
            foreach (int vertexKey in vertexKeys)
            {
                sum += VertexPosition(vertexKey);
            }
            return sum / vertexKeys.Count;
        }

        /// <summary>
        /// 꼭짓점 키가 가리키는 자리.
        /// </summary>
        /// <param name="vertexKey">꼭짓점 키.</param>
        /// <returns>월드 좌표.</returns>
        private Vector3 VertexPosition(int vertexKey)
        {
            return m_document.Blocks.blocks[vertexKey / ElementStride].vertices[vertexKey % ElementStride];
        }

        /// <summary>
        /// 카메라에서 한 점이 블록에 가려 보이지 않는지 본다.
        /// </summary>
        /// <param name="target">볼 점.</param>
        /// <returns>블록이 사이에 있으면 true.</returns>
        private bool HiddenByBlocks(Vector3 target)
        {
            Camera3D camera = m_view.Camera;
            Vector3 origin = camera.ProjectRayOrigin(camera.UnprojectPosition(target));
            Vector3 toTarget = target - origin;
            float distance = toTarget.Length() - k_blockOcclusionSlack;
            return distance > 0f && MapLoader.RaycastBlock(BlockLayer.Sight, origin, toTarget.Normalized(), distance, out _);
        }

        /// <summary>
        /// 요소가 지금 선택할 수 있게 보이는지 (X-RAY 가 꺼져 있을 때의 조건). 블록 단위는 꼭짓점이 하나라도 보이면 보이는 것으로 친다.
        /// </summary>
        /// <param name="element">요소의 단위.</param>
        /// <param name="key">요소의 키.</param>
        /// <returns>보이면 true.</returns>
        private bool ElementVisible(BlockElement element, int key)
        {
            if (element != BlockElement.Block) return !HiddenByBlocks(ElementCenter(element, key));

            for (int vertex = 0; vertex < BD2Block.VertexCount; vertex++)
            {
                if (!HiddenByBlocks(VertexPosition(key + vertex))) return true;
            }
            return false;
        }

        /// <summary>
        /// 블록 모드에서 화면의 한 점을 눌렀을 때의 선택. 꼭짓점과 모서리는 같은 자리에 겹친 다른 블록의 것까지 함께 선택한다
        /// (블록끼리 꼭짓점을 공유하지 않아서, 맞닿은 블록의 모서리를 함께 옮기려면 그래야 한다).
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <param name="extend">Shift 를 누르고 있었는지 (더하거나 빼기).</param>
        /// <param name="choose">Ctrl 을 누르고 있었거나 Ask overlap 이 켜져 있는지. 겹친 것이 여럿이면 어느 블록의 것을 고를지 메뉴로 묻는다.</param>
        private void BlockSelectAt(Vector2 screenPosition, bool extend, bool choose)
        {
            if (Transforming) return;

            m_coincidentKeys.Clear();
            int picked = PickElement(screenPosition);
            if (picked >= 0) CollectCoincident(picked, m_coincidentKeys);

            if (choose && m_coincidentKeys.Count > 1)
            {
                ShowCoincidentMenu(screenPosition, extend);
                return;
            }
            ApplyBlockPick(m_coincidentKeys, extend);
        }

        /// <summary>
        /// 고른 요소들을 선택에 반영한다. Shift 가 없으면 그것들만 선택하고(빈 곳이면 해제), 있으면 더하거나 뺀다.
        /// </summary>
        /// <param name="keys">고른 요소의 키들.</param>
        /// <param name="extend">Shift 를 누르고 있었는지.</param>
        private void ApplyBlockPick(List<int> keys, bool extend)
        {
            if (!extend)
            {
                m_blockSelection.Clear();
                m_blockSelection.UnionWith(keys);
            }
            else if (keys.Count > 0)
            {
                bool allSelected = keys.TrueForAll(m_blockSelection.Contains);
                foreach (int key in keys)
                {
                    if (allSelected) m_blockSelection.Remove(key);
                    else m_blockSelection.Add(key);
                }
            }
            RedrawBlocks();
            UpdateDetails();
        }

        /// <summary>
        /// 같은 자리에 겹친 요소들 가운데 무엇을 고를지 묻는 메뉴를 띄운다 (Ctrl + 클릭).
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <param name="extend">Shift 를 누르고 있었는지.</param>
        private void ShowCoincidentMenu(Vector2 screenPosition, bool extend)
        {
            m_coincidentExtend = extend;
            m_coincidentMenu.Clear();
            m_coincidentMenu.AddItem($"All ({m_coincidentKeys.Count})", k_coincidentAll);
            foreach (int key in m_coincidentKeys)
            {
                m_coincidentMenu.AddItem($"Block #{key / ElementStride}  {m_blockElement.ToString().ToLowerInvariant()} {key % ElementStride}", key);
            }
            m_coincidentMenu.Position = (Vector2I)(GetWindow().Position + screenPosition);
            m_coincidentMenu.Popup();
        }

        /// <summary>
        /// 겹친 것 메뉴에서 하나를 골랐을 때.
        /// </summary>
        /// <param name="id">고른 요소의 키. k_coincidentAll 이면 전부.</param>
        private void OnCoincidentChosen(long id)
        {
            var keys = new List<int>(m_coincidentKeys);
            if (id != k_coincidentAll) keys.RemoveAll(key => key != (int)id);
            ApplyBlockPick(keys, m_coincidentExtend);
        }

        /// <summary>
        /// 화면의 한 점에서 지금 단위의 요소 하나를 고른다. 꼭짓점과 모서리는 화면 거리로, 면과 블록은 그 자리로 쏜 레이가 처음 만나는 면으로 고른다.
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <returns>요소의 키. 없으면 −1.</returns>
        private int PickElement(Vector2 screenPosition)
        {
            Camera3D camera = m_view.Camera;
            List<BD2Block> blocks = m_document.Blocks.blocks;

            if (m_blockElement == BlockElement.Face || m_blockElement == BlockElement.Block)
            {
                int face = PickFace(camera.ProjectRayOrigin(screenPosition), camera.ProjectRayNormal(screenPosition));
                if (face < 0) return -1;
                return m_blockElement == BlockElement.Face ? face : face / ElementStride * ElementStride;
            }

            int best = -1;
            float bestDistance = float.MaxValue;
            float bestDepth = float.MaxValue;
            for (int b = 0; b < blocks.Count; b++)
            {
                for (int e = 0; e < s_elementCounts[(int)m_blockElement]; e++)
                {
                    int key = b * ElementStride + e;
                    float distance;
                    Vector3 depthPoint;
                    if (m_blockElement == BlockElement.Vertex)
                    {
                        depthPoint = blocks[b].vertices[e];
                        if (camera.IsPositionBehind(depthPoint)) continue;
                        distance = camera.UnprojectPosition(depthPoint).DistanceTo(screenPosition);
                    }
                    else
                    {
                        Vector3 from = blocks[b].vertices[BlockOverlay.Edges[e][0]];
                        Vector3 to = blocks[b].vertices[BlockOverlay.Edges[e][1]];
                        if (camera.IsPositionBehind(from) || camera.IsPositionBehind(to)) continue;
                        Vector2 a = camera.UnprojectPosition(from);
                        Vector2 c = camera.UnprojectPosition(to);
                        distance = Geometry2D.GetClosestPointToSegment(screenPosition, a, c).DistanceTo(screenPosition);
                        depthPoint = (from + to) * 0.5f;
                    }
                    if (distance > k_blockPickRadiusPixels) continue;

                    // 화면에서 더 가까운 것을 고르고, 거의 같은 자리(겹친 것)면 카메라에 가까운 것을 고른다.
                    float depth = camera.GlobalPosition.DistanceSquaredTo(depthPoint);
                    bool better = best < 0 || distance < bestDistance - k_pickTiePixels
                        || (Mathf.Abs(distance - bestDistance) <= k_pickTiePixels && depth < bestDepth);
                    if (!better || (!m_xray && !ElementVisible(m_blockElement, key))) continue;

                    best = key;
                    bestDistance = distance;
                    bestDepth = depth;
                }
            }
            return best;
        }

        /// <summary>
        /// 레이가 처음 만나는 블록 면을 찾는다. 문서의 블록에서 직접 계산하므로 통과 플래그가 켜진 블록도 고를 수 있다. 카메라를 향한 면만 맞는다.
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향 (정규화).</param>
        /// <returns>면의 키. 없으면 −1.</returns>
        private int PickFace(Vector3 origin, Vector3 direction)
        {
            return PickFace(origin, direction, null, out _);
        }

        /// <summary>
        /// 레이가 처음 만나는 블록 면과 그 거리를 찾는다. 몇몇 블록을 건너뛸 수 있다 (옮기는 중인 블록).
        /// </summary>
        /// <param name="origin">레이 시작점.</param>
        /// <param name="direction">레이 방향 (정규화).</param>
        /// <param name="excluded">건너뛸 블록의 번호들. null 이면 전부 본다.</param>
        /// <param name="hitDistance">만난 자리까지의 거리 (m). 없으면 0.</param>
        /// <returns>면의 키. 없으면 −1.</returns>
        private int PickFace(Vector3 origin, Vector3 direction, ISet<int> excluded, out float hitDistance)
        {
            List<BD2Block> blocks = m_document.Blocks.blocks;
            IReadOnlyList<int[]> faces = MapLoader.BlockFaceVertices;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int b = 0; b < blocks.Count; b++)
            {
                if (excluded != null && excluded.Contains(b)) continue;

                Vector3[] vertices = blocks[b].vertices;
                Vector3 blockCenter = Vector3.Zero;
                foreach (Vector3 vertex in vertices)
                {
                    blockCenter += vertex;
                }
                blockCenter /= vertices.Length;

                for (int f = 0; f < faces.Count; f++)
                {
                    Vector3 v0 = vertices[faces[f][0]];
                    Vector3 v1 = vertices[faces[f][1]];
                    Vector3 v2 = vertices[faces[f][2]];
                    Vector3 v3 = vertices[faces[f][3]];
                    // 블록의 가운데에서 바깥을 향하는 쪽이 면의 앞이다. 뒤에서 본 면은 건너뛴다.
                    Vector3 outward = (v0 + v1 + v2 + v3) * 0.25f - blockCenter;
                    if (outward.Dot(direction) >= 0f) continue;

                    Variant hit = Geometry3D.RayIntersectsTriangle(origin, direction, v0, v1, v2);
                    if (hit.VariantType == Variant.Type.Nil) hit = Geometry3D.RayIntersectsTriangle(origin, direction, v0, v2, v3);
                    if (hit.VariantType == Variant.Type.Nil) continue;

                    float distance = origin.DistanceTo(hit.AsVector3());
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = b * ElementStride + f;
                }
            }
            hitDistance = best >= 0 ? bestDistance : 0f;
            return best;
        }

        /// <summary>
        /// 한 요소와 같은 자리에 겹친 요소들을 모은다 (자신 포함). 꼭짓점은 같은 자리의 꼭짓점, 모서리는 두 끝이 같은 모서리다. 면과 블록은 자신뿐이다.
        /// </summary>
        /// <param name="key">기준 요소의 키.</param>
        /// <param name="result">겹친 요소의 키를 더할 목록 (기준이 맨 앞).</param>
        private void CollectCoincident(int key, List<int> result)
        {
            result.Add(key);
            if (m_blockElement != BlockElement.Vertex && m_blockElement != BlockElement.Edge) return;

            List<BD2Block> blocks = m_document.Blocks.blocks;
            var ends = new SortedSet<int>();
            CollectVertices(m_blockElement, key, ends);
            var endPositions = new List<Vector3>();
            foreach (int end in ends)
            {
                endPositions.Add(VertexPosition(end));
            }

            for (int b = 0; b < blocks.Count; b++)
            {
                for (int e = 0; e < s_elementCounts[(int)m_blockElement]; e++)
                {
                    int other = b * ElementStride + e;
                    if (other == key) continue;

                    bool same;
                    if (m_blockElement == BlockElement.Vertex)
                    {
                        same = blocks[b].vertices[e].DistanceTo(endPositions[0]) < k_coincidentDistance;
                    }
                    else
                    {
                        Vector3 from = blocks[b].vertices[BlockOverlay.Edges[e][0]];
                        Vector3 to = blocks[b].vertices[BlockOverlay.Edges[e][1]];
                        same = (from.DistanceTo(endPositions[0]) < k_coincidentDistance && to.DistanceTo(endPositions[1]) < k_coincidentDistance)
                            || (from.DistanceTo(endPositions[1]) < k_coincidentDistance && to.DistanceTo(endPositions[0]) < k_coincidentDistance);
                    }
                    if (same) result.Add(other);
                }
            }
        }

        /// <summary>
        /// 블록 모드의 사각형 선택. 가운데가 사각형 안에 있는 요소를 전부 고른다. X-RAY 가 꺼져 있으면 보이는 것만 고른다.
        /// </summary>
        /// <param name="rect">화면 사각형 (픽셀).</param>
        /// <param name="extend">Shift 를 누르고 있었는지 (더하기).</param>
        private void BlockSelectBox(Rect2 rect, bool extend)
        {
            Camera3D camera = m_view.Camera;
            int blockCount = m_document.Blocks.blocks.Count;
            if (!extend) m_blockSelection.Clear();

            for (int b = 0; b < blockCount; b++)
            {
                for (int e = 0; e < s_elementCounts[(int)m_blockElement]; e++)
                {
                    int key = b * ElementStride + e;
                    Vector3 center = ElementCenter(m_blockElement, key);
                    if (camera.IsPositionBehind(center) || !rect.HasPoint(camera.UnprojectPosition(center))) continue;
                    if (!m_xray && !ElementVisible(m_blockElement, key)) continue;

                    m_blockSelection.Add(key);
                }
            }
            RedrawBlocks();
            UpdateDetails();
        }

        /// <summary>
        /// 지금 단위의 요소를 전부 선택하거나 선택을 푼다.
        /// </summary>
        /// <param name="all">true 면 전부 선택, false 면 해제.</param>
        private void BlockSelectAll(bool all)
        {
            m_blockSelection.Clear();
            if (all)
            {
                for (int b = 0; b < m_document.Blocks.blocks.Count; b++)
                {
                    for (int e = 0; e < s_elementCounts[(int)m_blockElement]; e++)
                    {
                        m_blockSelection.Add(b * ElementStride + e);
                    }
                }
            }
            RedrawBlocks();
            UpdateDetails();
        }

        /// <summary>
        /// 선택한 요소들이 속한 블록의 번호를 모은다.
        /// </summary>
        /// <returns>블록 번호들 (오름차순).</returns>
        private SortedSet<int> SelectedBlocks()
        {
            var result = new SortedSet<int>();
            foreach (int key in m_blockSelection)
            {
                result.Add(key / ElementStride);
            }
            return result;
        }

        /// <summary>
        /// 블록들을 블록 단위로 선택한다.
        /// </summary>
        /// <param name="blockIndices">블록 번호들.</param>
        private void SelectWholeBlocks(IEnumerable<int> blockIndices)
        {
            m_blockElement = BlockElement.Block;
            m_blockSelection.Clear();
            foreach (int index in blockIndices)
            {
                m_blockSelection.Add(index * ElementStride);
            }
            SyncModeButtons();
        }

        /// <summary>
        /// 새 상자 블록을 놓는다 (블록 모드의 Shift+A). 화면의 그 자리 아래에 블록 면이 있으면 그 위에 올리고, 없으면 시점의 중심에 놓는다.
        /// 모든 면은 텍스처 0번이고 UV 는 면 하나에 텍스처 한 장이다.
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        private void AddBlock(Vector2 screenPosition)
        {
            if (Transforming) CancelTransform();

            List<BD2Block> blocks = m_document.Blocks.blocks;
            BD2Block[] before = BlockListCommand.Snapshot(blocks);
            Vector3 center = SurfaceUnder(screenPosition, out Vector3 hit) ? hit + Vector3.Up * (k_newBlockSize * 0.5f) : m_view.Pivot;
            if (ActiveSnap().HasFlag(SnapTarget.Grid)) center = (center / m_gridSize).Round() * m_gridSize;

            var block = new BD2Block();
            for (int i = 0; i < BD2Block.VertexCount; i++)
            {
                block.vertices[i] = center + s_boxCorners[i] * k_newBlockSize;
            }
            for (int f = 0; f < BD2Block.FaceCount; f++)
            {
                block.materialIndices[f] = -1;
                for (int v = 0; v < BD2Block.FaceVertexCount; v++)
                {
                    block.uvs[f * BD2Block.FaceVertexCount + v] = s_faceUVs[v];
                }
            }
            blocks.Add(block);

            m_history.Push(new BlockListCommand("Add block", blocks, before));
            m_blockDirty = true;
            SelectWholeBlocks(new[] { blocks.Count - 1 });
            RebuildBlocks();
            SetMessage("Added a block");
        }

        /// <summary>
        /// 선택한 요소들이 속한 블록을 복제한다. 복제한 블록들이 블록 단위로 선택되고 바로 옮기기가 시작된다.
        /// </summary>
        private void DuplicateBlocks()
        {
            if (Transforming) CancelTransform();
            SortedSet<int> selected = SelectedBlocks();
            if (selected.Count == 0)
            {
                SetMessage("Select blocks first");
                return;
            }

            List<BD2Block> blocks = m_document.Blocks.blocks;
            BD2Block[] before = BlockListCommand.Snapshot(blocks);
            var copies = new List<int>();
            foreach (int index in selected)
            {
                blocks.Add(BlockChangeCommand.Clone(blocks[index]));
                copies.Add(blocks.Count - 1);
            }
            m_history.Push(new BlockListCommand("Duplicate blocks", blocks, before));
            m_blockDirty = true;
            SelectWholeBlocks(copies);
            RebuildBlocks();
            BeginTransform(TransformMode.Move);
        }

        /// <summary>
        /// 선택한 요소들이 속한 블록을 지운다. 꼭짓점이나 면만 지울 수는 없다 (블록은 늘 육면체다).
        /// </summary>
        private void DeleteBlocks()
        {
            if (Transforming) CancelTransform();
            SortedSet<int> selected = SelectedBlocks();
            if (selected.Count == 0) return;

            List<BD2Block> blocks = m_document.Blocks.blocks;
            BD2Block[] before = BlockListCommand.Snapshot(blocks);
            foreach (int index in selected.Reverse())
            {
                blocks.RemoveAt(index);
            }
            m_history.Push(new BlockListCommand("Delete blocks", blocks, before));
            m_blockDirty = true;
            m_blockSelection.Clear();
            RebuildBlocks();
            SetMessage($"Deleted {selected.Count} block(s)");
        }

        /// <summary>
        /// 블록 데이터를 지금 경로에 저장한다. 연 적이 없으면 경로를 묻는다.
        /// </summary>
        private void SaveBlocks()
        {
            if (Transforming) ConfirmTransform();
            if (string.IsNullOrEmpty(m_document.BlockPath))
            {
                ShowFileDialog(k_menuSaveBlocksAs, "Save blocks as", "*" + BD2File.Extension, true);
                return;
            }
            SaveBlocksTo(m_document.BlockPath);
        }

        /// <summary>
        /// 블록 데이터를 BD2 파일로, 텍스처 목록을 그 JSON 파일로 쓴다. 덮어쓰는 파일은 먼저 .bak 으로 남긴다.
        /// 다른 이름으로 저장하거나 텍스처 목록의 경로가 아직 없으면, 텍스처 목록은 블록 파일 옆의 "이름_textures.json" 이 된다 (원래 맵과 목록을 함께 쓰지 않게).
        /// </summary>
        /// <param name="relativePath">BD2 경로 (exe 폴더 기준).</param>
        /// <returns>저장했으면 true.</returns>
        private bool SaveBlocksTo(string relativePath)
        {
            if (!HasExtension(relativePath, BD2File.Extension)) relativePath += BD2File.Extension;
            string fullPath = GamePath.ResolveForWrite(relativePath, BD2File.Extension, out string pathError);
            if (fullPath == null) return Fail(pathError);

            // 텍스처 목록의 경로는 블록 파일에 적혀 있던 값이다. 받은 파일이 다른 파일을 가리킬 수 있으므로 쓰기 전에 확인한다.
            string textureListPath = m_document.Blocks.textureListPath;
            if (string.IsNullOrEmpty(textureListPath) || !string.Equals(relativePath, m_document.BlockPath, StringComparison.OrdinalIgnoreCase))
            {
                textureListPath = TextureListPathFor(relativePath);
            }
            string textureListFull = GamePath.ResolveForWrite(textureListPath, k_textureListExtension, out pathError);
            if (textureListFull == null) return Fail($"Texture list: {pathError} (use Save As to write a new one)");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + k_backupExtension, true);
                if (File.Exists(textureListFull)) File.Copy(textureListFull, textureListFull + k_backupExtension, true);

                File.WriteAllText(textureListFull, JsonData.ToJson(m_document.Textures));
                m_document.Blocks.textureListPath = textureListPath;
                if (!m_document.Blocks.Write(fullPath, out string error)) return Fail($"Block file write failed: {relativePath} ({error})");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail($"Block file write failed: {relativePath} ({e.Message})");
            }

            m_document.BlockPath = relativePath;
            m_blockDirty = false;
            SetMessage($"Saved {relativePath} ({m_document.Blocks.blocks.Count} blocks)");
            return true;
        }

        /// <summary>
        /// 블록 모드의 선택을 한 줄로 요약한다 (오른쪽 패널의 머리말).
        /// </summary>
        /// <returns>요약.</returns>
        private string DescribeBlockSelection()
        {
            string unit = m_blockElement.ToString().ToLowerInvariant();
            if (m_blockSelection.Count == 0)
            {
                return $"Block mode ({unit}).\n\nClick or drag a box to select.\n1 vertex, 2 edge, 3 face, 4 block.\nOverlapping vertices and edges are selected together.\nTo pick one block's, turn on Ask overlap above (or Ctrl+click).";
            }
            if (m_blockSelection.Count == 1)
            {
                int key = m_blockSelection.Min;
                return m_blockElement == BlockElement.Block ? $"Block #{key / ElementStride}" : $"Block #{key / ElementStride}  {unit} {key % ElementStride}";
            }
            return $"{m_blockSelection.Count} {unit} elements selected\nin {SelectedBlocks().Count} block(s)";
        }
    }
}

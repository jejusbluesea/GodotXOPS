using System.Collections.Generic;
using System.Globalization;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 진행 중인 변형의 종류.
        /// </summary>
        private enum TransformMode
        {
            None,
            Move,
            Rotate,
            Scale,
        }

        /// <summary>
        /// 변형을 묶어 둔 축.
        /// </summary>
        private enum TransformAxis
        {
            None,
            X,
            Y,
            Z,
        }

        // 격자에 붙일 때 돌리기가 붙는 각도 (도)와 크기 바꾸기가 붙는 배율.
        private const float k_rotateSnapDegrees = 5f;
        private const float k_scaleSnap = 0.1f;
        // 크기 바꾸기의 배율 하한. 0 이면 꼭짓점이 한 점으로 모여 되돌릴 수밖에 없다.
        private const float k_minScale = 0.01f;
        // 숫자 입력으로 받는 글자 수의 상한.
        private const int k_maxNumericChars = 12;
        // 격자 단위의 기본값 (m). 원본 XOPS 의 1 단위와 같다. 화면에서 바꿀 수 있다.
        private const float k_defaultGridSize = 0.1f;
        private const float k_minGridSize = 0.01f;
        private const float k_maxGridSize = 100f;
        // 격자에 붙일 때, 이만큼(m)도 움직이지 않은 축은 건드리지 않는다 (위에서 보며 옮길 때 높이가 격자로 튀지 않게).
        private const float k_snapIdleDistance = 1e-4f;
        // 면에 붙인 채 격자에 맞출 때, 맞춘 자리의 면 높이를 다시 찾는 레이의 시작 높이와 길이 (m).
        private const float k_surfaceProbeUp = 2f;
        private const float k_surfaceProbeLength = 6f;

        private readonly EditorHistory m_history = new EditorHistory();
        private TransformMode m_transformMode = TransformMode.None;
        private TransformAxis m_transformAxis = TransformAxis.None;
        private string m_numeric = string.Empty;
        // 변형하는 것들의 시작 위치. 포인트 모드에서는 포인트마다, 블록 모드에서는 꼭짓점마다 하나다. 맨 앞의 것이 격자와 면에 붙일 때의 기준이다.
        private Vector3[] m_transformStarts;
        // 포인트 모드: 변형하는 포인트의 번호와 시작했을 때의 값.
        private int[] m_transformIndices;
        private PD2Point[] m_transformBefore;
        // 블록 모드: 변형하는 꼭짓점의 키, 그것들이 속한 블록의 번호와 시작했을 때의 내용.
        private int[] m_transformVertexKeys;
        private int[] m_transformBlockIndices;
        private BD2Block[] m_transformBlockBefore;
        private bool m_transformingBlocks;
        // 변형을 시작했을 때의 선택의 가운데와 마우스 자리, 지금의 마우스 자리.
        private Vector3 m_transformCenter;
        private Vector2 m_transformStartMouse;
        private Vector2 m_transformMouse;
        private bool m_gridLock;
        private float m_gridSize = k_defaultGridSize;
        // 옮길 때 마우스 아래의 블록 면에 붙일지 (Surface). 사람이나 무기처럼 바닥에 놓는 것을 옮길 때 쓴다. 포인트에만 듣고, 축을 묶거나 숫자를 치면 듣지 않는다.
        private bool m_surfaceSnap = true;
        // 저장한 뒤로 포인트가 바뀌었는지.
        private bool m_dirty;

        // 변형(옮기기, 돌리기, 크기 바꾸기)이 진행 중인지. 진행 중에는 클릭이 선택이 아니라 확정이고, 오른쪽 버튼은 취소다.
        private bool Transforming => m_transformMode != TransformMode.None;

        /// <summary>
        /// 선택한 것의 변형을 시작한다 (G 옮기기, R 돌리기, S 크기 바꾸기). 포인트 모드에서는 포인트를, 블록 모드에서는 선택한 요소의 꼭짓점을 움직인다.
        /// 마우스를 움직이면 미리 보이고, 왼쪽 클릭이나 Enter 로 확정, 오른쪽 클릭이나 Esc 로 취소한다. 도중에 X / Y / Z 로 축을 묶고, 숫자를 쳐서 값을 정할 수 있다.
        /// </summary>
        /// <param name="mode">변형의 종류.</param>
        private void BeginTransform(TransformMode mode)
        {
            if (Transforming) CancelTransform();
            if (mode == TransformMode.None) return;

            m_transformingBlocks = m_editMode == EditMode.Block;
            if (!(m_transformingBlocks ? CollectBlockTargets() : CollectPointTargets()))
            {
                SetMessage(m_transformingBlocks ? "Select block elements first" : "Select points first");
                return;
            }

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Vector3 start in m_transformStarts)
            {
                min = min.Min(start);
                max = max.Max(start);
            }

            m_transformMode = mode;
            m_transformAxis = TransformAxis.None;
            m_numeric = string.Empty;
            m_transformCenter = (min + max) * 0.5f;
            m_transformStartMouse = GetViewport().GetMousePosition();
            m_transformMouse = m_transformStartMouse;
            UpdateTransform();
        }

        /// <summary>
        /// 포인트 모드의 변형 대상을 모은다: 선택한 포인트들.
        /// </summary>
        /// <returns>대상이 있으면 true.</returns>
        private bool CollectPointTargets()
        {
            if (m_selection.Count == 0) return false;

            List<PD2Point> points = m_document.Points.points;
            m_transformIndices = new int[m_selection.Count];
            m_transformBefore = new PD2Point[m_selection.Count];
            m_transformStarts = new Vector3[m_selection.Count];
            int slot = 0;
            foreach (int index in m_selection)
            {
                m_transformIndices[slot] = index;
                m_transformBefore[slot] = PointChangeCommand.Clone(points[index]);
                m_transformStarts[slot] = points[index].position;
                slot++;
            }
            return true;
        }

        /// <summary>
        /// 블록 모드의 변형 대상을 모은다: 선택한 요소(꼭짓점, 모서리, 면, 블록)를 이루는 꼭짓점들.
        /// </summary>
        /// <returns>대상이 있으면 true.</returns>
        private bool CollectBlockTargets()
        {
            if (m_blockSelection.Count == 0) return false;

            var vertexKeys = new SortedSet<int>();
            foreach (int key in m_blockSelection)
            {
                CollectVertices(m_blockElement, key, vertexKeys);
            }
            var blockIndices = new SortedSet<int>();
            foreach (int key in vertexKeys)
            {
                blockIndices.Add(key / ElementStride);
            }

            List<BD2Block> blocks = m_document.Blocks.blocks;
            m_transformVertexKeys = new int[vertexKeys.Count];
            vertexKeys.CopyTo(m_transformVertexKeys);
            m_transformStarts = new Vector3[m_transformVertexKeys.Length];
            for (int i = 0; i < m_transformVertexKeys.Length; i++)
            {
                m_transformStarts[i] = VertexPosition(m_transformVertexKeys[i]);
            }
            m_transformBlockIndices = new int[blockIndices.Count];
            blockIndices.CopyTo(m_transformBlockIndices);
            m_transformBlockBefore = new BD2Block[m_transformBlockIndices.Length];
            for (int i = 0; i < m_transformBlockIndices.Length; i++)
            {
                m_transformBlockBefore[i] = BlockChangeCommand.Clone(blocks[m_transformBlockIndices[i]]);
            }
            return true;
        }

        /// <summary>
        /// 변형을 확정하고 되돌리기 기록에 남긴다. 블록을 변형했으면 화면의 블록 메시와 판정을 다시 만든다.
        /// </summary>
        private void ConfirmTransform()
        {
            if (!Transforming) return;

            string name = m_transformMode.ToString();
            m_transformMode = TransformMode.None;
            if (m_transformingBlocks)
            {
                m_history.Push(new BlockChangeCommand(name, m_document.Blocks.blocks, m_transformBlockIndices, m_transformBlockBefore));
                m_blockDirty = true;
                RebuildBlocks();
                SetMessage($"{name}: {m_transformVertexKeys.Length} vertices in {m_transformBlockIndices.Length} block(s)");
                return;
            }

            m_history.Push(new PointChangeCommand(name, m_document.Points.points, m_transformIndices, m_transformBefore));
            m_dirty = true;
            UpdateDetails();
            SetMessage($"{name}: {m_transformIndices.Length} point(s)");
        }

        /// <summary>
        /// 변형을 취소하고 시작했을 때의 값으로 되돌린다.
        /// </summary>
        private void CancelTransform()
        {
            if (!Transforming) return;

            m_transformMode = TransformMode.None;
            if (m_transformingBlocks)
            {
                List<BD2Block> blocks = m_document.Blocks.blocks;
                for (int i = 0; i < m_transformBlockIndices.Length; i++)
                {
                    BlockChangeCommand.Copy(m_transformBlockBefore[i], blocks[m_transformBlockIndices[i]]);
                }
                RedrawBlocks();
            }
            else
            {
                List<PD2Point> points = m_document.Points.points;
                for (int i = 0; i < m_transformIndices.Length; i++)
                {
                    PointChangeCommand.Copy(m_transformBefore[i], points[m_transformIndices[i]]);
                    m_markers.Refresh(m_transformIndices[i]);
                }
            }
            m_linksDirty = true;
            UpdateDetails();
            SetMessage("Cancelled");
        }

        /// <summary>
        /// 변형을 한 축에 묶는다. 같은 축을 다시 누르면 푼다.
        /// 포인트의 돌리기는 세로축 둘레로만 되므로 축을 받지 않는다 (포인트의 방향은 yaw 하나다). 블록의 돌리기는 고른 축 둘레로 돈다 (기본은 세로축).
        /// </summary>
        /// <param name="axis">축.</param>
        private void SetTransformAxis(TransformAxis axis)
        {
            if (!Transforming || (m_transformMode == TransformMode.Rotate && !m_transformingBlocks)) return;

            m_transformAxis = m_transformAxis == axis ? TransformAxis.None : axis;
            UpdateTransform();
        }

        /// <summary>
        /// 변형 도중의 숫자 입력. 숫자, 소수점, 맨 앞의 빼기 기호를 받고 Backspace 로 지운다. 숫자가 있으면 마우스 대신 그 값을 쓴다.
        /// </summary>
        /// <param name="key">키 이벤트.</param>
        /// <returns>이 키를 숫자 입력으로 썼으면 true.</returns>
        private bool TypeNumeric(InputEventKey key)
        {
            if (key.Keycode == Key.Backspace || key.PhysicalKeycode == Key.Backspace)
            {
                if (m_numeric.Length == 0) return false;
                m_numeric = m_numeric.Substring(0, m_numeric.Length - 1);
                UpdateTransform();
                return true;
            }

            char typed = (char)key.Unicode;
            bool digit = typed >= '0' && typed <= '9';
            bool point = typed == '.' && !m_numeric.Contains('.');
            bool minus = typed == '-' && m_numeric.Length == 0;
            if ((!digit && !point && !minus) || m_numeric.Length >= k_maxNumericChars) return false;

            m_numeric += typed;
            UpdateTransform();
            return true;
        }

        /// <summary>
        /// 지금의 마우스 자리, 축, 숫자 입력으로 대상을 다시 놓는다 (미리 보기).
        /// </summary>
        private void UpdateTransform()
        {
            if (!Transforming) return;

            bool hasNumber = float.TryParse(m_numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out float number);
            bool snap = m_gridLock || Input.IsKeyPressed(Key.Ctrl);
            string typedText = hasNumber ? $" [{m_numeric}]" : string.Empty;
            const string hint = "   |   X/Y/Z: axis, numbers: value, Ctrl: snap, click/Enter: confirm, right click/Esc: cancel";

            switch (m_transformMode)
            {
                case TransformMode.Move:
                {
                    // 맨 앞의 것이 기준이다: 면에 붙일 때도 격자에 맞출 때도 이것이 그 자리에 오고, 나머지는 같은 만큼 따라온다.
                    Vector3 anchor = m_transformStarts[0];
                    bool onSurface = false;
                    Vector3 delta;
                    if (hasNumber)
                    {
                        delta = AxisVector(m_transformAxis == TransformAxis.None ? TransformAxis.X : m_transformAxis) * number;
                    }
                    else if (m_surfaceSnap && !m_transformingBlocks && m_transformAxis == TransformAxis.None && SurfaceUnder(m_transformMouse, out Vector3 hit))
                    {
                        delta = hit - anchor;
                        onSurface = true;
                    }
                    else
                    {
                        delta = MouseMoveDelta();
                    }
                    if (snap && !hasNumber) delta = SnapToGrid(anchor, delta, onSurface);

                    for (int i = 0; i < m_transformStarts.Length; i++)
                    {
                        ApplyTransformed(i, m_transformStarts[i] + delta, 0f);
                    }
                    SetMessage(string.Format(CultureInfo.InvariantCulture, "Move{0}: {1:0.00}, {2:0.00}, {3:0.00} m{4}{5}",
                        m_transformAxis == TransformAxis.None ? (onSurface ? " on surface" : string.Empty) : " along " + m_transformAxis, delta.X, delta.Y, delta.Z, typedText, hint));
                    break;
                }

                case TransformMode.Rotate:
                {
                    // yaw 가 커지는 쪽(축의 + 쪽에서 내려다볼 때 시계 방향)으로 선택의 가운데 둘레를 돌린다. 포인트는 방향도 같은 만큼 돈다.
                    TransformAxis axis = m_transformingBlocks && m_transformAxis != TransformAxis.None ? m_transformAxis : TransformAxis.Y;
                    Vector3 axisVector = AxisVector(axis);
                    float angle = hasNumber ? number : MouseRotateAngle(axisVector);
                    if (snap && !hasNumber) angle = Mathf.Round(angle / k_rotateSnapDegrees) * k_rotateSnapDegrees;
                    float radians = Mathf.DegToRad(angle);
                    for (int i = 0; i < m_transformStarts.Length; i++)
                    {
                        Vector3 offset = m_transformStarts[i] - m_transformCenter;
                        ApplyTransformed(i, m_transformCenter + offset.Rotated(axisVector, -radians), angle);
                    }
                    SetMessage(string.Format(CultureInfo.InvariantCulture, "Rotate around {0}: {1:0.0} deg{2}{3}", axis, angle, typedText, hint));
                    break;
                }

                case TransformMode.Scale:
                {
                    float factor = hasNumber ? number : MouseScaleFactor();
                    if (snap && !hasNumber) factor = Mathf.Round(factor / k_scaleSnap) * k_scaleSnap;
                    factor = Mathf.Max(k_minScale, factor);
                    Vector3 scale = m_transformAxis == TransformAxis.None ? Vector3.One * factor : Vector3.One + AxisVector(m_transformAxis).Abs() * (factor - 1f);
                    for (int i = 0; i < m_transformStarts.Length; i++)
                    {
                        ApplyTransformed(i, m_transformCenter + (m_transformStarts[i] - m_transformCenter) * scale, 0f);
                    }
                    SetMessage(string.Format(CultureInfo.InvariantCulture, "Scale{0}: {1:0.00}{2}{3}",
                        m_transformAxis == TransformAxis.None ? string.Empty : " along " + m_transformAxis, factor, typedText, hint));
                    break;
                }
            }

            if (m_transformingBlocks) RedrawBlocks();
        }

        /// <summary>
        /// 변형 대상 하나를 새 자리에 놓는다.
        /// </summary>
        /// <param name="slot">대상의 순번 (m_transformStarts 의 번호).</param>
        /// <param name="position">새 위치.</param>
        /// <param name="yawDelta">포인트의 방향에 더할 각도 (도). 블록의 꼭짓점에는 쓰이지 않는다.</param>
        private void ApplyTransformed(int slot, Vector3 position, float yawDelta)
        {
            if (m_transformingBlocks)
            {
                int key = m_transformVertexKeys[slot];
                m_document.Blocks.blocks[key / ElementStride].vertices[key % ElementStride] = position;
                return;
            }

            PD2Point point = m_document.Points.points[m_transformIndices[slot]];
            point.position = position;
            point.direction = yawDelta == 0f ? m_transformBefore[slot].direction : Mathf.PosMod(m_transformBefore[slot].direction + yawDelta, 360f);
            m_markers.Refresh(m_transformIndices[slot]);
            m_linksDirty = true;
        }

        /// <summary>
        /// 이동량을 고쳐 기준이 되는 것이 격자점에 오게 한다 (격자 고정). 움직이지 않은 축은 그대로 둔다.
        /// 면에 붙어 있을 때는 가로 두 축만 맞추고, 맞춘 자리의 면 높이를 다시 찾는다.
        /// </summary>
        /// <param name="anchor">기준이 되는 것의 옮기기 전 위치.</param>
        /// <param name="delta">이동량.</param>
        /// <param name="onSurface">블록 면에 붙어 있는지.</param>
        /// <returns>고친 이동량.</returns>
        private Vector3 SnapToGrid(Vector3 anchor, Vector3 delta, bool onSurface)
        {
            Vector3 target = anchor + delta;
            if (Mathf.Abs(delta.X) > k_snapIdleDistance) target.X = Mathf.Round(target.X / m_gridSize) * m_gridSize;
            if (Mathf.Abs(delta.Z) > k_snapIdleDistance) target.Z = Mathf.Round(target.Z / m_gridSize) * m_gridSize;
            if (onSurface)
            {
                Vector3 above = target + Vector3.Up * k_surfaceProbeUp;
                if (MapLoader.RaycastBlock(BlockLayer.Human, above, Vector3.Down, k_surfaceProbeLength, out float distance)) target.Y = above.Y - distance;
            }
            else if (Mathf.Abs(delta.Y) > k_snapIdleDistance)
            {
                target.Y = Mathf.Round(target.Y / m_gridSize) * m_gridSize;
            }
            return target - anchor;
        }

        /// <summary>
        /// 마우스가 움직인 만큼의 이동량을 월드 좌표로 구한다. 축을 묶지 않았으면 화면과 나란한 면 위에서, 묶었으면 그 축 위에서 움직인다.
        /// </summary>
        /// <returns>이동량 (m).</returns>
        private Vector3 MouseMoveDelta()
        {
            Camera3D camera = m_view.Camera;
            if (m_transformAxis == TransformAxis.None)
            {
                var plane = new Plane(-camera.GlobalBasis.Z, m_transformCenter);
                Vector3? from = plane.IntersectsRay(camera.ProjectRayOrigin(m_transformStartMouse), camera.ProjectRayNormal(m_transformStartMouse));
                Vector3? to = plane.IntersectsRay(camera.ProjectRayOrigin(m_transformMouse), camera.ProjectRayNormal(m_transformMouse));
                return from.HasValue && to.HasValue ? to.Value - from.Value : Vector3.Zero;
            }

            // 축을 화면에 투영해, 마우스가 그 방향으로 움직인 픽셀 수를 축 위의 거리로 바꾼다.
            Vector3 axis = AxisVector(m_transformAxis);
            Vector2 origin = camera.UnprojectPosition(m_transformCenter);
            Vector2 along = camera.UnprojectPosition(m_transformCenter + axis) - origin;
            float pixelsPerMeter = along.Length();
            if (pixelsPerMeter < Mathf.Epsilon) return Vector3.Zero;

            float meters = (m_transformMouse - m_transformStartMouse).Dot(along / pixelsPerMeter) / pixelsPerMeter;
            return axis * meters;
        }

        /// <summary>
        /// 마우스가 선택의 가운데 둘레를 돈 각도를 구한다. 축의 + 쪽에서 내려다볼 때 화면의 시계 방향이 각도가 커지는 쪽이고, 반대쪽에서 보면 반대다.
        /// </summary>
        /// <param name="axis">돌리는 축.</param>
        /// <returns>각도 (도).</returns>
        private float MouseRotateAngle(Vector3 axis)
        {
            Camera3D camera = m_view.Camera;
            Vector2 center = camera.UnprojectPosition(m_transformCenter);
            Vector2 from = m_transformStartMouse - center;
            Vector2 to = m_transformMouse - center;
            if (from.LengthSquared() < 1f || to.LengthSquared() < 1f) return 0f;

            float angle = Mathf.RadToDeg(from.AngleTo(to));
            return (-camera.GlobalBasis.Z).Dot(axis) <= 0f ? angle : -angle;
        }

        /// <summary>
        /// 마우스가 선택의 가운데에서 멀어진 비율을 구한다 (크기 바꾸기의 배율). 시작했을 때의 거리가 1 이다.
        /// </summary>
        /// <returns>배율.</returns>
        private float MouseScaleFactor()
        {
            Vector2 center = m_view.Camera.UnprojectPosition(m_transformCenter);
            float from = m_transformStartMouse.DistanceTo(center);
            return from < 1f ? 1f : m_transformMouse.DistanceTo(center) / from;
        }

        /// <summary>
        /// 축의 방향 벡터.
        /// </summary>
        /// <param name="axis">축.</param>
        /// <returns>단위 벡터. 축이 없으면 0.</returns>
        private static Vector3 AxisVector(TransformAxis axis)
        {
            switch (axis)
            {
                case TransformAxis.X: return Vector3.Right;
                case TransformAxis.Y: return Vector3.Up;
                case TransformAxis.Z: return Vector3.Back;
                default: return Vector3.Zero;
            }
        }

        /// <summary>
        /// 마지막 편집을 되돌린다 (Ctrl+Z).
        /// </summary>
        private void Undo()
        {
            if (Transforming) CancelTransform();
            IEditorCommand command = m_history.Undo();
            AfterHistoryStep(command, "Undo");
        }

        /// <summary>
        /// 되돌린 편집을 다시 한다 (Ctrl+Shift+Z, Ctrl+Y).
        /// </summary>
        private void Redo()
        {
            if (Transforming) CancelTransform();
            IEditorCommand command = m_history.Redo();
            AfterHistoryStep(command, "Redo");
        }

        /// <summary>
        /// 되돌리기나 다시 하기 뒤에 화면을 맞춘다. 블록의 편집이었으면 블록을, 포인트의 편집이었으면 표식과 목록을 다시 만든다.
        /// </summary>
        /// <param name="command">방금 되돌리거나 다시 한 편집. 없었으면 null.</param>
        /// <param name="what">상태 줄에 보일 말 (Undo 또는 Redo).</param>
        private void AfterHistoryStep(IEditorCommand command, string what)
        {
            if (command == null)
            {
                SetMessage($"Nothing to {what.ToLowerInvariant()}");
                return;
            }

            if (command is BlockChangeCommand or BlockListCommand or TextureListCommand)
            {
                m_blockDirty = true;
                RebuildBlocks();
            }
            else if (command is ActionCommand { Changed: ActionCommand.Target.Asset })
            {
                AfterAssetHistoryStep();
            }
            else if (command is ActionCommand { Changed: ActionCommand.Target.Mission })
            {
                m_missionDirty = true;
                ApplyMission();
            }
            else
            {
                // 종류나 개수가 바뀌었을 수 있으므로 표식과 목록을 다시 만든다.
                m_dirty = true;
                RebuildPoints();
            }
            SetMessage($"{what}: {command.Name}");
        }
    }
}

using System;
using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// UV 편집 화면의 그림판. 면들의 UV 사각형을 텍스처 위에 그리고, 모서리를 골라 옮기기·돌리기·크기 바꾸기를 한다.
    /// 자기가 든 UV 의 복사본만 고친다. 고친 것을 확정하면 Committed 로 알리고, 문서에 넣는 것은 에디터가 한다.
    /// 화면은 텍스처를 보이는 그대로 놓는다: U 는 오른쪽, V 는 아래쪽이 + 이고 (0, 0) 이 텍스처의 왼쪽 위다 (블록의 UV 값과 같다).
    /// </summary>
    public partial class UVCanvas : Control
    {
        /// <summary>
        /// 진행 중인 변형의 종류.
        /// </summary>
        public enum Mode
        {
            None,
            Move,
            Rotate,
            Scale,
        }

        public const int CornerCount = 4;

        // 모서리를 클릭으로 고를 때의 반경과, 누른 채 끌기로 치는 거리 (픽셀).
        private const float k_pickRadiusPixels = 10f;
        private const float k_dragThresholdPixels = 4f;
        // 두 모서리를 같은 자리로 보는 UV 거리. 클릭하면 겹친 것이 함께 선택된다.
        private const float k_coincidentDistance = 1e-4f;
        // 확대의 한 칸 배율과 범위 (UV 1 이 화면에서 차지하는 픽셀 수).
        private const float k_zoomStep = 1.15f;
        private const float k_minZoom = 8f;
        private const float k_maxZoom = 20000f;
        private const float k_defaultZoom = 256f;
        // 맞춰 볼 때 가장자리에 남기는 여백의 비율.
        private const float k_frameMargin = 1.4f;
        // 격자선 사이가 이보다 좁아지면(픽셀) 칸을 두 배씩 넓혀 그린다.
        private const float k_minGridPixels = 6f;
        // 격자에 붙일 때 돌리기가 붙는 각도 (도)와 크기 바꾸기가 붙는 배율. 3D 화면과 같다.
        private const float k_rotateSnapDegrees = 5f;
        private const float k_scaleSnap = 0.1f;
        private const float k_minScale = 0.01f;
        private const float k_cornerSize = 7f;
        private const int k_labelFontSize = 12;
        public const int MinGridDivisions = 1;
        public const int MaxGridDivisions = 256;
        private const int k_defaultGridDivisions = 8;

        private static readonly Color s_background = new Color(0.16f, 0.16f, 0.16f);
        private static readonly Color s_textureTint = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color s_gridMinor = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color s_gridMajor = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color s_unitBorder = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color s_faceFill = new Color(0.55f, 0.75f, 1f, 0.22f);
        private static readonly Color s_faceEdge = new Color(0.9f, 0.95f, 1f, 0.95f);
        private static readonly Color s_corner = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color s_cornerSelected = new Color(1f, 0.6f, 0.1f);
        private static readonly Color s_boxFill = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color s_boxBorder = new Color(1f, 1f, 1f, 0.9f);

        // 면마다 모서리 4개의 UV (고치는 중인 복사본).
        private readonly List<Vector2[]> m_faces = new List<Vector2[]>();
        // 선택한 모서리의 키: 면 번호 × CornerCount + 모서리 번호.
        private readonly SortedSet<int> m_selection = new SortedSet<int>();
        private Texture2D m_texture;
        // 화면 가운데에 오는 UV 와 확대 (UV 1 의 픽셀 수).
        private Vector2 m_center = new Vector2(0.5f, 0.5f);
        private float m_zoom = k_defaultZoom;
        private int m_gridDivisions = k_defaultGridDivisions;

        private bool m_panning;
        private bool m_pressing;
        private bool m_boxing;
        private Vector2 m_pressPosition;
        private Vector2 m_mousePosition;
        // 누른 자리에 있던 모서리. 누른 채 끌면 그것을 잡고 옮긴다. 없으면 −1.
        private int m_pressedCorner = -1;
        // 누른 채 끌어서 시작한 옮기기인지. 버튼을 떼면 확정한다.
        private bool m_dragMove;

        private Mode m_mode = Mode.None;
        // 묶어 둔 축: 0 없음, 1 U, 2 V.
        private int m_axis;
        private int[] m_transformKeys;
        private Vector2[] m_transformStarts;
        // 격자에 붙일 때 격자점에 오는 모서리 (m_transformKeys 의 순번).
        private int m_transformAnchor;
        private Vector2 m_transformCenter;
        private Vector2 m_transformStartMouse;

        // 고친 것을 확정했을 때. 인자는 편집의 이름 (영어).
        public event Action<string> Committed;
        // 선택이나 진행 중인 변형이 바뀌었을 때 (옆의 숫자 칸과 안내 글을 다시 맞추게).
        public event Action StateChanged;
        public event Action UndoRequested;
        public event Action RedoRequested;

        public int FaceCount => m_faces.Count;
        public int SelectedCount => m_selection.Count;
        public bool Transforming => m_mode != Mode.None;
        public Mode TransformMode => m_mode;
        // 격자에 붙이기. Ctrl 을 누르고 있는 동안에는 반대로 듣는다.
        public bool SnapEnabled { get; set; }

        // UV 1 을 몇 칸으로 나눌지.
        public int GridDivisions
        {
            get => m_gridDivisions;
            set
            {
                m_gridDivisions = Mathf.Clamp(value, MinGridDivisions, MaxGridDivisions);
                QueueRedraw();
            }
        }

        public UVCanvas()
        {
            FocusMode = FocusModeEnum.All;
            ClipContents = true;
            TextureRepeat = TextureRepeatEnum.Enabled;
            MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>
        /// 보여 줄 면들을 정한다. 진행 중인 변형은 버린다.
        /// </summary>
        /// <param name="faces">면마다 모서리 4개의 UV. 값을 복사해 든다.</param>
        /// <param name="texture">배경에 깔 텍스처. 없으면 격자만 보인다.</param>
        /// <param name="keepSelection">true 면 선택을 남긴다 (같은 면들의 값만 바뀌었을 때).</param>
        public void SetFaces(IReadOnlyList<Vector2[]> faces, Texture2D texture, bool keepSelection)
        {
            m_mode = Mode.None;
            m_dragMove = false;
            m_pressing = false;
            m_boxing = false;
            m_faces.Clear();
            foreach (Vector2[] face in faces)
            {
                var copy = new Vector2[CornerCount];
                Array.Copy(face, copy, CornerCount);
                m_faces.Add(copy);
            }
            m_texture = texture;
            if (!keepSelection) m_selection.Clear();
            m_selection.RemoveWhere(key => key / CornerCount >= m_faces.Count);
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 면 하나의 UV 를 얻는다.
        /// </summary>
        /// <param name="face">면의 순번.</param>
        /// <returns>모서리 4개의 UV (그림판이 든 배열 그대로).</returns>
        public Vector2[] GetFace(int face)
        {
            return m_faces[face];
        }

        /// <summary>
        /// 선택한 모서리들 가운데 맨 앞의 것의 UV.
        /// </summary>
        /// <param name="uv">UV.</param>
        /// <returns>선택이 있으면 true.</returns>
        public bool TryGetSelectedUV(out Vector2 uv)
        {
            uv = Vector2.Zero;
            if (m_selection.Count == 0) return false;

            int key = m_selection.Min;
            uv = m_faces[key / CornerCount][key % CornerCount];
            return true;
        }

        /// <summary>
        /// 모서리 하나를 선택하거나 선택에서 뺀다.
        /// </summary>
        /// <param name="face">면의 순번.</param>
        /// <param name="corner">모서리 번호 (0 에서 3).</param>
        /// <param name="selected">선택할지.</param>
        public void SelectCorner(int face, int corner, bool selected)
        {
            if (face < 0 || face >= m_faces.Count || corner < 0 || corner >= CornerCount || Transforming) return;

            if (selected) m_selection.Add(face * CornerCount + corner);
            else m_selection.Remove(face * CornerCount + corner);
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 모서리를 전부 선택하거나 선택을 푼다 (A, Alt+A).
        /// </summary>
        /// <param name="all">true 면 전부 선택.</param>
        public void SelectAll(bool all)
        {
            if (Transforming) return;

            m_selection.Clear();
            for (int key = 0; all && key < m_faces.Count * CornerCount; key++)
            {
                m_selection.Add(key);
            }
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 선택한 모서리를 같은 만큼 옮기고 확정한다.
        /// </summary>
        /// <param name="delta">옮길 양 (UV).</param>
        public void MoveSelected(Vector2 delta)
        {
            if (m_selection.Count == 0 || Transforming || !delta.IsFinite()) return;

            foreach (int key in m_selection)
            {
                m_faces[key / CornerCount][key % CornerCount] += delta;
            }
            QueueRedraw();
            Committed?.Invoke("Move UV");
        }

        /// <summary>
        /// 선택한 모서리의 U 나 V 를 한 값으로 맞추고 확정한다 (숫자 칸).
        /// </summary>
        /// <param name="axis">0 이면 U, 1 이면 V.</param>
        /// <param name="value">넣을 값.</param>
        public void SetSelectedAxis(int axis, float value)
        {
            if (m_selection.Count == 0 || Transforming || !float.IsFinite(value)) return;

            foreach (int key in m_selection)
            {
                Vector2 uv = m_faces[key / CornerCount][key % CornerCount];
                if (axis == 0) uv.X = value;
                else uv.Y = value;
                m_faces[key / CornerCount][key % CornerCount] = uv;
            }
            QueueRedraw();
            Committed?.Invoke("Set UV");
        }

        /// <summary>
        /// UV 를 가장 가까운 격자점으로 옮긴다.
        /// </summary>
        /// <param name="uv">UV.</param>
        /// <returns>격자점.</returns>
        public Vector2 SnapUV(Vector2 uv)
        {
            float step = 1f / m_gridDivisions;
            return new Vector2(Mathf.Round(uv.X / step) * step, Mathf.Round(uv.Y / step) * step);
        }

        /// <summary>
        /// 면들이 전부 보이게 시점을 맞춘다. 면이 없으면 UV 0 에서 1 사이의 칸을 보여 준다.
        /// </summary>
        public void Frame()
        {
            Vector2 low = Vector2.Zero;
            Vector2 high = Vector2.One;
            if (m_faces.Count > 0)
            {
                low = m_faces[0][0];
                high = low;
            }
            foreach (Vector2[] face in m_faces)
            {
                foreach (Vector2 uv in face)
                {
                    low = low.Min(uv);
                    high = high.Max(uv);
                }
            }

            m_center = (low + high) * 0.5f;
            Vector2 extent = (high - low).Max(Vector2.One * (1f / MaxGridDivisions)) * k_frameMargin;
            Vector2 size = Size;
            m_zoom = size.X > 1f && size.Y > 1f ? Mathf.Clamp(Mathf.Min(size.X / extent.X, size.Y / extent.Y), k_minZoom, k_maxZoom) : k_defaultZoom;
            QueueRedraw();
        }

        /// <summary>
        /// 선택한 모서리의 변형을 시작한다 (G 옮기기, R 돌리기, S 크기 바꾸기). 마우스를 움직이면 미리 보이고 클릭이나 Enter 로 확정, 오른쪽 클릭이나 Esc 로 취소한다.
        /// </summary>
        /// <param name="mode">변형의 종류.</param>
        public void BeginTransform(Mode mode)
        {
            if (Transforming) CancelTransform();
            if (mode == Mode.None || m_selection.Count == 0) return;

            m_transformKeys = new int[m_selection.Count];
            m_selection.CopyTo(m_transformKeys);
            m_transformStarts = new Vector2[m_transformKeys.Length];
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 high = new Vector2(float.MinValue, float.MinValue);
            m_transformStartMouse = ScreenToUV(m_mousePosition);
            m_transformAnchor = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < m_transformKeys.Length; i++)
            {
                Vector2 uv = m_faces[m_transformKeys[i] / CornerCount][m_transformKeys[i] % CornerCount];
                m_transformStarts[i] = uv;
                low = low.Min(uv);
                high = high.Max(uv);
                float distance = uv.DistanceSquaredTo(m_transformStartMouse);
                if (distance < nearest)
                {
                    nearest = distance;
                    m_transformAnchor = i;
                }
            }
            m_transformCenter = (low + high) * 0.5f;
            m_mode = mode;
            m_axis = 0;
            UpdateTransform();
        }

        /// <summary>
        /// 변형을 확정하고 알린다.
        /// </summary>
        public void ConfirmTransform()
        {
            if (!Transforming) return;

            string name = $"{m_mode} UV";
            m_mode = Mode.None;
            m_dragMove = false;
            QueueRedraw();
            Committed?.Invoke(name);
        }

        /// <summary>
        /// 변형을 취소하고 시작했을 때의 값으로 되돌린다.
        /// </summary>
        public void CancelTransform()
        {
            if (!Transforming) return;

            for (int i = 0; i < m_transformKeys.Length; i++)
            {
                m_faces[m_transformKeys[i] / CornerCount][m_transformKeys[i] % CornerCount] = m_transformStarts[i];
            }
            m_mode = Mode.None;
            m_dragMove = false;
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 화면의 자리를 UV 로 바꾼다.
        /// </summary>
        /// <param name="screen">그림판 안의 자리 (픽셀).</param>
        /// <returns>UV.</returns>
        public Vector2 ScreenToUV(Vector2 screen)
        {
            return m_center + (screen - Size * 0.5f) / m_zoom;
        }

        /// <summary>
        /// UV 를 화면의 자리로 바꾼다.
        /// </summary>
        /// <param name="uv">UV.</param>
        /// <returns>그림판 안의 자리 (픽셀).</returns>
        public Vector2 UVToScreen(Vector2 uv)
        {
            return Size * 0.5f + (uv - m_center) * m_zoom;
        }

        /// <summary>
        /// 지금의 마우스 자리와 축으로 변형 대상을 다시 놓는다 (미리 보기).
        /// </summary>
        private void UpdateTransform()
        {
            if (!Transforming) return;

            bool snap = SnapEnabled != Input.IsKeyPressed(Key.Ctrl);
            Vector2 mouse = ScreenToUV(m_mousePosition);
            switch (m_mode)
            {
                case Mode.Move:
                {
                    Vector2 delta = mouse - m_transformStartMouse;
                    if (m_axis == 1) delta.Y = 0f;
                    if (m_axis == 2) delta.X = 0f;
                    if (snap)
                    {
                        // 기준 모서리가 격자점에 오게 한다. 묶어서 움직이지 않는 축은 그대로 둔다.
                        Vector2 anchor = m_transformStarts[m_transformAnchor];
                        Vector2 target = SnapUV(anchor + delta);
                        if (m_axis == 1) target.Y = anchor.Y;
                        if (m_axis == 2) target.X = anchor.X;
                        delta = target - anchor;
                    }
                    for (int i = 0; i < m_transformKeys.Length; i++)
                    {
                        SetCorner(m_transformKeys[i], m_transformStarts[i] + delta);
                    }
                    break;
                }

                case Mode.Rotate:
                {
                    Vector2 from = m_transformStartMouse - m_transformCenter;
                    Vector2 to = mouse - m_transformCenter;
                    float angle = from.LengthSquared() < Mathf.Epsilon || to.LengthSquared() < Mathf.Epsilon ? 0f : from.AngleTo(to);
                    if (snap) angle = Mathf.DegToRad(Mathf.Round(Mathf.RadToDeg(angle) / k_rotateSnapDegrees) * k_rotateSnapDegrees);
                    for (int i = 0; i < m_transformKeys.Length; i++)
                    {
                        SetCorner(m_transformKeys[i], m_transformCenter + (m_transformStarts[i] - m_transformCenter).Rotated(angle));
                    }
                    break;
                }

                case Mode.Scale:
                {
                    float from = m_transformStartMouse.DistanceTo(m_transformCenter);
                    float factor = from < Mathf.Epsilon ? 1f : mouse.DistanceTo(m_transformCenter) / from;
                    if (snap) factor = Mathf.Round(factor / k_scaleSnap) * k_scaleSnap;
                    factor = Mathf.Max(k_minScale, factor);
                    var scale = new Vector2(m_axis == 2 ? 1f : factor, m_axis == 1 ? 1f : factor);
                    for (int i = 0; i < m_transformKeys.Length; i++)
                    {
                        SetCorner(m_transformKeys[i], m_transformCenter + (m_transformStarts[i] - m_transformCenter) * scale);
                    }
                    break;
                }
            }
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 모서리 하나의 UV 를 넣는다.
        /// </summary>
        /// <param name="key">모서리의 키.</param>
        /// <param name="uv">UV.</param>
        private void SetCorner(int key, Vector2 uv)
        {
            m_faces[key / CornerCount][key % CornerCount] = uv;
        }

        /// <summary>
        /// 화면의 한 자리에서 가장 가까운 모서리를 찾는다.
        /// </summary>
        /// <param name="screen">그림판 안의 자리 (픽셀).</param>
        /// <returns>모서리의 키. 반경 안에 없으면 −1.</returns>
        private int CornerAt(Vector2 screen)
        {
            int best = -1;
            float bestDistance = k_pickRadiusPixels;
            for (int key = 0; key < m_faces.Count * CornerCount; key++)
            {
                float distance = UVToScreen(m_faces[key / CornerCount][key % CornerCount]).DistanceTo(screen);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = key;
            }
            return best;
        }

        /// <summary>
        /// 한 모서리와 같은 자리에 겹친 모서리들을 모은다 (그 모서리 자신 포함). 면끼리는 모서리를 공유하지 않으므로 함께 움직이려면 함께 골라야 한다.
        /// </summary>
        /// <param name="key">모서리의 키.</param>
        /// <returns>겹친 모서리들의 키.</returns>
        private List<int> CoincidentWith(int key)
        {
            var result = new List<int>();
            Vector2 at = m_faces[key / CornerCount][key % CornerCount];
            for (int other = 0; other < m_faces.Count * CornerCount; other++)
            {
                if (m_faces[other / CornerCount][other % CornerCount].DistanceTo(at) <= k_coincidentDistance) result.Add(other);
            }
            return result;
        }

        /// <summary>
        /// 클릭한 자리의 모서리(와 겹친 것)를 선택한다. Shift 면 선택에 더하거나 뺀다. 빈 자리를 누르면 선택을 푼다.
        /// </summary>
        /// <param name="screen">누른 자리.</param>
        /// <param name="extend">Shift 를 눌렀는지.</param>
        private void SelectAt(Vector2 screen, bool extend)
        {
            int key = CornerAt(screen);
            if (!extend) m_selection.Clear();
            if (key >= 0)
            {
                List<int> keys = CoincidentWith(key);
                bool remove = extend && m_selection.Contains(key);
                foreach (int each in keys)
                {
                    if (remove) m_selection.Remove(each);
                    else m_selection.Add(each);
                }
            }
            QueueRedraw();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// 사각형 안의 모서리를 선택한다. Shift 면 선택에 더한다.
        /// </summary>
        /// <param name="box">사각형 (픽셀).</param>
        /// <param name="extend">Shift 를 눌렀는지.</param>
        private void SelectBox(Rect2 box, bool extend)
        {
            if (!extend) m_selection.Clear();
            for (int key = 0; key < m_faces.Count * CornerCount; key++)
            {
                if (box.HasPoint(UVToScreen(m_faces[key / CornerCount][key % CornerCount]))) m_selection.Add(key);
            }
            QueueRedraw();
            StateChanged?.Invoke();
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            switch (inputEvent)
            {
                case InputEventMouseButton button:
                    OnMouseButton(button);
                    AcceptEvent();
                    break;
                case InputEventMouseMotion motion:
                    OnMouseMotion(motion);
                    break;
                case InputEventKey { Echo: false } key:
                    if (OnKey(key)) AcceptEvent();
                    break;
            }
        }

        /// <summary>
        /// 마우스 버튼: 가운데는 화면 옮기기, 휠은 확대·축소, 왼쪽은 선택과 끌어서 옮기기. 변형 중에는 왼쪽이 확정, 오른쪽이 취소다.
        /// </summary>
        /// <param name="button">버튼 이벤트.</param>
        private void OnMouseButton(InputEventMouseButton button)
        {
            m_mousePosition = button.Position;
            if (button.Pressed) GrabFocus();

            if (Transforming && !m_dragMove && button.Pressed && (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
            {
                if (button.ButtonIndex == MouseButton.Left) ConfirmTransform();
                else CancelTransform();
                return;
            }

            switch (button.ButtonIndex)
            {
                case MouseButton.Middle:
                    m_panning = button.Pressed;
                    break;

                case MouseButton.WheelUp:
                case MouseButton.WheelDown:
                    if (button.Pressed)
                    {
                        // 마우스 아래의 UV 가 제자리에 남게 확대한다.
                        Vector2 under = ScreenToUV(button.Position);
                        m_zoom = Mathf.Clamp(m_zoom * (button.ButtonIndex == MouseButton.WheelUp ? k_zoomStep : 1f / k_zoomStep), k_minZoom, k_maxZoom);
                        m_center = under - (button.Position - Size * 0.5f) / m_zoom;
                        if (Transforming) UpdateTransform();
                        QueueRedraw();
                    }
                    break;

                case MouseButton.Right:
                    if (button.Pressed && m_dragMove) CancelTransform();
                    break;

                case MouseButton.Left:
                    if (button.Pressed)
                    {
                        m_pressing = true;
                        m_boxing = false;
                        m_pressPosition = button.Position;
                        m_pressedCorner = CornerAt(button.Position);
                    }
                    else if (m_dragMove)
                    {
                        m_pressing = false;
                        ConfirmTransform();
                    }
                    else if (m_pressing)
                    {
                        m_pressing = false;
                        if (m_boxing) SelectBox(new Rect2(m_pressPosition, button.Position - m_pressPosition).Abs(), button.ShiftPressed);
                        else SelectAt(button.Position, button.ShiftPressed);
                        m_boxing = false;
                        QueueRedraw();
                    }
                    break;
            }
        }

        /// <summary>
        /// 마우스 이동: 화면을 옮기거나, 변형을 미리 보이거나, 누른 모서리를 끌어 옮기거나, 사각형 선택의 사각형을 그린다.
        /// </summary>
        /// <param name="motion">이동 이벤트.</param>
        private void OnMouseMotion(InputEventMouseMotion motion)
        {
            m_mousePosition = motion.Position;
            if (m_panning)
            {
                m_center -= motion.Relative / m_zoom;
                // 화면을 옮겨도 잡고 있는 것이 마우스를 따라오게 시작한 자리를 그대로 둔다.
                QueueRedraw();
                return;
            }
            if (Transforming)
            {
                UpdateTransform();
                return;
            }
            if (!m_pressing || motion.Position.DistanceTo(m_pressPosition) <= k_dragThresholdPixels) return;

            if (m_pressedCorner >= 0 && !m_boxing)
            {
                // 모서리를 누른 채 끌면 그것을 잡고 옮긴다. 선택에 없던 모서리면 그것(과 겹친 것)만 선택한다.
                if (!m_selection.Contains(m_pressedCorner))
                {
                    if (!motion.ShiftPressed) m_selection.Clear();
                    foreach (int key in CoincidentWith(m_pressedCorner))
                    {
                        m_selection.Add(key);
                    }
                }
                int pressed = m_pressedCorner;
                Vector2 current = m_mousePosition;
                m_mousePosition = m_pressPosition;
                BeginTransform(Mode.Move);
                m_transformAnchor = Mathf.Max(0, Array.IndexOf(m_transformKeys, pressed));
                m_mousePosition = current;
                m_dragMove = true;
                UpdateTransform();
                return;
            }

            m_boxing = true;
            QueueRedraw();
        }

        /// <summary>
        /// 키. 3D 화면과 같은 키를 쓴다: G / R / S 변형, X / Y 로 축(U / V) 묶기, A / Alt+A 선택, Home 과 F 로 맞춰 보기, Ctrl+Z 되돌리기.
        /// </summary>
        /// <param name="key">키 이벤트.</param>
        /// <returns>이 키를 썼으면 true.</returns>
        private bool OnKey(InputEventKey key)
        {
            // 물리 키 위치로 읽는다. 글자 코드는 한글 입력 상태에서 영문자로 오지 않을 수 있다.
            Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            if (code == Key.Ctrl)
            {
                // 누르거나 뗄 때 스냅이 뒤집힌 것이 바로 보이게 한다.
                if (Transforming) UpdateTransform();
                return Transforming;
            }
            if (!key.Pressed) return false;

            if (Transforming)
            {
                switch (code)
                {
                    case Key.X: m_axis = m_axis == 1 ? 0 : 1; UpdateTransform(); return true;
                    case Key.Y: m_axis = m_axis == 2 ? 0 : 2; UpdateTransform(); return true;
                    case Key.Enter:
                    case Key.KpEnter: ConfirmTransform(); return true;
                    case Key.Escape: CancelTransform(); return true;
                    case Key.G: BeginTransform(Mode.Move); return true;
                    case Key.R: BeginTransform(Mode.Rotate); return true;
                    case Key.S: BeginTransform(Mode.Scale); return true;
                }
                return false;
            }

            switch (code)
            {
                case Key.G: BeginTransform(Mode.Move); return true;
                case Key.R: BeginTransform(Mode.Rotate); return true;
                case Key.S: BeginTransform(Mode.Scale); return true;
                case Key.A: SelectAll(!key.AltPressed); return true;
                case Key.F:
                case Key.Home:
                case Key.KpPeriod: Frame(); return true;
                case Key.Y:
                    if (!key.CtrlPressed) return false;
                    RedoRequested?.Invoke();
                    return true;
                case Key.Z:
                    if (!key.CtrlPressed) return false;
                    if (key.ShiftPressed) RedoRequested?.Invoke();
                    else UndoRequested?.Invoke();
                    return true;
            }
            return false;
        }

        public override void _Draw()
        {
            Vector2 size = Size;
            DrawRect(new Rect2(Vector2.Zero, size), s_background);

            Vector2 low = ScreenToUV(Vector2.Zero);
            Vector2 high = ScreenToUV(size);
            if (m_texture != null)
            {
                // 화면 전체에 텍스처를 타일로 깐다 (반복은 TextureRepeat 가 한다).
                Vector2 textureSize = m_texture.GetSize();
                DrawTextureRectRegion(m_texture, new Rect2(Vector2.Zero, size), new Rect2(low * textureSize, (high - low) * textureSize), s_textureTint);
            }

            // 격자. 촘촘해지면 칸을 두 배씩 넓힌다. 정수 UV 의 선은 진하게 그린다.
            int every = 1;
            while (m_zoom / m_gridDivisions * every < k_minGridPixels && every < m_gridDivisions)
            {
                every *= 2;
            }
            float step = 1f / m_gridDivisions;
            for (int axis = 0; axis < 2; axis++)
            {
                int first = Mathf.FloorToInt(low[axis] / step);
                int last = Mathf.CeilToInt(high[axis] / step);
                for (int line = first; line <= last; line++)
                {
                    bool whole = line % m_gridDivisions == 0;
                    if (!whole && line % every != 0) continue;

                    float at = (line * step - m_center[axis]) * m_zoom + size[axis] * 0.5f;
                    Vector2 from = axis == 0 ? new Vector2(at, 0f) : new Vector2(0f, at);
                    Vector2 to = axis == 0 ? new Vector2(at, size.Y) : new Vector2(size.X, at);
                    DrawLine(from, to, whole ? s_gridMajor : s_gridMinor);
                }
            }
            DrawRect(new Rect2(UVToScreen(Vector2.Zero), Vector2.One * m_zoom), s_unitBorder, false);

            var points = new Vector2[CornerCount];
            var colors = new[] { s_faceFill, s_faceFill, s_faceFill, s_faceFill };
            var uvs = new Vector2[CornerCount];
            foreach (Vector2[] face in m_faces)
            {
                for (int corner = 0; corner < CornerCount; corner++)
                {
                    points[corner] = UVToScreen(face[corner]);
                }
                DrawPrimitive(points, colors, uvs);
                for (int corner = 0; corner < CornerCount; corner++)
                {
                    DrawLine(points[corner], points[(corner + 1) % CornerCount], s_faceEdge);
                }
            }

            Font font = ThemeDB.FallbackFont;
            Vector2 half = Vector2.One * (k_cornerSize * 0.5f);
            for (int key = 0; key < m_faces.Count * CornerCount; key++)
            {
                Vector2 at = UVToScreen(m_faces[key / CornerCount][key % CornerCount]);
                DrawRect(new Rect2(at - half, half * 2f), m_selection.Contains(key) ? s_cornerSelected : s_corner);
                DrawRect(new Rect2(at - half, half * 2f), s_faceEdge, false);
                // 맨 앞 면의 모서리 번호. 오른쪽 패널의 UV 숫자 칸(UV 0 에서 3)과 같은 번호다.
                if (key < CornerCount) DrawString(font, at + new Vector2(k_cornerSize, -k_cornerSize), key.ToString(), HorizontalAlignment.Left, -1f, k_labelFontSize, s_faceEdge);
            }

            if (m_boxing)
            {
                Rect2 box = new Rect2(m_pressPosition, m_mousePosition - m_pressPosition).Abs();
                DrawRect(box, s_boxFill);
                DrawRect(box, s_boxBorder, false);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터. 확장 형식의 블록 데이터(BD2), 포인트 데이터(PD2), 미션 파일(MIF2)을 만든다. 게임 안의 씬이고 실행 인자로 들어온다 (-- --scene editor).
    /// 모드로 나뉜다: 포인트와 블록은 3D 화면에서, 미션과 에셋(데이터 파일)은 3D 화면을 덮는 별도의 화면에서 고친다 (사용자 결정: 에디터를 여럿으로 나누지 않는다).
    /// 파일의 내용(MapDocument)을 편집하고 3D 화면은 그것을 보여 준다. 블록은 게임의 로더로 띄우고, 포인트는 게임처럼 스폰하지 않고 표식으로 그린다.
    /// 조작은 블렌더의 기본 키를 따른다 (사용자 결정). 같은 기능을 메뉴에서도 쓸 수 있다:
    ///   시점 — 가운데 버튼 끌기(돌리기), Shift + 가운데 버튼 끌기(옮기기), 휠(확대·축소), 넘버패드 1 / 3 / 7(정면 / 측면 / 위. Ctrl 과 함께면 반대쪽),
    ///          넘버패드 5(원근·직교), 넘버패드 . 이나 F(선택한 것으로 시점 맞추기), 오른쪽 버튼을 누른 채 이동 키·Q/E·Shift(날아다니기).
    ///   선택 — 왼쪽 클릭, Shift + 클릭(더하거나 빼기), 왼쪽 버튼으로 끌기(사각형 안의 것 전부), A(전부), Alt+A(해제), Alt+Z(X-RAY: 가려진 것도 보이고 선택된다).
    ///   모드 — Tab(포인트 / 블록). 블록 모드에서는 1 / 2 / 3 / 4 로 선택 단위(꼭짓점 / 모서리 / 면 / 블록)를 고르고, Ctrl + 클릭으로 겹친 것 가운데 하나를 고른다.
    ///   편집 — Shift+A(놓기), Shift+D(복제), X·Delete(지우기), 오른쪽 칸에서 포인트의 값 고치기, Ctrl+S(저장), F5(플레이 테스트).
    ///   변형 — G(옮기기), R(돌리기), S(크기 바꾸기). 도중에 X / Y / Z(축), 숫자(값), Ctrl(격자), 왼쪽 클릭·Enter(확정), 오른쪽 클릭·Esc(취소). Ctrl+Z / Ctrl+Shift+Z·Ctrl+Y(되돌리기 / 다시 하기).
    /// 명령행 인자("--" 뒤): --open 미션.mif2, --block 블록.bd2, --points 포인트.pd2 (exe 폴더 기준 경로), --select 번호[,번호...], --mode block|mission|asset, --asset 데이터.json (에셋 모드에서 볼 파일), --list events (왼쪽 목록을 이벤트 보기로), --element vertex|edge|face|block, --box x0,y0,x1,y1 (화면 사각형으로 선택),
    /// --xray, --view front|right|top, --ortho, --focus (선택한 것으로 시점 맞추기), --cam x,y,z,yaw,pitch, --screenshot 경로.png (화면을 저장하고 종료), --play (연 내용으로 바로 플레이 테스트), --selftest.
    /// 키는 물리 키 위치로 읽는다 (게임의 입력과 같다. 한글 입력 상태에서도 듣는다).
    /// 이벤트(이름 붙은 칸, 연결선, 이벤트 줄)는 XopsEditorEvents.cs, 메시지는 XopsEditorMessages.cs, 미션 모드는 XopsEditorMission.cs, 에셋 모드는 XopsEditorAssets.cs(미리 보기는 XopsEditorPreview.cs), 화면 구성은 XopsEditorInterface.cs, 플레이 테스트와 원본 형식 가져오기는 XopsEditorPlay.cs, 변형과 되돌리기는 XopsEditorTransform.cs, 포인트의 값·놓기·지우기·저장은 XopsEditorPoints.cs, 블록의 선택·놓기·지우기·저장은 XopsEditorBlocks.cs, 점검은 XopsEditorSelfTest.cs 에 있다.
    /// </summary>
    public partial class XopsEditor : Node3D
    {
        private const int k_screenshotWaitFrames = 12;
        // 왼쪽 버튼을 누른 채 이만큼(픽셀) 넘게 움직이면 클릭이 아니라 사각형 선택이다.
        private const float k_dragThreshold = 4f;
        // 맵 전체를 볼 때 내려다보는 각도 (도).
        private const float k_startPitch = 35f;
        // 정면·측면·위 시점의 각도 (도).
        private const float k_viewFrontYaw = 0f;
        private const float k_viewRightYaw = -90f;
        private const float k_viewTopPitch = 90f;
        // 선택한 것으로 시점을 맞출 때 포인트 하나가 차지하는 것으로 보는 반지름 (m).
        private const float k_pointFocusRadius = 1.5f;

        // 카메라가 그리는 가장 먼 거리 (m). 게임 설정의 값은 안개가 가리는 거리에 맞춰져 있어 맵 전체를 보기에는 짧다.
        private const float k_cameraFar = 4000f;

        private static readonly Vector2I s_windowSize = new Vector2I(1280, 720);

        private readonly MapDocument m_document = new MapDocument();
        private readonly SortedSet<int> m_selection = new SortedSet<int>();
        private readonly List<int> m_pickBuffer = new List<int>();
        private EditorCamera m_view;
        private PointMarkers m_markers;
        private bool m_xray;

        // 가운데 버튼으로 시점을 움직이는 중인지.
        private bool m_navigating;
        // 왼쪽 버튼을 3D 화면에서 눌렀는지, 누른 자리, 사각형 선택으로 넘어갔는지.
        private bool m_pressing;
        private Vector2 m_pressPosition;
        private bool m_boxing;
        // 변형을 취소한 오른쪽 버튼을 아직 누르고 있는지. 뗄 때까지 날아다니기를 막는다.
        private bool m_blockFly;

        private string m_screenshotPath;
        private int m_screenshotCountdown = -1;

        public override void _Ready()
        {
            // 게임 설정이 적용한 전체화면·저해상도 렌더를 크기를 바꿀 수 있는 도구용 창으로 되돌린다.
            Window root = GetTree().Root;
            root.Mode = Window.ModeEnum.Windowed;
            root.Unresizable = false;
            root.Size = s_windowSize;
            root.MoveToCenter();
            SetupWindow();

            var camera = new Camera3D { Name = "Camera" };
            AddChild(camera);
            MapLoader.ApplyCameraSettings(camera);
            camera.Far = k_cameraFar;
            camera.MakeCurrent();
            m_view = new EditorCamera(camera);

            m_markers = new PointMarkers { Name = "Markers", ModelFactory = BuildPointModel };
            AddChild(m_markers);
            m_blockOverlay = new BlockOverlay { Name = "BlockOverlay", Visible = false };
            AddChild(m_blockOverlay);
            m_links = new LinkOverlay { Name = "Links" };
            AddChild(m_links);

            BuildInterface();
            ApplyMission();
            RefreshPointList();

            string[] args = OS.GetCmdlineUserArgs();
            if (Array.IndexOf(args, "--selftest") >= 0)
            {
                RunSelfTest();
                return;
            }
            ApplyArguments(args);
        }

        public override void _Process(double delta)
        {
            if (m_screenshotCountdown >= 0)
            {
                UpdateStatus();
                if (m_screenshotCountdown-- == 0)
                {
                    Error error = GetViewport().GetTexture().GetImage().SavePng(m_screenshotPath);
                    GD.Print($"Screenshot {(error == Error.Ok ? "saved" : "failed")}: {m_screenshotPath}");
                    GetTree().Quit(error == Error.Ok ? 0 : 1);
                }
                return;
            }

            if (m_linksDirty) RedrawLinks();
            UpdateFly((float)delta);
            UpdateStatus();
        }

        public override void _UnhandledInput(InputEvent inputEvent)
        {
            switch (inputEvent)
            {
                case InputEventMouseButton button:
                    OnMouseButton(button);
                    break;
                case InputEventMouseMotion motion:
                    OnMouseMotion(motion);
                    break;
                case InputEventKey { Pressed: true, Echo: false } key:
                    OnKey(key);
                    break;
            }
        }

        public override void _EnterTree()
        {
            // 플레이 테스트에서 돌아왔다. 자식들이 트리에 들어온 뒤에 화면을 되돌린다.
            if (m_playing) Callable.From(ResumeFromPlay).CallDeferred();
        }

        public override void _ExitTree()
        {
            if (!m_playing) MapLoader.UnloadBlockData();
        }

        public override void _Notification(int what)
        {
            // 창의 닫기 버튼. 저장하지 않은 내용이 있으면 먼저 묻는다.
            if (what == NotificationWMCloseRequest) ConfirmDiscard(() => GetTree().Quit());
        }

        /// <summary>
        /// 3D 화면에서의 마우스 버튼: 가운데는 시점, 휠은 확대·축소, 왼쪽은 선택(클릭이나 사각형).
        /// </summary>
        /// <param name="button">버튼 이벤트.</param>
        private void OnMouseButton(InputEventMouseButton button)
        {
            if (Transforming && button.Pressed && (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
            {
                if (button.ButtonIndex == MouseButton.Left) ConfirmTransform();
                else CancelTransform();
                // 취소한 오른쪽 버튼을 누른 채로 있어도 날아다니기로 넘어가지 않게, 뗄 때까지 막는다.
                m_blockFly = button.ButtonIndex == MouseButton.Right;
                return;
            }

            switch (button.ButtonIndex)
            {
                case MouseButton.Right:
                    if (!button.Pressed) m_blockFly = false;
                    break;

                case MouseButton.Middle:
                    m_navigating = button.Pressed;
                    break;

                case MouseButton.WheelUp:
                    if (button.Pressed) m_view.Zoom(1f);
                    break;

                case MouseButton.WheelDown:
                    if (button.Pressed) m_view.Zoom(-1f);
                    break;

                case MouseButton.Left:
                    if (button.Pressed)
                    {
                        // 3D 화면을 누르면 입력 칸의 포커스를 풀어, 그 뒤의 키가 글자로 들어가지 않게 한다.
                        GetViewport().GuiReleaseFocus();
                        m_pressing = true;
                        m_boxing = false;
                        m_pressPosition = button.Position;
                    }
                    else if (m_pressing)
                    {
                        m_pressing = false;
                        Rect2 box = new Rect2(m_pressPosition, button.Position - m_pressPosition).Abs();
                        if (m_editMode == EditMode.Block)
                        {
                            if (m_boxing) BlockSelectBox(box, button.ShiftPressed);
                            else BlockSelectAt(button.Position, button.ShiftPressed, button.CtrlPressed);
                        }
                        else if (m_boxing)
                        {
                            SelectBox(box, button.ShiftPressed);
                        }
                        else
                        {
                            SelectAt(button.Position, button.ShiftPressed);
                        }
                        m_boxing = false;
                        ShowBox(false, default);
                    }
                    break;
            }
        }

        /// <summary>
        /// 3D 화면에서의 마우스 이동: 시점을 돌리거나 옮기고, 사각형 선택의 사각형을 그린다.
        /// </summary>
        /// <param name="motion">이동 이벤트.</param>
        private void OnMouseMotion(InputEventMouseMotion motion)
        {
            if (m_navigating)
            {
                if (motion.ShiftPressed) m_view.Pan(motion.Relative);
                else m_view.Orbit(motion.Relative);
                if (Transforming) UpdateTransform();
                return;
            }
            if (Transforming)
            {
                m_transformMouse = motion.Position;
                UpdateTransform();
                return;
            }
            if (!m_pressing) return;

            if (!m_boxing && motion.Position.DistanceTo(m_pressPosition) > k_dragThreshold) m_boxing = true;
            if (m_boxing) ShowBox(true, new Rect2(m_pressPosition, motion.Position - m_pressPosition).Abs());
        }

        /// <summary>
        /// 3D 화면에서의 키. 입력 칸에 글자를 치는 동안에는 오지 않는다. 날아다니는 동안(오른쪽 버튼)에는 이동 키와 겹치므로 받지 않는다.
        /// </summary>
        /// <param name="key">키 이벤트.</param>
        private void OnKey(InputEventKey key)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Right) && !Transforming) return;

            // 물리 키 위치로 읽는다. 글자 코드(Keycode)는 한글 입력 상태에서 영문자로 오지 않을 수 있다.
            Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;

            // 미션 모드와 에셋 모드에는 3D 화면이 없다. 저장, 되돌리기, 플레이, 모드 바꾸기만 받는다.
            if (m_editMode >= EditMode.Mission)
            {
                if (code == Key.Tab) SetEditMode(EditMode.Point);
                else if (code == Key.F5) PlayTest();
                else if (code == Key.S && key.CtrlPressed) SaveAll();
                else if (code == Key.Y && key.CtrlPressed) Redo();
                else if (code == Key.Z && key.CtrlPressed && key.ShiftPressed) Redo();
                else if (code == Key.Z && key.CtrlPressed) Undo();
                return;
            }

            if (Transforming)
            {
                switch (code)
                {
                    case Key.X: SetTransformAxis(TransformAxis.X); return;
                    case Key.Y: SetTransformAxis(TransformAxis.Y); return;
                    case Key.Z: SetTransformAxis(TransformAxis.Z); return;
                    case Key.Enter:
                    case Key.KpEnter: ConfirmTransform(); return;
                    case Key.Escape: CancelTransform(); return;
                    case Key.G: BeginTransform(TransformMode.Move); return;
                    case Key.R: BeginTransform(TransformMode.Rotate); return;
                    case Key.S: BeginTransform(TransformMode.Scale); return;
                    case Key.Ctrl: UpdateTransform(); return;
                }
                TypeNumeric(key);
                return;
            }

            switch (code)
            {
                case Key.G:
                    BeginTransform(TransformMode.Move);
                    break;
                case Key.Tab:
                    SetEditMode(m_editMode == EditMode.Point ? EditMode.Block : EditMode.Point);
                    break;
                case Key.Key1:
                case Key.Key2:
                case Key.Key3:
                case Key.Key4:
                    if (m_editMode == EditMode.Block) SetBlockElement((BlockElement)(code - Key.Key1));
                    break;
                case Key.D:
                    if (!key.ShiftPressed) break;
                    if (m_editMode == EditMode.Block) DuplicateBlocks();
                    else DuplicateSelection();
                    break;
                case Key.X:
                case Key.Delete:
                    if (m_editMode == EditMode.Block) DeleteBlocks();
                    else DeleteSelection();
                    break;
                case Key.S:
                    if (key.CtrlPressed) SaveAll();
                    else BeginTransform(TransformMode.Scale);
                    break;
                case Key.R:
                    BeginTransform(TransformMode.Rotate);
                    break;
                case Key.Y:
                    if (key.CtrlPressed) Redo();
                    break;
                case Key.A:
                    if (key.ShiftPressed)
                    {
                        if (m_editMode == EditMode.Block) AddBlock(GetViewport().GetMousePosition());
                        else ShowAddMenu();
                    }
                    else if (key.AltPressed)
                    {
                        SelectNone();
                    }
                    else
                    {
                        SelectAll();
                    }
                    break;
                case Key.Z:
                    if (key.AltPressed) SetXray(!m_xray);
                    else if (key.CtrlPressed && key.ShiftPressed) Redo();
                    else if (key.CtrlPressed) Undo();
                    break;
                case Key.F:
                case Key.KpPeriod:
                    FocusSelection();
                    break;
                case Key.F5:
                    PlayTest();
                    break;
                case Key.Escape:
                    CancelPick();
                    break;
                case Key.Kp1:
                    ViewFront(key.CtrlPressed);
                    break;
                case Key.Kp3:
                    ViewRight(key.CtrlPressed);
                    break;
                case Key.Kp7:
                    ViewTop(key.CtrlPressed);
                    break;
                case Key.Kp5:
                    m_view.ToggleOrthographic();
                    break;
            }
        }

        /// <summary>
        /// 날아다니기. 마우스 오른쪽 버튼을 누르고 있는 동안에만 돌고 움직인다 (입력 칸에 글자를 칠 때 카메라가 움직이지 않게).
        /// </summary>
        /// <param name="delta">프레임 시간 (초).</param>
        private void UpdateFly(float delta)
        {
            InputManager input = InputManager.Instance;
            bool flying = Input.IsMouseButtonPressed(MouseButton.Right) && !m_pressing && !Transforming && !m_blockFly;
            input.MouseCursorMode(false, flying, false);
            if (!flying) return;

            float sensitivity = ConfigManager.Instance.MouseSensitivity;
            float invertY = ConfigManager.Instance.InvertY ? -1f : 1f;
            Vector2 look = input.ReadVector(InputManager.Look);
            look = new Vector2(look.X * sensitivity, look.Y * sensitivity * invertY);
            float vertical = (input.IsKeyPressed(Key.E) ? 1f : 0f) - (input.IsKeyPressed(Key.Q) ? 1f : 0f);
            m_view.Fly(look, input.ReadVector(InputManager.Move), vertical, input.IsKeyPressed(Key.Shift), delta);
        }

        /// <summary>
        /// 정면에서 본다 (사람이 yaw 0 일 때 보는 방향, −Z).
        /// </summary>
        /// <param name="opposite">true 면 반대쪽(뒤)에서 본다.</param>
        private void ViewFront(bool opposite)
        {
            m_view.SetAngles(k_viewFrontYaw + (opposite ? 180f : 0f), 0f);
        }

        /// <summary>
        /// 오른쪽(+X 쪽)에서 본다.
        /// </summary>
        /// <param name="opposite">true 면 왼쪽에서 본다.</param>
        private void ViewRight(bool opposite)
        {
            m_view.SetAngles(k_viewRightYaw + (opposite ? 180f : 0f), 0f);
        }

        /// <summary>
        /// 위에서 내려다본다.
        /// </summary>
        /// <param name="opposite">true 면 아래에서 올려다본다.</param>
        private void ViewTop(bool opposite)
        {
            m_view.SetAngles(k_viewFrontYaw, opposite ? -k_viewTopPitch : k_viewTopPitch);
        }

        /// <summary>
        /// X-RAY 를 켜거나 끈다. 켜면 블록에 가려진 표식도 보이고 선택된다. 끄면 화면에 보이는 것만 선택된다.
        /// </summary>
        /// <param name="enabled">켤지.</param>
        private void SetXray(bool enabled)
        {
            m_xray = enabled;
            m_markers.SetXray(enabled);
            m_blockOverlay.SetXray(enabled);
            m_links.SetXray(enabled);
            SyncXrayButton();
            SetMessage($"X-Ray {(enabled ? "on: points behind blocks are shown and can be selected" : "off: only visible points are selected")}");
        }

        /// <summary>
        /// 화면의 한 점을 눌렀을 때의 선택. Shift 가 없으면 그 표식만 선택하고(빈 곳이면 선택 해제), 있으면 그 표식을 더하거나 뺀다.
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <param name="extend">Shift 를 누르고 있었는지.</param>
        private void SelectAt(Vector2 screenPosition, bool extend)
        {
            if (Transforming) return;
            if (m_pickSlot != null)
            {
                FinishPick(screenPosition);
                return;
            }

            int picked = m_markers.Pick(m_view.Camera, screenPosition, m_xray);
            if (!extend)
            {
                m_selection.Clear();
                if (picked >= 0) m_selection.Add(picked);
            }
            else if (picked >= 0 && !m_selection.Remove(picked))
            {
                m_selection.Add(picked);
            }
            ApplySelection();
        }

        /// <summary>
        /// 화면의 사각형으로 선택한다. Shift 가 없으면 사각형 안의 것으로 바꾸고, 있으면 더한다.
        /// </summary>
        /// <param name="rect">화면 사각형 (픽셀).</param>
        /// <param name="extend">Shift 를 누르고 있었는지.</param>
        private void SelectBox(Rect2 rect, bool extend)
        {
            m_pickBuffer.Clear();
            m_markers.PickRect(m_view.Camera, rect, m_xray, m_pickBuffer);
            if (!extend) m_selection.Clear();
            m_selection.UnionWith(m_pickBuffer);
            ApplySelection();
        }

        /// <summary>
        /// 모든 포인트를 선택한다.
        /// </summary>
        private void SelectAll()
        {
            if (m_editMode == EditMode.Block)
            {
                BlockSelectAll(true);
                return;
            }
            m_selection.Clear();
            for (int i = 0; i < m_document.Points.points.Count; i++)
            {
                m_selection.Add(i);
            }
            ApplySelection();
        }

        /// <summary>
        /// 선택을 푼다.
        /// </summary>
        private void SelectNone()
        {
            if (m_editMode == EditMode.Block)
            {
                BlockSelectAll(false);
                return;
            }
            m_selection.Clear();
            ApplySelection();
        }

        /// <summary>
        /// 선택을 주어진 포인트들로 바꾼다.
        /// </summary>
        /// <param name="indices">포인트 번호들. 범위 밖의 번호는 버린다.</param>
        private void SetSelection(IEnumerable<int> indices)
        {
            int count = m_document.Points.points.Count;
            m_selection.Clear();
            foreach (int index in indices)
            {
                if (index >= 0 && index < count) m_selection.Add(index);
            }
            ApplySelection();
        }

        /// <summary>
        /// 바뀐 선택을 표식의 테두리, 목록, 값 표시에 반영한다.
        /// </summary>
        private void ApplySelection()
        {
            m_markers.SetSelection(m_selection);
            SyncListSelection();
            UpdateDetails();
        }

        /// <summary>
        /// 실행 인자대로 파일을 열고, 선택과 시점을 맞추고, 화면 저장을 예약한다.
        /// </summary>
        /// <param name="args">"--" 뒤의 인자들.</param>
        private void ApplyArguments(string[] args)
        {
            string open = ArgumentValue(args, "--open");
            if (open != null) OpenMission(open);
            string block = ArgumentValue(args, "--block");
            if (block != null) OpenBlock(block);
            string points = ArgumentValue(args, "--points");
            if (points != null) OpenPoints(points);

            if (Array.IndexOf(args, "--xray") >= 0) SetXray(true);
            if (Array.IndexOf(args, "--ortho") >= 0) m_view.ToggleOrthographic();
            switch (ArgumentValue(args, "--view"))
            {
                case "front": ViewFront(false); break;
                case "right": ViewRight(false); break;
                case "top": ViewTop(false); break;
            }

            float[] cam = ParseNumbers(ArgumentValue(args, "--cam"), 5);
            if (cam != null) m_view.SetPose(new Vector3(cam[0], cam[1], cam[2]), cam[3], cam[4]);

            string select = ArgumentValue(args, "--select");
            if (select != null)
            {
                var indices = new List<int>();
                foreach (string part in select.Split(','))
                {
                    if (int.TryParse(part, out int index)) indices.Add(index);
                }
                SetSelection(indices);
            }
            if (ArgumentValue(args, "--mode") == "block") SetEditMode(EditMode.Block);
            if (ArgumentValue(args, "--mode") == "mission") SetEditMode(EditMode.Mission);
            if (ArgumentValue(args, "--mode") == "asset") SetEditMode(EditMode.Asset);
            string asset = ArgumentValue(args, "--asset");
            if (asset != null) OpenAsset(asset);
            if (ArgumentValue(args, "--list") == "events")
            {
                m_filter.Select(Array.FindIndex(s_filters, filter => filter.Name == k_eventFilterName));
                RefreshPointList();
            }
            if (System.Enum.TryParse(ArgumentValue(args, "--element"), true, out BlockElement element)) SetBlockElement(element);
            float[] box = ParseNumbers(ArgumentValue(args, "--box"), 4);
            if (box != null)
            {
                Rect2 rect = new Rect2(box[0], box[1], box[2] - box[0], box[3] - box[1]).Abs();
                if (m_editMode == EditMode.Block) BlockSelectBox(rect, false);
                else SelectBox(rect, false);
            }
            if (Array.IndexOf(args, "--focus") >= 0) FocusSelection();

            m_screenshotPath = ArgumentValue(args, "--screenshot");
            if (m_screenshotPath != null) m_screenshotCountdown = k_screenshotWaitFrames;
            if (Array.IndexOf(args, "--play") >= 0) PlayTest();
        }

        /// <summary>
        /// 인자 이름 바로 뒤의 값을 찾는다.
        /// </summary>
        /// <param name="args">인자들.</param>
        /// <param name="name">인자 이름.</param>
        /// <returns>값. 없으면 null.</returns>
        private static string ArgumentValue(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        /// <summary>
        /// 쉼표로 나눈 수들을 읽는다.
        /// </summary>
        /// <param name="text">글. null 이어도 된다.</param>
        /// <param name="count">있어야 하는 개수.</param>
        /// <returns>수들. 개수가 다르거나 수가 아닌 것이 있으면 null.</returns>
        private static float[] ParseNumbers(string text, int count)
        {
            string[] parts = text?.Split(',');
            if (parts == null || parts.Length != count) return null;

            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return null;
            }
            return result;
        }

        /// <summary>
        /// 확장 미션 파일(MIF2)이 가리키는 블록과 포인트를 연다. 미션 파일 자체는 고치지 않는다 (미션 에디터의 일이다).
        /// </summary>
        /// <param name="relativePath">MIF2 경로 (exe 폴더 기준).</param>
        /// <returns>블록과 포인트를 둘 다 열었으면 true.</returns>
        private bool OpenMission(string relativePath)
        {
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                return Fail($"Mission file open failed: {relativePath}");
            }
            if (!MIF2File.Read(fullPath, out ExtendedMissionData mission, out string error))
            {
                return Fail($"Mission file read failed: {relativePath} ({error})");
            }

            m_document.SkyIndex = mission.skyIndex;
            m_document.MissionPath = relativePath;
            m_document.Mission = mission;
            m_missionDirty = false;
            ApplyMission();
            bool block = OpenBlock(mission.blockPath);
            bool points = OpenPoints(mission.pointPath);
            return block && points;
        }

        /// <summary>
        /// 블록 데이터(BD2)를 열어 3D 화면에 띄우고 맵 전체가 보이게 시점을 맞춘다.
        /// </summary>
        /// <param name="relativePath">BD2 경로 (exe 폴더 기준).</param>
        /// <returns>열었으면 true.</returns>
        private bool OpenBlock(string relativePath)
        {
            if (!HasExtension(relativePath, BD2File.Extension))
            {
                return Fail($"The editor opens {BD2File.Extension} block files only: {relativePath}");
            }
            if (!m_document.LoadBlocks(relativePath, out string error))
            {
                return Fail($"Block file read failed: {relativePath} ({error})");
            }

            ShowBlocks();
            SetMessage($"Opened {relativePath}");
            return true;
        }

        /// <summary>
        /// 새로 열거나 가져온 블록 데이터를 3D 화면에 띄우고 맵 전체가 보이게 시점을 맞춘다. 선택과 되돌리기 기록은 비운다.
        /// </summary>
        private void ShowBlocks()
        {
            // 화면의 블록은 파일이 아니라 문서의 내용으로 만든다. 블록을 고친 뒤에도 같은 길로 다시 만든다.
            MapLoader.LoadBlockData(m_document.Blocks, m_document.Textures);
            m_blockSelection.Clear();
            m_transformMode = TransformMode.None;
            m_history.Clear();
            m_blockDirty = false;
            RedrawBlocks();
            MapLoader.LoadSkyData(m_document.SkyIndex);
            // 게임의 안개는 수십 미터 밖을 가린다. 에디터는 맵 전체를 멀리서 봐야 하므로 끈다.
            MapLoader.ClearFog();
            m_view.SetAngles(k_viewFrontYaw, k_startPitch);
            FocusAll();
        }

        /// <summary>
        /// 포인트 데이터(PD2)를 열어 표식과 목록을 다시 만든다.
        /// </summary>
        /// <param name="relativePath">PD2 경로 (exe 폴더 기준).</param>
        /// <returns>열었으면 true.</returns>
        private bool OpenPoints(string relativePath)
        {
            if (!HasExtension(relativePath, PD2File.Extension))
            {
                return Fail($"The editor opens {PD2File.Extension} point files only: {relativePath}");
            }
            if (!m_document.LoadPoints(relativePath, out string error))
            {
                return Fail($"Point file read failed: {relativePath} ({error})");
            }

            ShowPoints();
            SetMessage($"Opened {relativePath} ({m_document.Points.points.Count} points)");
            return true;
        }

        /// <summary>
        /// 새로 열거나 가져온 포인트 데이터로 표식과 목록을 다시 만든다. 선택과 되돌리기 기록은 비운다.
        /// </summary>
        private void ShowPoints()
        {
            m_markers.Rebuild(m_document.Points.points);
            m_selection.Clear();
            m_transformMode = TransformMode.None;
            m_history.Clear();
            m_dirty = false;
            RefreshPointList();
            UpdateDetails();
        }

        /// <summary>
        /// 선택한 것이 화면에 들어오게 시점을 맞춘다. 선택이 없으면 맵 전체를 본다.
        /// </summary>
        private void FocusSelection()
        {
            if (m_editMode == EditMode.Block)
            {
                if (m_blockSelection.Count == 0)
                {
                    FocusAll();
                    return;
                }
                var vertexKeys = new SortedSet<int>();
                foreach (int key in m_blockSelection)
                {
                    CollectVertices(m_blockElement, key, vertexKeys);
                }
                var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (int key in vertexKeys)
                {
                    low = low.Min(VertexPosition(key));
                    high = high.Max(VertexPosition(key));
                }
                m_view.Focus((low + high) * 0.5f, (high - low).Length() * 0.5f + k_pointFocusRadius);
                return;
            }
            if (m_selection.Count == 0)
            {
                FocusAll();
                return;
            }

            List<PD2Point> points = m_document.Points.points;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (int index in m_selection)
            {
                Vector3 center = PointMarkers.Center(points[index]);
                min = min.Min(center);
                max = max.Max(center);
            }
            m_view.Focus((min + max) * 0.5f, (max - min).Length() * 0.5f + k_pointFocusRadius);
        }

        /// <summary>
        /// 블록과 포인트 전체가 화면에 들어오게 시점을 맞춘다.
        /// </summary>
        private void FocusAll()
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (Block block in MapLoader.Blocks)
            {
                min = min.Min(block.boundsMin);
                max = max.Max(block.boundsMax);
            }
            foreach (PD2Point point in m_document.Points.points)
            {
                min = min.Min(point.position);
                max = max.Max(point.position);
            }
            if (min.X > max.X) return;

            m_view.Focus((min + max) * 0.5f, (max - min).Length() * 0.5f);
        }

        /// <summary>
        /// 선택을 한 줄로 요약한다 (값 칸 위의 머리말). 하나면 번호와 종류를, 여럿이면 종류별 개수를 보여 준다.
        /// </summary>
        /// <returns>값 표시용 글. 선택이 없으면 안내 문구.</returns>
        private string DescribeSelection()
        {
            if (m_editMode == EditMode.Block) return DescribeBlockSelection();

            List<PD2Point> points = m_document.Points.points;
            if (m_selection.Count == 0)
            {
                return "No point selected.\n\nClick a marker, drag a box, or pick from the list.\nShift adds to the selection.";
            }

            var text = new StringBuilder();
            if (m_selection.Count > 1)
            {
                var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (int index in m_selection)
                {
                    string name = PointTypeInfo.Get(points[index].type).Name;
                    counts[name] = counts.TryGetValue(name, out int count) ? count + 1 : 1;
                }
                text.Append($"{m_selection.Count} points selected").Append('\n').Append('\n');
                foreach (KeyValuePair<string, int> entry in counts)
                {
                    text.Append($"{entry.Key}: {entry.Value}").Append('\n');
                }
                return text.ToString();
            }

            int selected = m_selection.Min;
            PD2Point point = points[selected];
            PointTypeInfo.Info info = PointTypeInfo.Get(point.type);
            text.Append($"Point #{selected}  {info.Name}");
            return text.ToString();
        }

        /// <summary>
        /// 바뀐 것을 전부 저장한다 (Ctrl+S): 블록이 바뀌었으면 블록을, 포인트가 바뀌었으면 포인트를, 미션의 설정이 바뀌었으면 미션 파일을.
        /// </summary>
        private void SaveAll()
        {
            if (Transforming) ConfirmTransform();
            if (!m_dirty && !m_blockDirty && !m_missionDirty && !AssetsDirty())
            {
                SetMessage("Nothing to save");
                return;
            }
            SaveAssets();
            if (m_blockDirty) SaveBlocks();
            if (m_dirty) SavePoints();
            if (m_missionDirty) SaveMission();
        }

        /// <summary>
        /// 경로의 확장자가 주어진 것과 같은지 본다. 대소문자는 가리지 않는다.
        /// </summary>
        /// <param name="path">경로. null 이면 false.</param>
        /// <param name="extension">점을 포함한 확장자.</param>
        /// <returns>같으면 true.</returns>
        private static bool HasExtension(string path, string extension)
        {
            return !string.IsNullOrEmpty(path) && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 실패를 상태 줄에 알린다.
        /// </summary>
        /// <param name="message">이유 (영어).</param>
        /// <returns>늘 false.</returns>
        private bool Fail(string message)
        {
            SetMessage(message, true);
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        // 화면 구성의 크기 (픽셀): 위의 메뉴 줄, 아래의 상태 줄, 왼쪽 목록, 오른쪽 값 표시.
        private const float k_topBarHeight = 34f;
        private const float k_statusHeight = 26f;
        private const float k_leftWidth = 250f;
        private const float k_rightWidth = 330f;
        private const float k_panelMargin = 6f;

        // 메뉴 항목 번호. 키로 하는 일은 전부 메뉴에도 있다 (사용자 결정: 블렌더 방식의 키와 함께 GUI 로도 쓸 수 있어야 한다).
        private const int k_menuOpenMission = 0;
        private const int k_menuOpenBlock = 1;
        private const int k_menuOpenPoints = 2;
        private const int k_menuQuit = 3;
        private const int k_menuSavePoints = 4;
        private const int k_menuSavePointsAs = 5;
        private const int k_menuDuplicate = 34;
        private const int k_menuDelete = 35;
        private const int k_menuScale = 36;
        private const int k_menuSaveBlocks = 6;
        private const int k_menuSaveBlocksAs = 7;
        private const int k_menuNew = 8;
        private const int k_menuPlay = 9;
        private const int k_menuImportMission = 50;
        private const int k_menuImportOfficial = 51;
        private const int k_menuImportBlock = 52;
        private const int k_menuImportPoints = 53;
        // 메뉴에는 없고 파일 창의 쓰임으로만 쓰는 번호: 변환한 미션을 쓸 자리.
        private const int k_menuImportTarget = 54;
        private const int k_menuSaveMissionAs = 56;
        // 메뉴에는 없고 파일 창의 쓰임으로만 쓰는 번호: 미션의 설정에 넣을 파일.
        private const int k_menuPickMissionPath = 57;
        private const int k_menuMessages = 58;
        private const int k_menuShowLinks = 59;
        // 메뉴에는 없고 파일 창의 쓰임으로만 쓰는 번호: 에셋 모드에서 열 파일, 새로 만들 파일.
        private const int k_menuOpenAsset = 60;
        private const int k_menuNewAsset = 61;
        private const int k_menuShowGrid = 62;
        private const int k_menuShowFloorGrid = 63;
        // 메뉴에는 없고 파일 창의 쓰임으로만 쓰는 번호: 텍스처로 쓸 이미지 고르기.
        private const int k_menuPickTexture = 40;
        // Add 메뉴에서 포인트 종류가 아니라 상자 블록을 놓는 항목의 번호.
        private const int k_menuAddBlock = -100;
        private const int k_menuViewFront = 10;
        private const int k_menuViewBack = 11;
        private const int k_menuViewRight = 12;
        private const int k_menuViewLeft = 13;
        private const int k_menuViewTop = 14;
        private const int k_menuViewBottom = 15;
        private const int k_menuViewOrthographic = 16;
        private const int k_menuViewFocus = 17;
        private const int k_menuViewAll = 18;
        private const int k_menuSelectAll = 20;
        private const int k_menuSelectNone = 21;
        private const int k_menuUndo = 30;
        private const int k_menuRedo = 31;
        private const int k_menuMove = 32;
        private const int k_menuRotate = 33;

        private static readonly Color s_errorColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color s_boxFill = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color s_boxBorder = new Color(1f, 1f, 1f, 0.9f);

        /// <summary>
        /// 목록의 걸러 보기 항목 하나.
        /// </summary>
        private readonly struct Filter
        {
            public readonly string Name;
            public readonly int First;
            public readonly int Last;
            public readonly int Other;

            public Filter(string name, int first, int last, int other = -1)
            {
                Name = name;
                First = first;
                Last = last;
                Other = other;
            }

            public bool Accepts(int type)
            {
                return (type >= First && type <= Last) || type == Other;
            }
        }

        private static readonly Filter[] s_filters =
        {
            new Filter("All points", int.MinValue, int.MaxValue),
            new Filter("Humans", MapLoader.PointHuman, MapLoader.PointHuman, MapLoader.PointHuman2),
            new Filter("Human info", MapLoader.PointHumanInfo, MapLoader.PointHumanInfo),
            new Filter("Weapons", MapLoader.PointWeapon, MapLoader.PointWeapon, MapLoader.PointRandomWeapon),
            new Filter("Objects", MapLoader.PointSmallObject, MapLoader.PointSmallObject),
            new Filter("AI paths", MapLoader.PointAIPath, MapLoader.PointAIPath, MapLoader.PointRandomAIPath),
            new Filter("Events", MapLoader.PointEventFirst, int.MaxValue),
        };

        private PopupMenu m_viewMenu;
        private OptionButton m_filter;
        private ItemList m_pointList;
        private Label m_details;
        private Label m_status;
        private Label m_title;
        private CheckBox m_xrayButton;
        private OptionButton m_modeOption;
        private OptionButton m_elementOption;
        private Panel m_boxPanel;
        private FileDialog m_fileDialog;
        private int m_fileDialogPurpose;
        // 목록의 줄 번호 → 포인트 번호. 걸러 보는 동안에는 둘이 다르다.
        private readonly List<int> m_listToPoint = new List<int>();
        // 코드가 목록의 선택 표시를 맞추는 동안에는 목록이 보내는 선택 신호를 무시한다.
        private bool m_syncingList;
        private string m_message = string.Empty;
        private bool m_messageIsError;

        /// <summary>
        /// 화면을 만든다: 위의 메뉴 줄, 왼쪽의 포인트 목록, 오른쪽의 값 표시, 아래의 상태 줄. 가운데는 비워 3D 화면이 보이고 클릭이 그리로 간다.
        /// </summary>
        private void BuildInterface()
        {
            var layer = new CanvasLayer();
            AddChild(layer);
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(root);

            // 사각형 선택의 사각형. 패널들보다 먼저 넣어 그 아래에 그려지게 한다.
            m_boxPanel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            var boxStyle = new StyleBoxFlat { BgColor = s_boxFill, BorderColor = s_boxBorder };
            boxStyle.SetBorderWidthAll(1);
            m_boxPanel.AddThemeStyleboxOverride("panel", boxStyle);
            root.AddChild(m_boxPanel);

            // 위: 메뉴, X-RAY, 열려 있는 파일 이름.
            var top = new PanelContainer();
            Dock(root, top, 0f, 0f, 1f, 0f, 0f, 0f, 0f, k_topBarHeight);
            var topRow = new HBoxContainer();
            top.AddChild(topRow);

            PopupMenu file = AddMenu(topRow, "File");
            file.AddItem("New map", k_menuNew);
            file.AddItem("Open mission (.mif2)...", k_menuOpenMission);
            file.AddItem("Open block (.bd2)...", k_menuOpenBlock);
            file.AddItem("Open points (.pd2)...", k_menuOpenPoints);
            file.AddSeparator();
            file.AddItem("Import official mission...", k_menuImportOfficial);
            file.AddItem("Import mission (.mif)...", k_menuImportMission);
            file.AddItem("Import block (.bd1)...", k_menuImportBlock);
            file.AddItem("Import points (.pd1)...", k_menuImportPoints);
            file.AddSeparator();
            file.AddItem("Save changes    Ctrl+S", k_menuSavePoints);
            file.AddItem("Save points as...", k_menuSavePointsAs);
            file.AddItem("Save blocks as...", k_menuSaveBlocksAs);
            file.AddItem("Save mission as...", k_menuSaveMissionAs);
            file.AddSeparator();
            file.AddItem("Play test    F5", k_menuPlay);
            file.AddSeparator();
            file.AddItem("Quit", k_menuQuit);

            PopupMenu edit = AddMenu(topRow, "Edit");
            edit.AddItem("Undo    Ctrl+Z", k_menuUndo);
            edit.AddItem("Redo    Ctrl+Shift+Z", k_menuRedo);
            edit.AddSeparator();
            edit.AddItem("Move    G", k_menuMove);
            edit.AddItem("Rotate    R", k_menuRotate);
            edit.AddItem("Scale    S", k_menuScale);
            edit.AddSeparator();
            edit.AddItem("Duplicate    Shift+D", k_menuDuplicate);
            edit.AddItem("Delete    X, Delete", k_menuDelete);
            edit.AddSeparator();
            edit.AddItem("Messages...", k_menuMessages);

            // 새 포인트 놓기. 메뉴에서 고르면 3D 화면의 가운데 아래에, Shift+A 로 띄운 메뉴에서 고르면 마우스 아래에 놓인다.
            var add = new MenuButton { Text = "Add", Flat = false, FocusMode = Control.FocusModeEnum.None, TooltipText = "Add a point (Shift+A adds under the mouse)" };
            topRow.AddChild(add);
            // 항목은 이벤트 종류의 목록을 읽은 뒤에 채운다 (ReloadCatalog).
            m_addBarMenu = add.GetPopup();
            m_addBarMenu.IdPressed += type => AddFromBar((int)type);
            m_addMenu = new PopupMenu();
            m_addMenu.IdPressed += type => AddPoint((int)type, m_addMenuScreenPosition);
            layer.AddChild(m_addMenu);
            m_coincidentMenu = new PopupMenu();
            m_coincidentMenu.IdPressed += OnCoincidentChosen;
            layer.AddChild(m_coincidentMenu);

            PopupMenu view = AddMenu(topRow, "View");
            view.AddItem("Front    Numpad 1", k_menuViewFront);
            view.AddItem("Back    Ctrl+Numpad 1", k_menuViewBack);
            view.AddItem("Right    Numpad 3", k_menuViewRight);
            view.AddItem("Left    Ctrl+Numpad 3", k_menuViewLeft);
            view.AddItem("Top    Numpad 7", k_menuViewTop);
            view.AddItem("Bottom    Ctrl+Numpad 7", k_menuViewBottom);
            view.AddSeparator();
            view.AddItem("Perspective / Orthographic    Numpad 5", k_menuViewOrthographic);
            view.AddItem("Frame selected    F, Numpad .", k_menuViewFocus);
            view.AddItem("Frame all", k_menuViewAll);
            view.AddSeparator();
            view.AddCheckItem("Links between points (events, paths)", k_menuShowLinks);
            view.SetItemChecked(view.GetItemIndex(k_menuShowLinks), m_showLinks);
            view.AddCheckItem("Grid in orthographic views", k_menuShowGrid);
            view.SetItemChecked(view.GetItemIndex(k_menuShowGrid), m_showGrid);
            view.AddCheckItem("Floor grid in perspective (height 0)", k_menuShowFloorGrid);
            view.SetItemChecked(view.GetItemIndex(k_menuShowFloorGrid), m_showFloorGrid);
            m_viewMenu = view;

            PopupMenu select = AddMenu(topRow, "Select");
            select.AddItem("All    A", k_menuSelectAll);
            select.AddItem("None    Alt+A", k_menuSelectNone);

            m_xrayButton = new CheckBox { Text = "X-Ray", FocusMode = Control.FocusModeEnum.None, TooltipText = "Show and select points hidden behind blocks (Alt+Z)" };
            m_xrayButton.Toggled += pressed =>
            {
                if (pressed != m_xray) SetXray(pressed);
            };
            topRow.AddChild(m_xrayButton);
            m_modelsButton = new CheckBox { Text = "Models", ButtonPressed = m_markers.ShowModels, FocusMode = Control.FocusModeEnum.None, TooltipText = "Show humans, weapons and objects as their models. Off: boxes only" };
            m_modelsButton.Toggled += SetShowModels;
            topRow.AddChild(m_modelsButton);

            // 변형 도구. 누르면 키(G / R)와 같이 마우스로 움직이고 클릭으로 확정한다.
            topRow.AddChild(new VSeparator());
            AddToolButton(topRow, "Move", "Move the selected points (G). Then X / Y / Z: axis, numbers: value, click: confirm, right click: cancel", () => BeginTransform(TransformMode.Move));
            AddToolButton(topRow, "Rotate", "Rotate the selection (R). Points turn around the vertical axis; block elements around the axis chosen with X / Y / Z", () => BeginTransform(TransformMode.Rotate));
            AddToolButton(topRow, "Scale", "Scale the selection around its center (S)", () => BeginTransform(TransformMode.Scale));
            BuildSnapControls(topRow);
            var gridSize = new SpinBox
            {
                MinValue = k_minGridSize, MaxValue = k_maxGridSize, Step = k_minGridSize, Value = m_gridSize, Suffix = "m",
                TooltipText = "Grid size in meters, for the Grid snap and the grid drawn in orthographic views (0.1 m is one unit of the original XOPS)",
            };
            gridSize.ValueChanged += value => m_gridSize = (float)value;
            topRow.AddChild(gridSize);
            topRow.AddChild(new VSeparator());

            // 편집 모드와 블록의 선택 단위.
            m_modeOption = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Edit mode (Tab)" };
            m_modeOption.AddItem("Point mode");
            m_modeOption.AddItem("Block mode");
            m_modeOption.AddItem("Mission mode");
            m_modeOption.AddItem("Asset mode");
            m_modeOption.ItemSelected += index => SetEditMode((EditMode)(int)index);
            topRow.AddChild(m_modeOption);
            m_elementOption = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "What to select in block mode (1 / 2 / 3 / 4)", Visible = false };
            foreach (BlockElement element in System.Enum.GetValues<BlockElement>())
            {
                m_elementOption.AddItem(element.ToString());
            }
            m_elementOption.ItemSelected += index => SetBlockElement((BlockElement)(int)index);
            topRow.AddChild(m_elementOption);
            m_overlapButton = new CheckBox { Text = "Ask overlap", FocusMode = Control.FocusModeEnum.None, Visible = false, TooltipText = "When vertices or edges of several blocks share the clicked spot, ask which block's one to select instead of selecting them all (same as Ctrl + click)" };
            m_overlapButton.Toggled += pressed => m_askOverlap = pressed;
            topRow.AddChild(m_overlapButton);
            topRow.AddChild(new VSeparator());

            AddToolButton(topRow, "Play", "Play the map as it is now, unsaved changes included (F5). Esc in the game comes back here", () => PlayTest());
            topRow.AddChild(new VSeparator());

            m_title = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
            topRow.AddChild(m_title);

            // 왼쪽: 걸러 보기와 포인트 목록.
            var left = new PanelContainer();
            Dock(root, left, 0f, 0f, 0f, 1f, 0f, k_topBarHeight, k_leftWidth, -k_statusHeight);
            var leftColumn = new VBoxContainer();
            left.AddChild(leftColumn);
            m_filter = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            foreach (Filter filter in s_filters)
            {
                m_filter.AddItem(filter.Name);
            }
            m_filter.ItemSelected += _ => RefreshPointList();
            leftColumn.AddChild(m_filter);
            BuildEntryBox(leftColumn);
            m_pointList = new ItemList
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
                SelectMode = ItemList.SelectModeEnum.Multi,
            };
            m_pointList.MultiSelected += (_, _) => OnListSelectionChanged();
            m_pointList.ItemActivated += _ => FocusSelection();
            leftColumn.AddChild(m_pointList);

            // 오른쪽: 선택한 포인트의 값.
            var right = new PanelContainer();
            Dock(root, right, 1f, 0f, 1f, 1f, -k_rightWidth, k_topBarHeight, 0f, -k_statusHeight);
            var rightMargin = new MarginContainer();
            foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            {
                rightMargin.AddThemeConstantOverride(side, (int)k_panelMargin);
            }
            right.AddChild(rightMargin);
            // 칸이 패널보다 길어지면 세로로 굴린다.
            var rightScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            rightMargin.AddChild(rightScroll);
            var rightColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            rightScroll.AddChild(rightColumn);
            m_details = new Label { VerticalAlignment = VerticalAlignment.Top, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            rightColumn.AddChild(m_details);
            BuildInspector(rightColumn);
            BuildEventBox(rightColumn, layer);
            BuildBlockInspector(rightColumn);

            // 아래: 상태 줄.
            var bottom = new PanelContainer();
            Dock(root, bottom, 0f, 1f, 1f, 1f, 0f, -k_statusHeight, 0f, 0f);
            m_status = new Label { ClipText = true };
            bottom.AddChild(m_status);

            // 미션 모드의 화면. 양옆의 패널보다 뒤에 넣어 그 위를 덮는다.
            BuildMissionPanel(root);
            BuildAssetPanel(root, layer);
            BuildMessageDialog(layer);

            m_fileDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenFile,
                CurrentDir = GamePath.Root,
                Size = new Vector2I(760, 480),
            };
            m_fileDialog.FileSelected += OnFileSelected;
            layer.AddChild(m_fileDialog);

            BuildOfficialDialog(layer);
            m_discardDialog = new ConfirmationDialog { Title = "Unsaved changes", DialogText = "There are unsaved changes. Discard them?", OkButtonText = "Discard" };
            m_discardDialog.Confirmed += () => m_discardAction?.Invoke();
            layer.AddChild(m_discardDialog);

            UpdateDetails();
        }

        /// <summary>
        /// 메뉴 줄에 메뉴 하나를 더한다.
        /// </summary>
        /// <param name="row">메뉴 줄.</param>
        /// <param name="title">메뉴 이름.</param>
        /// <returns>항목을 넣을 팝업.</returns>
        private PopupMenu AddMenu(HBoxContainer row, string title)
        {
            var button = new MenuButton { Text = title, Flat = false, FocusMode = Control.FocusModeEnum.None };
            row.AddChild(button);
            PopupMenu popup = button.GetPopup();
            popup.IdPressed += OnMenu;
            return popup;
        }

        /// <summary>
        /// 메뉴 줄에 도구 버튼 하나를 더한다.
        /// </summary>
        /// <param name="row">메뉴 줄.</param>
        /// <param name="text">버튼의 글자.</param>
        /// <param name="tooltip">마우스를 올렸을 때의 설명.</param>
        /// <param name="action">눌렀을 때 할 일.</param>
        private static void AddToolButton(HBoxContainer row, string text, string tooltip, System.Action action)
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
            button.Pressed += action;
            row.AddChild(button);
        }

        /// <summary>
        /// 패널을 부모의 한쪽에 붙인다.
        /// </summary>
        /// <param name="parent">부모.</param>
        /// <param name="control">붙일 패널.</param>
        /// <param name="anchorLeft">왼쪽 변의 기준 (부모 너비의 비율).</param>
        /// <param name="anchorTop">위쪽 변의 기준.</param>
        /// <param name="anchorRight">오른쪽 변의 기준.</param>
        /// <param name="anchorBottom">아래쪽 변의 기준.</param>
        /// <param name="left">왼쪽 변의 오프셋 (픽셀).</param>
        /// <param name="top">위쪽 변의 오프셋.</param>
        /// <param name="right">오른쪽 변의 오프셋.</param>
        /// <param name="bottom">아래쪽 변의 오프셋.</param>
        private static void Dock(Control parent, Control control, float anchorLeft, float anchorTop, float anchorRight, float anchorBottom,
            float left, float top, float right, float bottom)
        {
            parent.AddChild(control);
            control.AnchorLeft = anchorLeft;
            control.AnchorTop = anchorTop;
            control.AnchorRight = anchorRight;
            control.AnchorBottom = anchorBottom;
            control.OffsetLeft = left;
            control.OffsetTop = top;
            control.OffsetRight = right;
            control.OffsetBottom = bottom;
        }

        /// <summary>
        /// 메뉴 항목을 눌렀을 때.
        /// </summary>
        /// <param name="id">항목 번호.</param>
        private void OnMenu(long id)
        {
            // 미션 모드에는 3D 화면이 없다. 선택과 변형의 항목은 듣지 않는다.
            if (m_editMode >= EditMode.Mission && id is k_menuSelectAll or k_menuSelectNone or k_menuMove or k_menuRotate or k_menuScale or k_menuDuplicate or k_menuDelete) return;

            switch ((int)id)
            {
                case k_menuSaveMissionAs: ShowFileDialog(k_menuSaveMissionAs, "Save mission as", "*" + MIF2File.Extension, true); break;
                case k_menuMessages: ShowMessageDialog(); break;
                case k_menuShowGrid:
                    m_showGrid = !m_showGrid;
                    m_viewMenu.SetItemChecked(m_viewMenu.GetItemIndex(k_menuShowGrid), m_showGrid);
                    break;
                case k_menuShowFloorGrid:
                    m_showFloorGrid = !m_showFloorGrid;
                    m_viewMenu.SetItemChecked(m_viewMenu.GetItemIndex(k_menuShowFloorGrid), m_showFloorGrid);
                    break;
                case k_menuShowLinks:
                    m_showLinks = !m_showLinks;
                    m_viewMenu.SetItemChecked(m_viewMenu.GetItemIndex(k_menuShowLinks), m_showLinks);
                    RedrawLinks();
                    break;
                case k_menuNew: ConfirmDiscard(NewMap); break;
                case k_menuOpenMission: ConfirmDiscard(() => ShowFileDialog(k_menuOpenMission, "Open mission", "*" + MIF2File.Extension, false)); break;
                case k_menuOpenBlock: ShowFileDialog(k_menuOpenBlock, "Open block", "*" + BD2File.Extension, false); break;
                case k_menuOpenPoints: ConfirmDiscard(() => ShowFileDialog(k_menuOpenPoints, "Open points", "*" + PD2File.Extension, false)); break;
                case k_menuPlay: PlayTest(); break;
                case k_menuImportOfficial: ConfirmDiscard(ShowOfficialDialog); break;
                case k_menuImportMission: ConfirmDiscard(() => ShowFileDialog(k_menuImportMission, "Import mission", "*.mif", false)); break;
                case k_menuImportBlock: ShowFileDialog(k_menuImportBlock, "Import block", "*.bd1", false); break;
                case k_menuImportPoints: ConfirmDiscard(() => ShowFileDialog(k_menuImportPoints, "Import points", "*.pd1", false)); break;
                case k_menuSavePoints: SaveAll(); break;
                case k_menuSavePointsAs: ShowFileDialog(k_menuSavePointsAs, "Save points as", "*" + PD2File.Extension, true); break;
                case k_menuSaveBlocksAs: ShowFileDialog(k_menuSaveBlocksAs, "Save blocks as", "*" + BD2File.Extension, true); break;
                case k_menuScale: BeginTransform(TransformMode.Scale); break;
                case k_menuQuit: ConfirmDiscard(() => GetTree().Quit()); break;
                case k_menuDuplicate:
                    if (m_editMode == EditMode.Block) DuplicateBlocks();
                    else DuplicateSelection();
                    break;
                case k_menuDelete:
                    if (m_editMode == EditMode.Block) DeleteBlocks();
                    else DeleteSelection();
                    break;
                case k_menuViewFront: ViewFront(false); break;
                case k_menuViewBack: ViewFront(true); break;
                case k_menuViewRight: ViewRight(false); break;
                case k_menuViewLeft: ViewRight(true); break;
                case k_menuViewTop: ViewTop(false); break;
                case k_menuViewBottom: ViewTop(true); break;
                case k_menuViewOrthographic: ToggleProjection(); break;
                case k_menuViewFocus: FocusSelection(); break;
                case k_menuViewAll: FocusAll(); break;
                case k_menuSelectAll: SelectAll(); break;
                case k_menuSelectNone: SelectNone(); break;
                case k_menuUndo: Undo(); break;
                case k_menuRedo: Redo(); break;
                case k_menuMove: BeginTransform(TransformMode.Move); break;
                case k_menuRotate: BeginTransform(TransformMode.Rotate); break;
            }
        }

        /// <summary>
        /// 파일 고르는 창을 띄운다.
        /// </summary>
        /// <param name="purpose">고른 파일로 할 일 (메뉴 항목 번호).</param>
        /// <param name="title">창 제목.</param>
        /// <param name="filter">보여 줄 파일 (예: *.pd2).</param>
        /// <param name="save">true 면 저장할 이름을 받는 창이다.</param>
        private void ShowFileDialog(int purpose, string title, string filter, bool save)
        {
            m_fileDialogPurpose = purpose;
            m_fileDialog.FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile;
            m_fileDialog.Title = title;
            m_fileDialog.Filters = new[] { filter };
            m_fileDialog.PopupCentered();
        }

        /// <summary>
        /// 파일을 골랐을 때. 게임 폴더 안의 파일만 받는다 (경로를 exe 폴더 기준으로 적어야 하기 때문이다).
        /// </summary>
        /// <param name="path">고른 파일의 전체 경로.</param>
        private void OnFileSelected(string path)
        {
            string relative = Path.GetRelativePath(GamePath.Root, path).Replace('\\', '/');
            if (relative.StartsWith("..", System.StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                Fail($"The file must be inside the game folder: {path}");
                return;
            }

            switch (m_fileDialogPurpose)
            {
                case k_menuOpenMission: OpenMission(relative); break;
                case k_menuOpenBlock: OpenBlock(relative); break;
                case k_menuOpenPoints: OpenPoints(relative); break;
                case k_menuSavePointsAs: SavePointsTo(relative); break;
                case k_menuSaveBlocksAs: SaveBlocksTo(relative); break;
                case k_menuPickTexture: SetTexture(relative, m_textureReplaceIndex); break;
                case k_menuImportBlock: ImportBlock(relative); break;
                case k_menuImportPoints: ImportPoints(relative); break;
                case k_menuImportMission: AskImportTarget(relative, 0, Path.GetFileNameWithoutExtension(relative)); break;
                case k_menuImportTarget: ImportMission(relative); break;
                case k_menuSaveMissionAs: SaveMissionTo(relative); break;
                case k_menuPickMissionPath: m_missionPathSetter?.Invoke(relative); break;
                case k_menuOpenAsset: OpenAsset(relative); break;
                case k_menuNewAsset: CreateAsset(relative, m_newAssetKind); break;
            }
        }

        /// <summary>
        /// 3D 화면(패널에 가려지지 않은 가운데 부분)의 가운데.
        /// </summary>
        /// <returns>화면 좌표 (픽셀).</returns>
        private Vector2 ViewportCenter()
        {
            Vector2 size = GetViewport().GetVisibleRect().Size;
            return new Vector2((k_leftWidth + size.X - k_rightWidth) * 0.5f, (k_topBarHeight + size.Y - k_statusHeight) * 0.5f);
        }

        /// <summary>
        /// 사각형 선택의 사각형을 보이거나 감춘다.
        /// </summary>
        /// <param name="visible">보일지.</param>
        /// <param name="rect">화면 사각형 (픽셀).</param>
        private void ShowBox(bool visible, Rect2 rect)
        {
            m_boxPanel.Visible = visible;
            if (!visible) return;

            m_boxPanel.Position = rect.Position;
            m_boxPanel.Size = rect.Size;
        }

        /// <summary>
        /// 모드와 선택 단위의 드롭다운을 지금 상태에 맞춘다. 포인트 목록은 포인트 모드에서만 고를 수 있다.
        /// </summary>
        private void SyncModeButtons()
        {
            if (m_modeOption == null) return;

            m_modeOption.Select((int)m_editMode);
            m_elementOption.Visible = m_editMode == EditMode.Block;
            m_elementOption.Select((int)m_blockElement);
            SyncSnapButtons();
        }

        /// <summary>
        /// X-RAY 체크 상자를 지금 상태에 맞춘다.
        /// </summary>
        private void SyncXrayButton()
        {
            if (m_xrayButton != null) m_xrayButton.SetPressedNoSignal(m_xray);
        }

        /// <summary>
        /// 포인트 목록을 지금의 걸러 보기대로 다시 채운다.
        /// </summary>
        private void RefreshPointList()
        {
            Filter filter = s_filters[Mathf.Max(0, m_filter.Selected)];
            List<PD2Point> points = m_document.Points.points;

            // 이벤트 보기: 번호 순서가 아니라 줄마다 따라간 순서로 보여 주고, 줄의 시작 번호를 고치는 칸을 함께 보인다.
            bool eventView = filter.Name == k_eventFilterName;
            m_entryBox.Visible = eventView;
            if (!m_entryEdit.HasFocus()) m_entryEdit.Text = string.Join(", ", m_document.Points.eventEntryIds);

            m_syncingList = true;
            m_pointList.Clear();
            m_listToPoint.Clear();
            if (eventView) FillEventList();
            for (int i = 0; i < points.Count && !eventView; i++)
            {
                PD2Point point = points[i];
                if (!filter.Accepts(point.type)) continue;

                PointTypeInfo.Info info = PointTypeInfo.Get(point.type);
                int row = m_pointList.AddItem($"#{i}  {info.Name}  id {point.id}");
                m_pointList.SetItemCustomFgColor(row, info.Color);
                m_listToPoint.Add(i);
            }
            m_syncingList = false;
            SyncListSelection();
        }

        /// <summary>
        /// 목록에서 선택을 바꿨을 때. 목록에 보이는 줄의 선택만 바꾸고, 걸러져 보이지 않는 포인트의 선택은 그대로 둔다.
        /// </summary>
        private void OnListSelectionChanged()
        {
            if (m_syncingList) return;

            foreach (int point in m_listToPoint)
            {
                m_selection.Remove(point);
            }
            foreach (int row in m_pointList.GetSelectedItems())
            {
                if (m_listToPoint[row] != k_listNoPoint) m_selection.Add(m_listToPoint[row]);
            }
            // 목록에서 포인트를 고르면 포인트 모드로 돌아간다.
            if (m_editMode != EditMode.Point) SetEditMode(EditMode.Point);
            m_markers.SetSelection(m_selection);
            UpdateDetails();
        }

        /// <summary>
        /// 목록의 선택 표시를 지금 선택에 맞춘다.
        /// </summary>
        private void SyncListSelection()
        {
            m_syncingList = true;
            m_pointList.DeselectAll();
            int firstRow = -1;
            for (int row = 0; row < m_listToPoint.Count; row++)
            {
                if (!m_selection.Contains(m_listToPoint[row])) continue;

                m_pointList.Select(row, false);
                if (firstRow < 0) firstRow = row;
            }
            m_syncingList = false;

            // 하나만 골랐을 때는 목록이 그 줄을 보여 주게 한다.
            if (m_selection.Count == 1 && firstRow >= 0)
            {
                m_pointList.SetItemSelectable(firstRow, true);
                m_pointList.EnsureCurrentIsVisible();
            }
        }

        /// <summary>
        /// 오른쪽의 값 표시를 다시 쓴다.
        /// </summary>
        private void UpdateDetails()
        {
            if (m_details == null) return;

            m_details.Text = DescribeSelection();
            RefreshInspector();
            RefreshBlockInspector();
        }

        /// <summary>
        /// 상태 줄에 띄울 알림을 정한다. 다음 알림이 올 때까지 남는다.
        /// </summary>
        /// <param name="message">알림 (영어).</param>
        /// <param name="isError">true 면 에러 색으로 보인다.</param>
        private void SetMessage(string message, bool isError = false)
        {
            m_message = message;
            m_messageIsError = isError;
            if (isError) Debugger.LogError(message, nameof(XopsEditor));
        }

        /// <summary>
        /// 위의 파일 이름과 아래의 상태 줄을 다시 쓴다.
        /// </summary>
        private void UpdateStatus()
        {
            string block = string.IsNullOrEmpty(m_document.BlockPath) ? "(no block file)" : m_document.BlockPath;
            string points = string.IsNullOrEmpty(m_document.PointPath) ? "(no point file)" : m_document.PointPath;
            string mission = string.IsNullOrEmpty(m_document.MissionPath) ? "(no mission file)" : m_document.MissionPath;
            if (AssetsDirty()) mission = "data files *   |   " + mission;
            m_title.Text = $"   {mission}{(m_missionDirty ? " *" : string.Empty)}   |   {block}{(m_blockDirty ? " *" : string.Empty)}   |   {points}{(m_dirty ? " *" : string.Empty)}";

            Vector3 pivot = m_view.Pivot;
            string view = string.Format(CultureInfo.InvariantCulture, "{0} view, center {1:0.0}, {2:0.0}, {3:0.0}",
                m_view.Orthographic ? "Orthographic" : "Perspective", pivot.X, pivot.Y, pivot.Z);
            int selected = m_editMode == EditMode.Block ? m_blockSelection.Count : m_selection.Count;
            m_status.Text = $" {m_message}   |   Blocks {m_document.Blocks.blocks.Count}, points {m_document.Points.points.Count}, selected {selected}   |   {view}   |   Middle drag: orbit, Shift+middle: pan, wheel: zoom, right hold: fly";
            m_status.Modulate = m_messageIsError ? s_errorColor : Colors.White;
        }
    }
}

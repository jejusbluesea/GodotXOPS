using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 포인트의 값 가운데 칸에서 고칠 수 있는 것. 여러 개를 선택했을 때 "무엇을 고칠지"의 목록이기도 하다.
        /// </summary>
        private enum PointField
        {
            Type,
            Id,
            P2,
            P3,
            X,
            Y,
            Z,
            Direction,
        }

        private const string k_backupExtension = ".bak";
        private const string k_messageExtension = ".msg";
        // 실수 칸의 한 칸 단위 (m, 도).
        private const double k_floatStep = 0.01;
        // 새 포인트를 놓을 블록 면을 찾지 못했을 때 블록 레이를 쏘는 최대 거리는 두지 않는다 (0 이면 무한).
        private const float k_unlimited = 0f;

        // 값 칸들. 선택이 하나일 때 보인다.
        private Control m_singleBox;
        private OptionButton m_typeOption;
        private readonly Dictionary<PointField, SpinBox> m_fieldBoxes = new Dictionary<PointField, SpinBox>();
        private Label m_p2Label;
        private Label m_p3Label;
        private LineEdit m_extraEdit;
        // 여러 개를 선택했을 때의 칸: 무엇을 고칠지, 값, 적용.
        private Control m_multiBox;
        private OptionButton m_multiField;
        private SpinBox m_multiValue;
        // 종류 드롭다운의 줄 번호 → 종류 번호.
        private readonly List<int> m_typeOptionTypes = new List<int>();
        // 코드가 칸의 값을 채우는 동안에는 칸이 보내는 변경 신호를 무시한다.
        private bool m_fillingInspector;
        private PopupMenu m_addMenu;
        private Vector2 m_addMenuScreenPosition;
        private ConfirmationDialog m_discardDialog;
        private Action m_discardAction;

        /// <summary>
        /// 오른쪽 패널의 값 칸들을 만든다.
        /// </summary>
        /// <param name="column">칸을 넣을 세로 상자.</param>
        private void BuildInspector(VBoxContainer column)
        {
            var grid = new GridContainer { Columns = 2, Visible = false };
            grid.AddThemeConstantOverride("h_separation", 8);
            m_singleBox = grid;
            column.AddChild(grid);

            grid.AddChild(new Label { Text = "Type" });
            m_typeOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
            m_typeOption.ItemSelected += index =>
            {
                if (!m_fillingInspector) ApplyField(PointField.Type, m_typeOptionTypes[(int)index]);
            };
            grid.AddChild(m_typeOption);

            AddFieldRow(grid, PointField.Id, "Id", false);
            m_p2Label = AddFieldRow(grid, PointField.P2, "P2", false);
            m_p3Label = AddFieldRow(grid, PointField.P3, "P3", false);
            AddFieldRow(grid, PointField.X, "X (m)", true);
            AddFieldRow(grid, PointField.Y, "Y (m)", true);
            AddFieldRow(grid, PointField.Z, "Z (m)", true);
            AddFieldRow(grid, PointField.Direction, "Direction (deg)", true);

            m_extraLabel = new Label { Text = "Extra", TooltipText = "Extra parameter cells (e0, e1, ...), comma separated integers" };
            grid.AddChild(m_extraLabel);
            m_extraEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, PlaceholderText = "none" };
            m_extraEdit.TextSubmitted += text =>
            {
                ApplyExtra(text);
                m_extraEdit.ReleaseFocus();
            };
            grid.AddChild(m_extraEdit);

            var multi = new VBoxContainer { Visible = false };
            m_multiBox = multi;
            column.AddChild(multi);
            multi.AddChild(new Label { Text = "Change for all selected:" });
            m_multiField = new OptionButton { FocusMode = Control.FocusModeEnum.None };
            foreach (PointField field in Enum.GetValues<PointField>())
            {
                m_multiField.AddItem(field.ToString());
            }
            m_multiField.ItemSelected += _ => ConfigureMultiValue();
            multi.AddChild(m_multiField);
            m_multiValue = CreateNumberBox(false);
            multi.AddChild(m_multiValue);
            var apply = new Button { Text = "Apply", FocusMode = Control.FocusModeEnum.None };
            apply.Pressed += () => ApplyField((PointField)m_multiField.Selected, m_multiValue.Value);
            multi.AddChild(apply);
            ConfigureMultiValue();
        }

        /// <summary>
        /// 값 칸 한 줄(이름과 숫자 칸)을 더한다.
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="field">이 칸이 고치는 값.</param>
        /// <param name="title">이름.</param>
        /// <param name="isFloat">실수 칸인지.</param>
        /// <returns>이름 라벨 (종류에 따라 이름이 바뀌는 칸이 쓴다).</returns>
        private Label AddFieldRow(GridContainer grid, PointField field, string title, bool isFloat)
        {
            var label = new Label { Text = title };
            grid.AddChild(label);
            SpinBox box = CreateNumberBox(isFloat);
            box.ValueChanged += value =>
            {
                if (!m_fillingInspector) ApplyField(field, value);
            };
            grid.AddChild(box);
            m_fieldBoxes[field] = box;
            return label;
        }

        /// <summary>
        /// 숫자 칸을 만든다. 범위 제한이 없다 (PD2 의 값은 int32 전체다).
        /// </summary>
        /// <param name="isFloat">실수 칸인지.</param>
        /// <returns>숫자 칸.</returns>
        private static SpinBox CreateNumberBox(bool isFloat)
        {
            return new SpinBox
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MinValue = int.MinValue,
                MaxValue = int.MaxValue,
                Step = isFloat ? k_floatStep : 1.0,
                Rounded = !isFloat,
                AllowGreater = true,
                AllowLesser = true,
            };
        }

        /// <summary>
        /// 여러 개를 고칠 때의 값 칸을 고른 항목에 맞춘다 (정수 칸인지 실수 칸인지).
        /// </summary>
        private void ConfigureMultiValue()
        {
            bool isFloat = (PointField)m_multiField.Selected >= PointField.X;
            m_multiValue.Step = isFloat ? k_floatStep : 1.0;
            m_multiValue.Rounded = !isFloat;
        }

        /// <summary>
        /// 값 칸들을 지금 선택에 맞춘다: 하나면 그 포인트의 값을 채우고, 여럿이면 "무엇을 고칠지" 칸을 보인다.
        /// </summary>
        private void RefreshInspector()
        {
            if (m_singleBox == null) return;

            // 값 칸은 포인트의 것이다. 블록 모드에서는 감춘다.
            bool pointMode = m_editMode == EditMode.Point;
            List<PD2Point> points = m_document.Points.points;
            m_singleBox.Visible = pointMode && m_selection.Count == 1;
            m_multiBox.Visible = pointMode && m_selection.Count > 1;
            RefreshEventBox();
            if (!m_singleBox.Visible) return;

            PD2Point point = points[m_selection.Min];
            PointTypeInfo.Info info = PointTypeInfo.Get(point.type);
            m_fillingInspector = true;

            m_typeOption.Clear();
            m_typeOptionTypes.Clear();
            foreach (int type in PointTypeInfo.EditableTypes)
            {
                m_typeOption.AddItem($"{PointTypeInfo.Get(type).Name} ({type})");
                m_typeOptionTypes.Add(type);
            }
            foreach (EventCatalog.Group group in m_catalog.Groups)
            {
                foreach (EventDefinitionData definition in group.Events)
                {
                    m_typeOption.AddItem($"{definition.name} ({definition.type})");
                    m_typeOptionTypes.Add(definition.type);
                }
            }
            if (!m_typeOptionTypes.Contains(point.type))
            {
                m_typeOption.AddItem($"{info.Name} ({point.type})");
                m_typeOptionTypes.Add(point.type);
            }
            m_typeOption.Select(m_typeOptionTypes.IndexOf(point.type));

            m_p2Label.Text = info.P2;
            m_p3Label.Text = info.P3;
            m_fieldBoxes[PointField.Id].SetValueNoSignal(point.id);
            m_fieldBoxes[PointField.P2].SetValueNoSignal(point.param1);
            m_fieldBoxes[PointField.P3].SetValueNoSignal(point.param2);
            m_fieldBoxes[PointField.X].SetValueNoSignal(point.position.X);
            m_fieldBoxes[PointField.Y].SetValueNoSignal(point.position.Y);
            m_fieldBoxes[PointField.Z].SetValueNoSignal(point.position.Z);
            m_fieldBoxes[PointField.Direction].SetValueNoSignal(point.direction);
            if (!m_extraEdit.HasFocus()) m_extraEdit.Text = string.Join(", ", point.extra);

            m_fillingInspector = false;
        }

        /// <summary>
        /// 선택한 포인트 전부의 값 하나를 바꾼다 (되돌릴 수 있다).
        /// </summary>
        /// <param name="field">바꿀 값.</param>
        /// <param name="value">새 값. 정수 칸이면 반올림한다.</param>
        private void ApplyField(PointField field, double value)
        {
            if (m_selection.Count == 0 || Transforming) return;

            List<PD2Point> points = m_document.Points.points;
            var indices = new int[m_selection.Count];
            var before = new PD2Point[m_selection.Count];
            int slot = 0;
            bool changed = false;
            foreach (int index in m_selection)
            {
                PD2Point point = points[index];
                indices[slot] = index;
                before[slot++] = PointChangeCommand.Clone(point);
                changed |= SetField(point, field, value);
            }
            if (!changed) return;

            PushPointChange(new PointChangeCommand($"Edit {field}", points, indices, before));
        }

        /// <summary>
        /// 선택한 포인트 하나의 추가 파라미터 칸들을 바꾼다. 쉼표로 나눈 정수들을 받는다.
        /// </summary>
        /// <param name="text">칸의 글. 비어 있으면 추가 파라미터를 없앤다.</param>
        private void ApplyExtra(string text)
        {
            if (m_selection.Count != 1 || Transforming) return;

            var cells = new List<int>();
            foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cell))
                {
                    Fail($"Extra parameters must be integers separated by commas: \"{part}\"");
                    return;
                }
                cells.Add(cell);
            }

            List<PD2Point> points = m_document.Points.points;
            int index = m_selection.Min;
            PD2Point[] before = { PointChangeCommand.Clone(points[index]) };
            points[index].extra = cells.ToArray();
            PushPointChange(new PointChangeCommand("Edit Extra", points, new[] { index }, before));
        }

        /// <summary>
        /// 포인트의 값 하나를 쓴다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <param name="field">쓸 값.</param>
        /// <param name="value">새 값.</param>
        /// <returns>값이 바뀌었으면 true.</returns>
        private static bool SetField(PD2Point point, PointField field, double value)
        {
            int whole = (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
            float real = (float)value;
            Vector3 position = point.position;
            switch (field)
            {
                case PointField.Type:
                    if (point.type == whole) return false;
                    point.type = whole;
                    return true;
                case PointField.Id:
                    if (point.id == whole) return false;
                    point.id = whole;
                    return true;
                case PointField.P2:
                    if (point.param1 == whole) return false;
                    point.param1 = whole;
                    return true;
                case PointField.P3:
                    if (point.param2 == whole) return false;
                    point.param2 = whole;
                    return true;
                case PointField.X: position.X = real; break;
                case PointField.Y: position.Y = real; break;
                case PointField.Z: position.Z = real; break;
                case PointField.Direction:
                    if (point.direction == real) return false;
                    point.direction = real;
                    return true;
            }
            if (!float.IsFinite(real) || position == point.position) return false;

            point.position = position;
            return true;
        }

        /// <summary>
        /// 포인트를 바꾼 편집을 기록하고 화면을 맞춘다.
        /// </summary>
        /// <param name="command">이미 적용된 편집.</param>
        private void PushPointChange(IEditorCommand command)
        {
            m_history.Push(command);
            m_dirty = true;
            RebuildPoints();
            SetMessage(command.Name);
        }

        /// <summary>
        /// 포인트의 종류·식별번호·개수가 바뀌었을 수 있을 때: 표식과 목록을 다시 만들고 선택을 유지한다.
        /// </summary>
        private void RebuildPoints()
        {
            int count = m_document.Points.points.Count;
            m_selection.RemoveWhere(index => index >= count);
            m_markers.Rebuild(m_document.Points.points);
            m_markers.SetSelection(m_selection);
            RefreshPointList();
            RedrawLinks();
            UpdateDetails();
        }

        /// <summary>
        /// 새 포인트를 놓는다. 화면의 그 자리 아래에 블록 면이 있으면 그 위에, 없으면 시점의 중심에 놓는다. 놓은 포인트가 선택된다.
        /// 식별번호는 같은 종류에서 가장 큰 번호의 다음이다.
        /// </summary>
        /// <param name="type">포인트 종류.</param>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        private void AddPoint(int type, Vector2 screenPosition)
        {
            if (Transforming) CancelTransform();

            List<PD2Point> points = m_document.Points.points;
            PD2Point[] before = PointListCommand.Snapshot(points);
            // 이벤트끼리는 종류가 달라도 식별번호로 서로를 찾으므로 이벤트 전체에서 겹치지 않는 번호를 준다.
            int id = type >= MapLoader.PointEventFirst ? NextEventId() : 0;
            foreach (PD2Point point in points)
            {
                if (point.type == type) id = Math.Max(id, point.id + 1);
            }

            points.Add(new PD2Point
            {
                type = type,
                id = id,
                position = SurfaceUnder(screenPosition, out Vector3 hit) ? hit : m_view.Pivot,
            });
            m_history.Push(new PointListCommand("Add point", points, before));
            m_dirty = true;
            m_selection.Clear();
            m_selection.Add(points.Count - 1);
            RebuildPoints();
            SetMessage($"Added {PointTypeInfo.Get(type).Name}");
        }

        /// <summary>
        /// 선택한 포인트들을 복제한다. 복제한 것들이 선택되고 바로 옮기기가 시작된다 (취소하면 제자리에 남는다).
        /// </summary>
        private void DuplicateSelection()
        {
            if (Transforming) CancelTransform();
            if (m_selection.Count == 0)
            {
                SetMessage("Select points first");
                return;
            }

            List<PD2Point> points = m_document.Points.points;
            PD2Point[] before = PointListCommand.Snapshot(points);
            var copies = new List<int>();
            foreach (int index in m_selection)
            {
                points.Add(PointChangeCommand.Clone(points[index]));
                copies.Add(points.Count - 1);
            }
            m_history.Push(new PointListCommand("Duplicate", points, before));
            m_dirty = true;
            m_selection.Clear();
            m_selection.UnionWith(copies);
            RebuildPoints();
            BeginTransform(TransformMode.Move);
        }

        /// <summary>
        /// 선택한 포인트들을 지운다.
        /// </summary>
        private void DeleteSelection()
        {
            if (Transforming) CancelTransform();
            if (m_selection.Count == 0) return;

            List<PD2Point> points = m_document.Points.points;
            PD2Point[] before = PointListCommand.Snapshot(points);
            int removed = m_selection.Count;
            foreach (int index in m_selection.Reverse())
            {
                points.RemoveAt(index);
            }
            m_history.Push(new PointListCommand("Delete", points, before));
            m_dirty = true;
            m_selection.Clear();
            RebuildPoints();
            SetMessage($"Deleted {removed} point(s)");
        }

        /// <summary>
        /// 화면의 한 점 아래에 있는 블록 면의 자리를 구한다.
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        /// <param name="hit">면 위의 점.</param>
        /// <returns>블록 면이 있으면 true.</returns>
        private bool SurfaceUnder(Vector2 screenPosition, out Vector3 hit)
        {
            Camera3D camera = m_view.Camera;
            Vector3 origin = camera.ProjectRayOrigin(screenPosition);
            Vector3 direction = camera.ProjectRayNormal(screenPosition);
            bool found = MapLoader.RaycastBlock(BlockLayer.Human, origin, direction, k_unlimited, out float distance);
            hit = origin + direction * distance;
            return found;
        }

        /// <summary>
        /// 새 포인트의 종류를 고르는 메뉴를 마우스 자리에 띄운다 (Shift+A). 고르면 그 자리 아래의 블록 면에 놓인다.
        /// </summary>
        private void ShowAddMenu()
        {
            m_addMenuScreenPosition = GetViewport().GetMousePosition();
            m_addMenu.Position = (Vector2I)(GetWindow().Position + m_addMenuScreenPosition);
            m_addMenu.Popup();
        }

        /// <summary>
        /// 포인트 데이터를 지금 경로에 저장한다 (Ctrl+S). 저장한 적이 없으면 경로를 묻는다.
        /// </summary>
        private void SavePoints()
        {
            if (Transforming) ConfirmTransform();
            if (string.IsNullOrEmpty(m_document.PointPath))
            {
                ShowFileDialog(k_menuSavePointsAs, "Save points as", "*" + PD2File.Extension, true);
                return;
            }
            SavePointsTo(m_document.PointPath);
        }

        /// <summary>
        /// 포인트 데이터를 PD2 파일로 쓴다. 메시지가 있으면 같은 이름의 .msg 도 쓴다. 덮어쓰는 파일은 먼저 .bak 으로 남긴다.
        /// </summary>
        /// <param name="relativePath">PD2 경로 (exe 폴더 기준).</param>
        /// <returns>저장했으면 true.</returns>
        private bool SavePointsTo(string relativePath)
        {
            if (!HasExtension(relativePath, PD2File.Extension)) relativePath += PD2File.Extension;
            string fullPath = GamePath.ResolveForWrite(relativePath, PD2File.Extension, out string pathError);
            if (fullPath == null) return Fail(pathError);

            try
            {
                string messagePath = Path.ChangeExtension(fullPath, k_messageExtension);
                if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + k_backupExtension, true);
                if (File.Exists(messagePath)) File.Copy(messagePath, messagePath + k_backupExtension, true);

                if (!m_document.Points.Write(fullPath, out string error)) return Fail($"Point file write failed: {relativePath} ({error})");
                if (m_document.Messages.Count > 0 || File.Exists(messagePath)) File.WriteAllLines(messagePath, m_document.Messages);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail($"Point file write failed: {relativePath} ({e.Message})");
            }

            m_document.PointPath = relativePath;
            m_dirty = false;
            SetMessage($"Saved {relativePath} ({m_document.Points.points.Count} points)");
            return true;
        }

        /// <summary>
        /// 저장하지 않은 내용을 버리게 되는 일(다른 파일 열기, 끝내기)을 하기 전에 묻는다. 바뀐 것이 없으면 바로 한다.
        /// </summary>
        /// <param name="action">할 일.</param>
        private void ConfirmDiscard(Action action)
        {
            if (!m_dirty && !m_blockDirty && !m_missionDirty && !AssetsDirty())
            {
                action();
                return;
            }
            m_discardAction = action;
            m_discardDialog.PopupCentered();
        }
    }
}

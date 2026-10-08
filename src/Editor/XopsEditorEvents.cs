using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 이벤트 칸의 한 줄: 등록 정보의 파라미터나 출구 하나와 그 입력 칸.
        /// </summary>
        private sealed class SlotRow
        {
            public EventSlotData Slot;
            public bool IsExit;
            public SpinBox Box;
            public CheckBox Check;
            public Label Hint;
        }

        private const string k_kindFloat = "float";
        private const string k_kindBool = "bool";
        private const string k_kindEvent = "event";
        private const string k_kindHuman = "human";
        private const string k_kindHumanInfo = "humaninfo";
        private const string k_kindObject = "object";
        private const string k_kindPath = "path";
        private const string k_kindMessage = "message";
        private const string k_kindLine = "line";
        private const string k_kindColor = "color";
        private const string k_eventFilterName = "Events";
        // 목록과 설명에서 메시지나 값의 요약을 이 글자 수에서 자른다.
        private const int k_summaryLength = 28;
        // "다음 이벤트 더하기"로 놓는 이벤트를 앞 이벤트에서 이만큼(m) 옆에 둔다.
        private const float k_nextEventOffset = 1f;
        // 목록에서 줄의 머리말처럼 포인트가 아닌 줄.
        private const int k_listNoPoint = -1;

        private static readonly string[] s_compareNames = { "==", "!=", "<", "<=", ">", ">=" };
        private static readonly string[] s_fontNames = { "OS font", "char.dds" };
        private static readonly string[] s_anchorNames =
        {
            "top left", "top center", "top right", "middle left", "center", "middle right", "bottom left", "bottom center", "bottom right",
        };
        // 이벤트 줄마다의 연결선 색. 줄이 더 많으면 돌려 쓴다.
        private static readonly Color[] s_lineColors =
        {
            new Color(0.95f, 0.45f, 1f), new Color(0.4f, 0.9f, 1f), new Color(1f, 0.6f, 0.3f), new Color(0.6f, 1f, 0.5f), new Color(1f, 0.4f, 0.5f), new Color(0.7f, 0.7f, 1f),
        };
        private static readonly Color s_looseEventColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color s_pathLinkColor = new Color(1f, 0.85f, 0.2f, 0.7f);
        private static readonly Color s_humanLinkColor = new Color(0.2f, 0.9f, 0.3f, 0.5f);
        private static readonly Color s_hintColor = new Color(0.7f, 0.7f, 0.7f);

        private readonly EventCatalog m_catalog = new EventCatalog();
        private LinkOverlay m_links;
        private bool m_showLinks = true;
        private bool m_linksDirty;

        // 이벤트 칸. 등록 정보가 있는 이벤트 하나를 선택했을 때 P2 / P3 / Extra 칸 대신 보인다.
        private Control m_eventBox;
        private GridContainer m_eventGrid;
        private readonly List<SlotRow> m_slotRows = new List<SlotRow>();
        private EventDefinitionData m_eventBoxDefinition;
        private Label m_extraLabel;
        // 고르는 중인 칸 (Pick). 다음에 클릭한 포인트의 식별번호가 이 칸에 들어간다. 고르는 중이 아니면 null.
        private EventSlotData m_pickSlot;
        // "다음 이벤트 더하기"가 채울 출구.
        private EventSlotData m_nextSlot;
        private PopupMenu m_nextMenu;
        private PopupMenu m_addBarMenu;
        private PopupMenu m_choiceMenu;
        private EventSlotData m_choiceSlot;
        // 이벤트 줄의 시작 번호 칸. 목록이 이벤트 보기일 때만 보인다.
        private Control m_entryBox;
        private LineEdit m_entryEdit;

        /// <summary>
        /// 이벤트 종류의 목록을 다시 읽고(설치형 묶음과 지금 미션의 묶음) 종류를 고르는 메뉴들을 다시 채운다.
        /// </summary>
        private void ReloadCatalog()
        {
            m_catalog.Reload(m_document.Mission.addonEventDataPath);
            PointTypeInfo.EventName = type => m_catalog.Get(type)?.name;
            if (m_addMenu == null) return;

            FillTypeMenu(m_addMenu, false, type => AddPoint(type, m_addMenuScreenPosition));
            FillTypeMenu(m_addBarMenu, false, AddFromBar);
            m_addBarMenu.AddSeparator();
            m_addBarMenu.AddItem("Block (box)", k_menuAddBlock);
            FillTypeMenu(m_nextMenu, true, AddNextEvent);
            m_eventBoxDefinition = null;
        }

        /// <summary>
        /// 위쪽 Add 메뉴에서 고른 것을 3D 화면의 가운데 아래에 놓는다.
        /// </summary>
        /// <param name="type">포인트 종류, 또는 블록을 뜻하는 번호.</param>
        private void AddFromBar(int type)
        {
            if (type == k_menuAddBlock)
            {
                if (m_editMode != EditMode.Block) SetEditMode(EditMode.Block);
                AddBlock(ViewportCenter());
                return;
            }
            if (m_editMode != EditMode.Point) SetEditMode(EditMode.Point);
            AddPoint(type, ViewportCenter());
        }

        /// <summary>
        /// 종류 메뉴를 채운다. 이벤트가 아닌 종류는 바로, 이벤트는 묶음마다 하위 메뉴로 넣는다. 항목 번호가 곧 종류 번호다.
        /// </summary>
        /// <param name="menu">채울 메뉴. 먼저 비운다.</param>
        /// <param name="eventsOnly">true 면 이벤트만 넣는다.</param>
        /// <param name="chosen">하위 메뉴에서 종류를 골랐을 때 부를 함수.</param>
        private void FillTypeMenu(PopupMenu menu, bool eventsOnly, Action<int> chosen)
        {
            menu.Clear();
            foreach (Node child in menu.GetChildren())
            {
                if (child is not PopupMenu) continue;
                menu.RemoveChild(child);
                child.Free();
            }

            if (!eventsOnly)
            {
                foreach (int type in PointTypeInfo.EditableTypes)
                {
                    menu.AddItem(PointTypeInfo.Get(type).Name, type);
                }
                menu.AddSeparator();
            }
            foreach (EventCatalog.Group group in m_catalog.Groups)
            {
                var sub = new PopupMenu();
                foreach (EventDefinitionData definition in group.Events)
                {
                    sub.AddItem($"{definition.name} ({definition.type})", definition.type);
                }
                sub.IdPressed += id => chosen((int)id);
                menu.AddSubmenuNodeItem($"Event: {group.Name}", sub);
            }
        }

        /// <summary>
        /// 오른쪽 패널의 이벤트 칸과 그것이 쓰는 메뉴들을 만든다.
        /// </summary>
        /// <param name="column">칸을 넣을 세로 상자.</param>
        /// <param name="layer">메뉴를 넣을 노드.</param>
        private void BuildEventBox(VBoxContainer column, Node layer)
        {
            var box = new VBoxContainer { Visible = false };
            m_eventBox = box;
            column.AddChild(box);
            m_eventGrid = new GridContainer { Columns = 2 };
            m_eventGrid.AddThemeConstantOverride("h_separation", 8);
            box.AddChild(m_eventGrid);

            m_nextMenu = new PopupMenu();
            layer.AddChild(m_nextMenu);
            m_choiceMenu = new PopupMenu();
            m_choiceMenu.IdPressed += id =>
            {
                if (m_choiceSlot != null) ApplySlot(m_choiceSlot, id);
            };
            layer.AddChild(m_choiceMenu);
        }

        /// <summary>
        /// 왼쪽 목록 위의 "이벤트 줄의 시작 번호" 칸을 만든다.
        /// </summary>
        /// <param name="column">칸을 넣을 세로 상자.</param>
        private void BuildEntryBox(VBoxContainer column)
        {
            var box = new VBoxContainer { Visible = false };
            m_entryBox = box;
            column.AddChild(box);
            box.AddChild(new Label { Text = "Line starts (event ids):", TooltipText = "One event line starts at each id. Comma separated. Imported PD1 maps use 156, 146, 136" });
            m_entryEdit = new LineEdit { PlaceholderText = "e.g. 156, 146, 136" };
            m_entryEdit.TextSubmitted += text =>
            {
                ApplyEntryIds(text);
                m_entryEdit.ReleaseFocus();
            };
            box.AddChild(m_entryEdit);
        }

        /// <summary>
        /// 이벤트 칸을 지금 선택에 맞춘다. 등록 정보가 있는 이벤트 하나를 선택했으면 그 파라미터와 출구를 이름으로 보여 주고, P2 / P3 / Extra 칸은 감춘다.
        /// </summary>
        private void RefreshEventBox()
        {
            if (m_eventBox == null) return;

            EventDefinitionData definition = null;
            PD2Point point = null;
            if (m_editMode == EditMode.Point && m_selection.Count == 1)
            {
                point = m_document.Points.points[m_selection.Min];
                definition = m_catalog.Get(point.type);
            }

            bool named = definition != null;
            m_eventBox.Visible = named;
            m_p2Label.Visible = !named;
            m_p3Label.Visible = !named;
            m_fieldBoxes[PointField.P2].Visible = !named;
            m_fieldBoxes[PointField.P3].Visible = !named;
            m_extraLabel.Visible = !named;
            m_extraEdit.Visible = !named;
            if (!named) return;

            // 같은 이벤트를 보는 동안에는 칸을 다시 만들지 않고 값만 채운다 (입력 중인 칸의 포커스가 풀리지 않게).
            if (!ReferenceEquals(definition, m_eventBoxDefinition)) BuildSlotRows(definition);

            m_fillingInspector = true;
            foreach (SlotRow row in m_slotRows)
            {
                double value = GetSlot(point, row.Slot);
                row.Check?.SetPressedNoSignal(value != 0.0);
                row.Box?.SetValueNoSignal(value);
                string hint = DescribeSlotValue(row.Slot, value);
                row.Hint.Text = hint;
                row.Hint.Visible = hint.Length > 0;
            }
            m_fillingInspector = false;
        }

        /// <summary>
        /// 이벤트 칸의 줄들을 등록 정보대로 다시 만든다.
        /// </summary>
        /// <param name="definition">등록 정보.</param>
        private void BuildSlotRows(EventDefinitionData definition)
        {
            foreach (Node child in m_eventGrid.GetChildren())
            {
                m_eventGrid.RemoveChild(child);
                child.Free();
            }
            m_slotRows.Clear();
            m_eventBoxDefinition = definition;

            foreach (EventSlotData slot in definition.parameters)
            {
                AddSlotRow(slot, false);
            }
            foreach (EventSlotData slot in m_catalog.ExitsOf(definition.type))
            {
                AddSlotRow(slot, true);
            }
        }

        /// <summary>
        /// 이벤트 칸에 한 줄을 더한다: 이름, 입력 칸, 값의 종류에 따른 버튼(목록에서 고르기, 화면에서 고르기, 다음 이벤트 더하기), 값의 뜻.
        /// </summary>
        /// <param name="slot">파라미터나 출구.</param>
        /// <param name="isExit">출구인지.</param>
        private void AddSlotRow(EventSlotData slot, bool isExit)
        {
            var row = new SlotRow { Slot = slot, IsExit = isExit };
            m_eventGrid.AddChild(new Label { Text = isExit ? $"→ {slot.name}" : slot.name, TooltipText = $"{slot.kind}, stored in {slot.slot}" });

            var line = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            m_eventGrid.AddChild(line);
            if (slot.kind == k_kindBool)
            {
                row.Check = new CheckBox { FocusMode = Control.FocusModeEnum.None };
                row.Check.Toggled += pressed =>
                {
                    if (!m_fillingInspector) ApplySlot(slot, pressed ? 1.0 : 0.0);
                };
                line.AddChild(row.Check);
            }
            else
            {
                row.Box = CreateNumberBox(IsFloatSlot(slot));
                row.Box.ValueChanged += value =>
                {
                    if (!m_fillingInspector) ApplySlot(slot, value);
                };
                line.AddChild(row.Box);
            }

            // 버튼들은 입력 칸 아래의 줄에 둔다 (한 줄에 넣으면 패널보다 넓어진다).
            var tools = new HBoxContainer();
            if (HasChoices(slot.kind))
            {
                var choose = new Button { Text = "Choose...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Choose from a list" };
                choose.Pressed += () => ShowChoices(slot, choose);
                tools.AddChild(choose);
            }
            if (IsPickable(slot.kind))
            {
                var pick = new Button { Text = "Pick", FocusMode = Control.FocusModeEnum.None, TooltipText = "Then click the point in the 3D view. Its id goes here (Esc cancels)" };
                pick.Pressed += () => BeginPick(slot);
                tools.AddChild(pick);
            }
            if (isExit)
            {
                var next = new Button { Text = "Add next...", FocusMode = Control.FocusModeEnum.None, TooltipText = "Add a new event after this one and link this exit to it" };
                next.Pressed += () =>
                {
                    m_nextSlot = slot;
                    m_nextMenu.Position = (Vector2I)(GetWindow().Position + GetViewport().GetMousePosition());
                    m_nextMenu.Popup();
                };
                tools.AddChild(next);
            }
            if (tools.GetChildCount() > 0)
            {
                m_eventGrid.AddChild(new Control());
                m_eventGrid.AddChild(tools);
            }
            else
            {
                tools.Free();
            }

            m_eventGrid.AddChild(new Control());
            row.Hint = new Label { Modulate = s_hintColor, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
            m_eventGrid.AddChild(row.Hint);
            // 값의 뜻이 없는 줄에서는 빈 줄이 자리를 차지하지 않게, 앞의 빈 칸도 뜻과 함께 보이고 감춘다.
            Control spacer = (Control)m_eventGrid.GetChild(m_eventGrid.GetChildCount() - 2);
            spacer.Visible = false;
            row.Hint.VisibilityChanged += () => spacer.Visible = row.Hint.Visible;
            m_slotRows.Add(row);
        }

        /// <summary>
        /// 칸의 위치를 읽는다.
        /// </summary>
        /// <param name="slot">칸 표기 ("p2", "p3", "e0" ...).</param>
        /// <param name="extraIndex">추가 파라미터의 번호. P2 는 −2, P3 은 −1.</param>
        /// <returns>읽을 수 있는 표기면 true.</returns>
        private static bool ParseSlot(string slot, out int extraIndex)
        {
            extraIndex = 0;
            if (slot == "p2") extraIndex = -2;
            else if (slot == "p3") extraIndex = -1;
            else if (slot.Length < 2 || slot[0] != 'e' || !int.TryParse(slot.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out extraIndex)) return false;
            return true;
        }

        /// <summary>
        /// 실수로 읽는 칸인지. P2 와 P3 은 늘 정수다.
        /// </summary>
        /// <param name="slot">파라미터.</param>
        /// <returns>추가 파라미터 칸의 실수면 true.</returns>
        private static bool IsFloatSlot(EventSlotData slot)
        {
            return slot.kind == k_kindFloat && ParseSlot(slot.slot, out int index) && index >= 0;
        }

        /// <summary>
        /// 포인트에서 칸 하나의 값을 읽는다. 없는 추가 파라미터 칸은 0 이다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <param name="slot">파라미터나 출구.</param>
        /// <returns>값.</returns>
        private static double GetSlot(PD2Point point, EventSlotData slot)
        {
            if (!ParseSlot(slot.slot, out int index)) return 0.0;
            if (index == -2) return point.param1;
            if (index == -1) return point.param2;
            if (index >= point.extra.Length) return 0.0;
            return IsFloatSlot(slot) ? BitConverter.Int32BitsToSingle(point.extra[index]) : point.extra[index];
        }

        /// <summary>
        /// 포인트의 칸 하나에 값을 쓴다. 추가 파라미터 칸이 모자라면 늘린다.
        /// </summary>
        /// <param name="point">포인트.</param>
        /// <param name="slot">파라미터나 출구.</param>
        /// <param name="value">새 값.</param>
        private static void SetSlot(PD2Point point, EventSlotData slot, double value)
        {
            if (!ParseSlot(slot.slot, out int index)) return;

            int whole = (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
            if (index == -2) point.param1 = whole;
            else if (index == -1) point.param2 = whole;
            else
            {
                if (index >= point.extra.Length)
                {
                    int[] grown = new int[index + 1];
                    Array.Copy(point.extra, grown, point.extra.Length);
                    point.extra = grown;
                }
                point.extra[index] = IsFloatSlot(slot) ? PD2File.FloatCell((float)value) : whole;
            }
        }

        /// <summary>
        /// 선택한 이벤트 하나의 칸 하나를 바꾼다 (되돌릴 수 있다).
        /// </summary>
        /// <param name="slot">파라미터나 출구.</param>
        /// <param name="value">새 값.</param>
        private void ApplySlot(EventSlotData slot, double value)
        {
            if (m_selection.Count != 1 || Transforming) return;

            List<PD2Point> points = m_document.Points.points;
            int index = m_selection.Min;
            if (GetSlot(points[index], slot) == value) return;

            PD2Point[] before = { PointChangeCommand.Clone(points[index]) };
            SetSlot(points[index], slot, value);
            PushPointChange(new PointChangeCommand($"Edit {slot.name}", points, new[] { index }, before));
        }

        /// <summary>
        /// 값을 목록에서 고를 수 있는 종류인지.
        /// </summary>
        /// <param name="kind">값의 종류.</param>
        /// <returns>고를 목록이 있으면 true.</returns>
        private static bool HasChoices(string kind)
        {
            switch (kind)
            {
                case "compare":
                case "font":
                case "anchor":
                case "weapon":
                case "humandata":
                case "objectdata":
                case "effect":
                case "sound":
                case k_kindMessage:
                case k_kindLine:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 맵의 다른 포인트를 가리키는 종류인지 (3D 화면에서 눌러 고를 수 있다).
        /// </summary>
        /// <param name="kind">값의 종류.</param>
        /// <returns>포인트의 식별번호를 담는 종류면 true.</returns>
        private static bool IsPickable(string kind)
        {
            return kind == k_kindEvent || kind == k_kindHuman || kind == k_kindHumanInfo || kind == k_kindObject || kind == k_kindPath;
        }

        /// <summary>
        /// 값의 종류가 가리킬 수 있는 포인트 종류인지.
        /// </summary>
        /// <param name="kind">값의 종류.</param>
        /// <param name="type">포인트 종류.</param>
        /// <returns>가리킬 수 있으면 true.</returns>
        private static bool KindAccepts(string kind, int type)
        {
            switch (kind)
            {
                case k_kindEvent: return type >= MapLoader.PointEventFirst;
                case k_kindHuman: return type == MapLoader.PointHuman || type == MapLoader.PointHuman2;
                case k_kindHumanInfo: return type == MapLoader.PointHumanInfo;
                case k_kindObject: return type == MapLoader.PointSmallObject;
                case k_kindPath: return type == MapLoader.PointAIPath || type == MapLoader.PointRandomAIPath;
            }
            return false;
        }

        /// <summary>
        /// 어떤 종류의 값이 가리키는 포인트를 찾는다 (그 종류에서 식별번호가 같은 첫 포인트. 게임이 찾는 방식과 같다).
        /// </summary>
        /// <param name="kind">값의 종류.</param>
        /// <param name="id">식별번호.</param>
        /// <returns>포인트 번호. 없으면 −1.</returns>
        private int FindPoint(string kind, int id)
        {
            List<PD2Point> points = m_document.Points.points;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].id == id && KindAccepts(kind, points[i].type)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 목록에서 고를 수 있는 값들의 이름.
        /// </summary>
        /// <param name="kind">값의 종류.</param>
        /// <returns>번호 순서대로의 이름. 고를 목록이 없는 종류면 빈 목록.</returns>
        private List<string> ChoiceNames(string kind)
        {
            var names = new List<string>();
            DataManager data = DataManager.Instance;
            switch (kind)
            {
                case "compare": names.AddRange(s_compareNames); break;
                case "font": names.AddRange(s_fontNames); break;
                case "anchor": names.AddRange(s_anchorNames); break;
                case "weapon":
                    for (int i = 0; i < data.WeaponParameterData.weaponData.Count; i++) names.Add(data.WeaponParameterData.weaponData[i].name);
                    break;
                case "humandata":
                    for (int i = 0; i < data.HumanParameterData.humanData.Count; i++) names.Add(data.HumanParameterData.humanData[i].name);
                    break;
                case "objectdata":
                    for (int i = 0; i < data.ObjectParameterData.objectData.Count; i++) names.Add(data.ObjectParameterData.objectData[i].name);
                    break;
                case "effect":
                    for (int i = 0; i < data.EffectParameterData.effectData.Count; i++) names.Add(data.EffectParameterData.effectData[i].name);
                    break;
                case "sound":
                    for (int i = 0; i < data.SoundParameterData.soundData.Count; i++) names.Add(data.SoundParameterData.soundData[i].name);
                    break;
                case k_kindMessage:
                    foreach (string message in m_document.Messages) names.Add(Shorten(message));
                    break;
                case k_kindLine:
                    foreach (int entry in m_document.Points.eventEntryIds) names.Add($"starts at {entry}");
                    break;
            }
            return names;
        }

        /// <summary>
        /// 값을 고르는 목록을 버튼 아래에 띄운다.
        /// </summary>
        /// <param name="slot">고른 값이 들어갈 칸.</param>
        /// <param name="button">누른 버튼.</param>
        private void ShowChoices(EventSlotData slot, Control button)
        {
            m_choiceSlot = slot;
            m_choiceMenu.Clear();
            List<string> names = ChoiceNames(slot.kind);
            for (int i = 0; i < names.Count; i++)
            {
                m_choiceMenu.AddItem($"{i}  {names[i]}", i);
            }
            if (names.Count == 0) m_choiceMenu.AddItem("(nothing to choose)", 0);
            m_choiceMenu.SetItemDisabled(0, names.Count == 0);
            m_choiceMenu.Position = (Vector2I)(GetWindow().Position + button.GlobalPosition + new Vector2(0f, button.Size.Y));
            m_choiceMenu.Popup();
        }

        /// <summary>
        /// 칸의 값이 무엇을 뜻하는지 한 줄로 적는다 (이름, 가리키는 포인트).
        /// </summary>
        /// <param name="slot">파라미터나 출구.</param>
        /// <param name="value">값.</param>
        /// <returns>설명. 덧붙일 것이 없으면 빈 문자열.</returns>
        private string DescribeSlotValue(EventSlotData slot, double value)
        {
            int whole = (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
            if (IsPickable(slot.kind))
            {
                int target = FindPoint(slot.kind, whole);
                if (target >= 0) return $"→ #{target} {PointTypeInfo.Get(m_document.Points.points[target].type).Name}";
                return slot.kind == k_kindEvent ? $"no event with id {whole}: the line ends here" : $"no {slot.kind} point with id {whole}";
            }
            if (slot.kind == k_kindColor) return $"#{whole & 0xFFFFFF:X6}";
            if (!HasChoices(slot.kind)) return string.Empty;

            List<string> names = ChoiceNames(slot.kind);
            if (whole >= 0 && whole < names.Count) return names[whole];
            return whole >= DataList<WeaponData>.AddonBase ? "mission addon data" : "(not in the list)";
        }

        /// <summary>
        /// 글을 요약 길이로 자른다.
        /// </summary>
        /// <param name="text">글.</param>
        /// <returns>잘린 글.</returns>
        private static string Shorten(string text)
        {
            return text.Length <= k_summaryLength ? text : text.Substring(0, k_summaryLength) + "...";
        }

        /// <summary>
        /// 칸에 넣을 포인트를 3D 화면에서 고르기 시작한다. 다음 클릭이 선택 대신 이 칸을 채운다.
        /// </summary>
        /// <param name="slot">채울 칸.</param>
        private void BeginPick(EventSlotData slot)
        {
            if (m_selection.Count != 1) return;

            m_pickSlot = slot;
            SetMessage($"Click the {slot.kind} point for \"{slot.name}\" (Esc: cancel)");
        }

        /// <summary>
        /// 고르기를 그만둔다.
        /// </summary>
        /// <returns>고르는 중이었으면 true.</returns>
        private bool CancelPick()
        {
            if (m_pickSlot == null) return false;

            m_pickSlot = null;
            SetMessage("Pick cancelled");
            return true;
        }

        /// <summary>
        /// 고르는 중에 화면을 눌렀을 때: 그 자리의 포인트가 칸의 종류에 맞으면 식별번호를 칸에 넣는다. 선택은 바뀌지 않는다.
        /// </summary>
        /// <param name="screenPosition">화면 좌표 (픽셀).</param>
        private void FinishPick(Vector2 screenPosition)
        {
            EventSlotData slot = m_pickSlot;
            m_pickSlot = null;
            int picked = m_markers.Pick(m_view.Camera, screenPosition, m_xray);
            if (picked < 0)
            {
                SetMessage("Pick cancelled: no point there");
                return;
            }

            PD2Point target = m_document.Points.points[picked];
            if (!KindAccepts(slot.kind, target.type))
            {
                Fail($"\"{slot.name}\" needs a {slot.kind} point, but #{picked} is {PointTypeInfo.Get(target.type).Name}");
                return;
            }
            ApplySlot(slot, target.id);
            SetMessage($"\"{slot.name}\" is now {target.id} (#{picked})");
        }

        /// <summary>
        /// 새 이벤트에 줄 식별번호: 이벤트끼리는 종류가 달라도 번호로 서로를 찾으므로, 모든 이벤트에서 가장 큰 번호의 다음이다.
        /// </summary>
        /// <returns>식별번호.</returns>
        private int NextEventId()
        {
            int id = 0;
            foreach (PD2Point point in m_document.Points.points)
            {
                if (point.type >= MapLoader.PointEventFirst) id = Math.Max(id, point.id + 1);
            }
            return id;
        }

        /// <summary>
        /// 선택한 이벤트 뒤에 새 이벤트를 놓고, 골라 둔 출구가 그것을 가리키게 한다. 새 이벤트가 선택된다.
        /// </summary>
        /// <param name="type">새 이벤트의 종류.</param>
        private void AddNextEvent(int type)
        {
            if (m_selection.Count != 1 || m_nextSlot == null || Transforming) return;

            List<PD2Point> points = m_document.Points.points;
            PD2Point[] before = PointListCommand.Snapshot(points);
            PD2Point source = points[m_selection.Min];
            int id = NextEventId();
            Vector3 side = m_view.Camera.GlobalBasis.X;
            side.Y = 0f;
            side = side.LengthSquared() > 1e-6f ? side.Normalized() : Vector3.Right;

            SetSlot(source, m_nextSlot, id);
            points.Add(new PD2Point { type = type, id = id, position = source.position + side * k_nextEventOffset });
            m_history.Push(new PointListCommand("Add next event", points, before));
            m_dirty = true;
            m_selection.Clear();
            m_selection.Add(points.Count - 1);
            RebuildPoints();
            SetMessage($"Added {PointTypeInfo.Get(type).Name} (id {id}) after \"{m_nextSlot.name}\"");
        }

        /// <summary>
        /// 이벤트 줄의 시작 번호들을 바꾼다 (되돌릴 수 있다). 번호 하나가 줄 하나다.
        /// </summary>
        /// <param name="text">쉼표로 나눈 식별번호들. 비어 있으면 이벤트 줄이 없다.</param>
        /// <returns>바꿨으면 true.</returns>
        private bool ApplyEntryIds(string text)
        {
            var entries = new List<int>();
            foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int entry))
                {
                    return Fail($"Line starts must be event ids separated by commas: \"{part}\"");
                }
                entries.Add(entry);
            }

            int[] before = m_document.Points.eventEntryIds.ToArray();
            int[] after = entries.ToArray();
            if (System.Linq.Enumerable.SequenceEqual(before, after)) return false;

            void Set(int[] values)
            {
                m_document.Points.eventEntryIds.Clear();
                m_document.Points.eventEntryIds.AddRange(values);
            }
            Set(after);
            m_history.Push(new ActionCommand("Edit line starts", ActionCommand.Target.Points, () => Set(before), () => Set(after)));
            m_dirty = true;
            RebuildPoints();
            SetMessage($"{after.Length} event line(s)");
            return true;
        }

        /// <summary>
        /// 포인트 목록을 이벤트 보기로 채운다: 줄마다 시작 번호에서 출구를 따라간 순서대로, 그 뒤에 어느 줄에도 들지 않은 이벤트들.
        /// </summary>
        private void FillEventList()
        {
            List<PD2Point> points = m_document.Points.points;
            var visited = new HashSet<int>();
            List<int> entries = m_document.Points.eventEntryIds;
            for (int line = 0; line < entries.Count; line++)
            {
                AddListHeader($"Line {line}  (starts at {entries[line]})", s_lineColors[line % s_lineColors.Length]);
                int start = FindPoint(k_kindEvent, entries[line]);
                if (start < 0) AddListHeader("    (no event with this id: the line does nothing)", s_looseEventColor);
                else AddEventRows(start, string.Empty, 1, visited);
            }

            bool header = false;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].type < MapLoader.PointEventFirst || visited.Contains(i)) continue;

                if (!header) AddListHeader("Not in any line", s_looseEventColor);
                header = true;
                AddEventRow(i, string.Empty, 1);
            }
        }

        /// <summary>
        /// 목록에 포인트가 아닌 줄(머리말)을 더한다.
        /// </summary>
        /// <param name="text">글.</param>
        /// <param name="color">글자 색.</param>
        private void AddListHeader(string text, Color color)
        {
            int row = m_pointList.AddItem(text, null, false);
            m_pointList.SetItemCustomFgColor(row, color);
            m_listToPoint.Add(k_listNoPoint);
        }

        /// <summary>
        /// 목록에 이벤트 한 줄을 더한다.
        /// </summary>
        /// <param name="index">포인트 번호.</param>
        /// <param name="prefix">앞에 붙일 말 (어느 출구로 왔는지).</param>
        /// <param name="depth">들여 쓸 단계.</param>
        private void AddEventRow(int index, string prefix, int depth)
        {
            PD2Point point = m_document.Points.points[index];
            PointTypeInfo.Info info = PointTypeInfo.Get(point.type);
            int row = m_pointList.AddItem($"{new string(' ', depth * 3)}{prefix}{point.id}  {info.Name}{SummarizeEvent(point)}");
            m_pointList.SetItemCustomFgColor(row, info.Color);
            m_pointList.SetItemTooltip(row, $"Point #{index}");
            m_listToPoint.Add(index);
        }

        /// <summary>
        /// 이벤트 하나와 그 출구들이 가리키는 이벤트들을 차례로 목록에 더한다. 이미 나온 이벤트로 돌아오면(고리) 그 사실만 적고 멈춘다.
        /// </summary>
        /// <param name="start">시작할 포인트 번호.</param>
        /// <param name="prefix">앞에 붙일 말.</param>
        /// <param name="depth">들여 쓸 단계.</param>
        /// <param name="visited">이미 나온 포인트 번호들.</param>
        private void AddEventRows(int start, string prefix, int depth, HashSet<int> visited)
        {
            List<PD2Point> points = m_document.Points.points;
            int index = start;
            // 출구가 하나인 동안은 재귀 없이 따라간다 (긴 줄에서 스택이 깊어지지 않게).
            while (index >= 0)
            {
                if (!visited.Add(index))
                {
                    AddListHeader($"{new string(' ', depth * 3)}{prefix}back to {points[index].id}", s_looseEventColor);
                    return;
                }
                AddEventRow(index, prefix, depth);
                prefix = string.Empty;

                IReadOnlyList<EventSlotData> exits = m_catalog.ExitsOf(points[index].type);
                if (exits.Count == 0) return;
                if (exits.Count == 1)
                {
                    index = FindPoint(k_kindEvent, (int)GetSlot(points[index], exits[0]));
                    continue;
                }
                foreach (EventSlotData exit in exits)
                {
                    int target = FindPoint(k_kindEvent, (int)GetSlot(points[index], exit));
                    if (target >= 0) AddEventRows(target, $"{exit.name}: ", depth + 1, visited);
                }
                return;
            }
        }

        /// <summary>
        /// 이벤트의 파라미터를 목록에 보일 한 토막으로 줄인다.
        /// </summary>
        /// <param name="point">이벤트 포인트.</param>
        /// <returns>"  (이름 값, ...)". 등록 정보가 없으면 P2 의 값만.</returns>
        private string SummarizeEvent(PD2Point point)
        {
            EventDefinitionData definition = m_catalog.Get(point.type);
            if (definition == null) return $"  ({point.param1})";
            if (definition.parameters.Count == 0) return string.Empty;

            var text = new StringBuilder("  (");
            for (int i = 0; i < definition.parameters.Count; i++)
            {
                EventSlotData slot = definition.parameters[i];
                if (i > 0) text.Append(", ");
                double value = GetSlot(point, slot);
                string shown = slot.kind == k_kindMessage && value >= 0 && value < m_document.Messages.Count
                    ? $"\"{Shorten(m_document.Messages[(int)value])}\""
                    : value.ToString("0.###", CultureInfo.InvariantCulture);
                text.Append(slot.name).Append(' ').Append(shown);
                if (text.Length > k_summaryLength * 2) break;
            }
            return text.Append(')').ToString();
        }

        /// <summary>
        /// 연결선을 다시 그린다: 이벤트의 출구(줄마다 다른 색), AI 경로의 다음 경로, 사람의 첫 경로.
        /// </summary>
        private void RedrawLinks()
        {
            m_linksDirty = false;
            if (m_links == null) return;

            m_links.Visible = m_showLinks && m_editMode < EditMode.Mission;
            m_links.Begin();
            if (!m_links.Visible) return;

            List<PD2Point> points = m_document.Points.points;
            // 이벤트마다 어느 줄에 속하는지: 시작 번호에서 출구를 따라가며 칠한다.
            var lineOf = new Dictionary<int, int>();
            var pending = new Stack<int>();
            List<int> entries = m_document.Points.eventEntryIds;
            for (int line = 0; line < entries.Count; line++)
            {
                pending.Push(FindPoint(k_kindEvent, entries[line]));
                while (pending.Count > 0)
                {
                    int index = pending.Pop();
                    if (index < 0 || !lineOf.TryAdd(index, line)) continue;
                    foreach (EventSlotData exit in m_catalog.ExitsOf(points[index].type))
                    {
                        pending.Push(FindPoint(k_kindEvent, (int)GetSlot(points[index], exit)));
                    }
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                PD2Point point = points[i];
                Vector3 from = PointMarkers.Center(point);
                if (point.type >= MapLoader.PointEventFirst)
                {
                    Color color = lineOf.TryGetValue(i, out int line) ? s_lineColors[line % s_lineColors.Length] : s_looseEventColor;
                    foreach (EventSlotData exit in m_catalog.ExitsOf(point.type))
                    {
                        AddLink(from, k_kindEvent, (int)GetSlot(point, exit), color);
                    }
                }
                else if (point.type == MapLoader.PointAIPath)
                {
                    AddLink(from, k_kindPath, point.param2, s_pathLinkColor);
                }
                else if (point.type == MapLoader.PointRandomAIPath)
                {
                    AddLink(from, k_kindPath, point.param1, s_pathLinkColor);
                    AddLink(from, k_kindPath, point.param2, s_pathLinkColor);
                }
                else if (point.type == MapLoader.PointHuman || point.type == MapLoader.PointHuman2)
                {
                    AddLink(from, k_kindPath, point.param2, s_humanLinkColor);
                }
            }
            m_links.End();
        }

        /// <summary>
        /// 가리키는 포인트가 있으면 그리로 화살표 선을 더한다.
        /// </summary>
        /// <param name="from">시작점.</param>
        /// <param name="kind">가리키는 것의 종류.</param>
        /// <param name="id">가리키는 식별번호.</param>
        /// <param name="color">색.</param>
        private void AddLink(Vector3 from, string kind, int id, Color color)
        {
            int target = FindPoint(kind, id);
            if (target >= 0) m_links.Add(from, PointMarkers.Center(m_document.Points.points[target]), color);
        }
    }
}

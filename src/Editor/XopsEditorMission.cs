using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        private const float k_missionFormWidth = 720f;
        private const float k_briefingHeight = 180f;

        // 미션 모드의 화면. 3D 화면과 양옆의 패널을 덮는다.
        private Control m_missionPanel;
        private Label m_missionFileLabel;
        private Label m_missionMapLabel;
        // 칸마다의 "문서의 값으로 다시 채우기".
        private readonly List<Action> m_missionRefreshers = new List<Action>();
        private bool m_fillingMission;
        // 저장한 뒤로 미션의 설정이 바뀌었는지.
        private bool m_missionDirty;
        // 파일 창에서 고른 경로를 받을 칸.
        private Action<string> m_missionPathSetter;
        // 화면에 반영해 둔 값. 바뀌면 하늘과 이벤트 목록을 다시 읽는다.
        private int m_shownSky = -1;
        private string m_shownEventPack;

        /// <summary>
        /// 미션 모드의 화면을 만든다: 미션 파일(MIF2)의 설정을 고치는 칸들.
        /// </summary>
        /// <param name="root">화면 전체를 덮는 부모.</param>
        private void BuildMissionPanel(Control root)
        {
            var panel = new PanelContainer { Visible = false };
            // 불투명한 바탕으로 3D 화면을 가린다.
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.13f, 0.14f, 0.16f) });
            Dock(root, panel, 0f, 0f, 1f, 1f, 0f, k_topBarHeight, 0f, -k_statusHeight);
            m_missionPanel = panel;

            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            panel.AddChild(scroll);
            var center = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.AddChild(center);
            var column = new VBoxContainer { CustomMinimumSize = new Vector2(k_missionFormWidth, 0f) };
            center.AddChild(column);

            column.AddChild(new Label { Text = "Mission (MIF2)" });
            m_missionFileLabel = new Label();
            column.AddChild(m_missionFileLabel);
            m_missionMapLabel = new Label { Modulate = s_hintColor };
            column.AddChild(m_missionMapLabel);
            var fileRow = new HBoxContainer();
            column.AddChild(fileRow);
            AddDialogButton(fileRow, "Save mission", () => SaveMission());
            AddDialogButton(fileRow, "Save mission as...", () => ShowFileDialog(k_menuSaveMissionAs, "Save mission as", "*" + MIF2File.Extension, true));
            column.AddChild(new HSeparator());

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 12);
            column.AddChild(grid);

            AddMissionText(grid, "Name", "Shown in the mission list", m => m.name, (m, v) => m.name = v);
            AddMissionText(grid, "Full name", "Title of the briefing", m => m.fullname, (m, v) => m.fullname = v);
            AddMissionNumber(grid, "Sky", "Sky number (0: none)", m => m.skyIndex, (m, v) => m.skyIndex = v);
            AddMissionCheck(grid, "Additional collision", "Extra collision checks for characters (the original screen flag bit 1)", m => m.adjustCollision, (m, v) => m.adjustCollision = v);
            AddMissionCheck(grid, "Dark screen", "Darker blocks, sky and models (the original screen flag bit 2)", m => m.darkScreen, (m, v) => m.darkScreen = v);
            AddMissionNumber(grid, "Default block material", "Material used by faces whose material is \"mission default\"", m => m.defaultBlockMaterial, (m, v) => m.defaultBlockMaterial = v);
            AddMissionPath(grid, "Briefing image 1", "*.bmp, *.png, *.jpg, *.dds", m => m.image0, (m, v) => m.image0 = v);
            AddMissionPath(grid, "Briefing image 2", "*.bmp, *.png, *.jpg, *.dds", m => m.image1, (m, v) => m.image1 = v);

            grid.AddChild(new Label { Text = "Briefing", TooltipText = "One line of the box is one line of the briefing" });
            var briefing = new TextEdit { CustomMinimumSize = new Vector2(0f, k_briefingHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            briefing.FocusExited += () => EditMission("Edit briefing", m =>
            {
                m.briefing.Clear();
                m.briefing.AddRange(briefing.Text.Replace("\r", string.Empty).Split('\n'));
            });
            grid.AddChild(briefing);
            m_missionRefreshers.Add(() =>
            {
                if (!briefing.HasFocus()) briefing.Text = string.Join("\n", m_document.Mission.briefing);
            });

            AddMissionPath(grid, "Addon human data", "*.json", m => m.addonHumanDataPath, (m, v) => m.addonHumanDataPath = v);
            AddMissionPath(grid, "Addon weapon data", "*.json", m => m.addonWeaponDataPath, (m, v) => m.addonWeaponDataPath = v);
            AddMissionPath(grid, "Addon object data", "*.json", m => m.addonObjectDataPath, (m, v) => m.addonObjectDataPath = v);
            AddMissionPath(grid, "Addon effect data", "*.json", m => m.addonEffectDataPath, (m, v) => m.addonEffectDataPath = v);
            AddMissionPath(grid, "Addon block material data", "*.json", m => m.addonBlockMaterialDataPath, (m, v) => m.addonBlockMaterialDataPath = v);
            AddMissionPath(grid, "Addon sound data", "*.json", m => m.addonSoundDataPath, (m, v) => m.addonSoundDataPath = v);
            AddMissionPath(grid, "Addon event pack", "*.json", m => m.addonEventDataPath, (m, v) => m.addonEventDataPath = v);
            column.AddChild(new Label
            {
                Text = "Numbers 10000 and up in the map point at the mission's addon data. The addon event pack adds this mission's own events to the Add menu.",
                Modulate = s_hintColor,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }

        /// <summary>
        /// 글 칸 한 줄을 더한다.
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="title">이름.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="get">미션에서 값을 읽는 함수.</param>
        /// <param name="set">미션에 값을 쓰는 함수.</param>
        /// <returns>만든 글 칸.</returns>
        private LineEdit AddMissionText(GridContainer grid, string title, string tooltip, Func<ExtendedMissionData, string> get, Action<ExtendedMissionData, string> set)
        {
            grid.AddChild(new Label { Text = title, TooltipText = tooltip });
            var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            void Apply() => EditMission($"Edit {title}", m => set(m, edit.Text));
            edit.TextSubmitted += _ => Apply();
            edit.FocusExited += Apply;
            grid.AddChild(edit);
            m_missionRefreshers.Add(() =>
            {
                if (!edit.HasFocus()) edit.Text = get(m_document.Mission) ?? string.Empty;
            });
            return edit;
        }

        /// <summary>
        /// 파일 경로 칸 한 줄을 더한다 (글 칸과 파일 고르기 버튼).
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="title">이름.</param>
        /// <param name="filter">파일 창이 보여 줄 파일.</param>
        /// <param name="get">미션에서 값을 읽는 함수.</param>
        /// <param name="set">미션에 값을 쓰는 함수.</param>
        private void AddMissionPath(GridContainer grid, string title, string filter, Func<ExtendedMissionData, string> get, Action<ExtendedMissionData, string> set)
        {
            grid.AddChild(new Label { Text = title, TooltipText = "Path from the game folder. Empty: none" });
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddChild(row);
            var edit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            void Apply() => EditMission($"Edit {title}", m => set(m, edit.Text.Trim()));
            edit.TextSubmitted += _ => Apply();
            edit.FocusExited += Apply;
            row.AddChild(edit);
            AddDialogButton(row, "Browse...", () =>
            {
                m_missionPathSetter = path => EditMission($"Edit {title}", m => set(m, path));
                ShowFileDialog(k_menuPickMissionPath, title, filter, false);
            });
            m_missionRefreshers.Add(() =>
            {
                if (!edit.HasFocus()) edit.Text = get(m_document.Mission) ?? string.Empty;
            });
        }

        /// <summary>
        /// 정수 칸 한 줄을 더한다.
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="title">이름.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="get">미션에서 값을 읽는 함수.</param>
        /// <param name="set">미션에 값을 쓰는 함수.</param>
        private void AddMissionNumber(GridContainer grid, string title, string tooltip, Func<ExtendedMissionData, int> get, Action<ExtendedMissionData, int> set)
        {
            grid.AddChild(new Label { Text = title, TooltipText = tooltip });
            SpinBox box = CreateNumberBox(false);
            box.ValueChanged += value =>
            {
                if (!m_fillingMission) EditMission($"Edit {title}", m => set(m, (int)value));
            };
            grid.AddChild(box);
            m_missionRefreshers.Add(() => box.SetValueNoSignal(get(m_document.Mission)));
        }

        /// <summary>
        /// 체크 상자 한 줄을 더한다.
        /// </summary>
        /// <param name="grid">칸을 넣을 격자.</param>
        /// <param name="title">이름.</param>
        /// <param name="tooltip">설명.</param>
        /// <param name="get">미션에서 값을 읽는 함수.</param>
        /// <param name="set">미션에 값을 쓰는 함수.</param>
        private void AddMissionCheck(GridContainer grid, string title, string tooltip, Func<ExtendedMissionData, bool> get, Action<ExtendedMissionData, bool> set)
        {
            grid.AddChild(new Label { Text = title, TooltipText = tooltip });
            var check = new CheckBox { FocusMode = Control.FocusModeEnum.None };
            check.Toggled += pressed =>
            {
                if (!m_fillingMission) EditMission($"Edit {title}", m => set(m, pressed));
            };
            grid.AddChild(check);
            m_missionRefreshers.Add(() => check.SetPressedNoSignal(get(m_document.Mission)));
        }

        /// <summary>
        /// 미션의 설정을 JSON 으로 뜬다 (되돌리기와 비교에 쓴다).
        /// </summary>
        /// <returns>JSON.</returns>
        private string MissionSnapshot()
        {
            return JsonSerializer.Serialize(m_document.Mission, JsonData.Options);
        }

        /// <summary>
        /// 미션의 설정을 고친다 (되돌릴 수 있다). 바뀐 것이 없으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="name">편집의 이름 (영어).</param>
        /// <param name="change">미션을 고치는 함수.</param>
        /// <returns>바뀌었으면 true.</returns>
        private bool EditMission(string name, Action<ExtendedMissionData> change)
        {
            if (m_fillingMission) return false;

            string before = MissionSnapshot();
            change(m_document.Mission);
            string after = MissionSnapshot();
            if (before == after) return false;

            void Set(string json) => m_document.Mission = JsonSerializer.Deserialize<ExtendedMissionData>(json, JsonData.Options);
            m_history.Push(new ActionCommand(name, ActionCommand.Target.Mission, () => Set(before), () => Set(after)));
            m_missionDirty = true;
            ApplyMission();
            SetMessage(name);
            return true;
        }

        /// <summary>
        /// 바뀐 미션의 설정을 화면에 반영한다: 칸의 값, 하늘, 이 미션의 이벤트 묶음.
        /// </summary>
        private void ApplyMission()
        {
            ExtendedMissionData mission = m_document.Mission;
            if (m_missionPanel != null)
            {
                m_fillingMission = true;
                foreach (Action refresh in m_missionRefreshers)
                {
                    refresh();
                }
                m_fillingMission = false;
                m_missionFileLabel.Text = string.IsNullOrEmpty(m_document.MissionPath) ? "File: (not saved yet)" : $"File: {m_document.MissionPath}";
                string block = string.IsNullOrEmpty(m_document.BlockPath) ? "(not saved yet)" : m_document.BlockPath;
                string points = string.IsNullOrEmpty(m_document.PointPath) ? "(not saved yet)" : m_document.PointPath;
                m_missionMapLabel.Text = $"Blocks: {block}\nPoints: {points}\nThe mission file points at these two files. They are filled in when the mission is saved.";
            }

            RefreshAssetList();
            if (m_shownSky != mission.skyIndex)
            {
                m_shownSky = mission.skyIndex;
                MapLoader.LoadSkyData(mission.skyIndex);
                MapLoader.ClearFog();
            }
            if (m_shownEventPack != mission.addonEventDataPath)
            {
                m_shownEventPack = mission.addonEventDataPath;
                ReloadCatalog();
                if (m_markers != null) RebuildPoints();
            }
        }

        /// <summary>
        /// 미션 파일을 지금 경로에 저장한다. 저장한 적이 없으면 경로를 묻는다.
        /// </summary>
        /// <returns>저장했으면 true.</returns>
        private bool SaveMission()
        {
            if (string.IsNullOrEmpty(m_document.MissionPath))
            {
                ShowFileDialog(k_menuSaveMissionAs, "Save mission as", "*" + MIF2File.Extension, true);
                return false;
            }
            return SaveMissionTo(m_document.MissionPath);
        }

        /// <summary>
        /// 미션 파일(MIF2)을 쓴다. 블록과 포인트의 경로는 지금 열려 있는 파일의 것을 적으므로, 둘 다 먼저 저장돼 있어야 한다. 덮어쓰는 파일은 .bak 으로 남긴다.
        /// </summary>
        /// <param name="relativePath">MIF2 경로 (exe 폴더 기준).</param>
        /// <returns>저장했으면 true.</returns>
        private bool SaveMissionTo(string relativePath)
        {
            if (!HasExtension(relativePath, MIF2File.Extension)) relativePath += MIF2File.Extension;
            string fullPath = GamePath.ResolveForWrite(relativePath, MIF2File.Extension, out string pathError);
            if (fullPath == null) return Fail(pathError);
            if (string.IsNullOrEmpty(m_document.BlockPath) || string.IsNullOrEmpty(m_document.PointPath))
            {
                return Fail("Save the blocks and the points first: the mission file points at those two files");
            }

            ExtendedMissionData mission = m_document.Mission;
            mission.blockPath = m_document.BlockPath;
            mission.pointPath = m_document.PointPath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                if (File.Exists(fullPath)) File.Copy(fullPath, fullPath + k_backupExtension, true);
                if (!MIF2File.Write(fullPath, mission, out string error)) return Fail($"Mission file write failed: {relativePath} ({error})");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail($"Mission file write failed: {relativePath} ({e.Message})");
            }

            m_document.MissionPath = relativePath;
            m_missionDirty = false;
            ApplyMission();
            SetMessage($"Saved {relativePath}");
            return true;
        }
    }
}

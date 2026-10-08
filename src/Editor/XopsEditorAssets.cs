using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        /// <summary>
        /// 에셋 화면의 트리 한 줄이 가리키는 값: 형식과 읽고 쓰는 방법.
        /// </summary>
        private sealed class AssetNode
        {
            public string Label;
            public Type Type;
            public Func<object> Get;
            public Action<object> Set;
            public AssetNode Parent;
            // 목록의 항목이면 그 번호, 아니면 −1.
            public int Index = -1;
            // 목록이면 항목의 형식, 아니면 null.
            public Type ElementType;
            // 다른 목록의 항목을 번호로 가리키는 값이면 그 목록. 번호들의 목록이면 목록과 그 항목들이 함께 갖는다.
            public AssetReference Reference;
            public TreeItem Item;
        }

        private const string k_dataFolder = "godotdata";
        private const string k_configFileName = "config.json";
        private const float k_assetListWidth = 300f;
        private const int k_assetKeyColumn = 0;
        private const int k_assetValueColumn = 1;
        private static readonly string[] s_vector2Names = { "x", "y" };
        private static readonly string[] s_vector3Names = { "x", "y", "z" };
        private static readonly string[] s_colorNames = { "r", "g", "b", "a" };

        // 에셋 모드의 화면. 3D 화면과 양옆의 패널을 덮는다.
        private Control m_assetPanel;
        private ItemList m_assetFileList;
        private Tree m_assetTree;
        private Label m_assetTitle;
        private PopupMenu m_assetKindMenu;
        // 왼쪽 목록의 줄 번호 → 파일 경로. 머리말 줄은 null.
        private readonly List<string> m_assetListPaths = new List<string>();
        // 연 파일들 (경로 → 내용). 저장하지 않은 편집을 든 채로 다른 파일을 볼 수 있다.
        private readonly Dictionary<string, AssetFile> m_assetFiles = new Dictionary<string, AssetFile>(StringComparer.OrdinalIgnoreCase);
        // 목록에 따로 더한 파일들 (godotdata 밖의 파일, 새로 만든 파일).
        private readonly List<string> m_assetExtraPaths = new List<string>();
        private AssetFile m_asset;
        private readonly List<AssetNode> m_assetNodes = new List<AssetNode>();
        private AssetFile.Kind m_newAssetKind;
        // 방금 되돌리거나 다시 한 편집이 건드린 파일.
        private AssetFile m_assetTouched;

        /// <summary>
        /// 에셋 모드의 화면을 만든다: 왼쪽에 데이터 파일의 목록, 오른쪽에 고른 파일의 내용(키와 값의 트리).
        /// </summary>
        /// <param name="root">화면 전체를 덮는 부모.</param>
        /// <param name="layer">메뉴를 넣을 노드.</param>
        private void BuildAssetPanel(Control root, Node layer)
        {
            var panel = new PanelContainer { Visible = false };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.13f, 0.14f, 0.16f) });
            Dock(root, panel, 0f, 0f, 1f, 1f, 0f, k_topBarHeight, 0f, -k_statusHeight);
            m_assetPanel = panel;

            var split = new HBoxContainer();
            panel.AddChild(split);

            var left = new VBoxContainer { CustomMinimumSize = new Vector2(k_assetListWidth, 0f) };
            split.AddChild(left);
            left.AddChild(new Label { Text = "Data files" });
            m_assetFileList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            m_assetFileList.ItemSelected += index =>
            {
                string path = m_assetListPaths[(int)index];
                if (path != null) OpenAsset(path);
            };
            left.AddChild(m_assetFileList);
            var fileButtons = new HBoxContainer();
            left.AddChild(fileButtons);
            AddDialogButton(fileButtons, "Open other...", () => ShowFileDialog(k_menuOpenAsset, "Open data file", "*.json", false));
            AddDialogButton(fileButtons, "New addon file...", () =>
            {
                m_assetKindMenu.Position = (Vector2I)(GetWindow().Position + GetViewport().GetMousePosition());
                m_assetKindMenu.Popup();
            });
            var saveButtons = new HBoxContainer();
            left.AddChild(saveButtons);
            AddDialogButton(saveButtons, "Save file", () => SaveAsset(m_asset));
            AddDialogButton(saveButtons, "Save all", () => SaveAssets());

            var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            split.AddChild(right);
            m_assetTitle = new Label { Text = "Choose a data file on the left.", ClipText = true };
            right.AddChild(m_assetTitle);
            var tools = new HBoxContainer();
            right.AddChild(tools);
            AddDialogButton(tools, "Add item", () => AssetAddItem(false));
            AddDialogButton(tools, "Duplicate item", () => AssetAddItem(true));
            AddDialogButton(tools, "Remove item", AssetRemoveItem);
            AddDialogButton(tools, "Move up", () => AssetMoveItem(-1));
            AddDialogButton(tools, "Move down", () => AssetMoveItem(1));
            tools.AddChild(new Label { Text = "  Select a list or one of its items. Double click a value to edit.", Modulate = s_hintColor, ClipText = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            m_assetTree = new Tree
            {
                Columns = 3,
                HideRoot = true,
                ColumnTitlesVisible = true,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SelectMode = Tree.SelectModeEnum.Row,
            };
            m_assetTree.SetColumnTitle(k_assetKeyColumn, "Key");
            m_assetTree.SetColumnTitle(k_assetValueColumn, "Value");
            m_assetTree.SetColumnTitle(k_assetNameColumn, "Refers to");
            m_assetTree.SetColumnExpandRatio(k_assetKeyColumn, 3);
            m_assetTree.SetColumnExpandRatio(k_assetValueColumn, 3);
            m_assetTree.SetColumnExpandRatio(k_assetNameColumn, 2);
            m_assetTree.ItemEdited += OnAssetEdited;
            m_assetTree.ItemSelected += RefreshAssetPreview;
            right.AddChild(m_assetTree);
            BuildAssetPreview(split);

            // 새 에드온 데이터 파일의 종류. 미션이 들고 올 수 있는 것들이다.
            m_assetKindMenu = new PopupMenu();
            for (int i = 0; i < AssetFile.Kinds.Length; i++)
            {
                if (MissionPathField(AssetFile.Kinds[i].Type) != null) m_assetKindMenu.AddItem(AssetFile.Kinds[i].Name, i);
            }
            m_assetKindMenu.IdPressed += id =>
            {
                m_newAssetKind = AssetFile.Kinds[(int)id];
                Callable.From(() => ShowFileDialog(k_menuNewAsset, $"New {m_newAssetKind.Name.ToLowerInvariant()} file", "*.json", true)).CallDeferred();
            };
            layer.AddChild(m_assetKindMenu);
            BuildReferenceMenu(layer);
        }

        /// <summary>
        /// 미션 파일에서 그 종류의 에드온 데이터 파일을 가리키는 필드.
        /// </summary>
        /// <param name="type">데이터 클래스.</param>
        /// <returns>ExtendedMissionData 의 필드. 미션이 들고 오지 않는 종류면 null.</returns>
        private static FieldInfo MissionPathField(Type type)
        {
            string name = null;
            if (type == typeof(HumanParameterData)) name = nameof(ExtendedMissionData.addonHumanDataPath);
            else if (type == typeof(WeaponParameterData)) name = nameof(ExtendedMissionData.addonWeaponDataPath);
            else if (type == typeof(ObjectParameterData)) name = nameof(ExtendedMissionData.addonObjectDataPath);
            else if (type == typeof(EffectParameterData)) name = nameof(ExtendedMissionData.addonEffectDataPath);
            else if (type == typeof(BlockMaterialParameterData)) name = nameof(ExtendedMissionData.addonBlockMaterialDataPath);
            else if (type == typeof(SoundParameterData)) name = nameof(ExtendedMissionData.addonSoundDataPath);
            else if (type == typeof(EventPackData)) name = nameof(ExtendedMissionData.addonEventDataPath);
            return name == null ? null : typeof(ExtendedMissionData).GetField(name);
        }

        /// <summary>
        /// 왼쪽의 파일 목록을 다시 채운다: 기본 데이터(godotdata 의 JSON. 설정 파일은 뺀다), 지금 미션의 에드온 데이터, 따로 연 파일.
        /// </summary>
        private void RefreshAssetList()
        {
            if (m_assetFileList == null) return;

            m_assetFileList.Clear();
            m_assetListPaths.Clear();

            AddAssetHeader("Base data (godotdata)");
            string folder = GamePath.Resolve(k_dataFolder);
            if (folder != null && Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    if (string.Equals(Path.GetFileName(file), k_configFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    AddAssetRow(Path.GetRelativePath(GamePath.Root, file).Replace('\\', '/'));
                }
            }

            var missionPaths = new List<string>();
            foreach (AssetFile.Kind kind in AssetFile.Kinds)
            {
                string path = MissionPathField(kind.Type)?.GetValue(m_document.Mission) as string;
                if (!string.IsNullOrEmpty(path)) missionPaths.Add(path);
            }
            if (missionPaths.Count > 0) AddAssetHeader("This mission's addon data");
            foreach (string path in missionPaths)
            {
                AddAssetRow(path);
            }

            bool header = false;
            foreach (string path in m_assetExtraPaths)
            {
                if (m_assetListPaths.Exists(listed => string.Equals(listed, path, StringComparison.OrdinalIgnoreCase))) continue;
                if (!header) AddAssetHeader("Other files");
                header = true;
                AddAssetRow(path);
            }
        }

        /// <summary>
        /// 파일 목록에 머리말 줄을 더한다.
        /// </summary>
        /// <param name="text">글.</param>
        private void AddAssetHeader(string text)
        {
            int row = m_assetFileList.AddItem(text, null, false);
            m_assetFileList.SetItemCustomFgColor(row, s_hintColor);
            m_assetListPaths.Add(null);
        }

        /// <summary>
        /// 파일 목록에 파일 한 줄을 더한다. 저장하지 않은 편집이 있으면 별표가 붙고, 보고 있는 파일이면 선택된다.
        /// </summary>
        /// <param name="path">파일 경로 (exe 폴더 기준).</param>
        private void AddAssetRow(string path)
        {
            bool dirty = m_assetFiles.TryGetValue(path, out AssetFile file) && file.Dirty;
            string shown = path.StartsWith(k_dataFolder + "/", StringComparison.OrdinalIgnoreCase) ? path.Substring(k_dataFolder.Length + 1) : path;
            int row = m_assetFileList.AddItem($"  {shown}{(dirty ? " *" : string.Empty)}");
            m_assetFileList.SetItemTooltip(row, path);
            m_assetListPaths.Add(path);
            if (m_asset != null && string.Equals(m_asset.Path, path, StringComparison.OrdinalIgnoreCase)) m_assetFileList.Select(row);
        }

        /// <summary>
        /// 데이터 파일을 열어 오른쪽에 보여 준다. 이미 연 파일이면 들고 있던 내용(저장하지 않은 편집 포함)을 보여 준다.
        /// </summary>
        /// <param name="relativePath">파일 경로 (exe 폴더 기준).</param>
        /// <returns>열었으면 true.</returns>
        private bool OpenAsset(string relativePath)
        {
            if (!m_assetFiles.TryGetValue(relativePath, out AssetFile file))
            {
                if (!AssetFile.Load(relativePath, out file, out string error)) return Fail($"Data file open failed: {relativePath} ({error})");
                m_assetFiles[relativePath] = file;
            }

            m_asset = file;
            if (!relativePath.StartsWith(k_dataFolder + "/", StringComparison.OrdinalIgnoreCase) && !m_assetExtraPaths.Contains(relativePath)) m_assetExtraPaths.Add(relativePath);
            RefreshAssetList();
            RebuildAssetTree();
            SetMessage(file.UnknownKeys > 0
                ? $"Opened {relativePath} as {file.FileKind.Name}. {file.UnknownKeys} unknown key(s) will be dropped when saved"
                : $"Opened {relativePath} ({file.FileKind.Name})");
            return true;
        }

        /// <summary>
        /// 새 에드온 데이터 파일을 만들어 쓰고 연다. 지금 미션에 그 종류의 파일이 아직 없으면 미션이 이 파일을 가리키게 한다.
        /// </summary>
        /// <param name="relativePath">파일 경로 (exe 폴더 기준).</param>
        /// <param name="kind">종류.</param>
        /// <returns>만들었으면 true.</returns>
        private bool CreateAsset(string relativePath, AssetFile.Kind kind)
        {
            if (!HasExtension(relativePath, ".json")) relativePath += ".json";
            AssetFile file = AssetFile.Create(relativePath, kind);
            if (!file.Save(out string error)) return Fail($"Data file write failed: {relativePath} ({error})");

            m_assetFiles[relativePath] = file;
            FieldInfo missionField = MissionPathField(kind.Type);
            if (missionField != null && string.IsNullOrEmpty(missionField.GetValue(m_document.Mission) as string))
            {
                EditMission($"Use {relativePath}", mission => missionField.SetValue(mission, relativePath));
            }
            bool opened = OpenAsset(relativePath);
            SetMessage($"Created {relativePath} ({kind.Name})");
            return opened;
        }

        /// <summary>
        /// 데이터 파일 하나를 저장한다. 게임이 읽고 있는 것이면 다시 읽게 한다 (기본 데이터, 이벤트 묶음).
        /// </summary>
        /// <param name="file">저장할 파일. null 이면 아무것도 하지 않는다.</param>
        /// <returns>저장했으면 true.</returns>
        private bool SaveAsset(AssetFile file)
        {
            if (file == null) return false;
            if (!file.Save(out string error)) return Fail($"Data file write failed: {file.Path} ({error})");

            // 기본 데이터는 게임이 시작할 때 읽어 둔 것을 쓴다. 고친 것이 표식과 플레이 테스트에 바로 보이게 다시 읽는다.
            if (file.Path.StartsWith(k_dataFolder + "/", StringComparison.OrdinalIgnoreCase) && file.FileKind.Type != typeof(EventPackData)) DataManager.Instance.Reload();
            if (file.FileKind.Type == typeof(EventPackData))
            {
                ReloadCatalog();
                RebuildPoints();
            }
            RefreshAssetList();
            UpdateAssetTitle();
            SetMessage($"Saved {file.Path}");
            return true;
        }

        /// <summary>
        /// 저장하지 않은 편집이 있는 데이터 파일을 전부 저장한다.
        /// </summary>
        /// <returns>전부 저장했으면 true.</returns>
        private bool SaveAssets()
        {
            bool all = true;
            foreach (AssetFile file in new List<AssetFile>(m_assetFiles.Values))
            {
                if (file.Dirty) all &= SaveAsset(file);
            }
            return all;
        }

        /// <summary>
        /// 저장하지 않은 편집이 있는 데이터 파일이 있는지.
        /// </summary>
        /// <returns>있으면 true.</returns>
        private bool AssetsDirty()
        {
            foreach (AssetFile file in m_assetFiles.Values)
            {
                if (file.Dirty) return true;
            }
            return false;
        }

        /// <summary>
        /// 오른쪽 위의 파일 이름 줄을 다시 쓴다.
        /// </summary>
        private void UpdateAssetTitle()
        {
            if (m_assetTitle == null) return;

            m_assetTitle.Text = m_asset == null ? "Choose a data file on the left." : $"{m_asset.Path}{(m_asset.Dirty ? " *" : string.Empty)}   ({m_asset.FileKind.Name})";
        }

        /// <summary>
        /// 보고 있는 파일의 내용으로 트리를 다시 만든다.
        /// </summary>
        private void RebuildAssetTree()
        {
            if (m_assetTree == null) return;

            m_assetTree.Clear();
            m_assetNodes.Clear();
            UpdateAssetTitle();
            if (m_asset == null)
            {
                RefreshAssetPreview();
                return;
            }

            AssetFile file = m_asset;
            TreeItem root = m_assetTree.CreateItem();
            foreach (FieldInfo field in file.Fields)
            {
                AddAssetItem(root, null, field.Name, field.FieldType, () => field.GetValue(file.Container), value => field.SetValue(file.Container, value), -1, 0, file.FileKind.Type);
            }
            RefreshAssetPreview();
        }

        /// <summary>
        /// 값 하나를 트리의 한 줄로 더한다. 묶음(목록, 객체, 벡터, 색)이면 그 안의 값들을 아래에 더한다.
        /// </summary>
        /// <param name="parentItem">위의 줄.</param>
        /// <param name="parent">위의 값. 최상위면 null.</param>
        /// <param name="label">키 칸에 보일 이름.</param>
        /// <param name="type">값의 형식.</param>
        /// <param name="get">값을 읽는 함수.</param>
        /// <param name="set">값을 쓰는 함수.</param>
        /// <param name="index">목록의 항목이면 그 번호, 아니면 −1.</param>
        /// <param name="depth">최상위에서 몇 단계 아래인지. 최상위 말고는 접어 둔다.</param>
        /// <param name="owner">이 값이 필드로 든 데이터 클래스 (label 이 그 필드 이름이다). 목록의 항목이나 벡터의 성분이면 null.</param>
        /// <returns>만든 줄의 값.</returns>
        private AssetNode AddAssetItem(TreeItem parentItem, AssetNode parent, string label, Type type, Func<object> get, Action<object> set, int index, int depth, Type owner = null)
        {
            TreeItem item = m_assetTree.CreateItem(parentItem);
            var node = new AssetNode { Label = label, Type = type, Get = get, Set = set, Parent = parent, Index = index, Item = item };
            item.SetText(k_assetKeyColumn, label);
            item.SetMetadata(k_assetKeyColumn, m_assetNodes.Count);
            m_assetNodes.Add(node);
            // 번호들의 목록(예: caseWeaponIndex)은 항목마다 같은 목록을 가리킨다.
            node.Reference = owner != null ? AssetReference.Find(owner, label) : (index >= 0 ? parent?.Reference : null);

            object value = get();
            if (type == typeof(bool))
            {
                item.SetCellMode(k_assetValueColumn, TreeItem.TreeCellMode.Check);
                item.SetChecked(k_assetValueColumn, (bool)value);
                item.SetEditable(k_assetValueColumn, true);
            }
            else if (IsAssetLeaf(type))
            {
                item.SetText(k_assetValueColumn, FormatAssetValue(value));
                item.SetEditable(k_assetValueColumn, true);
                if (type.IsEnum) item.SetTooltipText(k_assetValueColumn, string.Join(", ", Enum.GetNames(type)));
            }
            else if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Color))
            {
                string[] names = type == typeof(Vector2) ? s_vector2Names : type == typeof(Vector3) ? s_vector3Names : s_colorNames;
                for (int i = 0; i < names.Length; i++)
                {
                    int component = i;
                    AddAssetItem(item, node, names[i], typeof(float), () => GetComponent(get(), component), v => set(WithComponent(get(), component, (float)v)), -1, depth + 1);
                }
                item.Collapsed = true;
            }
            else if (value is IList list && TryGetElementType(type, out Type elementType))
            {
                node.ElementType = elementType;
                for (int i = 0; i < list.Count; i++)
                {
                    int element = i;
                    AddAssetItem(item, node, $"[{i}]", elementType, () => ((IList)get())[element], v => ((IList)get())[element] = v, i, depth + 1);
                }
                item.Collapsed = depth > 0;
            }
            else if (type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type))
            {
                if (value == null && type.GetConstructor(Type.EmptyTypes) != null)
                {
                    value = Activator.CreateInstance(type);
                    set(value);
                }
                if (value != null)
                {
                    foreach (FieldInfo field in AssetFile.DataFields(type))
                    {
                        AddAssetItem(item, node, field.Name, field.FieldType, () => field.GetValue(get()), v => field.SetValue(get(), v), -1, depth + 1, value.GetType());
                    }
                }
                item.Collapsed = depth > 0;
            }
            else
            {
                item.SetText(k_assetValueColumn, "(edit this value in the file)");
            }
            RefreshAssetSummary(node);
            RefreshAssetReference(node);
            return node;
        }

        /// <summary>
        /// 글 한 칸으로 고치는 값인지 (수, 글, 열거형).
        /// </summary>
        /// <param name="type">값의 형식.</param>
        /// <returns>한 칸으로 고치면 true.</returns>
        private static bool IsAssetLeaf(Type type)
        {
            return type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long) || type == typeof(string) || type.IsEnum;
        }

        /// <summary>
        /// 목록 형식의 항목 형식을 얻는다.
        /// </summary>
        /// <param name="type">목록의 형식 (배열, List, DataList).</param>
        /// <param name="elementType">항목의 형식.</param>
        /// <returns>목록이면 true.</returns>
        private static bool TryGetElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = current.GetGenericArguments()[0];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 벡터나 색의 성분 하나를 읽는다.
        /// </summary>
        /// <param name="value">Vector2, Vector3, Color.</param>
        /// <param name="component">성분 번호.</param>
        /// <returns>성분의 값.</returns>
        private static object GetComponent(object value, int component)
        {
            switch (value)
            {
                case Vector2 vector2: return vector2[component];
                case Vector3 vector3: return vector3[component];
                case Color color: return color[component];
            }
            return 0f;
        }

        /// <summary>
        /// 벡터나 색의 성분 하나를 바꾼 값을 만든다.
        /// </summary>
        /// <param name="value">Vector2, Vector3, Color.</param>
        /// <param name="component">성분 번호.</param>
        /// <param name="number">새 값.</param>
        /// <returns>바뀐 벡터나 색.</returns>
        private static object WithComponent(object value, int component, float number)
        {
            switch (value)
            {
                case Vector2 vector2:
                    vector2[component] = number;
                    return vector2;
                case Vector3 vector3:
                    vector3[component] = number;
                    return vector3;
                case Color color:
                    color[component] = number;
                    return color;
            }
            return value;
        }

        /// <summary>
        /// 값을 값 칸에 보일 글로 바꾼다.
        /// </summary>
        /// <param name="value">값.</param>
        /// <returns>글.</returns>
        private static string FormatAssetValue(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case float number: return number.ToString(CultureInfo.InvariantCulture);
                case double number: return number.ToString(CultureInfo.InvariantCulture);
                case Vector2 vector: return string.Format(CultureInfo.InvariantCulture, "{0}, {1}", vector.X, vector.Y);
                case Vector3 vector: return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", vector.X, vector.Y, vector.Z);
                case Color color: return string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 묶음인 줄의 값 칸에 요약을 적는다: 목록은 항목 수, 벡터와 색은 성분들, 객체는 name 필드의 값.
        /// </summary>
        /// <param name="node">줄의 값.</param>
        private void RefreshAssetSummary(AssetNode node)
        {
            if (node == null || node.Type == typeof(bool) || IsAssetLeaf(node.Type)) return;

            object value = node.Get();
            if (node.ElementType != null) node.Item.SetText(k_assetValueColumn, $"{((IList)value).Count} item(s)");
            else if (value is Vector2 or Vector3 or Color) node.Item.SetText(k_assetValueColumn, FormatAssetValue(value));
            else if (value != null && value.GetType().GetField("name") is FieldInfo nameField && nameField.FieldType == typeof(string))
            {
                node.Item.SetText(k_assetValueColumn, nameField.GetValue(value) as string ?? string.Empty);
            }
        }

        /// <summary>
        /// 트리의 줄이 가리키는 값을 찾는다.
        /// </summary>
        /// <param name="item">줄. null 이어도 된다.</param>
        /// <returns>값. 없으면 null.</returns>
        private AssetNode AssetNodeOf(TreeItem item)
        {
            if (item == null) return null;

            int id = item.GetMetadata(k_assetKeyColumn).AsInt32();
            return id >= 0 && id < m_assetNodes.Count ? m_assetNodes[id] : null;
        }

        /// <summary>
        /// 트리에서 값 칸을 고쳤을 때: 칸의 글을 그 값의 형식으로 읽어 넣는다. 읽을 수 없으면 원래 값으로 되돌린다.
        /// </summary>
        private void OnAssetEdited()
        {
            TreeItem item = m_assetTree.GetEdited();
            AssetNode node = AssetNodeOf(item);
            if (node == null) return;

            object value;
            if (node.Type == typeof(bool)) value = item.IsChecked(k_assetValueColumn);
            else if (!TryParseAssetValue(node.Type, item.GetText(k_assetValueColumn), out value))
            {
                string typed = item.GetText(k_assetValueColumn);
                item.SetText(k_assetValueColumn, FormatAssetValue(node.Get()));
                Fail($"\"{typed}\" is not a valid {node.Type.Name} for {node.Label}");
                return;
            }
            SetAssetValue(node, value);
        }

        /// <summary>
        /// 글을 값의 형식으로 읽는다. 열거형은 이름이나 번호를 받는다.
        /// </summary>
        /// <param name="type">값의 형식.</param>
        /// <param name="text">글.</param>
        /// <param name="value">읽은 값.</param>
        /// <returns>읽었으면 true.</returns>
        private static bool TryParseAssetValue(Type type, string text, out object value)
        {
            value = null;
            text = text.Trim();
            if (type == typeof(string))
            {
                value = text;
                return true;
            }
            if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole))
            {
                value = whole;
                return true;
            }
            if (type == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long big))
            {
                value = big;
                return true;
            }
            if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float real) && float.IsFinite(real))
            {
                value = real;
                return true;
            }
            if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double wide) && double.IsFinite(wide))
            {
                value = wide;
                return true;
            }
            if (type.IsEnum && Enum.TryParse(type, text, true, out object named) && Enum.IsDefined(type, named))
            {
                value = named;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 값 하나를 바꾼다 (되돌릴 수 있다). 그 줄과 위의 묶음들의 요약을 다시 적는다.
        /// </summary>
        /// <param name="node">줄의 값.</param>
        /// <param name="value">새 값. 그 줄의 형식이어야 한다.</param>
        /// <returns>바뀌었으면 true.</returns>
        private bool SetAssetValue(AssetNode node, object value)
        {
            bool changed = EditAsset($"Edit {node.Label}", () => node.Set(value));
            if (node.Type == typeof(bool)) node.Item.SetChecked(k_assetValueColumn, (bool)node.Get());
            else node.Item.SetText(k_assetValueColumn, FormatAssetValue(node.Get()));
            for (AssetNode up = node.Parent; up != null; up = up.Parent)
            {
                RefreshAssetSummary(up);
            }
            if (changed)
            {
                RefreshAssetReferences();
                RefreshAssetPreview();
            }
            return changed;
        }

        /// <summary>
        /// 보고 있는 데이터 파일을 고친다 (되돌릴 수 있다). 내용이 바뀌지 않았으면 기록하지 않는다.
        /// </summary>
        /// <param name="name">편집의 이름 (영어).</param>
        /// <param name="change">내용을 고치는 함수.</param>
        /// <returns>바뀌었으면 true.</returns>
        private bool EditAsset(string name, Action change)
        {
            AssetFile file = m_asset;
            if (file == null) return false;

            string before = file.Serialize();
            change();
            string after = file.Serialize();
            if (before == after) return false;

            void Set(string json)
            {
                file.Restore(json);
                m_assetTouched = file;
            }
            m_history.Push(new ActionCommand(name, ActionCommand.Target.Asset, () => Set(before), () => Set(after)));
            bool wasDirty = file.Dirty;
            file.Dirty = true;
            if (!wasDirty) RefreshAssetList();
            UpdateAssetTitle();
            SetMessage(name);
            return true;
        }

        /// <summary>
        /// 데이터 파일의 편집을 되돌리거나 다시 한 뒤에 화면을 맞춘다.
        /// </summary>
        private void AfterAssetHistoryStep()
        {
            if (m_assetTouched == null) return;

            m_assetTouched.Dirty = true;
            if (m_assetTouched == m_asset) RebuildAssetTree();
            RefreshAssetList();
            m_assetTouched = null;
        }

        /// <summary>
        /// 트리에서 선택한 줄이 속한 목록과 그 안에서의 번호를 찾는다. 목록 자체를 선택했으면 번호는 −1 이다.
        /// </summary>
        /// <param name="list">목록의 줄.</param>
        /// <param name="index">항목 번호.</param>
        /// <returns>목록이나 그 항목(의 안쪽)을 선택했으면 true.</returns>
        private bool SelectedAssetList(out AssetNode list, out int index)
        {
            list = null;
            index = -1;
            AssetNode node = m_assetTree == null ? null : AssetNodeOf(m_assetTree.GetSelected());
            if (node == null) return false;
            if (node.ElementType != null)
            {
                list = node;
                return true;
            }
            for (; node != null; node = node.Parent)
            {
                if (node.Parent != null && node.Parent.ElementType != null && node.Index >= 0)
                {
                    list = node.Parent;
                    index = node.Index;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 목록의 항목들을 바꾼 뒤 트리를 다시 만들고 그 목록을 펴서 항목 하나를 선택한다.
        /// </summary>
        /// <param name="name">편집의 이름 (영어).</param>
        /// <param name="list">목록의 줄.</param>
        /// <param name="mutate">항목들을 고치는 함수.</param>
        /// <param name="select">선택할 항목 번호. 음수면 목록을 선택한다.</param>
        private void ChangeAssetList(string name, AssetNode list, Action<List<object>> mutate, int select)
        {
            // 다시 만든 트리에서 같은 목록을 찾을 수 있게, 위에서부터 몇 번째 자식인지를 적어 둔다.
            var route = new List<int>();
            for (TreeItem item = list.Item; item.GetParent() != null; item = item.GetParent())
            {
                route.Insert(0, item.GetIndex());
            }

            bool changed = EditAsset(name, () =>
            {
                var current = (IList)list.Get();
                var items = new List<object>();
                foreach (object element in current) items.Add(element);
                mutate(items);

                if (list.Type.IsArray)
                {
                    Array array = Array.CreateInstance(list.ElementType, items.Count);
                    for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                    list.Set(array);
                }
                else
                {
                    current.Clear();
                    foreach (object element in items) current.Add(element);
                }
            });
            if (!changed) return;

            RebuildAssetTree();
            TreeItem found = m_assetTree.GetRoot();
            foreach (int step in route)
            {
                if (found == null || step >= found.GetChildCount()) return;
                found = found.GetChild(step);
                found.Collapsed = false;
            }
            if (select >= 0 && select < found.GetChildCount()) found = found.GetChild(select);
            found.Select(k_assetKeyColumn);
            m_assetTree.ScrollToItem(found);
            RefreshAssetPreview();
        }

        /// <summary>
        /// 선택한 목록에 항목을 더한다. 항목을 선택하고 있었으면 그 바로 뒤에, 목록을 선택하고 있었으면 맨 뒤에 넣는다.
        /// </summary>
        /// <param name="duplicate">true 면 선택한 항목의 복사본을, false 면 기본값의 새 항목을 넣는다.</param>
        private void AssetAddItem(bool duplicate)
        {
            if (!SelectedAssetList(out AssetNode list, out int index) || (duplicate && index < 0))
            {
                SetMessage(duplicate ? "Select an item of a list first" : "Select a list or one of its items first");
                return;
            }

            Type type = list.ElementType;
            ChangeAssetList(duplicate ? "Duplicate item" : "Add item", list, items =>
            {
                object element;
                if (duplicate) element = JsonSerializer.Deserialize(JsonSerializer.Serialize(items[index], type, JsonData.Options), type, JsonData.Options);
                else if (type == typeof(string)) element = string.Empty;
                else element = Activator.CreateInstance(type);
                items.Insert(index < 0 ? items.Count : index + 1, element);
            }, index < 0 ? ((IList)list.Get()).Count : index + 1);
        }

        /// <summary>
        /// 선택한 항목을 목록에서 지운다. 뒤의 항목들의 번호가 하나씩 당겨진다 (그 번호를 가리키던 값은 고쳐 주지 않는다).
        /// </summary>
        private void AssetRemoveItem()
        {
            if (!SelectedAssetList(out AssetNode list, out int index) || index < 0)
            {
                SetMessage("Select an item of a list first");
                return;
            }
            ChangeAssetList("Remove item", list, items => items.RemoveAt(index), index - 1);
        }

        /// <summary>
        /// 선택한 항목을 목록 안에서 한 칸 옮긴다 (두 항목의 번호가 서로 바뀐다).
        /// </summary>
        /// <param name="step">−1 이면 앞으로, 1 이면 뒤로.</param>
        private void AssetMoveItem(int step)
        {
            if (!SelectedAssetList(out AssetNode list, out int index) || index < 0) return;

            int other = index + step;
            if (other < 0 || other >= ((IList)list.Get()).Count) return;
            ChangeAssetList("Move item", list, items => (items[index], items[other]) = (items[other], items[index]), other);
        }
    }
}

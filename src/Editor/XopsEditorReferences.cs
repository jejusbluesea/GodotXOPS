using System;
using System.Collections;
using System.Reflection;
using Godot;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        private const int k_assetNameColumn = 2;
        // 목록에서 고르는 메뉴에 한 번에 넣는 항목 수의 한계.
        private const int k_referenceMenuLimit = 400;

        private PopupMenu m_referenceMenu;
        // 메뉴에서 고른 번호가 들어갈 줄.
        private AssetNode m_referenceNode;

        /// <summary>
        /// 번호 칸의 이름을 고르는 메뉴를 만들고, 트리의 이름 칸을 눌렀을 때 그 메뉴가 뜨게 잇는다.
        /// </summary>
        /// <param name="layer">메뉴를 넣을 노드.</param>
        private void BuildReferenceMenu(Node layer)
        {
            m_referenceMenu = new PopupMenu();
            m_referenceMenu.IdPressed += id =>
            {
                if (m_referenceNode != null) SetAssetValue(m_referenceNode, (int)id);
            };
            layer.AddChild(m_referenceMenu);
            m_assetTree.CustomPopupEdited += _ => ShowReferenceMenu(AssetNodeOf(m_assetTree.GetEdited()));
        }

        /// <summary>
        /// 다른 목록의 항목을 번호로 가리키는 값인지.
        /// </summary>
        /// <param name="node">줄의 값.</param>
        /// <returns>번호 칸이면 true.</returns>
        private static bool IsReference(AssetNode node)
        {
            return node != null && node.Reference != null && node.Type == typeof(int);
        }

        /// <summary>
        /// 번호 칸이 가리키는 목록을 찾는다. 같은 항목 안의 목록(모델의 textures)이면 위로 올라가며 그 필드를 가진 항목을 찾는다.
        /// </summary>
        /// <param name="node">번호 칸.</param>
        /// <param name="addon">true 면 지금 미션의 에드온 데이터의 목록 (번호 10000 부터).</param>
        /// <returns>목록. 없으면 null.</returns>
        private IList ReferenceList(AssetNode node, bool addon)
        {
            AssetReference reference = node.Reference;
            if (!reference.Local) return addon ? AddonListOf(reference.Container, reference.ListPath) : ListOf(reference.Container, reference.ListPath);
            if (addon) return null;

            for (AssetNode up = node.Parent; up != null; up = up.Parent)
            {
                object value = up.Type.IsValueType || up.Type == typeof(string) ? null : up.Get();
                FieldInfo field = value?.GetType().GetField(reference.ListPath);
                if (field != null) return field.GetValue(value) as IList;
            }
            return null;
        }

        /// <summary>
        /// 지금 미션의 에드온 데이터에서 목록 하나를 찾는다. 파일이 에디터에 열려 있으면 그 내용(저장하지 않은 편집 포함)을 쓴다.
        /// </summary>
        /// <param name="containerType">목록이 든 데이터 클래스.</param>
        /// <param name="listPath">목록의 필드 이름 (안쪽이면 점으로 이은 것).</param>
        /// <returns>목록. 미션에 그 종류의 에드온 데이터가 없으면 null.</returns>
        private IList AddonListOf(Type containerType, string listPath)
        {
            string path = MissionPathField(containerType)?.GetValue(m_document.Mission) as string;
            if (string.IsNullOrEmpty(path)) return null;
            if (!m_assetFiles.TryGetValue(path, out AssetFile file))
            {
                if (!AssetFile.Load(path, out file, out _)) return null;
                m_assetFiles[path] = file;
            }
            return file.FileKind.Type == containerType ? WalkList(file.Container, listPath.Split('.')) : null;
        }

        /// <summary>
        /// 목록의 한 항목을 사람이 알아볼 이름으로 적는다: name 필드, 없으면 처음 나오는 글 필드(텍스처 경로 등), 글의 목록이면 그 글.
        /// </summary>
        /// <param name="item">항목.</param>
        /// <returns>이름. 붙일 이름이 없으면 빈 문자열.</returns>
        private static string ItemName(object item)
        {
            if (item == null) return string.Empty;
            if (item is string text) return text;

            Type type = item.GetType();
            if (type.GetField("name") is FieldInfo nameField && nameField.FieldType == typeof(string)) return nameField.GetValue(item) as string ?? string.Empty;
            foreach (FieldInfo field in AssetFile.DataFields(type))
            {
                if (field.FieldType == typeof(string)) return field.GetValue(item) as string ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// 번호 칸의 값이 가리키는 것을 한 줄로 적는다.
        /// </summary>
        /// <param name="node">번호 칸.</param>
        /// <returns>"→ 이름", 없는 번호면 그 사실.</returns>
        private string DescribeReference(AssetNode node)
        {
            int value = (int)node.Get();
            bool addon = value >= DataList<WeaponData>.AddonBase;
            IList list = ReferenceList(node, addon);
            int index = addon ? value - DataList<WeaponData>.AddonBase : value;
            if (list == null) return addon ? "→ (no mission addon data)" : "→ ?";
            if (index < 0 || index >= list.Count) return value < 0 ? "→ (none)" : "→ (no such item)";

            string name = ItemName(list[index]);
            return $"→ {(name.Length > 0 ? name : $"#{value}")}{(addon ? "  (mission)" : string.Empty)}";
        }

        /// <summary>
        /// 줄이 번호 칸이면 이름 칸을 채우고, 눌러서 목록에서 고를 수 있게 한다.
        /// </summary>
        /// <param name="node">줄의 값.</param>
        private void RefreshAssetReference(AssetNode node)
        {
            if (!IsReference(node)) return;

            node.Item.SetCellMode(k_assetNameColumn, TreeItem.TreeCellMode.Custom);
            node.Item.SetEditable(k_assetNameColumn, true);
            node.Item.SetText(k_assetNameColumn, DescribeReference(node));
            node.Item.SetTooltipText(k_assetNameColumn, "Click to choose from the list");
        }

        /// <summary>
        /// 모든 번호 칸의 이름을 다시 적는다 (값이나 이름이 바뀐 뒤).
        /// </summary>
        private void RefreshAssetReferences()
        {
            foreach (AssetNode node in m_assetNodes)
            {
                if (IsReference(node)) node.Item.SetText(k_assetNameColumn, DescribeReference(node));
            }
        }

        /// <summary>
        /// 번호 칸이 고를 수 있는 항목들을 메뉴로 띄운다: 기본 데이터의 목록, 그 뒤에 지금 미션의 에드온 데이터(10000 부터). 항목의 번호가 곧 값이다.
        /// </summary>
        /// <param name="node">번호 칸. 번호 칸이 아니면 아무것도 하지 않는다.</param>
        /// <returns>메뉴에 넣은 항목 수.</returns>
        private int ShowReferenceMenu(AssetNode node)
        {
            if (!IsReference(node)) return 0;

            m_referenceNode = node;
            m_referenceMenu.Clear();
            int added = 0;
            foreach (bool addon in new[] { false, true })
            {
                IList list = ReferenceList(node, addon);
                if (list == null || list.Count == 0) continue;
                if (addon) m_referenceMenu.AddSeparator("This mission's addon data");

                int offset = addon ? DataList<WeaponData>.AddonBase : 0;
                for (int i = 0; i < list.Count && added < k_referenceMenuLimit; i++, added++)
                {
                    m_referenceMenu.AddItem($"{offset + i}  {ItemName(list[i])}", offset + i);
                }
            }
            if (added == 0)
            {
                SetMessage("Nothing to choose from: the list is empty");
                return 0;
            }
            m_referenceMenu.Position = (Vector2I)(GetWindow().Position + GetViewport().GetMousePosition());
            m_referenceMenu.Popup();
            return added;
        }
    }
}

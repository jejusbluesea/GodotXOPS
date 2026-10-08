using System.Collections.Generic;
using Godot;

namespace GodotXOPS.Editor
{
    public partial class XopsEditor
    {
        private AcceptDialog m_messageDialog;
        private ItemList m_messageList;
        private LineEdit m_messageEdit;

        /// <summary>
        /// 메시지(.msg) 편집 창을 만든다. 메시지는 포인트 데이터와 같은 이름의 .msg 파일의 줄들이고, Message 이벤트가 줄 번호로 가리킨다.
        /// </summary>
        /// <param name="parent">창을 넣을 노드.</param>
        private void BuildMessageDialog(Node parent)
        {
            m_messageDialog = new AcceptDialog { Title = "Messages", OkButtonText = "Close", Size = new Vector2I(640, 480) };
            var column = new VBoxContainer();
            m_messageDialog.AddChild(column);
            column.AddChild(new Label { Text = "Message events show a line by its number. Saved next to the point file as .msg" });

            m_messageList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            m_messageList.ItemSelected += index => m_messageEdit.Text = m_document.Messages[(int)index];
            column.AddChild(m_messageList);

            m_messageEdit = new LineEdit { PlaceholderText = "Text of the selected message (Enter applies)" };
            m_messageEdit.TextSubmitted += text => EditMessage(SelectedMessage(), text);
            column.AddChild(m_messageEdit);

            var buttons = new HBoxContainer();
            column.AddChild(buttons);
            AddDialogButton(buttons, "Add", () => InsertMessage(m_document.Messages.Count, m_messageEdit.Text));
            AddDialogButton(buttons, "Apply text", () => EditMessage(SelectedMessage(), m_messageEdit.Text));
            AddDialogButton(buttons, "Move up", () => MoveMessage(SelectedMessage(), -1));
            AddDialogButton(buttons, "Move down", () => MoveMessage(SelectedMessage(), 1));
            AddDialogButton(buttons, "Remove", () => RemoveMessage(SelectedMessage()));
            buttons.AddChild(new Label { Text = "  Removing or moving changes the numbers of the lines after it." });
            parent.AddChild(m_messageDialog);
        }

        /// <summary>
        /// 창의 버튼 하나를 더한다.
        /// </summary>
        /// <param name="row">버튼 줄.</param>
        /// <param name="text">글자.</param>
        /// <param name="action">눌렀을 때 할 일.</param>
        private static void AddDialogButton(HBoxContainer row, string text, System.Action action)
        {
            var button = new Button { Text = text };
            button.Pressed += action;
            row.AddChild(button);
        }

        /// <summary>
        /// 메시지 편집 창을 띄운다.
        /// </summary>
        private void ShowMessageDialog()
        {
            RefreshMessageList(-1);
            m_messageDialog.PopupCentered();
        }

        /// <summary>
        /// 창의 목록을 문서의 메시지로 다시 채운다.
        /// </summary>
        /// <param name="select">선택해 둘 줄 번호. 음수면 선택하지 않는다.</param>
        private void RefreshMessageList(int select)
        {
            if (m_messageList == null) return;

            m_messageList.Clear();
            for (int i = 0; i < m_document.Messages.Count; i++)
            {
                m_messageList.AddItem($"{i}:  {m_document.Messages[i]}");
            }
            if (select >= 0 && select < m_document.Messages.Count)
            {
                m_messageList.Select(select);
                m_messageEdit.Text = m_document.Messages[select];
            }
        }

        /// <summary>
        /// 창에서 선택한 메시지의 번호.
        /// </summary>
        /// <returns>줄 번호. 선택이 없으면 −1.</returns>
        private int SelectedMessage()
        {
            int[] selected = m_messageList.GetSelectedItems();
            return selected.Length > 0 ? selected[0] : -1;
        }

        /// <summary>
        /// 메시지 목록을 바꾸는 편집을 기록하고 화면을 맞춘다 (되돌릴 수 있다).
        /// </summary>
        /// <param name="name">편집의 이름 (영어).</param>
        /// <param name="before">바뀌기 전의 목록.</param>
        /// <param name="select">창에서 선택해 둘 줄 번호.</param>
        private void PushMessageChange(string name, string[] before, int select)
        {
            string[] after = m_document.Messages.ToArray();
            List<string> messages = m_document.Messages;
            void Set(string[] values)
            {
                messages.Clear();
                messages.AddRange(values);
                RefreshMessageList(-1);
            }
            m_history.Push(new ActionCommand(name, ActionCommand.Target.Points, () => Set(before), () => Set(after)));
            m_dirty = true;
            RefreshMessageList(select);
            RebuildPoints();
            SetMessage(name);
        }

        /// <summary>
        /// 메시지 한 줄을 끼워 넣는다.
        /// </summary>
        /// <param name="index">넣을 자리 (그 자리의 줄부터 뒤로 밀린다).</param>
        /// <param name="text">글.</param>
        private void InsertMessage(int index, string text)
        {
            string[] before = m_document.Messages.ToArray();
            index = Mathf.Clamp(index, 0, m_document.Messages.Count);
            m_document.Messages.Insert(index, text ?? string.Empty);
            PushMessageChange("Add message", before, index);
        }

        /// <summary>
        /// 메시지 한 줄의 글을 바꾼다.
        /// </summary>
        /// <param name="index">줄 번호. 범위 밖이면 아무것도 하지 않는다.</param>
        /// <param name="text">새 글.</param>
        private void EditMessage(int index, string text)
        {
            if (index < 0 || index >= m_document.Messages.Count || m_document.Messages[index] == text) return;

            string[] before = m_document.Messages.ToArray();
            m_document.Messages[index] = text ?? string.Empty;
            PushMessageChange("Edit message", before, index);
        }

        /// <summary>
        /// 메시지 한 줄을 위나 아래로 한 칸 옮긴다 (두 줄의 번호가 서로 바뀐다).
        /// </summary>
        /// <param name="index">줄 번호.</param>
        /// <param name="step">−1 이면 위로, 1 이면 아래로.</param>
        private void MoveMessage(int index, int step)
        {
            int other = index + step;
            if (index < 0 || index >= m_document.Messages.Count || other < 0 || other >= m_document.Messages.Count) return;

            string[] before = m_document.Messages.ToArray();
            (m_document.Messages[index], m_document.Messages[other]) = (m_document.Messages[other], m_document.Messages[index]);
            PushMessageChange("Move message", before, other);
        }

        /// <summary>
        /// 메시지 한 줄을 지운다. 뒤의 줄들의 번호가 하나씩 당겨진다 (그것을 가리키는 이벤트는 고쳐 주지 않는다).
        /// </summary>
        /// <param name="index">줄 번호.</param>
        private void RemoveMessage(int index)
        {
            if (index < 0 || index >= m_document.Messages.Count) return;

            string[] before = m_document.Messages.ToArray();
            m_document.Messages.RemoveAt(index);
            PushMessageChange("Remove message", before, Mathf.Min(index, m_document.Messages.Count - 1));
        }
    }
}

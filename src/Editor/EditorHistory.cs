using System.Collections.Generic;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 되돌릴 수 있는 편집 한 번. 이미 적용된 상태로 만들어지고, 되돌리기와 다시 하기를 할 수 있다.
    /// </summary>
    public interface IEditorCommand
    {
        // 상태 줄에 보일 이름 (영어).
        string Name { get; }

        /// <summary>
        /// 편집을 되돌린다.
        /// </summary>
        void Undo();

        /// <summary>
        /// 되돌린 편집을 다시 한다.
        /// </summary>
        void Redo();
    }

    /// <summary>
    /// 되돌리기 기록. 에디터를 켜 둔 동안 횟수 제한 없이 쌓인다. 새 편집을 하면 다시 하기 쪽은 비워진다.
    /// </summary>
    public sealed class EditorHistory
    {
        private readonly Stack<IEditorCommand> m_undo = new Stack<IEditorCommand>();
        private readonly Stack<IEditorCommand> m_redo = new Stack<IEditorCommand>();

        public int UndoCount => m_undo.Count;
        public int RedoCount => m_redo.Count;

        /// <summary>
        /// 이미 적용된 편집을 기록한다.
        /// </summary>
        /// <param name="command">편집.</param>
        public void Push(IEditorCommand command)
        {
            m_undo.Push(command);
            m_redo.Clear();
        }

        /// <summary>
        /// 마지막 편집을 되돌린다.
        /// </summary>
        /// <returns>되돌린 편집. 되돌릴 것이 없으면 null.</returns>
        public IEditorCommand Undo()
        {
            if (m_undo.Count == 0) return null;

            IEditorCommand command = m_undo.Pop();
            command.Undo();
            m_redo.Push(command);
            return command;
        }

        /// <summary>
        /// 되돌린 편집을 다시 한다.
        /// </summary>
        /// <returns>다시 한 편집. 다시 할 것이 없으면 null.</returns>
        public IEditorCommand Redo()
        {
            if (m_redo.Count == 0) return null;

            IEditorCommand command = m_redo.Pop();
            command.Redo();
            m_undo.Push(command);
            return command;
        }

        /// <summary>
        /// 기록을 전부 버린다 (다른 파일을 열었을 때).
        /// </summary>
        public void Clear()
        {
            m_undo.Clear();
            m_redo.Clear();
        }
    }
}

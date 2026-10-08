using System;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 되돌리기와 다시 하기를 함수 둘로 받는 편집. 값 몇 개를 통째로 바꾸는 편집(이벤트 줄의 시작 번호, 메시지 목록, 미션의 설정)에 쓴다.
    /// </summary>
    public sealed class ActionCommand : IEditorCommand
    {
        /// <summary>
        /// 편집이 건드린 것. 되돌린 뒤 에디터가 무엇을 다시 그리고 무엇에 "바뀜" 표시를 할지 정한다.
        /// </summary>
        public enum Target
        {
            // 포인트 파일에 들어가는 것 (이벤트 줄의 시작 번호, 메시지).
            Points,
            // 미션 파일의 설정.
            Mission,
            // 데이터 파일(에셋 모드).
            Asset,
        }

        private readonly Action m_undo;
        private readonly Action m_redo;

        public string Name { get; }
        public Target Changed { get; }

        /// <summary>
        /// 편집을 만든다. 편집은 이미 적용돼 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="changed">편집이 건드린 것.</param>
        /// <param name="undo">되돌리는 함수.</param>
        /// <param name="redo">다시 하는 함수.</param>
        public ActionCommand(string name, Target changed, Action undo, Action redo)
        {
            Name = name;
            Changed = changed;
            m_undo = undo;
            m_redo = redo;
        }

        public void Undo()
        {
            m_undo();
        }

        public void Redo()
        {
            m_redo();
        }
    }
}

using System.Collections.Generic;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록의 수나 순서를 바꾼 편집 (더하기, 복제, 지우기). 편집 전과 후의 블록 목록을 통째로 들고 있다.
    /// </summary>
    public sealed class BlockListCommand : IEditorCommand
    {
        private readonly List<BD2Block> m_blocks;
        private readonly BD2Block[] m_before;
        private readonly BD2Block[] m_after;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 블록 목록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="blocks">문서의 블록 목록.</param>
        /// <param name="before">바뀌기 전의 목록 (Snapshot 으로 떠 둔 것).</param>
        public BlockListCommand(string name, List<BD2Block> blocks, BD2Block[] before)
        {
            Name = name;
            m_blocks = blocks;
            m_before = before;
            m_after = Snapshot(blocks);
        }

        public void Undo()
        {
            Restore(m_before);
        }

        public void Redo()
        {
            Restore(m_after);
        }

        /// <summary>
        /// 블록 목록 전체의 복사본을 뜬다.
        /// </summary>
        /// <param name="blocks">블록 목록.</param>
        /// <returns>복사본.</returns>
        public static BD2Block[] Snapshot(List<BD2Block> blocks)
        {
            var result = new BD2Block[blocks.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = BlockChangeCommand.Clone(blocks[i]);
            }
            return result;
        }

        /// <summary>
        /// 목록을 떠 둔 내용으로 바꾼다.
        /// </summary>
        /// <param name="snapshot">떠 둔 내용.</param>
        private void Restore(BD2Block[] snapshot)
        {
            m_blocks.Clear();
            foreach (BD2Block block in snapshot)
            {
                m_blocks.Add(BlockChangeCommand.Clone(block));
            }
        }
    }
}

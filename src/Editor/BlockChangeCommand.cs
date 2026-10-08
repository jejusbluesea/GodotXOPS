using System.Collections.Generic;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록 몇 개의 내용을 바꾼 편집 (꼭짓점 옮기기, 면의 값 고치기). 바뀐 블록마다 전과 후의 내용을 통째로 들고 있다.
    /// 블록의 수와 순서는 바꾸지 않는다 (더하기와 지우기는 BlockListCommand 다).
    /// </summary>
    public sealed class BlockChangeCommand : IEditorCommand
    {
        private readonly List<BD2Block> m_blocks;
        private readonly int[] m_indices;
        private readonly BD2Block[] m_before;
        private readonly BD2Block[] m_after;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 블록 목록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="blocks">문서의 블록 목록.</param>
        /// <param name="indices">바뀐 블록의 번호들.</param>
        /// <param name="before">바뀌기 전의 내용 (indices 와 같은 순서의 복사본).</param>
        public BlockChangeCommand(string name, List<BD2Block> blocks, int[] indices, BD2Block[] before)
        {
            Name = name;
            m_blocks = blocks;
            m_indices = indices;
            m_before = before;
            m_after = new BD2Block[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                m_after[i] = Clone(blocks[indices[i]]);
            }
        }

        public void Undo()
        {
            for (int i = 0; i < m_indices.Length; i++)
            {
                Copy(m_before[i], m_blocks[m_indices[i]]);
            }
        }

        public void Redo()
        {
            for (int i = 0; i < m_indices.Length; i++)
            {
                Copy(m_after[i], m_blocks[m_indices[i]]);
            }
        }

        /// <summary>
        /// 블록의 복사본을 만든다.
        /// </summary>
        /// <param name="block">원본.</param>
        /// <returns>복사본 (배열도 따로 갖는다).</returns>
        public static BD2Block Clone(BD2Block block)
        {
            var copy = new BD2Block();
            Copy(block, copy);
            return copy;
        }

        /// <summary>
        /// 한 블록의 내용을 다른 블록에 덮어쓴다. 받는 쪽의 배열 객체는 그대로 두고 값만 옮긴다 (게임의 로더가 그 배열을 가리키고 있을 수 있다).
        /// </summary>
        /// <param name="from">내용을 가져올 블록.</param>
        /// <param name="to">내용을 받을 블록.</param>
        public static void Copy(BD2Block from, BD2Block to)
        {
            System.Array.Copy(from.vertices, to.vertices, from.vertices.Length);
            System.Array.Copy(from.uvs, to.uvs, from.uvs.Length);
            System.Array.Copy(from.textureIndices, to.textureIndices, from.textureIndices.Length);
            System.Array.Copy(from.materialIndices, to.materialIndices, from.materialIndices.Length);
            to.flags = from.flags;
        }
    }
}

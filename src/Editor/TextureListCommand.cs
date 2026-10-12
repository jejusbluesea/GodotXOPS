using System.Collections.Generic;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록 텍스처 목록을 바꾼 편집 (텍스처 더하기, 바꾸기). 편집 전과 후의 경로 목록을 들고 있다.
    /// </summary>
    public sealed class TextureListCommand : IEditorCommand
    {
        private readonly List<BlockTextureData> m_textures;
        private readonly BlockTextureData[] m_before;
        private readonly BlockTextureData[] m_after;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 목록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="textures">문서의 텍스처 목록.</param>
        /// <param name="before">바뀌기 전의 항목들 (Snapshot 으로 떠 둔 것).</param>
        public TextureListCommand(string name, List<BlockTextureData> textures, BlockTextureData[] before)
        {
            Name = name;
            m_textures = textures;
            m_before = before;
            m_after = Snapshot(textures);
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
        /// 텍스처 목록의 항목들(경로와 재질)을 떠 둔다.
        /// </summary>
        /// <param name="textures">텍스처 목록.</param>
        /// <returns>항목들의 복사본.</returns>
        public static BlockTextureData[] Snapshot(List<BlockTextureData> textures)
        {
            var result = new BlockTextureData[textures.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new BlockTextureData { diffusePath = textures[i].diffusePath, materialIndex = textures[i].materialIndex };
            }
            return result;
        }

        /// <summary>
        /// 목록을 떠 둔 항목들로 바꾼다.
        /// </summary>
        /// <param name="items">항목들.</param>
        private void Restore(BlockTextureData[] items)
        {
            m_textures.Clear();
            foreach (BlockTextureData item in items)
            {
                m_textures.Add(new BlockTextureData { diffusePath = item.diffusePath, materialIndex = item.materialIndex });
            }
        }
    }
}

using System.Collections.Generic;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록 텍스처 목록을 바꾼 편집 (텍스처 더하기, 바꾸기). 편집 전과 후의 경로 목록을 들고 있다.
    /// </summary>
    public sealed class TextureListCommand : IEditorCommand
    {
        private readonly List<BlockTextureData> m_textures;
        private readonly string[] m_before;
        private readonly string[] m_after;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 목록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="textures">문서의 텍스처 목록.</param>
        /// <param name="before">바뀌기 전의 경로들 (Snapshot 으로 떠 둔 것).</param>
        public TextureListCommand(string name, List<BlockTextureData> textures, string[] before)
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
        /// 텍스처 목록의 경로들을 떠 둔다.
        /// </summary>
        /// <param name="textures">텍스처 목록.</param>
        /// <returns>경로들.</returns>
        public static string[] Snapshot(List<BlockTextureData> textures)
        {
            var result = new string[textures.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = textures[i].diffusePath;
            }
            return result;
        }

        /// <summary>
        /// 목록을 떠 둔 경로들로 바꾼다.
        /// </summary>
        /// <param name="paths">경로들.</param>
        private void Restore(string[] paths)
        {
            m_textures.Clear();
            foreach (string path in paths)
            {
                m_textures.Add(new BlockTextureData { diffusePath = path });
            }
        }
    }
}

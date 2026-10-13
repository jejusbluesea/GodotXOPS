namespace GodotXOPS.Editor
{
    /// <summary>
    /// 텍스처 목록에서 한 자리를 지운 편집. 목록이 줄면서 블록의 면이 가리키는 번호도 함께 바뀌므로, 목록의 편집과 블록의 편집을 한 번에 되돌린다.
    /// </summary>
    public sealed class TextureDeleteCommand : IEditorCommand
    {
        private readonly TextureListCommand m_textures;
        private readonly BlockListCommand m_blocks;

        public string Name { get; }

        /// <summary>
        /// 편집을 만든다. 목록과 블록에는 이미 바뀐 내용이 들어 있어야 한다.
        /// </summary>
        /// <param name="name">상태 줄에 보일 이름 (영어).</param>
        /// <param name="textures">텍스처 목록의 편집.</param>
        /// <param name="blocks">면의 텍스처 번호를 고친 블록의 편집.</param>
        public TextureDeleteCommand(string name, TextureListCommand textures, BlockListCommand blocks)
        {
            Name = name;
            m_textures = textures;
            m_blocks = blocks;
        }

        public void Undo()
        {
            m_blocks.Undo();
            m_textures.Undo();
        }

        public void Redo()
        {
            m_textures.Redo();
            m_blocks.Redo();
        }
    }
}

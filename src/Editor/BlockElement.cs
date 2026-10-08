namespace GodotXOPS.Editor
{
    /// <summary>
    /// 블록 편집의 선택 단위. 블록은 꼭짓점 8개, 모서리 12개, 면 6개로 된 육면체다.
    /// </summary>
    public enum BlockElement
    {
        Vertex,
        Edge,
        Face,
        Block,
    }
}

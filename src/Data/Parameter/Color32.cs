using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 채널당 0~255 정수로 적는 색. JSON 에 {r, g, b, a} 정수로 저장되는 값(스카이 fog 색 등)에 쓴다.
    /// </summary>
    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        /// <summary>
        /// Godot 색(채널당 0~1)으로 바꾼다.
        /// </summary>
        /// <returns>같은 색의 Godot Color.</returns>
        public Color ToColor()
        {
            return Color.Color8(r, g, b, a);
        }
    }
}

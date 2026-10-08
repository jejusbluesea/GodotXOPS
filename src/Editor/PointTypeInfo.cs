using Godot;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 포인트 종류마다 에디터가 보여 줄 이름, 표식의 색과 모양, 두 파라미터(원본 P2, P3)의 뜻.
    /// 이벤트가 아닌 종류(1 에서 8)는 여기서 정하고, 이벤트(10 이상)의 이름은 EventCatalog 에서 온다.
    /// </summary>
    public static class PointTypeInfo
    {
        /// <summary>
        /// 표식의 모양.
        /// </summary>
        public enum Shape
        {
            // 사람 키만 한 기둥. 방향 화살표가 붙는다.
            Human,
            // 바닥에 놓인 작은 상자. 방향 화살표가 붙는다.
            Item,
            // 작은 정육면체. 방향이 없다.
            Node,
        }

        /// <summary>
        /// 종류 하나의 표시 정보.
        /// </summary>
        public readonly struct Info
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly Shape Shape;
            public readonly string P2;
            public readonly string P3;

            public Info(string name, Color color, Shape shape, string p2, string p3)
            {
                Name = name;
                Color = color;
                Shape = shape;
                P2 = p2;
                P3 = p3;
            }
        }

        // 에디터에서 새로 놓거나 종류를 바꿀 때 고를 수 있는, 이벤트가 아닌 종류 (메뉴에 나오는 순서). 이벤트는 EventCatalog 가 준다.
        public static readonly int[] EditableTypes =
        {
            MapLoader.PointHuman, MapLoader.PointHuman2, MapLoader.PointHumanInfo, MapLoader.PointWeapon, MapLoader.PointRandomWeapon,
            MapLoader.PointSmallObject, MapLoader.PointAIPath, MapLoader.PointRandomAIPath,
        };

        // 이벤트 종류의 이름을 주는 함수 (에디터가 읽은 등록 정보). 모르는 종류면 null 을 돌려준다.
        public static System.Func<int, string> EventName;

        private static readonly Color s_humanColor = new Color(0.2f, 0.9f, 0.3f);
        private static readonly Color s_weaponColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color s_pathColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color s_infoColor = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color s_objectColor = new Color(0.3f, 0.6f, 1f);
        private static readonly Color s_eventColor = new Color(0.9f, 0.4f, 1f);
        private static readonly Color s_unknownColor = new Color(1f, 1f, 1f);

        /// <summary>
        /// 종류 번호의 표시 정보를 돌려준다.
        /// </summary>
        /// <param name="type">포인트 종류.</param>
        /// <returns>표시 정보. 모르는 종류도 값이 온다.</returns>
        public static Info Get(int type)
        {
            switch (type)
            {
                case MapLoader.PointHuman: return new Info("Human", s_humanColor, Shape.Human, "Human info id", "Path id");
                case MapLoader.PointWeapon: return new Info("Weapon", s_weaponColor, Shape.Item, "Weapon", "Bullets");
                case MapLoader.PointAIPath: return new Info("AI path", s_pathColor, Shape.Node, "Mode", "Next id");
                case MapLoader.PointHumanInfo: return new Info("Human info", s_infoColor, Shape.Node, "Human data", "Team");
                case MapLoader.PointSmallObject: return new Info("Object", s_objectColor, Shape.Item, "Object data", "Snap to ground");
                case MapLoader.PointHuman2: return new Info("Human (no primary)", s_humanColor, Shape.Human, "Human info id", "Path id");
                case MapLoader.PointRandomWeapon: return new Info("Random weapon", s_weaponColor, Shape.Item, "Weapon A", "Weapon B");
                case MapLoader.PointRandomAIPath: return new Info("Random path", s_pathColor, Shape.Node, "Next id A", "Next id B");
            }

            if (type >= MapLoader.PointEventFirst)
            {
                return new Info(EventName?.Invoke(type) ?? $"Event {type}", s_eventColor, Shape.Node, "P2", "Next id");
            }
            return new Info($"Type {type}", s_unknownColor, Shape.Node, "P2", "P3");
        }
    }
}

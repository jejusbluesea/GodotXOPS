using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// Node 기반 싱글톤 제네릭 베이스 클래스.
    /// 인스턴스는 project.godot 의 Autoload 로 등록해 엔진이 생성하며, 등록 순서가 곧 초기화 순서다(지연 생성 없음).
    /// </summary>
    public abstract partial class Singleton<T> : Node where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        public static bool Loaded => Instance != null;

        public override void _EnterTree()
        {
            if (Instance != null && Instance != this)
            {
                Debugger.LogWarning($"Duplicate singleton removed: {typeof(T).Name}");
                QueueFree();
                return;
            }

            Instance = (T)this;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

using System.Diagnostics;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 에디터 전용 로그 출력 유틸리티. 익스포트 빌드에서는 호출 자체가 제거된다.
    /// </summary>
    public static class Debugger
    {
        /// <summary>
        /// 일반 로그 메시지를 출력한다. 에디터 전용.
        /// </summary>
        /// <param name="message">출력할 내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        [Conditional("TOOLS")]
        public static void Log(object message, string label = "GodotXOPS")
        {
            GD.Print($"[{label}] {message}");
        }

        /// <summary>
        /// 경고 로그 메시지를 출력한다. 에디터 전용.
        /// </summary>
        /// <param name="message">출력할 내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        [Conditional("TOOLS")]
        public static void LogWarning(object message, string label = "GodotXOPS")
        {
            GD.PushWarning($"[{label}] {message}");
        }

        /// <summary>
        /// 에러 로그 메시지를 출력한다. 에디터 전용.
        /// </summary>
        /// <param name="message">출력할 내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        [Conditional("TOOLS")]
        public static void LogError(object message, string label = "GodotXOPS")
        {
            GD.PushError($"[{label}] {message}");
        }
    }
}

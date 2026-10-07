using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 로그 한 줄의 수준. 디버그 콘솔이 수준별로 색을 달리해 보여 준다.
    /// </summary>
    public enum LogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>
    /// 로그 출력 유틸리티. 로그는 메모리에 쌓여 디버그 콘솔이 가져가 보여 주고(익스포트 빌드에서도), 에디터에서는 Godot 출력 창에도 찍힌다.
    /// 디버그 콘솔에 나오므로 메시지는 영어로 쓴다 (콘솔의 글자는 영어만 쓴다는 사용자 결정).
    /// </summary>
    public static class Debugger
    {
        // 기억해 두는 로그 줄 수. 넘치면 가장 오래된 것부터 버린다.
        private const int k_maxEntries = 256;

        private static readonly List<(LogLevel level, string text)> s_entries = new List<(LogLevel, string)>();
        // 지금까지 기록한 로그의 누계. 버려진 것도 센다. 디버그 콘솔이 어디까지 가져갔는지 기억하는 데 쓴다.
        private static int s_totalCount;
        // 에러 메시지만 따로 (분류 라벨 없이). 첫 항목의 에러 누계 번호는 ErrorCount − 개수다.
        private static readonly List<string> s_errors = new List<string>();

        // 지금까지 기록한 로그의 누계.
        public static int TotalCount => s_totalCount;
        // 에러 수준 로그의 누계. 어떤 작업 중에 난 에러를 찾을 때, 작업 전의 값을 FirstErrorSince 에 넘긴다.
        public static int ErrorCount { get; private set; }

        /// <summary>
        /// 에러 누계가 since 였던 시점 뒤에 처음 난 에러 메시지를 찾는다. 작업이 실패한 원인은 대개 첫 에러다 (뒤의 것은 그 여파다).
        /// </summary>
        /// <param name="since">작업을 시작하기 전의 ErrorCount.</param>
        /// <returns>에러 메시지 (분류 라벨 없이). 그 뒤로 에러가 없었거나 이미 버려졌으면 빈 문자열.</returns>
        public static string FirstErrorSince(int since)
        {
            int index = since - (ErrorCount - s_errors.Count);
            return index >= 0 && index < s_errors.Count ? s_errors[index] : string.Empty;
        }

        /// <summary>
        /// 일반 로그 메시지를 남긴다.
        /// </summary>
        /// <param name="message">내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        public static void Log(object message, string label = "GodotXOPS")
        {
            Record(LogLevel.Info, $"[{label}] {message}");
            PrintToEditor(LogLevel.Info, $"[{label}] {message}");
        }

        /// <summary>
        /// 경고 로그 메시지를 남긴다.
        /// </summary>
        /// <param name="message">내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        public static void LogWarning(object message, string label = "GodotXOPS")
        {
            Record(LogLevel.Warning, $"[{label}] {message}");
            PrintToEditor(LogLevel.Warning, $"[{label}] {message}");
        }

        /// <summary>
        /// 에러 로그 메시지를 남긴다.
        /// </summary>
        /// <param name="message">내용.</param>
        /// <param name="label">메시지 앞에 붙는 분류 라벨.</param>
        public static void LogError(object message, string label = "GodotXOPS")
        {
            s_errors.Add(message?.ToString() ?? string.Empty);
            if (s_errors.Count > k_maxEntries) s_errors.RemoveAt(0);
            ErrorCount++;
            Record(LogLevel.Error, $"[{label}] {message}");
            PrintToEditor(LogLevel.Error, $"[{label}] {message}");
        }

        /// <summary>
        /// 누계 번호가 since 이상인 로그를 꺼낸다. 이미 버려진 것은 건너뛴다.
        /// </summary>
        /// <param name="since">가져올 첫 로그의 누계 번호 (0 부터).</param>
        /// <param name="levels">꺼낸 로그의 수준.</param>
        /// <param name="texts">꺼낸 로그의 글자.</param>
        public static void GetSince(int since, List<LogLevel> levels, List<string> texts)
        {
            int firstNumber = s_totalCount - s_entries.Count;
            for (int i = Mathf.Max(0, since - firstNumber); i < s_entries.Count; i++)
            {
                levels.Add(s_entries[i].level);
                texts.Add(s_entries[i].text);
            }
        }

        private static void Record(LogLevel level, string text)
        {
            s_entries.Add((level, text));
            if (s_entries.Count > k_maxEntries) s_entries.RemoveAt(0);
            s_totalCount++;
        }

        /// <summary>
        /// 에디터에서 실행할 때만 Godot 출력 창에 찍는다. 익스포트 빌드에서는 호출이 제거된다.
        /// </summary>
        /// <param name="level">수준.</param>
        /// <param name="text">글자.</param>
        [Conditional("TOOLS")]
        private static void PrintToEditor(LogLevel level, string text)
        {
            switch (level)
            {
                case LogLevel.Warning: GD.PushWarning(text); break;
                case LogLevel.Error: GD.PushError(text); break;
                default: GD.Print(text); break;
            }
        }
    }
}

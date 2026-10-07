using System;
using System.IO;
using Godot;

namespace GodotXOPS
{
    public partial class GameBridge
    {
        private const string k_screenshotFolder = "screenshot";

        private readonly DebugConsole m_console = new DebugConsole();
        private ColliderView m_colliderView;
        // 콘솔이 가져간 로그의 누계 번호.
        private int m_logCursor;

        // 판정 범위 표시 (디버그 콘솔의 collider 가 켜고 끈다).
        public ColliderView ColliderView => m_colliderView;

        /// <summary>
        /// 디버그 콘솔 명령 한 줄을 실행한다.
        /// </summary>
        /// <param name="line">입력한 줄.</param>
        /// <returns>콘솔에 보여 줄 글자 (여러 줄일 수 있다). 없으면 빈 문자열.</returns>
        public string ConsoleExecute(string line)
        {
            return m_console.Execute(line);
        }

        /// <summary>
        /// 직전 명령이 화면에 맡긴 일을 꺼낸다 ("clear", "restart", "screenshot", "scene:씬이름"). 꺼내면 비워진다.
        /// </summary>
        /// <returns>일의 이름. 없으면 빈 문자열.</returns>
        public string ConsoleTakeAction()
        {
            return m_console.TakeUiAction();
        }

        /// <summary>
        /// 콘솔이 아직 가져가지 않은 로그(Debugger 의 경고·에러 등)를 꺼낸다. 콘솔이 매 프레임 불러 화면에 찍는다.
        /// 줄마다 첫 글자가 수준이다: "0" 일반, "1" 경고, "2" 에러. 그 뒤가 내용이다.
        /// </summary>
        /// <returns>새 로그 줄들. 없으면 빈 배열.</returns>
        public string[] ConsoleTakeLogs()
        {
            if (Debugger.TotalCount == m_logCursor) return Array.Empty<string>();

            var levels = new System.Collections.Generic.List<LogLevel>();
            var texts = new System.Collections.Generic.List<string>();
            Debugger.GetSince(m_logCursor, levels, texts);
            m_logCursor = Debugger.TotalCount;

            var lines = new string[texts.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = $"{(int)levels[i]}{texts[i]}";
            return lines;
        }

        /// <summary>
        /// 디버그 텍스트(콘솔의 info)를 보일지.
        /// </summary>
        /// <returns>보여야 하면 true.</returns>
        public bool ConsoleInfoVisible()
        {
            return m_console.InfoVisible;
        }

        /// <summary>
        /// 디버그 텍스트의 내용.
        /// </summary>
        /// <returns>여러 줄 문자열.</returns>
        public string ConsoleInfoText()
        {
            return m_console.BuildInfoText();
        }

        /// <summary>
        /// 지금 화면을 게임 폴더의 screenshot 폴더에 PNG 로 저장한다. 그려진 뒤(frame_post_draw)에 불러야 이번 프레임이 담긴다.
        /// </summary>
        /// <returns>저장한 파일의 전체 경로. 실패하면 빈 문자열.</returns>
        public string SaveScreenshot()
        {
            try
            {
                string folder = Path.Combine(GamePath.Root, k_screenshotFolder);
                Directory.CreateDirectory(folder);
                // 에디터에서 실행하면 게임 폴더가 프로젝트 폴더라, Godot 이 이 폴더를 임포트하지 않게 막는다.
                string ignore = Path.Combine(folder, ".gdignore");
                if (!File.Exists(ignore)) File.WriteAllText(ignore, string.Empty);

                string path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                Error error = GetViewport().GetTexture().GetImage().SavePng(path);
                return error == Error.Ok ? path : string.Empty;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debugger.LogError($"Screenshot save failed: {e.Message}", nameof(GameBridge));
                return string.Empty;
            }
        }
    }
}

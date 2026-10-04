using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 외부 게임 데이터(data/, addon/, godotdata/)의 실제 디스크 경로를 해석하는 단일 창구.
    /// 이 폴더들은 res:// 로 임포트하지 않고 런타임에 파일로 직접 읽는다.
    /// </summary>
    public static class GamePath
    {
        public const string DataFolder = "data";
        public const string AddonFolder = "addon";
        public const string GodotDataFolder = "godotdata";

        private static string s_root;

        // 에디터에서는 프로젝트 폴더, 익스포트 빌드에서는 실행 파일이 있는 폴더.
        public static string Root => s_root ??= ResolveRoot();

        /// <summary>
        /// 데이터 루트 기준 상대 경로를 전체 경로로 바꾼다. 루트 밖으로 나가는 경로는 거부한다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 상대 경로 (예: "data/sound/bang1.wav").</param>
        /// <returns>전체 경로. 경로가 비었거나 루트를 벗어나면 null.</returns>
        public static string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            return SafePath.Combine(Root, relativePath);
        }

        private static string ResolveRoot()
        {
            return OS.HasFeature("editor")
                ? ProjectSettings.GlobalizePath("res://")
                : OS.GetExecutablePath().GetBaseDir();
        }
    }
}

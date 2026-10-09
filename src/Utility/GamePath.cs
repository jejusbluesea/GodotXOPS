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

        /// <summary>
        /// 맵 파일(블록, 포인트, 미션, 텍스처 목록)을 쓸 경로를 전체 경로로 바꾼다. 루트 안이어야 하고, 확장자가 맞아야 하고,
        /// 기본 데이터 폴더(godotdata/) 안이면 안 된다. 받은 파일에 적힌 경로대로 썼다가 설정이나 기본 데이터를 덮어쓰는 것을 막는다.
        /// </summary>
        /// <param name="relativePath">데이터 루트 기준 상대 경로.</param>
        /// <param name="extension">점을 포함한 확장자 (예: ".bd2").</param>
        /// <param name="error">거절한 이유 (영어). 성공하면 null.</param>
        /// <returns>전체 경로. 거절하면 null.</returns>
        public static string ResolveForWrite(string relativePath, string extension, out string error)
        {
            error = null;
            string fullPath = Resolve(relativePath);
            if (fullPath == null)
            {
                error = $"The file must be inside the game folder: {relativePath}";
                return null;
            }
            if (!string.Equals(System.IO.Path.GetExtension(fullPath), extension, System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"The file must be a {extension} file: {relativePath}";
                return null;
            }

            // 정규화한 전체 경로로 본다 ("addon/../godotdata/.." 같은 우회를 함께 막는다).
            string dataFolder = Resolve(GodotDataFolder) + System.IO.Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(dataFolder, System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"Map files cannot be written into {GodotDataFolder}/: {relativePath}";
                return null;
            }
            return fullPath;
        }

        private static string ResolveRoot()
        {
            return OS.HasFeature("editor")
                ? ProjectSettings.GlobalizePath("res://")
                : OS.GetExecutablePath().GetBaseDir();
        }
    }
}

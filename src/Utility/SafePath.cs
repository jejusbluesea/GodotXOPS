using System;
using System.IO;

namespace GodotXOPS
{
    /// <summary>
    /// 경로 탈출 공격(Path Traversal)을 방지하며 안전하게 경로를 결합하는 유틸리티 클래스.
    /// </summary>
    public static class SafePath
    {
        private static readonly StringComparison s_pathComparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        /// <summary>
        /// 지정한 루트 경로 하위에 있는 경로만 허용하며, 경로 구성 요소들을 결합한다.
        /// </summary>
        /// <param name="root">기준이 되는 루트 디렉터리 경로.</param>
        /// <param name="paths">결합할 경로 구성 요소들.</param>
        /// <returns>결합된 전체 경로. 탈출 시도가 감지되면 null을 반환한다.</returns>
        public static string Combine(string root, params string[] paths)
        {
            string fullRoot = Path.GetFullPath(root);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            string[] allParts = new string[paths.Length + 1];
            allParts[0] = root;
            Array.Copy(paths, 0, allParts, 1, paths.Length);

            string combined = Path.Combine(allParts);
            string fullCombined = Path.GetFullPath(combined);

            if (!fullCombined.StartsWith(fullRoot, s_pathComparison))
            {
                Debugger.LogError($"Traversal directory detected: '{string.Join(", ", paths)}'\n'{fullCombined}' is outside of '{root}'");
                return null;
            }

            return fullCombined;
        }
    }
}

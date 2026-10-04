using System.Collections.Generic;
using System.IO;
using Godot;
using GodotXOPS.IO;

namespace GodotXOPS.Dev
{
    /// <summary>
    /// 개발용 점검 씬 스크립트. data/ 와 addon/ 의 모든 이미지·사운드·모델을 로더로 읽어 보고 결과를 요약 출력한 뒤 종료한다.
    /// 실행: Godot 콘솔 실행 파일로 --headless --path . res://scenes/dev/loader_check.tscn
    /// </summary>
    public partial class LoaderCheck : Node
    {
        public override void _Ready()
        {
            var counts = new SortedDictionary<string, int[]>();
            var failures = new List<string>();

            foreach (string folder in new[] { GamePath.DataFolder, GamePath.AddonFolder })
            {
                string root = GamePath.Resolve(folder);
                if (root == null || !Directory.Exists(root))
                {
                    GD.Print($"폴더 없음: {folder}");
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    bool? ok = TryLoad(path, extension);
                    if (ok == null)
                    {
                        continue;
                    }

                    if (!counts.TryGetValue(extension, out int[] count))
                    {
                        count = new int[2];
                        counts.Add(extension, count);
                    }
                    count[ok.Value ? 0 : 1]++;
                    if (!ok.Value)
                    {
                        failures.Add(Path.GetRelativePath(GamePath.Root, path));
                    }
                }
            }

            GD.Print($"데이터 루트: {GamePath.Root}");
            foreach (KeyValuePair<string, int[]> pair in counts)
            {
                GD.Print($"{pair.Key,-6} 성공 {pair.Value[0],4}  실패 {pair.Value[1],4}");
            }
            foreach (string failure in failures)
            {
                GD.Print($"실패: {failure}");
            }

            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 확장자에 맞는 로더로 파일 하나를 읽어 본다.
        /// </summary>
        /// <param name="path">파일 전체 경로.</param>
        /// <param name="extension">소문자 확장자.</param>
        /// <returns>성공 true, 실패 false, 점검 대상이 아닌 확장자면 null.</returns>
        private static bool? TryLoad(string path, string extension)
        {
            switch (extension)
            {
                case ".bmp":
                case ".tga":
                case ".dds":
                case ".png":
                case ".jpg":
                case ".jpeg":
                    return ImageLoader.LoadTexture(path) != null;
                case ".wav":
                    return SoundLoader.LoadAudio(path) != null;
                case ".x":
                    return ModelLoader.LoadMesh(path) != null;
                default:
                    return null;
            }
        }
    }
}

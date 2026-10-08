using System.Collections.Generic;
using System.IO;
using GodotXOPS.IO;

namespace GodotXOPS.Editor
{
    /// <summary>
    /// 에디터가 편집하는 내용. 파일에 들어가는 것 그대로를 든다 (게임 상태가 아니다): 블록 데이터와 텍스처 목록, 포인트 데이터와 메시지, 미션의 설정.
    /// 경로는 모두 exe 폴더 기준이다.
    /// </summary>
    public sealed class MapDocument
    {
        // 블록 데이터(BD2)의 경로. 아직 열지 않았으면 빈 문자열.
        public string BlockPath = string.Empty;
        // 포인트 데이터(PD2)의 경로. 아직 저장한 적이 없으면 빈 문자열.
        public string PointPath = string.Empty;
        // 표시할 하늘 번호 (미션 파일에서 열었을 때만 온다. 맵 파일에는 없는 값이다).
        public int SkyIndex
        {
            get => Mission.skyIndex;
            set => Mission.skyIndex = value;
        }
        // 미션 파일(MIF2)의 경로와 내용. 미션 모드에서 고치고, 플레이 테스트가 이 설정(하늘, 에드온 데이터 등)을 쓴다. 미션 없이 열었으면 경로는 비어 있고 내용은 기본값이다.
        public string MissionPath = string.Empty;
        public ExtendedMissionData Mission = new ExtendedMissionData();
        public PD2File Points = new PD2File();
        // 블록 데이터. 아직 열지 않았으면 블록이 없는 빈 내용이다.
        public BD2File Blocks = new BD2File();
        // 블록 텍스처 목록 (BD2 가 가리키는 JSON 의 내용). 블록과 함께 열고 저장한다.
        public BlockTextureListData Textures = new BlockTextureListData();
        // 포인트 데이터와 같은 이름의 .msg 파일의 줄들.
        public List<string> Messages = new List<string>();

        /// <summary>
        /// 블록 데이터 파일을 읽는다.
        /// </summary>
        /// <param name="relativePath">BD2 파일 경로 (exe 폴더 기준).</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽었으면 true. 실패하면 지금 내용은 그대로다.</returns>
        public bool LoadBlocks(string relativePath, out string error)
        {
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                error = $"Block file open failed: {relativePath}";
                return false;
            }
            if (!BD2File.Read(fullPath, out BD2File file, out error)) return false;

            // 텍스처 목록이 없거나 깨졌으면 빈 목록으로 연다 (면이 그려지지 않는다. 에디터에서 다시 채울 수 있다).
            var textures = new BlockTextureListData();
            string texturePath = GamePath.Resolve(file.textureListPath);
            if (texturePath != null && File.Exists(texturePath))
            {
                JsonData.Overwrite(EncodingHelper.ReadAllText(texturePath), textures, file.textureListPath);
            }

            Blocks = file;
            Textures = textures;
            BlockPath = relativePath;
            return true;
        }

        /// <summary>
        /// 포인트 데이터 파일을 읽는다. 같은 이름의 .msg 가 있으면 메시지도 읽는다.
        /// </summary>
        /// <param name="relativePath">PD2 파일 경로 (exe 폴더 기준).</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽었으면 true. 실패하면 지금 내용은 그대로다.</returns>
        public bool LoadPoints(string relativePath, out string error)
        {
            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                error = $"Point file open failed: {relativePath}";
                return false;
            }
            if (!PD2File.Read(fullPath, out PD2File file, out error)) return false;

            Points = file;
            PointPath = relativePath;
            Messages.Clear();
            string messagePath = Path.ChangeExtension(fullPath, ".msg");
            if (File.Exists(messagePath)) Messages.AddRange(EncodingHelper.ReadAllLines(messagePath));
            return true;
        }
    }
}

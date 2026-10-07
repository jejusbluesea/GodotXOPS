using System.IO;
using GodotXOPS.IO;

namespace GodotXOPS
{
    public partial class MapLoader
    {
        /// <summary>
        /// BD2 파일과 그 파일이 가리키는 텍스처 목록을 읽어 BD1 을 읽었을 때와 같은 구조로 만든다.
        /// BD2 의 좌표는 이미 Godot 축의 미터라서 변환하지 않는다.
        /// </summary>
        /// <param name="filepath">BD2 파일 전체 경로.</param>
        /// <param name="texturePaths">텍스처 전체 경로. 목록의 항목 수만큼이고, 경로가 비었거나 exe 폴더를 벗어나면 빈 문자열.</param>
        /// <param name="rawBlocks">블록 원시 데이터.</param>
        /// <returns>파싱에 성공했으면 true. 텍스처 목록을 읽지 못해도 블록은 로드한다 (면을 그리지 않는다).</returns>
        private static bool LoadBD2File(string filepath, out string[] texturePaths, out RawBlockData[] rawBlocks)
        {
            texturePaths = null;
            rawBlocks = null;

            if (!BD2File.Read(filepath, out BD2File file, out string error))
            {
                Debugger.LogError($"BD2 read failed: {filepath}\n{error}", nameof(MapLoader));
                return false;
            }

            BlockTextureListData list = LoadBlockTextureList(file.textureListPath);
            texturePaths = new string[list.blockTextureData.Count];
            for (int i = 0; i < texturePaths.Length; i++)
            {
                texturePaths[i] = GamePath.Resolve(list.blockTextureData[i].diffusePath) ?? string.Empty;
            }

            rawBlocks = new RawBlockData[file.blocks.Count];
            for (int i = 0; i < rawBlocks.Length; i++)
            {
                BD2Block block = file.blocks[i];
                rawBlocks[i] = new RawBlockData
                {
                    vertices = block.vertices,
                    uvs = block.uvs,
                    textureIndices = block.textureIndices,
                    materialIndices = block.materialIndices,
                    hasPassFlags = true,
                    passFlags = block.flags,
                };
            }

            return true;
        }

        /// <summary>
        /// BD1 파일을 같은 화면과 같은 판정을 내는 BD2 로 바꾼다. 텍스처 슬롯 10개는 텍스처 목록의 항목 10개가 되고,
        /// 판형 블록(충돌 없음)은 세 판정을 모두 끈 플래그가 된다. 재질은 전부 기본 재질(-1)이다.
        /// </summary>
        /// <param name="bd1Path">BD1 파일 전체 경로. exe 폴더 안에 있어야 텍스처 경로를 적을 수 있다.</param>
        /// <param name="textureListPath">BD2 에 적을 텍스처 목록 파일 경로 (exe 폴더 기준).</param>
        /// <param name="file">만든 BD2. 실패하면 null.</param>
        /// <param name="textures">만든 텍스처 목록. 실패하면 null.</param>
        /// <returns>변환에 성공했으면 true.</returns>
        public static bool ConvertBD1(string bd1Path, string textureListPath, out BD2File file, out BlockTextureListData textures)
        {
            file = null;
            textures = null;

            if (!File.Exists(bd1Path) || !LoadBD1File(bd1Path, out string[] texturePaths, out RawBlockData[] rawBlocks))
            {
                return false;
            }

            textures = new BlockTextureListData();
            foreach (string texturePath in texturePaths)
            {
                string relative = string.IsNullOrEmpty(texturePath)
                    ? string.Empty
                    : Path.GetRelativePath(GamePath.Root, texturePath).Replace('\\', '/');
                textures.blockTextureData.Add(new BlockTextureData { diffusePath = relative });
            }

            file = new BD2File { textureListPath = textureListPath };
            for (int i = 0; i < rawBlocks.Length; i++)
            {
                RawBlockData raw = rawBlocks[i];
                Block built = BuildBlock(raw, i, false, texturePaths.Length);

                var block = new BD2Block
                {
                    vertices = raw.vertices,
                    uvs = raw.uvs,
                    flags = built.layerMask == 0 ? BD2File.PassHuman | BD2File.PassBullet | BD2File.PassSight : 0,
                };
                for (int f = 0; f < BD2Block.FaceCount; f++)
                {
                    // BD1 에서 슬롯 범위 밖의 번호는 그리지 않는 면이다. BD2 에서는 음수로 적는다.
                    int textureIndex = raw.textureIndices[f];
                    block.textureIndices[f] = textureIndex >= 0 && textureIndex < texturePaths.Length ? textureIndex : -1;
                    block.materialIndices[f] = -1;
                }
                file.blocks.Add(block);
            }

            return true;
        }

        /// <summary>
        /// 블록 텍스처 목록 JSON 을 읽는다.
        /// </summary>
        /// <param name="relativePath">exe 폴더 기준 경로.</param>
        /// <returns>읽은 목록. 파일이 없거나 깨졌으면 빈 목록.</returns>
        private static BlockTextureListData LoadBlockTextureList(string relativePath)
        {
            var list = new BlockTextureListData();

            string fullPath = GamePath.Resolve(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                Debugger.LogError($"Block texture list not exists: {relativePath}", nameof(MapLoader));
                return list;
            }

            try
            {
                JsonData.Overwrite(EncodingHelper.ReadAllText(fullPath), list, relativePath);
            }
            catch (IOException e)
            {
                Debugger.LogError($"Block texture list read failed: {relativePath}\n{e.Message}", nameof(MapLoader));
            }

            return list;
        }
    }
}

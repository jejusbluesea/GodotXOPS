using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// BD2 파일의 블록 하나. 정점은 Godot 축의 미터 좌표이고, 정점 8개와 면 6개의 순서는 BD1 과 같다.
    /// </summary>
    public class BD2Block
    {
        public const int VertexCount = 8;
        public const int FaceCount = 6;
        public const int FaceVertexCount = 4;

        public Vector3[] vertices = new Vector3[VertexCount];
        // 면 f 의 v 번째 정점의 UV 는 uvs[f * 4 + v] 다.
        public Vector2[] uvs = new Vector2[FaceCount * FaceVertexCount];
        // 텍스처 목록의 번호. 음수면 그 면을 그리지 않는다. 면의 재질(착탄, 발소리)은 텍스처 목록의 그 항목이 정한다.
        public int[] textureIndices = new int[FaceCount];
        // 판정을 끄는 비트 (BD2File.PassHuman / PassBullet / PassSight). 0 이면 전부 충돌한다. 그 밖의 비트는 예약이고 0 으로 쓴다.
        public int flags;
    }

    /// <summary>
    /// BD2 파일의 구역 블록 하나. 모양은 블록과 같은 꼭짓점 8개(Godot 축의 미터 좌표)이고 그리지도 판정하지도 않는다.
    /// 같은 태그의 구역 블록들이 구역 하나다.
    /// </summary>
    public class BD2Area
    {
        public Vector3[] vertices = new Vector3[BD2Block.VertexCount];
        // 맵 데이터 파일의 구역 태그(areaTagData) 번호.
        public int tag;
    }

    /// <summary>
    /// 확장 블록 데이터(BD2) 파일의 읽기와 쓰기. 게임 싱글톤과 무관해서 에디터 같은 다른 도구에서도 쓸 수 있다.
    /// 구조 (정수는 리틀 엔디안): 매직 8바이트(GDXOPSB3) → 맵 데이터 경로 길이(uint32) → 경로(UTF-8) → 블록 개수(uint32) → 블록 × 316바이트
    /// → 구역 블록 개수(uint32) → 구역 블록 × 100바이트.
    /// 블록: 정점 8개(x, y, z float32) → UV 24개(u, v float32) → 면 텍스처 번호 6개(int32) → 플래그(int32).
    /// 구역 블록: 정점 8개(x, y, z float32) → 구역 태그 번호(int32).
    /// 1.2.5 까지의 파일(매직 GDXOPSB2)은 구역이 없고 블록에서 끝난다. 1.2.2 까지의 파일(매직 GDXOPSBD)은 블록이 340바이트이고
    /// 텍스처 번호 뒤에 면 재질 번호 6개(int32)가 있었다. 둘 다 읽을 수 있고 (면 재질 번호는 버린다), 쓰는 것은 늘 지금의 형식이다.
    /// </summary>
    public class BD2File
    {
        public const string Extension = ".bd2";

        // 블록 플래그는 통과 여부만 담는다. 켜져 있으면 그 판정이 이 블록을 통과한다. 나머지 비트는 예약이다 (읽을 때 무시한다).
        public const int PassHuman = 1 << 0;
        public const int PassBullet = 1 << 1;
        public const int PassSight = 1 << 2;

        private const int k_blockSize = 316;
        private const int k_areaSize = BD2Block.VertexCount * 3 * sizeof(float) + sizeof(int);
        // 1.2.2 까지의 블록 크기와, 그 안에 있던 면 재질 번호의 바이트 수.
        private const int k_legacyBlockSize = 340;
        private const int k_legacyMaterialBytes = BD2Block.FaceCount * sizeof(int);
        // 깨진 파일이 경로 길이로 큰 값을 줘도 메모리를 잡지 않게 하는 한계 (바이트).
        private const int k_maxPathLength = 4096;

        private static readonly byte[] s_magic = Encoding.ASCII.GetBytes("GDXOPSB3");
        // 1.2.3 에서 1.2.5 까지의 형식 (구역이 없다).
        private static readonly byte[] s_noAreaMagic = Encoding.ASCII.GetBytes("GDXOPSB2");
        private static readonly byte[] s_legacyMagic = Encoding.ASCII.GetBytes("GDXOPSBD");

        // 맵 데이터 JSON(텍스처 목록과 구역 태그)의 경로 (exe 폴더 기준).
        public string mapDataPath = string.Empty;
        public List<BD2Block> blocks = new List<BD2Block>();
        public List<BD2Area> areas = new List<BD2Area>();
        // 읽은 파일이 1.2.2 까지의 형식이었고, 지금은 쓰지 않는 면 재질 번호에 기본값(-1)이 아닌 것이 있었는지. 쓸 때는 보지 않는다.
        public bool droppedFaceMaterials;

        /// <summary>
        /// BD2 파일을 읽는다.
        /// </summary>
        /// <param name="filepath">BD2 파일 전체 경로.</param>
        /// <param name="file">읽은 내용. 실패하면 null.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>읽기에 성공했으면 true.</returns>
        public static bool Read(string filepath, out BD2File file, out string error)
        {
            file = null;
            error = null;

            try
            {
                using var reader = new BinaryReader(File.OpenRead(filepath));

                byte[] magic = reader.ReadBytes(s_magic.Length);
                bool legacy = magic.AsSpan().SequenceEqual(s_legacyMagic);
                bool noAreas = legacy || magic.AsSpan().SequenceEqual(s_noAreaMagic);
                if (!noAreas && !magic.AsSpan().SequenceEqual(s_magic))
                {
                    error = "not a BD2 file (magic mismatch)";
                    return false;
                }

                uint pathLength = reader.ReadUInt32();
                if (pathLength > k_maxPathLength)
                {
                    error = $"map data path is too long ({pathLength} bytes)";
                    return false;
                }

                byte[] pathBytes = reader.ReadBytes((int)pathLength);
                if (pathBytes.Length != pathLength)
                {
                    error = "file ends inside the map data path";
                    return false;
                }

                uint blockCount = reader.ReadUInt32();
                long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
                if ((long)blockCount * (legacy ? k_legacyBlockSize : k_blockSize) > remaining)
                {
                    error = $"file is too short for {blockCount} blocks";
                    return false;
                }

                var result = new BD2File { mapDataPath = Encoding.UTF8.GetString(pathBytes) };
                for (uint b = 0; b < blockCount; b++)
                {
                    var block = new BD2Block();
                    for (int i = 0; i < block.vertices.Length; i++)
                    {
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();
                        block.vertices[i] = new Vector3(x, y, z);
                    }
                    for (int i = 0; i < block.uvs.Length; i++)
                    {
                        float u = reader.ReadSingle();
                        float v = reader.ReadSingle();
                        block.uvs[i] = new Vector2(u, v);
                    }
                    for (int i = 0; i < block.textureIndices.Length; i++) block.textureIndices[i] = reader.ReadInt32();
                    for (int i = 0; legacy && i < k_legacyMaterialBytes / sizeof(int); i++)
                    {
                        if (reader.ReadInt32() != -1) result.droppedFaceMaterials = true;
                    }
                    block.flags = reader.ReadInt32();
                    result.blocks.Add(block);
                }

                if (!noAreas)
                {
                    if (reader.BaseStream.Length - reader.BaseStream.Position < sizeof(uint))
                    {
                        error = "file ends before the area count";
                        return false;
                    }
                    uint areaCount = reader.ReadUInt32();
                    if ((long)areaCount * k_areaSize > reader.BaseStream.Length - reader.BaseStream.Position)
                    {
                        error = $"file is too short for {areaCount} areas";
                        return false;
                    }
                    for (uint a = 0; a < areaCount; a++)
                    {
                        var area = new BD2Area();
                        for (int i = 0; i < area.vertices.Length; i++)
                        {
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            area.vertices[i] = new Vector3(x, y, z);
                        }
                        area.tag = reader.ReadInt32();
                        result.areas.Add(area);
                    }
                }

                file = result;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// BD2 파일로 쓴다. 같은 이름의 파일이 있으면 덮어쓴다.
        /// </summary>
        /// <param name="filepath">BD2 파일 전체 경로.</param>
        /// <param name="error">실패한 이유 (영어). 성공하면 null.</param>
        /// <returns>쓰기에 성공했으면 true.</returns>
        public bool Write(string filepath, out string error)
        {
            error = null;

            byte[] pathBytes = Encoding.UTF8.GetBytes(mapDataPath ?? string.Empty);
            if (pathBytes.Length > k_maxPathLength)
            {
                error = $"map data path is too long ({pathBytes.Length} bytes)";
                return false;
            }

            try
            {
                using var writer = new BinaryWriter(File.Create(filepath));

                writer.Write(s_magic);
                writer.Write((uint)pathBytes.Length);
                writer.Write(pathBytes);
                writer.Write((uint)blocks.Count);

                foreach (BD2Block block in blocks)
                {
                    foreach (Vector3 vertex in block.vertices)
                    {
                        writer.Write(vertex.X);
                        writer.Write(vertex.Y);
                        writer.Write(vertex.Z);
                    }
                    foreach (Vector2 uv in block.uvs)
                    {
                        writer.Write(uv.X);
                        writer.Write(uv.Y);
                    }
                    foreach (int index in block.textureIndices) writer.Write(index);
                    writer.Write(block.flags);
                }

                writer.Write((uint)areas.Count);
                foreach (BD2Area area in areas)
                {
                    foreach (Vector3 vertex in area.vertices)
                    {
                        writer.Write(vertex.X);
                        writer.Write(vertex.Y);
                        writer.Write(vertex.Z);
                    }
                    writer.Write(area.tag);
                }

                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}

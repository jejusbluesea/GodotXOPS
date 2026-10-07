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
        // 텍스처 목록의 번호. 음수면 그 면을 그리지 않는다.
        public int[] textureIndices = new int[FaceCount];
        // 재질 목록의 번호. -1 이면 미션의 기본 재질이다.
        public int[] materialIndices = new int[FaceCount];
        // 판정을 끄는 비트 (BD2File.PassHuman / PassBullet / PassSight). 0 이면 전부 충돌한다. 그 밖의 비트는 예약이고 0 으로 쓴다.
        public int flags;
    }

    /// <summary>
    /// 확장 블록 데이터(BD2) 파일의 읽기와 쓰기. 게임 싱글톤과 무관해서 에디터 같은 다른 도구에서도 쓸 수 있다.
    /// 구조 (정수는 리틀 엔디안): 매직 8바이트 → 텍스처 목록 경로 길이(uint32) → 경로(UTF-8) → 블록 개수(uint32) → 블록 × 340바이트.
    /// 블록: 정점 8개(x, y, z float32) → UV 24개(u, v float32) → 면 텍스처 번호 6개(int32) → 면 재질 번호 6개(int32) → 플래그(int32).
    /// </summary>
    public class BD2File
    {
        public const string Extension = ".bd2";

        // 블록 플래그는 통과 여부만 담는다. 켜져 있으면 그 판정이 이 블록을 통과한다. 나머지 비트는 예약이다 (읽을 때 무시한다).
        public const int PassHuman = 1 << 0;
        public const int PassBullet = 1 << 1;
        public const int PassSight = 1 << 2;

        private const int k_blockSize = 340;
        // 깨진 파일이 경로 길이로 큰 값을 줘도 메모리를 잡지 않게 하는 한계 (바이트).
        private const int k_maxPathLength = 4096;

        private static readonly byte[] s_magic = Encoding.ASCII.GetBytes("GDXOPSBD");

        // 텍스처 목록 JSON 의 경로 (exe 폴더 기준).
        public string textureListPath = string.Empty;
        public List<BD2Block> blocks = new List<BD2Block>();

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
                if (!magic.AsSpan().SequenceEqual(s_magic))
                {
                    error = "not a BD2 file (magic mismatch)";
                    return false;
                }

                uint pathLength = reader.ReadUInt32();
                if (pathLength > k_maxPathLength)
                {
                    error = $"texture list path is too long ({pathLength} bytes)";
                    return false;
                }

                byte[] pathBytes = reader.ReadBytes((int)pathLength);
                if (pathBytes.Length != pathLength)
                {
                    error = "file ends inside the texture list path";
                    return false;
                }

                uint blockCount = reader.ReadUInt32();
                long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
                if ((long)blockCount * k_blockSize > remaining)
                {
                    error = $"file is too short for {blockCount} blocks";
                    return false;
                }

                var result = new BD2File { textureListPath = Encoding.UTF8.GetString(pathBytes) };
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
                    for (int i = 0; i < block.materialIndices.Length; i++) block.materialIndices[i] = reader.ReadInt32();
                    block.flags = reader.ReadInt32();
                    result.blocks.Add(block);
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

            byte[] pathBytes = Encoding.UTF8.GetBytes(textureListPath ?? string.Empty);
            if (pathBytes.Length > k_maxPathLength)
            {
                error = $"texture list path is too long ({pathBytes.Length} bytes)";
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
                    foreach (int index in block.materialIndices) writer.Write(index);
                    writer.Write(block.flags);
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
